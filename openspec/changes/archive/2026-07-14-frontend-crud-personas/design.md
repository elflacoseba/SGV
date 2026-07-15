# Design: Frontend CRUD de Personas

## Technical Approach

Calcar patrón **Cargos** (#101/125): endpoint paginado `GET /api/v1/personas/consulta`, wire-types en `SGV.Contracts.Personas` (sibling), cliente HTTP en `SGV.Web/Integration/Personas/`, Razor Pages en `SGV.Web/Pages/Personas/` con ruta `/personas`. **Mover** records `PersonaDto`/`PersonaCommandResult`/`PersonaError`/`PersonaErrorType` y los `Request` desde `SGV.Aplicacion.Personas.*` a `SGV.Contracts.Personas.*` (precedente Cargo: Controller/Servicio/tests actualizan `using`; JSON shape no cambia). Typeahead como partial con `GET /api/v1/personas` cacheado + filtro ≥2 chars.

## Architecture Decisions

| Decisión | Elegido | Rationale |
|----------|---------|-----------|
| Wire-types | `SGV.Contracts.Personas` (sibling) | Subdominio independiente; `Contracts → Web` se mantiene leaf |
| Records existentes | **Mover** Aplicacion→Contracts + actualizar `using` | Precedente Cargo; evita duplicación; ~12 archivos, mismo JSON shape |
| Routing | `@page "/personas"` (sibling de `/organizacion/cargos`) | ✅ Decidido en sesión interactiva: ni carpeta (`Pages/Personas/`) ni URL cuelgan de `/organizacion/`. Spec actualizado |
| Endpoint | `GET /api/v1/personas/consulta?status=&page=&pageSize=&search=&sort=` (espejo cargos) | Repo aplica search+sort antes de Skip/Take |
| Typeahead | `GET /api/v1/personas` cacheado en `OnGetAsync`, filtro client-side ≥2 chars debounce 250ms | Spec lo pide; dataset típico <500 activos |
| Authorization | `[Authorize]` Index/Details/GET /consulta; `[Authorize(Roles=Administrador)]` Create/Edit POST + Delete/Reactivate | Spec lo fija |
| FieldErrors | `PersonaFormHelpers.ApplyFieldErrorsToModelState` con `InputPrefix="Input."`; backend camelCase; ModelState matchea `OrdinalIgnoreCase` | Espejo `CargoFormHelpers` |
| PRG + TempData | Create: PRG a Details + TempData. Delete: PRG con `?deletedId=` → GET persiste `LastDeletedId`. Reactivate: success → TempData + `ClearLastDeleted()` + Activas; fallo → permanece en Eliminadas | Espejo Cargos |
| Búsqueda backend | `Legajo OR Nombres OR Apellidos OR Email OR NumeroDocumento.Contains(q)` case-insensitive | Spec: 5 campos |
| Sort backend | 8 valores: `legajo_asc/desc`, `apellidos_asc/desc`, `nombres_asc/desc`, `email_asc/desc`; default `apellidos_asc` | Consistencia Cargos |

## Data Flow

```
SGV.Api ── PersonasController.GetConsulta ──► IPersonaServicioConsulta.ListarAsync ──► EF Core / MySQL
SGV.Web ── Pages/Personas/Index ──► IPersonaApiClient.QueryAsync ──► GET /api/v1/personas/consulta
            Create ──► CreateAsync ──► POST /api/v1/personas ──► PRG → Details + TempData
            Edit ───► UpdateAsync ──► PUT /api/v1/personas/{id} ──► PRG → Details + TempData
            Details ► GetByIdAsync ► GET /api/v1/personas/{id}
            Shared/_PersonaTypeahead ──► fetch GET /api/v1/personas (cache) + filtro JS ≥2 chars
```

## File Changes

**Backend** (modify): `PersonasController.cs` (`HttpGet("consulta")` + `using` Contracts), `IPersonaServicioConsulta.cs`+`PersonaServicioConsulta.cs` (`ListarAsync` + `using`), `IPersonaRepository.cs`+`PersonaRepository.cs` (`QueryAsync` con `ApplySort` 8 valores + search 5 campos), `ApiResults.cs` (overload `ToProblemResult(PersonaError, HttpContext?)`), `ErrorCategoriaMappers.cs` (`ToCategoria(PersonaErrorType)` + `ToTipoPersona`).

**Wire-types nuevos** en `SGV.Contracts.Personas/`: `Consultas/Dtos/{PersonaDto,PersonaSegmentoListado,PersonaListQuery,PersonaListadoDto}.cs`, `Comandos/{CrearPersonaRequest,ActualizarPersonaRequest,PersonaErrorType,PersonaCommandResult,PersonaDeleteResult}.cs`.

**Eliminados** (movidos): `Aplicacion/Personas/Consultas/Dtos/PersonaDto.cs`, `Personas/Comandos/PersonaCommandResult.cs`, `Personas/Comandos/PersonaRequests.cs`. Aplicacion: `PersonaServicioComandos.cs`, `PersonaServicioConsulta.cs` solo cambian `using`.

**Integration** (`Integration/Personas/`, 7 archivos): `IPersonaApiClient.cs`, `PersonaApiClient.cs` (`BaseRoute="/api/v1/personas"`, `BuildQueryUri` espejo Cargos, `ToCommandResultAsync` con `FieldErrors`, `MapCategoriaToLegacyType` colapsa a `Validation`), `PersonaInputModel.cs`, `PersonaListItemViewModel.cs`, `PersonaFormHelpers.cs` (`PersonaFormKeys { InputPrefix="Input.", LegajoKey, NombresKey, ApellidosKey, EmailKey, TipoDocumentoKey, NumeroDocumentoKey, TelefonoKey }`), `PersonaPostResultMapper.cs`, `IPersonaForm.cs`.

**Razor Pages** (`Pages/Personas/`, 8 archivos + 1 JS): `Index.cshtml(.cs)` (`@page "/personas"`, banner + CTA Reactivar, toggle, grilla 8 columnas, paginación), `Create/Edit/Details.cshtml(.cs)`, `_Form.cshtml`, `Shared/_PersonaTypeahead.cshtml`. JS: `wwwroot/js/pages/personas-index.js`.

**Web composition**: `Program.cs` (`AddHttpClient<IPersonaApiClient, PersonaApiClient>` con bearer, después de `IHabilidadApiClient`), `_Sidenav.cshtml` (item "Personas" icono `ti ti-user`).

**Tests**: `Api/PersonasControllerTests.cs` (Modify: `GetConsulta_*` 6 tests espejo Cargos + `SortCapturingFake`), `Aplicacion/Personas/{PersonaServicioComandos,PersonaServicioConsulta}Tests.cs` (Modify: solo `using`), `Persistencia/PersonaRepositoryTests.cs` (Modify: `QueryAsync_*` 6 `[MySqlFact]`), `tests/SGV.Tests/Web/Persona/` (Create: IndexPageTests 10, CreatePageTests 6, EditPageTests 6, DetailsPageTests 4, TypeaheadTests 3, `PersonaWebTestFixture`, `FakePersonaApiClient`, `PersonaApiClientBasicTests`, `IPersonaApiClientContractTests`, `PersonaWebSeamTests`).

## Interfaces / Contracts

```csharp
public enum PersonaErrorType { NotFound, Conflict, Validation }
public sealed record PersonaError(PersonaErrorType Type, string Code, string Message,
    int? StatusCode = null, ErrorCategoria Categoria = ErrorCategoria.Unexpected);
public sealed record PersonaCommandResult(bool IsSuccess, PersonaDto? Value,
    PersonaError? Error, IReadOnlyDictionary<string, string[]>? FieldErrors = null);
public enum PersonaSegmentoListado { Activas = 0, Eliminadas = 1 }
public sealed record PersonaListQuery(int Page, int PageSize, string? Search, string? Sort,
    PersonaSegmentoListado Segmento = PersonaSegmentoListado.Activas);
public sealed record PersonaListadoDto(IReadOnlyList<PersonaDto> Items, int Total, int Page, int PageSize);
Task<PagedResult<PersonaDto>> ListarAsync(PersonaListQuery query, CancellationToken ct = default);
Task<(IReadOnlyList<Persona> Items, int TotalCount)> QueryAsync(string? search, int page, int pageSize,
    string? sort = null, PersonaSegmentoListado segmento = PersonaSegmentoListado.Activas, CancellationToken ct = default);
```

## Testing Strategy

| Capa | Test | Approach |
|------|------|----------|
| Unit Application | `PersonaServicioConsultaTests.ListarAsync_*` (6) | Fake repo captura params; assert `PagedResult<PersonaDto>` |
| Integration `[MySqlFact]` | `PersonaRepositoryTests.QueryAsync_*` (6) | Sembrado en `sgv_test` con 3 activas + 2 eliminadas |
| Integration API | `PersonasControllerTests.GetConsulta_*` (6) | `SortCapturingFake`; assert 401/200/403 |
| Integration Web | `Web/Persona/` (`IndexPageTests` 10, `CreatePageTests` 6, `EditPageTests` 6, `DetailsPageTests` 4, `TypeaheadTests` 3) | `FakePersonaApiClient` espejo `FakeCargoApiClient`; cubre listado, toggle, role gating, PRG, 409→field error, 404 recuperable, typeahead ≥2 chars |
| Contratos | `IPersonaApiClientContractTests` (espejo Cargo) | 404→null, 204→DeleteResult.Success, 201→CommandResult.Success, 409→Conflict, 400→Validation+FieldErrors |
| Web seam | `PersonaApiClientBasicTests` (espejo `CargoApiClientBasicTests`) | 8 tests con `HttpClient` mockeado |

## Threat Matrix

`N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.`

## Migration / Rollout

**No migration required.** Cambio 100% aditivo. Rollback: borrar `Pages/Personas/`, `Integration/Personas/`, revertir `Program.cs`, `_Sidenav.cshtml` y `using`. Cero impacto en API runtime, BD o datos.

## Work Units

12 WU, 18-24h: (1) Wire-types Contracts [9 nuevos]. (2) Mover records Aplicacion→Contracts [4 deletes, ~8 `using`]. (3) Backend `/consulta`. (4) Tests backend [18]. (5) Integration client [7]. (6) DI. (7) Pages Index + JS. (8) Pages Create/Edit/Details/_Form. (9) Typeahead. (10) Navegación. (11) Tests web [~10]. (12) Doc.

## Riesgos Residuales

| # | Riesgo | Prob | Mitigación |
|---|--------|------|------------|
| 1 | ~~Spec dice `/organizacion/personas`, diseño dice `/personas`~~ Resuelto en sesión interactiva (ruta `/personas` confirmada; spec y design alineados) | ~~Alta~~ Cerrado | – |
| 2 | Mover 4 records toca ~12 archivos; riesgo de regresión si shape JSON cambia | Media | Comparar shape JSON; `dotnet test`. Fallback: duplicar con misma shape |
| 3 | Typeahead carga `/api/v1/personas` completo — si dataset >1000 activas, primer GET pesa >100KB | Baja-Media | Documentar asunción "<500 activas"; cache en `OnGetAsync`. Follow-up: `/buscar?q=` |
| 4 | `GET /consulta` expone datos personales a cualquier autenticado | Baja | Mantener matriz actual; si compliance endurece, patrón Puestos/Unidades (#101) |
| 5 | `PersonaSkill*` queda en Aplicacion | Baja | Documentar en `docs/decisiones-implementacion.md` hasta frontend de habilidades de persona |

## Open Questions

- [ ] **Pagination default**: ¿`pageSize=10` (Cargos UI) o `pageSize=20` (Cargos backend)? Recomendación: 10.
- [ ] **Typeahead hook**: ¿cómo propaga selección al form padre? Recomendación: `data-persona-typeahead-selected-id` + `change` en input hidden.