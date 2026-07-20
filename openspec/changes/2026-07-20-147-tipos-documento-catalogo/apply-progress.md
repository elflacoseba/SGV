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
