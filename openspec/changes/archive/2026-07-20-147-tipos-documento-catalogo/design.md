# Design: Catálogo `TipoDocumento` y FK en `Persona` (issue #147)

> **Change:** `2026-07-20-147-tipos-documento-catalogo`. **Precedentes:** `NivelCargo` (catálogo inmutable), `CambiarNivelStringANivelId` (`20260618180508`), `FixActivePuestoIdUniqueType` (`20260711181615`, forward-only), `20260624153353` (columna generada `CONCAT`), `ICargoApiClient.GetNivelesAsync` + `Cargos/Create.cshtml.cs:171-182` (patrón web).

## Resumen ejecutivo

Catálogo inmutable `TipoDocumento` (4 filas `DNI|LE|LC|Pasaporte`, bloque GUID `71000000-…`). Reemplazo de `Personas.TipoDocumento` (string) por FK nullable `Personas.TipoDocumentoId` (`Guid?`, `char(36) NULL`, `OnDelete(Restrict)`). `ActiveDocumentoUnique` redefinida con `CONCAT(TipoDocumentoId, ':', NumeroDocumento)`. Validación de patrón/longitud en `CrearPersonaRequestValidator`/`ActualizarPersonaRequestValidator` consumiendo `ITipoDocumentoCatalogoConsulta`. Delta a REQ-SPA-EVOLUTION-001 variante opt-in relajada ya formalizado en `specs/sgv-persistence-architecture/spec.md`: backfill NO aborta, `NumeroDocumento` huérfano se preserva.

## Decisiones de arquitectura

