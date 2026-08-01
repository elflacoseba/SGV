# Tasks: Implementa el módulo de Auditorias

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~530 (S1+S2+S3) |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units (stackable S1→S2→S3)

| Unit | Goal | Test command | Rollback boundary |
|------|------|--------------|-------------------|
| S1 | Capa lectura: contracts + port + impl EF + DI + unit + threat-matrix RED | `dotnet test tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Borrar `Contracts/Auditoria/`, `IAuditoriaServicioConsulta.cs`, `AuditoriaServicioConsulta.cs` y su DI; no tocar `AuditoriaEntity`/interceptor/escritura |
| S2 | REST admin-only + tests API | `dotnet test tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Borrar `AuditoriasController.cs` y tests; escritura intacta |
| S3 | Cliente Web + PageModel admin + sidenav + docs | `dotnet test tests/SGV.Tests/Web/AuditoriasIndexTests.cs` + `bun run build` | Borrar `Web/Integration/Auditoria/`, `Web/Pages/Auditorias/`, revertir `Program.cs` y `_Sidenav.cshtml` |

S1 base de S2/S3. `[MySqlFact]` skipea sin DB. Cada slice borra archivos nuevos sin tocar escritura.

## Phase 1: S1 — Servicio de consulta

- [x] 1.1 (RED) `AuditoriaServicioConsultaTests.cs`: filtros omitidos no filtran, combinados sí, orden `Id DESC` en empates, `DateFrom>DateTo` → `ArgumentException`.
- [x] 1.2 (RED) Mismo archivo: JSON del DTO NO contiene `oldValuesJson`/`newValuesJson`.
- [x] 1.3 (RED) Threat-matrix (D-4): filas `Auditorias` antes/después de `QueryAsync` — ninguna nueva.
- [x] 1.4 (GREEN) `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` (record sealed, 8 campos).
- [x] 1.5 (GREEN) `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs`.
- [x] 1.6 (GREEN) `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` (`QueryAsync`/`GetByIdAsync`).
- [x] 1.7 (GREEN) `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` (EF, `AsNoTracking`, `Select` sin old/new, clamp `PageSize [1,100]`).
- [x] 1.8 (GREEN) `AddScoped<IAuditoriaServicioConsulta, AuditoriaServicioConsulta>()` en `src/SGV.Infraestructura/DependencyInjection.cs`.
- [x] 1.9 (VERIFY) `dotnet build SGV.slnx` + suite S1 verde.

## Phase 2: S2 — Controller API admin-only

- [x] 2.1 (RED) `AuditoriasControllerTests.cs`: GET sin creds → 401, sin Admin → 403, Admin → 200 `PagedResult`, paginación+filtros, detalle → 200/404, JSON sin old/new, `[Authorize]` por reflexión.
- [x] 2.2 (RED) Mismo archivo: `DateFrom>DateTo` → 400 con `ProblemDetails` y mensaje de rango invertido.
- [x] 2.3 (GREEN) `src/SGV.Api/Controllers/AuditoriasController.cs` (`[ApiController]`, `[Authorize(Roles=RolesSgv.Administrador)]`, `ArgumentException` → `ApiResults.ToValidationProblemResult`).
- [x] 2.4 (VERIFY) `dotnet build SGV.slnx` + suite API S2 verde.

## Phase 3: S3 — Web (cliente + Page + sidenav + docs)

- [x] 3.1 (RED) `AuditoriasIndexTests.cs` con `WithAuditoriaApiClient(fake)`: admin 200 con tabla y paginación, lista vacía legible, error de transporte recuperable sin perder filtros, paginación conserva filtros, no-admin → error, anónimo → redirect.
- [x] 3.2 (GREEN) `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs`.
- [x] 3.3 (GREEN) `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` (`EnsureSuccessStatusCode`, 404 → `null`).
- [x] 3.4 (GREEN) `AddHttpClient<IAuditoriaApiClient, AuditoriaApiClient>(...).AddHttpMessageHandler<ApiBearerTokenHandler>()` en `Program.cs`.
- [x] 3.5 (GREEN) `src/SGV.Web/Pages/Auditorias/Index.cshtml` + `.cs` (`[Authorize(Roles=RolesSgv.Administrador)]`, `OnGetAsync`, sidebar filtros, PRG).
- [x] 3.6 (GREEN) Entrada «Auditorías» en `_Sidenav.cshtml` gateada por `@if (esAdministrador)`.
- [x] 3.7 (GREEN) Documentar módulo transversal en `docs/decisiones-implementacion.md`.
- [x] 3.8 (VERIFY) `dotnet build SGV.slnx` + suite Web S3 + `bun run build` en `src/SGV.Web`.

## Notas

- Cost-benefit: pocos tests significativos (AGENTS.md). Sin tests de DTOs/records/DI/markup.
- Ausencia old/new: 2.1 (API); 1.2 cubre servicio.
- 1.3 = guardrail D-4 contra recursión.
