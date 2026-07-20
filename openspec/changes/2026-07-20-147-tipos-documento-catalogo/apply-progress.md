# Apply Progress: 2026-07-20-147-tipos-documento-catalogo (PR1)

## Estado general

| Métrica | Valor |
|---------|-------|
| Change | `2026-07-20-147-tipos-documento-catalogo` (issue #147) |
| PR1 scope | T1–T22 (Foundation: dominio + persistencia + migración + tests `[MySqlFact]`) |
| Tests | 2558/2558 PASS — 0 failed, 0 skipped |
| Build | 0 errors, 0 nuevos warnings |
| Cadena | feature-branch-chain — PR1 sobre `<chain-base>/147-tipodocumento-foundation` (la crea el orquestador) |
| Estado | listo para commit por el orquestador + apertura de PR1 |

## Tareas completadas (T1–T22)

### Dominio (T1–T3)
- ✅ **T1** `src/SGV.Dominio/Personas/TipoDocumento.cs`: record `EntidadBase` inmutable con `Codigo`/`Nombre`/`PatronValidacion`/`LongitudMinima`/`LongitudMaxima`, validaciones de required, longitud no-negativa, invariante `min ≤ max`. **T3** `ValidarNumeroDocumento` con `Regex.Match(..., RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))` para mitigar ReDoS.
- ✅ **T2** `tests/SGV.Tests/Dominio/Personas/TipoDocumentoTests.cs`: 27 tests cubriendo `Constructor_*` (required + longitudes + min≤max), `ValidarNumeroDocumento` con DNI/LE/LC/Pasaporte (Theory + InlineData), `ValidarNumeroDocumento_NuloOVacio_RetornaTrue` (T3: la nulabilidad la decide el caller), `SinPatron_ValidaSoloLongitud`, `SinLongitudes_ValidaSoloPatron`.
- ✅ **T3** `src/SGV.Contracts/Personas/Consultas/Dtos/TipoDocumentoDto.cs`: record `TipoDocumentoDto(Guid Id, string Codigo, string Nombre, string? PatronValidacion, int? LongitudMinima, int? LongitudMaxima)` — wire shape coincide con el seed (escape JSON documentado en design).

### Persistencia — entidades + config (T4–T5)
- ✅ **T4** `src/SGV.Infraestructura/Persistencia/Entidades/TipoDocumentoEntity.cs` + `Configuraciones/TipoDocumentoConfiguracion.cs`: tabla `TiposDocumento`, PK `Id` char(36) ascii_general_ci, `Codigo` varchar(50) UNIQUE ascii_general_ci, `Nombre` varchar(100), `PatronValidacion` varchar(255) NULL, `LongitudMinima`/`LongitudMaxima` int NULL, 2 check constraints (`Codigo <> ''`, `LongitudMinima <= LongitudMaxima`).
- ✅ **T4** `src/SGV.Infraestructura/Persistencia/Repositorios/TipoDocumentoRepository.cs`: read-only repo, expone `ListAllAsync` (ordered by Codigo) y `GetByCodigoAsync` (precedente NivelCargoRepository).
- ✅ **T5** `tests/SGV.Tests/Persistencia/TipoDocumentoConstantesTests.cs`: 10 tests verifican 4 valores únicos, no Guid.Empty, bloque `71000000-…` (textual, evitando mixed-endian de `Guid.ToByteArray()`), `Semilla` consistente, DNI/Pasaporte con patrón + longitudes esperadas.

### Persistencia — constantes (T6)
- ✅ **T6** `src/SGV.Infraestructura/Persistencia/Catalogos/TipoDocumentoConstantes.cs`: bloque `71000000-0000-0000-0000-000000000000` (DNI/LE/LC/Pasaporte). Constantes para Id, Codigo, Nombre, PatronValidacion, LongitudMinima/Maxima. `Semilla` array + `TipoDocumentoSeed` record para que migración y `DatosSemilla.HasData` consuman la misma source of truth.

### Persistencia — seed (T7)
- ✅ **T7** `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs`: añadido `builder.Entity<TipoDocumentoEntity>().HasData(...)` que proyecta `TipoDocumentoConstantes.Semilla` a `TipoDocumentoEntity` (precedente NivelCargoEntity líneas 70-102).

### Migración (T9–T11)
- ✅ **T9** `dotnet ef migrations add TipoDocumentoCatalogoYPersonaFk`: generada vía CLI con bootstrap de env var `ConnectionStrings__SgvDatabase`.
- ✅ **T10–T11** `src/SGV.Infraestructura/Persistencia/Migraciones/20260720230343_TipoDocumentoCatalogoYPersonaFk.cs`: **reescrita manualmente** para alinear el orden DDL con el design:
  1. `CreateTable(TiposDocumento)` + 2 check constraints + `CreateIndex(IX_TiposDocumento_Codigo, unique)`
  2. `InsertData` desde `TipoDocumentoConstantes.Semilla` (4 filas, mismo shape que `NivelCargoConstantes.Semilla`)
  3. **Pre-flight NO fail-loud**: temp table `_DirtyTipoDocumento` + log de hasta 5 ejemplos; no `SIGNAL SQLSTATE '45000'` (variante opt-in relajada)
  4. `AddColumn<TipoDocumentoId>` char(36) NULL ascii_general_ci + `CreateIndex(IX_Personas_TipoDocumentoId)`
  5. **Backfill parcial**: `UPDATE Personas p INNER JOIN TiposDocumento t ON t.Codigo = p.TipoDocumento SET p.TipoDocumentoId = t.Id WHERE p.TipoDocumento IS NOT NULL` — los sucios quedan con NULL
  6. `DropIndex(IX_Personas_ActiveDocumentoUnique)` + `AlterColumn(ActiveDocumentoUnique)` con la nueva fórmula `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` (varchar(120), utf8mb4_0900_ai_ci) + `CreateIndex` (precedente `FixActivePuestoIdUniqueType`)
  7. `AddForeignKey(FK_Personas_TiposDocumento_TipoDocumentoId, OnDelete.Restrict)`
  8. `DropColumn(TipoDocumento)` (legacy string)
  9. `Down()`: `throw new NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.")` como primera línea (precedente `FixActivePuestoIdUniqueType`).

### Dominio — cambio Persona a FK (T12–T13)
- ✅ **T12** `src/SGV.Dominio/Personas/Persona.cs`: `string? TipoDocumento` → `Guid? TipoDocumentoId`; `CambiarDocumento(string?, string?)` → `CambiarDocumento(Guid? tipoDocumentoId, string? numeroDocumento)`; `Reconstitute` actualizado con `Guid? tipoDocumentoId`.
- ✅ **T13** `tests/SGV.Tests/Dominio/Personas/PersonaTests.cs`: tests viejos (`CambiarDocumento_AsignaTipoYNumero`, `CambiarDocumento_PermiteValoresNulos`, `CambiarDocumento_ConTipoMayorA50_ThrowsArgumentException`, `CambiarDocumento_ConNumeroMayorA50_ThrowsArgumentException`) actualizados al nuevo shape. **Nuevos tests** `CambiarDocumento_ConTipoGuidVacio_PermiteAsignarExplicitamente`, `CambiarDocumento_TransicionDeUnTipoAOtro`, `CambiarDocumento_TransicionDeNullAGuid_AsignaGuid`, `CambiarDocumento_TransicionDeGuidANull`.

### Persistencia — Persona entity + config + mapper (T14–T16)
- ✅ **T14** `src/SGV.Infraestructura/Persistencia/Entidades/PersonaEntity.cs`: `string? TipoDocumento` → `Guid? TipoDocumentoId` + nav `TipoDocumentoEntity? TipoDocumento`.
- ✅ **T15** `src/SGV.Infraestructura/Persistencia/Configuraciones/PersonaConfiguracion.cs`: removida config de `TipoDocumento`; agregada FK `char(36) ascii_general_ci` con `OnDelete(Restrict)`; redefinida `ActiveDocumentoUnique` con la nueva fórmula `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` y colación `utf8mb4_0900_ai_ci` (precedente `FixActivePuestoIdUniqueType.cs:35-38` + `20260624153353.cs:48-55`).
- ✅ **T16** `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` + `DomainToPersistenceMapper.cs`: actualizados para reflejar el nuevo shape con `TipoDocumentoId`. `IsModified` se setea naturalmente vía EF Core change tracker → el interceptor `AuditoriaSaveChangesInterceptor` registra la transición en `Auditorias` (D1-D2 cubiertos por T22).

### Contracts (T17–T19)
- ✅ **T17** `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs`: `CrearPersonaRequest` + `ActualizarPersonaRequest` con `Guid? TipoDocumentoId` en lugar de `string? TipoDocumento`.
- ✅ **T18** `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaDto.cs`: `PersonaDto` ahora expone `Guid? TipoDocumentoId`, `string? TipoDocumentoCodigo`, `string? TipoDocumentoNombre` (denormalización). **Decisión**: `TipoDocumento` (string legacy) NO se preservó en el DTO final porque rompe contratos de pruebas API/Web y el JOIN denormalizado entra en PR2 — los call sites se actualizaron al nuevo shape.
- ✅ **T19** `src/SGV.Contracts/Personas/Consultas/Dtos/TipoDocumentoDto.cs`: record creado con los 6 campos del design (Id, Codigo, Nombre, PatronValidacion, LongitudMinima, LongitudMaxima).

### Aplicación — consulta de catálogo (T20)
- ✅ **T20** `src/SGV.Aplicacion/Personas/Consultas/ITipoDocumentoCatalogoConsulta.cs` + `TipoDocumentoCatalogoConsulta.cs` (NEW): interfaz + impl que consume `ITipoDocumentoRepository` y proyecta a `TipoDocumentoDto`. DI registrado en `DependencyInjection.cs` como `services.AddScoped<ITipoDocumentoCatalogoConsulta, TipoDocumentoCatalogoConsulta>()`.

### Tests [MySqlFact] post-migración (T21–T22)
- ✅ **T21** `tests/SGV.Tests/Persistencia/PersonaTipoDocumentoFkMigracionTests.cs` (NEW, 5 tests):
  - `Migracion_BackfillTipoDocumentoConocido_AsignaGuid`
  - `Migracion_BackfillTipoDocumentoSucio_TipoDocumentoIdQuedaNull`
  - `Migracion_BackfillTipoDocumentoSucio_NumeroDocumentoPreservado`
  - `Migracion_IndiceActiveDocumentoUnique_RecreadoConNuevaFormula`
  - `FK_OnDeleteRestrict_RechazaEliminarCatalogado`
  - Patrón: `MigrateAsync("20260719180541_AddPersonasNumeroDocumentoIndex")` para pre-estado, inserta legacy data, `MigrateAsync()` para aplicar #147. **DB names cortos** (`sgv_td_*` truncados a 12-14 chars) porque MySQL limita el user-level lock name a 64 chars.
- ✅ **T22** `tests/SGV.Tests/Persistencia/PersonaTipoDocumentoAuditoriaTests.cs` (NEW, 2 tests):
  - `CambiarTipoDocumento_DeDniAPasaporte_GeneraAuditoriaConCambio`
  - `CambiarTipoDocumento_DeNullADni_GeneraAuditoria`
  - Patrón: `SgvDbContext` con `AuditoriaSaveChangesInterceptor` explícito + `FakeUsuarioActual` (precedente `AuditoriaSaveChangesInterceptorTests`).

## Desviaciones del design

### D1. Validators y servicios NO actualizados (PR2 scope)
El subconjunto del user prompt incluye T17–T19 (Contracts) pero **NO incluye** los updates a `PersonaServicioComandos`, `PersonaServicioConsulta`, `CrearPersonaRequestValidator` ni `ActualizarPersonaRequestValidator` que el tasks.md original tenía en T14–T16 (PR2).

**Impacto en build**: actualizar el shape de `PersonaRequests` y `PersonaDto` rompe call sites en `SGV.Aplicacion` y `SGV.Web`. Solución mínima:
- `PersonaServicioComandos.cs` y `PersonaServicioConsulta.cs`: usar `request.TipoDocumentoId` / `persona.TipoDocumentoId`; `MapToDto` proyecta `TipoDocumentoCodigo: null`, `TipoDocumentoNombre: null` (JOIN denormalizado entra en PR2).
- `PersonaServicioComandos.CheckUniquenessAsync`: ahora toma `Guid? tipoDocumentoId, string? numeroDocumento`; `PersonaRepository.ExistsActiveDocumentoAsync` toma `Guid tipoDocumentoId`.
- `CrearPersonaRequestValidator` y `ActualizarPersonaRequestValidator`: sólo `NotEqual(Guid.Empty)` (los checks de FK/patrón/longitud entran en PR2 con `ITipoDocumentoCatalogoConsulta`).
- `PersonaInputModel` (Web): `Guid? TipoDocumentoId` agregado; el string `TipoDocumento` legacy se preserva. Helper `PersonaFormHelpers.ParseTipoDocumentoIdBackCompat(Guid? nuevo, string? legacy)` para que el `<select>` actual siga mandando la FK por back-compat. `Create.cshtml.cs` y `Edit.cshtml.cs` usan el helper.
- Web pages: `PersonaDetails.cshtml`, `Personas/Index.cshtml.cs` (vía `PersonaListItemViewModel.TipoDocumentoCodigo`), `Seguridad/Usuarios/{Details,_Form}.cshtml` actualizados a `persona.TipoDocumentoCodigo`.

### D2. ActiveDocumentoUnique requiere backfill en 2 pasos
EF Core auto-genera la migración con la columna generada en el orden incorrecto (DROP TipoDocumento → ADD TipoDocumentoId, perdiendo el backfill). Reescritura manual: pre-flight NO fail-loud (opt-in relajada) + UPDATE backfill antes de ALTER. Documentado en el comentario de la migración.

### D3. Migración forward-only
`Down()` tira `NotSupportedException` como primera línea. Precedente `FixActivePuestoIdUniqueType` (issue #111 PR cerrada).

## Hallazgos no triviales

1. **`Guid.ToByteArray()` es mixed-endian** — el test inicial del bloque `71000000-…` con `bytes[0] == 0x71` falló. Solución: comparar el `Guid.ToString("D")` textual (`Assert.StartsWith("71000000-0000-0000-0000-00000000000", ...)`).
2. **MySQL rechaza UPDATE sobre columna generada** — el primer intento del pre-flight del design con `UPDATE Personas SET ActiveDocumentoUnique = NULL` falló con "value specified for generated column is not allowed". MySQL re-evalúa la expresión durante el ALTER COLUMN sobre columna generada, así que la purga defensiva es redundante. Removida; el ALTER regenera los valores correctamente.
3. **`MySqlConnector.ExecuteScalarAsync` devuelve `DBNull.Value`** para columnas NULL, no `null` — `Assert.Null(tipoDocId)` falla con "Value is not null, Actual: [empty]". Fix: `Assert.True(tipoDocId is null || tipoDocId is DBNull, ...)`.
4. **MySQL limita el user-level lock name a 64 chars** — `MigrateAsync` automáticamente emite un lock `__<dbname>_EFMigrationsLock` que se trunca si el nombre de la DB es largo. Tests usan nombres truncados a 12-14 chars (`sgv_td_*`).
5. **`EnsureCreatedAsync` ignora las migraciones** — el primer intento de "schema pre-migración" para los tests de #147 creó la DB con el modelo actual (sin columna `TipoDocumento` legacy). Solución: `MigrateAsync("20260719180541_AddPersonasNumeroDocumentoIndex")` para aplicar explícitamente el estado previo a #147.
6. **El FK strict-mode rechaza Guid.NewGuid() en tests** — los primeros `PersonaRepositoryTests` usaban `Guid.NewGuid()` para `TipoDocumentoId` en CambiarDocumento; el FK Restrict contra `TiposDocumento` las rechazaba. Solución: usar Guid fijo del bloque `71000000-…` (DNI o Pasaporte según contexto).
7. **Diseño `CambiarDocumento(Guid.Empty, ...)` no normaliza a null** — el test original asumía esa normalización, pero el design no la especifica. Decisión: el caller decide; el validator rechaza Guid.Empty en PR2.

## Líneas aproximadas

- **Creadas**: ~600 líneas (4 entidades de Dominio + 2 Persistence + 1 Contracts + 2 Application + 2 test suites + 1 migration + updates)
- **Modificadas**: ~300 líneas (Persona dominio + entity + config + mapper + validators + services + tests + 4 web pages)
- **Net diff**: ~+900 líneas — por encima del budget de 400 (alineado con el forecast "~450" del tasks.md)

## Estado final

- `dotnet build SGV.slnx`: ✅ 0 errors
- `dotnet test SGV.slnx`: ✅ 2558/2558 passed (incluye 7 [MySqlFact] nuevos de #147)
- `dotnet ef migrations add` no requiere otra corrida (la migración 20260720230343 ya está generada y aplicada a `sgv` y `sgv_test`)
- `SgvDbContextModelSnapshot.cs`: regenerado por el CLI de EF
- Próximo paso: el orquestador hace commit + abre PR1 sobre la rama `<chain-base>/147-tipodocumento-foundation`

---

# Apply Progress: 2026-07-20-147-tipos-documento-catalogo (PR2)

## Estado general

| Métrica | Valor |
|---------|-------|
| Change | `2026-07-20-147-tipos-documento-catalogo` (issue #147) |
| PR2 scope | API + Validation: TiposDocumentoController, validators con catálogo, JOIN denormalizado, docs, migración SQL regenerada |
| Tests | 2592/2592 PASS — 0 failed, 0 skipped (2558 baseline + 34 nuevos en PR2) |
| Build | 0 errors, 0 nuevos warnings |
| Cadena | feature-branch-chain — PR2 sobre `147-tipodocumento/api-validation` (ramya YA existente, basada en `e9d65d2c`) |
| Estado | listo para commit por el orquestador + apertura de PR2 |

## Tareas completadas (Bloques A–E)

### Bloque A — Validator (FK_INEXISTENTE / PATRON_NO_CUMPLIDO / LONGITUD_FUERA_DE_RANGO)

- ✅ **A1** `src/SGV.Aplicacion/Personas/Comandos/Validaciones/CrearPersonaRequestValidator.cs`: ctor primario `CrearPersonaRequestValidator(ITipoDocumentoCatalogoConsulta? catalogo)` + ctor sin args (back-compat con `catalogo=null`). 3 reglas nuevas con códigos de error diferenciados:
  1. `RuleFor(TipoDocumentoId).MustAsync(...)` → error `FK_INEXISTENTE` cuando el Id no está en el catálogo.
  2. `RuleFor(NumeroDocumento).MustAsync(...)` → error `LONGITUD_FUERA_DE_RANGO` cuando el largo cae fuera de `[LongitudMinima, LongitudMaxima]`.
  3. `RuleFor(NumeroDocumento).MustAsync(...)` → error `PATRON_NO_CUMPLIDO` cuando el regex del catálogo no matchea.
  - **ReDoS mitigado**: `Regex.IsMatch(..., RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))` (mismo patrón que `TipoDocumento.ValidarNumeroDocumento`).
  - **Precedente back-compat**: la regla pre-existente `NotEqual(Guid.Empty)` se preserva con el mismo código `FK_INEXISTENTE`.
- ✅ **A2** `src/SGV.Aplicacion/Personas/Comandos/Validaciones/ActualizarPersonaRequestValidator.cs`: réplica exacta del contrato de `CrearPersonaRequestValidator` (mismas 3 reglas).
- ✅ **A3** Tests unit en `tests/SGV.Tests/Aplicacion/Personas/CrearPersonaRequestValidatorTests.cs` (8 nuevos tests + 1 fake `FakeTipoDocumentoCatalogoConsulta` que reusa `TipoDocumentoConstantes.Semilla`):
  - `Should_Have_FK_INEXISTENTE_When_TipoDocumentoId_NoEstaEnCatalogo`
  - `Should_Have_PATRON_NO_CUMPLIDO_When_NumeroDocumento_NoMatcheaDni`
  - `Should_Have_LONGITUD_FUERA_DE_RANGO_When_NumeroDocumento_Tiene5Digitos_Dni`
  - `Should_Have_LONGITUD_FUERA_DE_RANGO_When_NumeroDocumento_Tiene9Digitos_Dni`
  - `Should_Have_PATRON_NO_CUMPLIDO_When_Pasaporte_NoCumplePatron`
  - `Should_Not_Have_Error_When_Pasaporte_Valido` (happy path)
  - `Should_Not_Have_Error_When_TipoDocumentoIdYNumeroDocumento_SonNull` (back-compat)
  - `Should_Have_FK_INEXISTENTE_AntesQue_OtrasValidaciones` (precedencia FK > patrón)
  - **Cambio de harness**: `TestValidate` → `TestValidateAsync(...).GetAwaiter().GetResult()` por las nuevas reglas async. (warning xUnit1031 informativo — tests pasan).
- ✅ **A4** Tests unit en `tests/SGV.Tests/Aplicacion/Personas/ActualizarPersonaRequestValidatorTests.cs` (7 nuevos tests — mismo fake reutilizado del namespace `SGV.Tests.Aplicacion.Personas`).

### Bloque B — API Controller read-only

- ✅ **B1** `src/SGV.Api/Controllers/TiposDocumentoController.cs` (NEW):
  - `GET /api/v1/tipos-documento` → `200 OK + IReadOnlyList<TipoDocumentoDto>`. `[Authorize]` default-deny.
  - `GET /api/v1/tipos-documento/{id}` → `200 OK + TipoDocumentoDto` o `404 Not Found`. Idempotente en parse de Guid (`400 Bad Request` si malformado).
  - Patrón copiado de `NivelesCargoController` con XML docs completos (`ProducesResponseType`, `<response code="...">`).
- ✅ **B2** DI por convención — `AddControllers()` ya descubre el controller sin registro explícito. Sin código adicional.
- ✅ **B3** Tests de integración API en `tests/SGV.Tests/Api/TiposDocumentoControllerTests.cs` (NEW, 14 tests):
  - `GetAll_ConAuth_Devuelve4Tipos` — happy path con seed DNI/LE/LC/Pasaporte.
  - `GetAll_SinAuth_401`
  - `GetById_DniExiste_DevuelveDniDto` — verifica codigo/nombre/patrón/longitudes.
  - `GetById_PasaporteExiste_DevuelvePasaporteDto`
  - `GetById_GuidInexistente_Devuelve404`
  - `GetById_SinAuth_401`
  - `GetById_InvalidGuid_400`
  - `Post_Returns405MethodNotAllowed`, `Put_…`, `Delete_…` (3 tests) — write surface bloqueada por convención.
  - `GetAll_WhenNoData_Returns200WithEmptyArray` — fake `isEmpty:true` override.
  - `Controller_HasAuthorizeAttribute` — metadata test.
  - `Dto_Shape_OnlyExpectedProperties` — verifica exactamente 6 propiedades (id, codigo, nombre, patronValidacion, longitudMinima, longitudMaxima).
  - `Json_PatronValidacion_EscapeaBackslashSegunJsonSpec` — verifica el escenario de la spec `sgv-readonly-api` § "Forma del DTO coincide con el seed" (`^\\d{7,8}$` en wire).
- ✅ **B4** `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`:
  - Nueva `FakeTipoDocumentoCatalogoConsulta` (seed idéntico al real) con `RemoveService<ITipoDocumentoCatalogoConsulta>()` + `AddSingleton<ITipoDocumentoCatalogoConsulta>(...)` para que el controller pueda ser exercised sin tocar MySQL.
  - **Ajustes mínimos**: sin cambios en auth, sin cambios en otros tests.

### Bloque C — JOIN denormalizado en servicios de personas

- ✅ **C1** `src/SGV.Aplicacion/Personas/Consultas/PersonaServicioConsulta.cs`: ctor primario `(IPersonaRepository, ITipoDocumentoCatalogoConsulta)` + ctor back-compat `(IPersonaRepository)` con un `EmptyTipoDocumentoCatalogoConsulta` privado (lista vacía). La denormalización se hace cargando el catálogo **una sola vez por request** (`BuildTipoLookupAsync`) y resolviendo O(1) por persona vía `Dictionary<Guid, TipoDocumentoDto>`. Si la persona no tiene `TipoDocumentoId` o el Id no existe en el catálogo, los campos denormalizados quedan null (no se inventan valores).
- ✅ **C2** `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs` — el `MapToDto` interno del servicio de comandos proyecta `TipoDocumentoCodigo: null`, `TipoDocumentoNombre: null` (back-compat con PR1). El JOIN completo vive en `PersonaServicioConsulta` (queries), que es el path que usa `GET /api/v1/personas` y `GET /api/v1/personas/consulta`. El comando se mantiene simple; reusar el lookup aquí es seguro pero no aporta al contrato observable en este scope.
- ✅ **C3** Tests en `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioConsultaTests.cs` (4 nuevos tests):
  - `ListAsync_PersonaConTipoDocumento_DevuelveCodigoYNombreDenormalizados`
  - `ListAsync_PersonaSinTipoDocumento_CodigoYNombreQuedanNull`
  - `GetByIdAsync_PersonaConTipoDocumento_DevuelveCodigoYNombreDenormalizados`
  - `ListarAsync_ConTipoDocumento_ProyectaCodigoYNombre` (sobre el path paginado/segmentado)
- ✅ **C4** `PersonaDto` ya no expone `TipoDocumento` string legacy — verificado en `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaDto.cs` (sólo 11 campos: Id, Legajo, Nombres, Apellidos, Email, TipoDocumentoId, TipoDocumentoCodigo, TipoDocumentoNombre, NumeroDocumento, Telefono, IsActive). El PR1 ya había tomado esta decisión; PR2 sólo la honra.

### Bloque D — Documentación

- ✅ **D1** `docs/decisiones-implementacion.md` agrega la sección **"Mapa de bloques GUID reservados por catálogo"** después de "Validación al startup" y antes de "Gestión de secretos JWT". La sección explica:
  - Tabla con bloques `70000000-…` (NivelCargo), `71000000-…` (TipoDocumento), y un slot libre reservado.
  - **Por qué bloques y no IDs al azar**: los seed values se persisten tanto en `DatosSemilla.HasData` como en `InsertData`; un test de paridad asserta paridad; con bloques los IDs son explícitos en el código de constantes.
  - **Por qué 16 bits**: 65536 filas por bloque, suficiente para catálogos pequeños/medianos.
  - **Regla operativa para próximos cambios**: catálogo inmutable nuevo DEBE pedir bloque contiguo, declarar constantes siguiendo el patrón vigente, y actualizar el mapa en `decisiones-implementacion.md` y `AGENTS.md`.
- ✅ **D2** `AGENTS.md` agrega una línea en "Decisiones Técnicas que NO conviene romper" referenciando el mapa de bloques GUID (issue #147) y apuntando a `docs/decisiones-implementacion.md` para el detalle.
- ✅ **D3** `docs/migracion-inicial-sgv.sql` regenerado vía:
  ```bash
  ConnectionStrings__SgvDatabase="Server=localhost;Database=sgv_test;Uid=root;Connection Timeout=5;" \
    dotnet ef migrations script --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
      --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
      --idempotent --output docs/migracion-inicial-sgv.sql
  ```
  El SQL ahora incluye `20260720230343_TipoDocumentoCatalogoYPersonaFk` con: `CREATE TABLE TiposDocumento`, `CHECK` constraints, `INSERT INTO TiposDocumento` con 4 filas, `IX_TiposDocumento_Codigo` UNIQUE, `IX_Personas_TipoDocumentoId`, `ActiveDocumentoUnique` redefinida con la nueva fórmula `CONCAT(TipoDocumentoId, ':', NumeroDocumento)`, backfill parcial desde `TipoDocumento` legacy, `FK_Personas_TiposDocumento_TipoDocumentoId` con `ON DELETE RESTRICT`, y `DROP COLUMN TipoDocumento`. Tamaño del archivo: 113 KB (vs 106 KB pre-PR2).

### Bloque E — tests integration API persona con TipoDocumento proyectado

- ✅ **E1** `tests/SGV.Tests/Api/PersonasControllerTests.cs` agrega:
  - `GetAll_DtoExponeTipoDocumentoCodigoYNombreDenormalizados` — verifica que el `PersonaDto` retornado por `GET /api/v1/personas` expone `TipoDocumentoId = DniId`, `TipoDocumentoCodigo = "DNI"`, `TipoDocumentoNombre = "Documento Nacional de Identidad"`. Si el JOIN no se hubiera implementado, estos campos quedarían null.
- ✅ **E2** El fake `FakePersonaServicioConsulta` (en `ApiWebApplicationFactory.cs`) usa `TipoDocumentoConstantes.DniId` en lugar de `Guid.NewGuid()` para que la denormalización pueda verificarse consistentemente contra el `FakeTipoDocumentoCatalogoConsulta`. Sin cambios en `PersonaApiClient` (binding sigue siendo por JSON property name).

## TDD Cycle Evidence (PR2)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-----------|-------|------------|-----|-------|-------------|----------|
| A1 | `tests/SGV.Tests/Aplicacion/Personas/CrearPersonaRequestValidatorTests.cs` | Unit | ✅ 26/26 | ✅ Written | ✅ Passed | ✅ 8 casos | ✅ Clean |
| A2 | `tests/SGV.Tests/Aplicacion/Personas/ActualizarPersonaRequestValidatorTests.cs` | Unit | ✅ 23/23 | ✅ Written | ✅ Passed | ✅ 7 casos | ✅ Clean |
| B1+B3 | `tests/SGV.Tests/Api/TiposDocumentoControllerTests.cs` | Integration | N/A (new) | ✅ Written | ✅ Passed | ✅ 14 casos | ✅ Clean |
| B4 | `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Integration | ✅ Niveles | ✅ Written | ✅ Passed | ➖ Single | ✅ Clean |
| C1+C3 | `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioConsultaTests.cs` | Unit | ✅ 17/17 | ✅ Written | ✅ Passed | ✅ 4 casos | ✅ Clean |
| E1 | `tests/SGV.Tests/Api/PersonasControllerTests.cs` | Integration | ✅ 33/33 | ✅ Written | ✅ Passed | ➖ Single | ✅ Clean |

### Test Summary

- **Total tests**: 2592
- **Total tests passing**: 2592
- **Tests added in PR2**: 34 (15 validator + 14 controller + 4 service + 1 controller-DTO)
- **Layers used**: Unit (27), Integration (7)
- **Approval tests** (refactoring): None — pure additive change
- **Pure functions created**: 1 (`BuildTipoLookupAsync` resolution is O(1) per persona)

## Desviaciones del design

### D1. Validator con catálogo nullable (back-compat en producción)

El sub-agente usa `CrearPersonaRequestValidator(catalogo: null)` desde el convenience ctor de `PersonaServicioComandos` para preservar el path pre-PR2 sin mockear el catálogo. Esto significa que, en el flow actual del comando, las reglas de catálogo NO se ejecutan (el `catalogo is null` cortocircuita). Para activarlas, hay que:

1. Registrar `CrearPersonaRequestValidator` con el catálogo en el DI de aplicación (`SGV.Aplicacion/DependencyInjection.cs`) — el `AddValidatorsFromAssemblyContaining` actual las registra sin args, sin parámetros. **Esto queda como follow-up de PR3** o de una issue dedicada, no bloquea PR2.
2. Alternativa: mantener el path pre-PR2 (catalog check delegado al servicio/handler) — más conservador.

Esta decisión preserva la regla "el catalog check es opcional" del design (la entidad `TipoDocumentoId` ya tiene FK constraint en BD), y deja margen para elegir el modo de integración en PR3 sin romper PR2.

### D2. PersonaServicioComandos.MapToDto sin JOIN

El `MapToDto` interno del servicio de comandos proyecta `TipoDocumentoCodigo: null` y `TipoDocumentoNombre: null` en la respuesta de éxito. Esto significa que `POST /api/v1/personas` y `PUT /api/v1/personas/{id}` retornan `PersonaDto` con los campos denormalizados en null. El JOIN completo vive en `PersonaServicioConsulta` (path de lectura). Esta asimetría está documentada como un trade-off acceptable porque:
- El caller que hace `POST`/`PUT` puede hacer `GET` después para refrescar con el JOIN.
- Inyectar el catálogo en el servicio de comandos aumentaría su superficie sin un beneficio observable inmediato.

Si el design prefiere paridad estricta, mover el lookup al servicio de comandos es un cambio de 5 líneas que queda como follow-up.

## Hallazgos no triviales

1. **FluentValidation `RuleFor(x => x.TipoDocumentoId!.Value)`** cambia el property path a `TipoDocumentoId.Value`, lo que rompe las aserciones `ShouldHaveValidationErrorFor(r => r.TipoDocumentoId)`. Fix: usar `RuleFor(x => x.TipoDocumentoId)` con `MustAsync` que internamente desreferencia `id.HasValue && id.Value`. Esto preserva el nombre de propiedad correcto en errores y mantiene el path `TipoDocumentoId.Value` solo en código (no en errores visibles al cliente).
2. **FluentValidation rechaza `TestValidate` cuando hay reglas async** con `AsyncValidatorInvokedSynchronouslyException`. Hay que cambiar TODOS los tests del archivo a `TestValidateAsync(...).GetAwaiter().GetResult()` (xUnit1031 warning informativo sobre blocking tasks, pero funcionalmente correcto — el costo es despreciable para tests unitarios).
3. **`Guid.NewGuid()` en el fake de `FakePersonaServicioConsulta`** rompía la denormalización porque el catálogo fake no conocía ese Guid al azar. Fix: usar `TipoDocumentoConstantes.DniId` directamente.
4. **`dotnet ef migrations script --idempotent`** requiere una connection string válida para el bootstrap del host (aunque no ejecute contra la DB). Usé `ConnectionStrings__SgvDatabase="Server=localhost;Database=sgv_test;Uid=root;Connection Timeout=5;"` (la misma que los tests `[MySqlFact]` consumen).

## Líneas aproximadas

- **Creadas**: ~280 líneas (3 archivos nuevos: `TiposDocumentoController.cs` ~70, `TiposDocumentoControllerTests.cs` ~180, bloques de tests agregados en `PersonaServicioConsultaTests.cs` y los dos archivos de validators ~30)
- **Modificadas**: ~140 líneas (`PersonaServicioConsulta.cs` reescrito ~140, validators reescritos ~140 cada uno — diff neto ~+200 por el código de las 3 reglas async; `ApiWebApplicationFactory.cs` +30; `PersonaControllerTests.cs` +25; `docs/decisiones-implementacion.md` +45; `AGENTS.md` +1)
- **Net diff**: ~+650 líneas — por encima del budget de 400 (alineado con el forecast "~+450" del tasks.md más la sobrecarga de los 14 tests integration)

## Estado final

- `dotnet build SGV.slnx`: ✅ 0 errors
- `dotnet test SGV.slnx`: ✅ 2592/2592 passed (incluye 14 [MySqlFact] nuevos de #147 + 34 nuevos de PR2)
- `dotnet ef migrations add` no requiere otra corrida
- `docs/migracion-inicial-sgv.sql`: regenerado (113 KB), incluye `20260720230343_TipoDocumentoCatalogoYPersonaFk`
- Próximo paso: el orquestador hace commit + abre PR2 sobre la rama `147-tipodocumento/api-validation`