| Decisión | Elegido | Precedente |
|---|---|---|
| Acceso `Validator`s al catálogo | `ITipoDocumentoCatalogoConsulta` (interfaz en `Aplicacion/Personas/Consultas`, impl en Infraestructura) | `INivelCargoServicioConsulta` + `NivelCargoServicioConsulta` |
| Acceso Web al catálogo | `IPersonaApiClient.GetTiposDocumentoAsync` (HTTP tipado). Web NO usa `ITipoDocumentoCatalogoConsulta` | `ICargoApiClient.GetNivelesAsync` |
| Caché `IMemoryCache` | **No** — 4 filas, costo MySQL despreciable | — |
| Constantes hard-coded en `Validator` | **No** — el catálogo evoluciona por migración | REQ-SPA-EVOLUTION-001 #4 |
| Tipo CLR FK / columna | `Guid?` / `char(36) NULL COLLATE ascii_general_ci` | Decisión producto + spec |
| Columna generada tipo | `varchar(120) NULL COLLATE utf8mb4_0900_ai_ci` (Guid=36 + ':' + NumeroDoc≤50 + margen) | `20260624153353.cs:48-55` (mismo nombre, misma colación) |
| Orden DDL columna generada | `DropIndex → UPDATE defensivo → AlterColumn computed → CreateIndex unique` | `FixActivePuestoIdUniqueType.cs:67-75` (regla MySQL InnoDB) |
| `Down()` migración | `throw new NotSupportedException(...)` como **primera línea** | `FixActivePuestoIdUniqueType.Down` |
| Backfill legacy sucio | NO aborta; pre-flight lista ofensivos para logging. `TipoDocumentoId = NULL` + `NumeroDocumento` preservado | Delta en `specs/sgv-persistence-architecture/spec.md` |
| Inyección `ITipoDocumentoCatalogoConsulta` en `Validator` | Ctor primario + ctor sin args (seed in-memory) para back-compat | `PersonaServicioComandos.cs:26-33` |
| Auditoría `TipoDocumentoId` | Automática vía `AuditoriaSaveChangesInterceptor` (mapear `PersonaEntity.TipoDocumentoId`; cubre cualquier transición). Sin código extra para D1-D2 | `AuditoriaSaveChangesInterceptor.cs:35-58` |
| Auditoría `string → NULL` backfill | **No aplica** — columna se elimina en mismo batch. Trazabilidad = `NumeroDocumento` huérfano | — |
| Escape JSON `PatronValidacion` | Default `System.Text.Json` (1 `\` runtime, 2 `\\` wire). Round-trip natural con `ReadFromJsonAsync` | Escenario `sgv-readonly-api/spec.md` § "Forma del DTO coincide con el seed" |
| Regex DoS | `Regex.Match(..., RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))` | Mitigación estándar |

## Mapeo por capas

| Capa | Cambio (síntesis) | Precedente |
|---|---|---|
| Dominio | `TipoDocumento` record (EF-agnóstico); `Persona.TipoDocumentoId: Guid?` reemplaza `TipoDocumento: string?`; `CambiarDocumento(Guid? id)`; `Reconstitute` agrega `tipoDocumentoId` | `SGV.Dominio/Organizacion/NivelCargo.cs`; `Persona.cs:29,95` |
| Persistencia | `TipoDocumentoEntity` + `Configuracion` + `Repository` + `Constantes` (Guid bloque `71000000-…`) + `DatosSemilla.HasData` + mapper. `PersonaConfiguracion`: drop string, agregar FK+nav+índice+columna generada redefinida | `NivelCargoEntity.cs` + `NivelCargoConfiguracion.cs` + `NivelCargoRepository.cs` |
| Aplicación | `ITipoDocumentoCatalogoConsulta` (+ impl) con `ListarAsync`/`ObtenerPorIdAsync`; inyectada en `Validator`s. `PersonaServicioComandos` arma `Persona` con `TipoDocumentoId`. `PersonaServicioConsulta.MapToDto` JOIN para denormalizar | `NivelCargoServicioConsulta`; `PersonaServicioComandos.cs:67-70` |
| Contracts | `TipoDocumentoDto(Guid Id, string Codigo, string Nombre, string? PatronValidacion, int? LongitudMinima, int? LongitudMaxima)`. `PersonaDto`: `TipoDocumento` sale; entran `TipoDocumentoId: Guid?` y `TipoDocumento: TipoDocumentoDto?`. `Crear/ActualizarPersonaRequest`: `TipoDocumento: string?` → `TipoDocumentoId: Guid?` | `NivelCargoDto`; `PersonaDto.cs:9-18` |
| Api | `TipoDocumentosController` (`api/v1/tipos-documento`, `[Authorize]`, sólo `[HttpGet]` ⇒ 405 natural para writes) | `NivelesCargoController.cs:11-65` |
| Web | `IPersonaApiClient.GetTiposDocumentoAsync`; `PersonaInputModel.TipoDocumentoId: Guid? + TiposDocumento: IReadOnlyList<TipoDocumentoDto>`; `IPersonaForm.TiposDocumento`; `_Form.cshtml` con `<select name="TipoDocumentoId">`; `Create/Edit.cshtml.cs.LoadCatalogsAsync`. `FakePersonaApiClient.TiposDocumentoResult` + `GetTiposDocumentoCalls` | `ICargoApiClient.GetNivelesAsync`; `Cargos/_Form.cshtml:33-39`; `Cargos/Create.cshtml.cs:171-182` |
| Tests | Dominio (unit), Aplicación (mock), Persistencia `[MySqlFact]` (seed, backfill, FK, computed, unicidad, auditoría), API (bearer), Web (render + fake), Modelo (asserts tipo/computed) | `NivelCargoConstantesTests`, `MigracionFailLoudTests`, `OcupacionGeneratedColumnRegressionTests`, `AuditoriaSaveChangesInterceptorTests` |

## Archivos a crear / modificar

**Crear**: `Dominio/Personas/TipoDocumento.cs`; en `Infraestructura/Persistencia`: `Entidades/TipoDocumentoEntity.cs`, `Configuraciones/TipoDocumentoConfiguracion.cs`, `Repositorios/TipoDocumentoRepository.cs`, `Catalogos/TipoDocumentoConstantes.cs`; `Aplicacion/Personas/Consultas/ITipoDocumentoCatalogoConsulta.cs` + impl; `Contracts/Personas/Consultas/Dtos/TipoDocumentoDto.cs`; `Api/Controllers/TipoDocumentosController.cs`; migración EF nueva; tests (`TipoDocumentoConstantesTests`, `TipoDocumentoTests`, `TipoDocumentoValidatorTests`, `TipoDocumentoMigracionBackfillTests`, `TipoDocumentosControllerTests`, `PersonaSelectTipoDocumentoTests`).

**Modificar**: `Dominio/Personas/Persona.cs`; en `Infraestructura`: `PersonaEntity.cs`, `PersonaConfiguracion.cs`, `Mapeos/PersistenceToDomainMapper.cs`, `DatosSemilla.cs`, `SgvDbContext.cs`, `Migraciones/SgvDbContextModelSnapshot.cs` (regenerado), `DependencyInjection.cs`; en `Aplicacion/Personas`: `Comandos/PersonaServicioComandos.cs`, `Comandos/Validaciones/CrearPersonaRequestValidator.cs`, `Comandos/Validaciones/ActualizarPersonaRequestValidator.cs`, `Consultas/PersonaServicioConsulta.cs`, `Consultas/IPersonaRepository.cs`; en `Infraestructura/Persistencia/Repositorios`: `PersonaRepository.cs`; en `Contracts/Personas`: `Consultas/Dtos/PersonaDto.cs`, `Comandos/PersonaRequests.cs`; `Api/Controllers/PersonasController.cs`; en `Web/Integration/Personas`: `IPersonaApiClient.cs`, `PersonaApiClient.cs`, `PersonaInputModel.cs`, `IPersonaForm.cs`; `Web/Pages/Personas/{Create,Edit}.{cshtml,cshtml.cs}` y `_Form.cshtml`; `tests/.../Web/Persona/FakePersonaApiClient.cs` + `tests/.../Persistencia/ModeloPersistenciaTests.cs`; `docs/migracion-inicial-sgv.sql` (regenerado); `docs/decisiones-implementacion.md` (mapa GUIDs); `AGENTS.md` (mapa rangos).

## Modelo de datos

```sql
CREATE TABLE TiposDocumento (
  Id CHAR(36) NOT NULL COLLATE ascii_general_ci,
  Codigo VARCHAR(50) NOT NULL COLLATE ascii_general_ci,        -- UNIQUE (IX_TiposDocumento_Codigo)
  Nombre VARCHAR(100) NOT NULL,
  PatronValidacion VARCHAR(255) NULL,
  LongitudMinima INT NULL,
  LongitudMaxima INT NULL,
  PRIMARY KEY (Id),
  CHECK (Codigo <> ''),
  CHECK (LongitudMinima IS NULL OR LongitudMaxima IS NULL OR LongitudMinima <= LongitudMaxima)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Personas (post-migración, sintético)
ALTER TABLE Personas
  ADD COLUMN TipoDocumentoId CHAR(36) NULL COLLATE ascii_general_ci,
  ADD INDEX IX_Personas_TipoDocumentoId (TipoDocumentoId),
  DROP INDEX IX_Personas_ActiveDocumentoUnique,
  UPDATE Personas SET ActiveDocumentoUnique = NULL WHERE IsDeleted = 0,  -- purga defensiva
  MODIFY ActiveDocumentoUnique VARCHAR(120) NULL COLLATE utf8mb4_0900_ai_ci
    AS (CASE WHEN TipoDocumentoId IS NOT NULL AND NumeroDocumento IS NOT NULL AND IsDeleted = 0
             THEN CONCAT(TipoDocumentoId, ':', NumeroDocumento) ELSE NULL END),
  CREATE UNIQUE INDEX IX_Personas_ActiveDocumentoUnique (ActiveDocumentoUnique),
  ADD CONSTRAINT FK_Personas_TiposDocumento_TipoDocumentoId FOREIGN KEY (TipoDocumentoId)
    REFERENCES TiposDocumento(Id) ON DELETE RESTRICT,
  DROP COLUMN TipoDocumento;
```

## Orden DDL exacto de la migración (pasos de `Up`)

1. `CreateTable("TiposDocumento")` + `CreateIndex(IX_TiposDocumento_Codigo, unique)` + 2 `HasCheckConstraint`.
2. `InsertData("TiposDocumento", …)` desde `TipoDocumentoConstantes.Semilla` (record + array, mismo patrón que `20260618180508.cs:74-88`). 4 filas `71000000-…`.
3. Pre-flight NO fail-loud: temp table `_DirtyTipoDocumento` + `GROUP_CONCAT(DISTINCT TipoDocumento LIMIT 5)` para logging + `DROP TEMPORARY TABLE`. NO `SIGNAL SQLSTATE '45000'`.
4. `AddColumn<Guid>("TipoDocumentoId", "Personas", char(36), nullable, ascii_general_ci)` + `CreateIndex(IX_Personas_TipoDocumentoId)`.
5. Backfill parcial: `UPDATE Personas p INNER JOIN TiposDocumento t ON t.Codigo = p.TipoDocumento SET p.TipoDocumentoId = t.Id`. No matcheadas → `NULL`.
6. Drop índice + purga + alterar columna generada + crear índice único (orden obligatorio MySQL InnoDB).
7. `AddForeignKey(FK_Personas_TiposDocumento_TipoDocumentoId, OnDelete.Restrict)`.
8. `DropColumn("TipoDocumento", "Personas")` post-backfill.
9. `Down()`: `throw new NotSupportedException(...)` como **primera línea**.

## Acceso al catálogo desde `Validator`s

```csharp
public interface ITipoDocumentoCatalogoConsulta
{
    Task<IReadOnlyList<TipoDocumentoDto>> ListarAsync(CancellationToken ct = default);
    Task<TipoDocumentoDto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
}

// Fragmento CrearPersonaRequestValidator (Mockeable en unit tests)
RuleFor(x => x.TipoDocumentoId!.Value)
    .MustAsync((id, ct) => Task.FromResult(catalago.ObtenerPorIdAsync(id, ct).Result is not null))
    .WithErrorCode("FK_INEXISTENTE").When(x => x.TipoDocumentoId.HasValue);
RuleFor(x => x.NumeroDocumento)
    .MustAsync(/* match catalago.ObtenerPorIdAsync(req.TipoDocumentoId).PatronValidacion con timeout 50ms */)
    .WithErrorCode("PATRON_NO_CUMPLIDO").When(x => x.TipoDocumentoId.HasValue && !string.IsNullOrEmpty(x.NumeroDocumento));
RuleFor(x => x.NumeroDocumento)
    .MustAsync(/* valida [LongitudMinima, LongitudMaxima] */)
    .WithErrorCode("LONGITUD_FUERA_DE_RANGO").When(x => x.TipoDocumentoId.HasValue && !string.IsNullOrEmpty(x.NumeroDocumento));
```

## Auditoría D1-D2

`PersonaEntity.TipoDocumentoId` queda mapeada. Cualquier transición (`Guid → Guid`, `null → Guid`, `Guid → null`) activa `IsModified`; `ChangedPropertiesJson` incluye `"TipoDocumentoId"`; `OldValuesJson`/`NewValuesJson` registran el `Guid` (o `null`). El interceptor ya itera entradas `Modified` para entidades `AuditableEntityBase` y serializa todas las propiedades no sensibles. Test `[MySqlFact]` cubre `DNI → Pasaporte` y `null → DNI`.

## Escape JSON `PatronValidacion`

Fuente C# (seed): `"^\d{7,8}$"` (1 `\`). Wire JSON: `"^\\d{7,8}$"` (2 `\\`). Cliente `ReadFromJsonAsync<TipoDocumentoDto>` round-trip revierte a 1 `\`. Sin código adicional.

## Plan de pruebas por capa

| Capa | Test | Tipo |
|---|---|---|
| Dominio | `Constructor_*_LanzaArgumentException` (codigo/nombre/longitud) | Unit |
| Aplicación | `Crear_FkInexistente_FK_INEXISTENTE` + `PatronNoCumplido_PATRON_NO_CUMPLIDO` + `LongitudFueraDeRango_LONGITUD_FUERA_DE_RANGO` + `AceptarValido_NoError` (mock `ITipoDocumentoCatalogoConsulta`) | Unit |
| Persistencia | `Constantes_Tiene4Valores_Unicos` + `Migration_NoContieneGuidsLiterales` + `DatosSemilla_TipoDocumento_SeedIdsMatchConstantes` | Unit |
| Persistencia | `Migracion_BackfillLimpio_*` + `Migracion_BackfillConSucio_TipoDocumentoIdNullYNumeroDocumentoPreservado` + `RecreaColumnaGeneradaConConcat` + `IndiceUnico_RechazaDuplicadoActivo` + `FK_OnDeleteRestrict_RechazaEliminarCatalogado` + `Auditoria_CambioTipoDocumentoIdRegistrado` | `[MySqlFact]` |
| Persistencia | `Modelo_Persona_TipoDocumentoIdEsChar36AsciiCiNull` + `ActiveDocumentoUniqueComputedSqlConcat` + `NavigationTipoDocumentoConfigurada` + `TiposDocumento_TablaSinIsActiveNiIsDeleted` | Unit (sin MySQL) |
| API | `GetAll_SinAuth_401` + `GetAll_ConAuth_Devuelve4Tipos` + `GetById_NoExiste_404` + `PostNoExpuesto_405` | Integration (bearer) |
| Web | `Create_Get_CargaTiposDocumento` + `Edit_Get_PreSeleccionaTipoActual` + `Post_PatronInvalido_RenderizaMensajeEspañol` + `FakePersonaApiClient.GetTiposDocumentoCalls.Count==1` | Smoke |
| Regresión | `Modelo_Persona_PreservaActiveLegajoUniqueYActiveEmailUnique` | Unit |

## Compliance

- **Clean Architecture + `strict_tdd`**: Dominio no importa EF/Aplicación; `ITipoDocumentoCatalogoConsulta` es la única dependencia de `Validator`s; tests unit con mocks.
- **`docs/decisiones-implementacion.md`**: agregar nota breve sobre bloque `71000000-…` reservado por `TipoDocumento` (mapa de bloques GUID) + nota operativa sobre la no-auditoría del backfill (`NumeroDocumento` huérfano como mitigación).
- **`AGENTS.md`**: línea en "Mapa de rangos GUID" mencionando `TipoDocumento` ocupando `71000000-…`.

## Riesgos y mitigaciones

| # | Riesgo | Mitigación |
|---|---|---|
| 1 | Backfill deja `TipoDocumentoId NULL` huérfano (UX sin selección posible) | `NumeroDocumento` preservado + `Auditorias` permite remediación post-deploy; UI muestra opción vacía |
| 2 | Drift `TipoDocumentoConstantes` vs `DatosSemilla` | Test `DatosSemilla_TipoDocumento_SeedIdsMatchConstantes` (precedente `NivelCargoConstantesTests`) |
| 3 | Cambio de forma `PersonaDto` rompe Index/Typeahead/Detalles | Compilación rompe en todos los call sites; `TipoDocumento` se mantiene anulable para back-compat |
| 4 | Race `OnPost` sin catálogo cargado | `LoadCatalogsAsync` se invoca también en cada `OnPost` que retorne `Page()` (patrón Cargos) |
| 5 | Backtracking regex expone DoS | `Regex.Match(..., RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))` |
| 6 | Bloque GUID `700…` colisión con `NivelCargo` | Reasignado a `71000000-…`; documentado en proposal § "Mapa de rangos GUID del proyecto" |

## Próximo paso

`sdd-tasks` descompone en work units TDD: constantes + dominio → entity/config/repository → migración → `DatosSemilla` + tests paridad → mapper → DTOs Contracts → servicio consulta → controller API → validators con mock → `PersonaServicioComandos` → `IPersonaApiClient` + Create/Edit + `_Form.cshtml` → tests web → regenerar `docs/migracion-inicial-sgv.sql` + actualizar `AGENTS.md` y `docs/decisiones-implementacion.md`.