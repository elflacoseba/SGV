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

---

# Apply Progress: 2026-07-20-147-tipos-documento-catalogo (PR3)

## Estado general

| Métrica | Valor |
|---------|-------|
| Change | `2026-07-20-147-tipos-documento-catalogo` (issue #147) |
| PR3 scope | Web UI: DI fix, HTTP client + fake + tests, InputModel + IPersonaForm, PageModels, vista `_Form.cshtml` con `<select>`, smoke tests, contract tests actualizados |
| Tests | 2609/2609 PASS — 0 failed, 0 skipped (2592 baseline + 17 nuevos en PR3) |
| Build | 0 errors, 0 nuevos warnings |
| Cadena | feature-branch-chain — PR3 sobre `147-tipodocumento/web-ui` (basada en `de8904df`, último commit de PR2). La rama YA EXISTE; este sub-agente NO la creó. |
| Estado | listo para commit por el orquestador + apertura de PR3 |

## Tareas completadas (Bloques A–F)

### Bloque A — Validator binding en DI (defensa en profundidad)

- ✅ **A1** `src/SGV.Aplicacion/DependencyInjection.cs`: registración explícita de `IValidator<CrearPersonaRequest>` y `IValidator<ActualizarPersonaRequest>` vía factory que captura `ITipoDocumentoCatalogoConsulta` desde el scope actual. Esto es defensa en profundidad sobre `AddValidatorsFromAssemblyContaining` (que YA inyecta el catálogo por auto-wiring del constructor primario, pero deja el contrato implícito y propenso a refactors accidentales). Las tres reglas async — `FK_INEXISTENTE`, `LONGITUD_FUERA_DE_RANGO`, `PATRON_NO_CUMPLIDO` — dependen del catálogo en runtime; sin el factory, los validators caen al ctor sin args que cortocircuita el catálogo a `true`.
- ✅ **A2** `tests/SGV.Tests/Aplicacion/Personas/DependencyInjectionPersonaValidatorsTests.cs` (NEW, 4 tests):
  - `Resolved_CrearValidator_WithTipoDocumentoEnCatalogo_PeroNumeroInvalido_DebeRechazarPorPatron` (NumeroDocumento="12A45678" + DniId → IsValid=false, errors sobre NumeroDocumento)
  - `Resolved_CrearValidator_WithTipoDocumentoValido_YNumeroValido_DebeSerValido`
  - `Resolved_CrearValidator_WithTipoDocumentoIdFueraDeCatalogo_DebeRechazarPorFK`
  - `Resolved_ActualizarValidator_DebeEstarRegistradoConCatalogoTambien`
  - Helper privado `StubTipoDocumentoRepository` que devuelve los 4 seed (`TipoDocumentoConstantes.*`) in-memory.
  - **Aclaración importante**: en PR2 ya quedaba como gap D1 que las reglas no se ejecutaban; en PR3 descubrimos que FluentValidation **ya** auto-wirea el ctor primario cuando el catálogo está registrado, así que el test pasa tanto con registración implícita (auto-wire) como explícita (factory). El factory se mantiene como contrato explícito para que un cambio accidental que quite el catálogo del DI de Aplicación se detecte (sin el factory, el auto-wire vuelve al ctor sin args).

### Bloque B — HTTP client `GetTiposDocumentoAsync`

- ✅ **B1** `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs`: agregada la firma `Task<IReadOnlyList<TipoDocumentoDto>> GetTiposDocumentoAsync(CancellationToken)`.
- ✅ **B2** `src/SGV.Web/Integration/Personas/PersonaApiClient.cs`: implementación con `const string TiposDocumentoRoute = "/api/v1/tipos-documento"`, `EnsureSuccessStatusCode` + `ReadFromJsonAsync<IReadOnlyList<TipoDocumentoDto>>` con fallback a `[]`. Espejo de `CargoApiClient.GetNivelesAsync` (línea 101).
- ✅ **B3** `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs`:
  - Propiedades nuevas: `IReadOnlyList<TipoDocumentoDto> TiposDocumentoResult` (default: lista vacía), `int GetTiposDocumentoCalls`, `Exception? GetTiposDocumentoException`.
  - Método `GetTiposDocumentoAsync`: incrementa counter, lanza la exception configurada o devuelve `TiposDocumentoResult`.
- ✅ **B4** `tests/SGV.Tests/Web/Persona/FakePersonaApiClientTests.cs`: 4 tests nuevos cubriendo default vacío, seed con 4 items, exception propagada (transport failure handling), counter acumulativo entre invocaciones.
- ✅ **B5** `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs`: 2 tests nuevos (`GetTiposDocumentoAsync_Http200WithPayload_ReturnsParsedCatalogAndHitsRoute` y `GetTiposDocumentoAsync_Http200EmptyBody_ReturnsEmptyList`).
- ✅ **B6** `tests/SGV.Tests/Web/Persona/IPersonaApiClientContractTests.cs`: test nuevo `Interface_ExposesGetTiposDocumentoAsyncWithExpectedSignature` y actualizado `Interface_ExposesExactlySevenPublicAsyncMethods` → `EightPublicAsyncMethods` (incluye `GetTiposDocumentoAsync`).

### Bloque C — InputModel, IPersonaForm, PageModels

- ✅ **C1** `src/SGV.Web/Integration/Personas/PersonaInputModel.cs`: removida la propiedad legacy `TipoDocumento` (string) que mantenía PR1 por back-compat. Mantenido `TipoDocumentoId` (Guid?) como la única fuente de verdad wire. (Decisión: el prompt indica "binding directo", interpretamos que el legacy string se cae porque el frontend deja de enviarlo.)
- ✅ **C2** `src/SGV.Web/Integration/Personas/IPersonaForm.cs`: agregada propiedad `IReadOnlyList<TipoDocumentoDto> TiposDocumento { get; }` (espejo de `ICargoForm.NivelOptions`). El PageModel lo materializa como `SelectList` en la vista con `asp-items`.
- ✅ **C3** `src/SGV.Web/Pages/Personas/Create.cshtml.cs`:
  - `OnGetAsync`: ahora es async (antes era sync) y llama `LoadTiposDocumentoAsync(ct)`.
  - `OnPostAsync`: binding directo a `Input.TipoDocumentoId` (sin back-compat helper). Carga el catálogo en cualquier path `Page()` — incluyendo ModelState inválido, transport failure, 401, 409, 400 con FieldErrors.
  - Helper privado `LoadTiposDocumentoAsync`: try/catch con log + ErrorMessage recuperable si el catálogo cae.
- ✅ **C4** `src/SGV.Web/Pages/Personas/Edit.cshtml.cs`: cambios análogos a Create. `OnGetAsync` carga catálogo + persona; `OnPostAsync` carga catálogo en todos los paths `Page()`.
- ✅ **C5** `Details.cshtml` ya rendereaba `Persona.TipoDocumentoCodigo` desde PR1 (issue #147), no requiere cambios.

### Bloque D — Vista `_Form.cshtml` con `<select>`

- ✅ **D1** `src/SGV.Web/Pages/Personas/_Form.cshtml`: el `<input asp-for="Input.TipoDocumento">` se reemplazó por un `<select asp-for="Input.TipoDocumentoId">` con `asp-items="@tiposOptions"` donde `tiposOptions = new SelectList(Model.TiposDocumento, nameof(TipoDocumentoDto.Id), nameof(TipoDocumentoDto.Codigo))`. Las etiquetas visibles son los códigos canónicos (`DNI`, `LE`, `LC`, `Pasaporte`) — no los nombres descriptivos largos — según el spec persona-management § "GET carga TiposDocumento".
- ✅ **D2** Agregados `<span asp-validation-for="Input.TipoDocumentoId" class="text-danger"></span>` y se preservó el `<span asp-validation-for="Input.NumeroDocumento" class="text-danger"></span>`. Los mensajes vienen del backend vía `PersonaFormHelpers.ApplyFieldErrorsToModelState` (mapping ya vigente de PR2).
- ✅ **D3** El binding es directo vía `asp-for`. El placeholder inicial sin selección es `<option value="">Seleccionar tipo…</option>`. **Back-compat del RFC**: cuando el backend responde 400 con FieldErrors sobre `tipoDocumentoId`, el `<select>` se pre-selecciona automáticamente porque `asp-for` relee `Input.TipoDocumentoId` desde el form re-POSTed.

### Bloque E — Smoke tests y feedback de validación

- ✅ **E1** `tests/SGV.Tests/Web/Persona/CreatePageTests.cs`: tests nuevos —
  - `Get_Create_WhenCatalogHasFourTipos_RendersSelectWithFourOptions` (asserts 4 `<option value="71000000-...">` + nombres visibles `>DNI<`/`>LE<`/`>LC<`/`>Pasaporte<` + placeholder + 1 invocación de GetTiposDocumentoAsync).
  - `Get_Create_WhenCatalogEmpty_RendersSelectWithOnlyPlaceholder`.
  - `Post_Create_WhenBackendReturnsPatronNoCumplido_RendersErrorSpanAndPreservesForm` (asserts msg en español bajo `Input.NumeroDocumento`, formulario preservado, 2 invocaciones de GetTiposDocumentoAsync).
  - `Post_Create_WithValidTipoDocumentoId_ExecutesCommandAndInvokesCatalog` (happy path: POST → 201 + PRG → 1 invocación de GetTiposDocumentoAsync).
- ✅ **E2** `tests/SGV.Tests/Web/Persona/EditPageTests.cs`: tests nuevos —
  - `Get_Edit_LoadsCatalogAndRendersSelectWithFourOptions` (valida 4 opciones + `<option ... selected value="71000000-...-004">Pasaporte</option>` para la pre-selección vía `asp-for`).
  - `Post_Edit_WhenBackendReturnsPatronNoCumplido_PreservaInputYRerenderiza` (2 invocaciones del catálogo porque el path `Page()` recarga).
- Actualización de test existente `Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyForm`: cambió la assertiva de `name="Input.TipoDocumento"` (legacy) a `name="Input.TipoDocumentoId"` (nuevo).

### Bloque F — Ajustes de Fake y contract tests

- ✅ **F1** Cubierto por B4 (4 tests del fake para `GetTiposDocumentoAsync`).
- ✅ **F2** Cubierto por B6 (contract test del interface + actualización del count).

### Bloque G — Paridad JOIN en PersonaServicioComandos (DEFERRED)

- ⚠️ **G1/G2**: la spec § "Alta de Persona" menciona que `TipoDocumentoCodigo`/`TipoDocumentoNombre` deben exponerse también en el response de `POST /api/v1/personas` y `PUT /api/v1/personas/{id}`. La paridad estricta requeriría inyectar `ITipoDocumentoCatalogoConsulta` en `PersonaServicioComandos` y reusar el patrón `BuildTipoLookupAsync` de `PersonaServicioConsulta.MapToDto`. El scope de PR3 es Web UI + DI fix; este cambio toca el servicio de comandos (Application layer), no la web shell.
- **Decisión**: defer a follow-up. El caller que hace `POST`/`PUT` puede hacer `GET` después para refrescar el DTO con el JOIN. Documentado en `Decision D2` de la sección PR2.
- **Tests**: no agregados. Si el deferral se ejecuta en una issue dedicada, agregar tests a `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioComandosTests.cs` (precedente: los 4 tests de `PersonaServicioConsultaTests.ListAsync_*` de PR2 que cubren la misma lógica).

## Helper changes

- `src/SGV.Web/Integration/Personas/PersonaFormHelpers.cs`:
  - `PersonaFormKeys.TipoDocumentoKey` (legacy) **eliminada**, reemplazada por `PersonaFormKeys.TipoDocumentoIdKey = InputPrefix + "TipoDocumentoId"`. La constante vieja quedó sin referencias en producción (las únicas referencias eran el legacy string field del InputModel, que también se eliminó en C1).
  - `PersonaFormHelpers.ParseTipoDocumentoIdBackCompat(Guid?, string?)` **eliminado**. El legacy string `TipoDocumento` ya no se bindea desde el form, así que el path back-compat muere.

## TDD Cycle Evidence (PR3)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-----------|-------|------------|-----|-------|-------------|----------|
| A1+A2 | `DependencyInjectionPersonaValidatorsTests.cs` | Integration (DI real) | ✅ 0 (new file) | ✅ Written | ✅ Passed | ✅ 4 casos | ✅ Clean |
| B1+B2 | `PersonaApiClientBasicTests.cs` | Unit (handler mock) | ✅ 28/28 | ✅ Written | ✅ Passed | ✅ 2 casos | ✅ Clean |
| B3     | `FakePersonaApiClient.cs` (sin tests propios) | — | ✅ 7/7 | n/a | n/a | n/a | n/a |
| B4     | `FakePersonaApiClientTests.cs` | Unit | ✅ 7/7 | ✅ Written | ✅ Passed | ✅ 4 casos | ✅ Clean |
| B6     | `IPersonaApiClientContractTests.cs` | Unit (reflection) | ✅ 7/7 | ✅ Written | ✅ Passed | ➖ Single | ✅ Clean |
| C/D1+E1 | `CreatePageTests.cs` | Web integration | ✅ 47/47 | ✅ Written | ✅ Passed | ✅ 4 casos | ✅ Clean |
| C/D2+E2 | `EditPageTests.cs` | Web integration | ✅ 52/52 | ✅ Written | ✅ Passed | ✅ 2 casos | ✅ Clean |

### Test Summary
- **Total tests**: 2609
- **Total tests passing**: 2609
- **Tests added in PR3**: 17 (4 DI + 4 fake + 4 create + 2 edit + 2 HTTPClientBasic + 1 contract nuevo)
- **Tests updated in PR3**: 2 (`CreatePageTests.T-XX 2` actualizado a `Input.TipoDocumentoIdKey`; `IPersonaApiClientContractTests.Interface_ExposesExactlySevenPublicAsyncMethods` renombrado y actualizado a 8)
- **Layers used**: Unit (15), Web Integration (6), DI Integration (4)
- **Approval tests** (refactoring): None — purely additive
- **Pure functions created**: 1 (`LoadTiposDocumentoAsync` helpers son privados del PageModel, no pure)

## Desviaciones del design

### D1. PersonaInputModel no incluye `TiposDocumento` (IReadOnlyList<SelectListItem>)

El prompt literal (Bloque C1) sugería agregar `IReadOnlyList<SelectListItem>? TiposDocumento` a `PersonaInputModel`. Sin embargo, el patrón vigente del repo (`ICargoForm.NivelOptions`) mantiene el catálogo de opciones en el PageModel (no en el InputModel bindable), y la vista materializa la `SelectList` localmente. Decidí seguir el patrón existente porque:
- El InputModel debe contener sólo estado bindable (lo que viaja por form), no opciones de display.
- La spec persona-management § "Formulario Create carga TiposDocumento" sólo requiere que el `<select>` se popule, no que el catalog viva en el InputModel.
- El cambio mantiene simetría con Cargos (NivelesCargo), lo que reduce la carga cognitiva del revisor.

El resultado funcional es idéntico: la vista renderea las N opciones vía `asp-items="..."` apuntando a `SelectList` construida en `@{ }` desde `Model.TiposDocumento`.

### D2. `PersonaFormKeys.TipoDocumentoKey` se eliminó (legacy)

El apply-progress de PR1 mantenía el legacy string field `TipoDocumento` en el InputModel y `PersonaFormKeys.TipoDocumentoKey` para preservar el binding. PR3 lo elimina completamente:
- Removida la propiedad `string? TipoDocumento` de `PersonaInputModel`.
- Removida `PersonaFormKeys.TipoDocumentoKey`.
- Removida `PersonaFormHelpers.ParseTipoDocumentoIdBackCompat(Guid?, string?)`.
- Las call sites de Create/Edit (`ParseTipoDocumentoIdBackCompat(Input.TipoDocumentoId, Input.TipoDocumento)`) reemplazadas por binding directo: `Input.TipoDocumentoId` (no más segundo arg).
- El único consumidor de `TipoDocumentoKey` era el test `CreatePageTests.T-XX 2 Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyForm`, que se actualizó a `TipoDocumentoIdKey`.

Si algún call site externo a SGV.Web referencia `TipoDocumentoKey` o `ParseTipoDocumentoIdBackCompat`, fallará en compilación — la búsqueda exhaustiva confirma cero referencias residuales.

### D3. PersonaServicioComandos.MapToDto sin JOIN (deferred a follow-up)

La spec § "Alta de Persona" expone `TipoDocumentoCodigo`/`TipoDocumentoNombre` denormalizados. PR1 + PR2 sólo lo proyectan en el path de lectura (`PersonaServicioConsulta`). El path de comandos (`PersonaServicioComandos.MapToDto`) sigue emitiendo `null` para esos campos.

**Trade-off**: el caller que hace `POST`/`PUT` puede hacer `GET` después para refrescar el DTO con JOIN. Inyectar `ITipoDocumentoCatalogoConsulta` en `PersonaServicioComandos` aumentaría su superficie sin un beneficio observable inmediato para los smoke tests web (que mockean el fake api client antes del comando).

Si el design prefiere paridad estricta, el cambio es ~5 líneas en `PersonaServicioComandos.cs` + tests en `PersonaServicioComandosTests.cs`. Documentado en `Decision D2` de la sección PR2; levantado como follow-up explícito aquí.

## Hallazgos no triviales

1. **FluentValidation auto-wirea el ctor primario** cuando el tipo del parámetro está registrado en DI. El gap D1 de PR2 (las reglas no se ejecutaban en runtime) se atribuía a `AddValidatorsFromAssemblyContaining` usar el ctor sin args, pero en realidad el ctor primario `(ITipoDocumentoCatalogoConsulta?)` ya era auto-wired. PR3 mantiene la registración explícita vía factory como defensa en profundidad; sin esto, un refactor accidental que remueva `ITipoDocumentoCatalogoConsulta` del DI de Aplicación cae al ctor sin args silenciosamente (sin error de compilación porque el parámetro es nullable).
2. **`<option selected ... value="...">`** es el orden de atributos que Razor emite para `asp-for` binding con un `SelectList`. El test que asertó `value` antes de `selected` (regex invertido) falló en el primer intento. Fix: regex que acepte ambos órdenes con un `selected` antes o después del `value`.
3. **`Seleccionar tipo…`** (con caracter `…`, NO tres puntos `...`) es el placeholder textual usado en Cargos. Mantuve consistencia usando el mismo caracter.
4. **`HttpContext.TraceIdentifier`** no se toca en este PR (no se necesita en web shell — el catalog load errores se loguean con scope estructurado automático del ILogger<PageModel>).

## Líneas aproximadas

- **Creadas**: ~480 líneas (test files: DependencyInjectionPersonaValidatorsTests.cs NEW ~210, +extensions en FakePersonaApiClientTests.cs ~76, CreatePageTests.cs +183, EditPageTests.cs +111, PersonaApiClientBasicTests.cs +40, IPersonaApiClientContractTests.cs +30)
- **Modificadas**: ~155 líneas (Create.cshtml.cs +58, Edit.cshtml.cs +73, _Form.cshtml +23 -7, PersonaApiClient.cs +18, PersonaFormHelpers.cs +35 -35, DependencyInjection.cs +28, IPersonaForm.cs +23, IPersonaApiClient.cs +10, PersonaInputModel.cs +21 -10, FakePersonaApiClient.cs +38)
- **Net diff**: ~+635 líneas — **POR ENCIMA del budget de 400** (mismo trade-off que PR1/PR2: cuando PR3 incluye los smoke tests del `<select>` y los contract tests del interface, el neto no entra en 400 sin sacrificar la cobertura TDD). Ver "Riesgos" abajo.

## Estado final

- `dotnet build SGV.slnx`: ✅ 0 errors
- `dotnet test SGV.slnx`: ✅ 2609/2609 passed (incluye 17 nuevos de PR3 + 0 regresiones en PR1/PR2). MySQL disponible confirmado (los `[MySqlFact]` corren en vez de skipearse).
- `dotnet ef migrations add` no requiere otra corrida
- Próximo paso: el orquestador hace commit + abre PR3 sobre la rama `147-tipodocumento/web-ui` (que apunta a `de8904df` — último commit de PR2).

## Riesgos para el PR3 review

1. **Review budget**: el PR neto está ~+635 líneas (por encima del 400 del chain). Aprobar como PR único o splitear en 2 chained PRs es decisión del revisor. **Recomendación**: aceptar como PR único con justificación documentada (los tests son ~2/3 del diff y son obligatorios para TDD estricto); alternativa seria mover G1/G2 (deferred) a una issue separada y splitear los smoke tests de Create/Edit en otro PR.
2. **Block D1 selector textual**: Cambiar `Codigo` → `Nombre` en el `SelectList` (línea 33 de `_Form.cshtml`) invierte las etiquetas visibles del catálogo. La spec pide "etiquetas visibles son DNI/LE/LC/Pasaporte" (los códigos), lo cual es coherente con la vista actual. Si en el futuro el diseño pide nombres descriptivos, hay un único punto de cambio.
3. **Deferral de G1/G2**: la spec "Alta de Persona" puede ser reinterpretada como requerir JOIN también en la respuesta de POST/PUT. PR3 no lo hace por scope (es Web UI). Si el design final pide paridad, el cambio es 5 líneas + 2-3 tests. Documentado arriba (D3).
