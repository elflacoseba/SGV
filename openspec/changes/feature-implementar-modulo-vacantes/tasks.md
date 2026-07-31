# Tasks: Implementar el módulo de Vacantes

> OQ-1 aprobada: `Vacante.ActualizarObservaciones(string?)` en dominio.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Slice 1 | ~1200 (Dom 35 + Contracts 90 + Repo/Mapper 260 + App 290 + Ctrl 150 + Tests 360 + Docs 25) |
| Slice 2 | ~860 (ApiClient 60 + VMs 80 + Pages 560 + Sidenav 10 + Tests 150) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Delivery strategy | feature-branch-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Work Units (chained PR)

| Unit | Goal | Base | Test cmd | Runtime | Rollback |
|------|------|------|----------|---------|----------|
| 1.x | Slice 1 backend (3 sub-PRs) | PR 1 base=feature | `dotnet test --filter "VacanteTests\|VacanteRepository\|VacantesController"` | `dotnet run SGV.Api` + `curl /api/v1/estados-vacante` | `Vacante.cs`, repo, controllers, `DependencyInjection.cs`, `EstadoVacanteConstantes.cs`, doc |
| 2.x | Slice 2 web (2 sub-PRs) | PR 2 base=PR1 | `dotnet test --filter "Vacantes.Web"` | `dotnet run SGV.Web` + `/organizacion/vacantes` + Create/Edit/Details | `Integration/Vacantes/*`, `Pages/.../Vacantes/*`, `_Sidenav.cshtml` |

## Phase 1 — Foundation

- [x] 1.1 `Vacante.ActualizarObservaciones(string?)` en `src/SGV.Dominio/Vacantes/Vacante.cs` (≤500 chars).
- [x] 1.2 RED `VacanteTests.ActualizarObservaciones_SetValido_Asigna` en `tests/SGV.Tests/Dominio/Vacantes/`.
- [x] 1.3 RED `VacanteTests.ActualizarObservaciones_Nulo_Limpia`.
- [x] 1.4 `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs`.
- [x] 1.5 DTOs en `src/SGV.Contracts/Vacantes/Consultas/Dtos/{VacanteDto,VacanteDetailDto,HistorialEstadoVacanteDto,EstadoVacanteDto}.cs`.
- [x] 1.6 `CrearVacanteRequest` + `CambiarEstadoVacanteRequest` (con `Observaciones`) en `src/SGV.Contracts/Vacantes/Comandos/`.
- [x] 1.7 `VacanteCommandResult`, `VacanteError`, `VacanteSegmentoListado`, `VacanteListQuery`.

## Phase 2 — Data layer

- [x] 2.1 `src/SGV.Infraestructura/Persistencia/Repositorios/VacanteRepository.cs` con `GetByIdAsync`, `ListarAsync(segmento)`, `ExistsAbiertaByPuestoAsync`, `GetByIdForUpdateAsync`.
- [x] 2.2 `ToDomain`/`ToEntity` en `PersistenceToDomainMapper.cs` y `DomainToPersistenceMapper.cs`.
- [x] 2.3 RED `VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales`.
- [x] 2.4 RED `VacanteRepositoryQueryTests.CambiarEstado_AtomicidadVacanteEHistorial`.

## Phase 3 — Behavior

- [x] 3.1 `src/SGV.Aplicacion/Vacantes/Comandos/{IVacanteServicioComandos,VacanteServicioComandos}.cs` (invoca `ActualizarObservaciones`).
- [x] 3.2 `FluentValidation` en `src/SGV.Aplicacion/Vacantes/Comandos/Validaciones/`.
- [x] 3.3 `src/SGV.Aplicacion/Vacantes/Consultas/{IVacanteServicioConsulta,IEstadoVacanteServicioConsulta,IVacanteRepository}.cs` + impls.
- [x] 3.4 `src/SGV.Api/Controllers/VacantesController.cs` y `EstadosVacanteController.cs` con `[Authorize]` y `RolesSgvMutacion=Administrador,GestorVacantes`.
- [x] 3.5 Registrar en `src/SGV.Infraestructura/DependencyInjection.cs`.
- [x] 3.6 RED `VacanteServicioComandosTests` (conflict PuestoId-abierta, terminal inmutable, atomicidad).
- [x] 3.7 RED `VacantesControllerTests` (201/400/403/404/409/401; `?status=invalido`→abiertas).
- [x] 3.8 `src/SGV.Infraestructura/Persistencia/Catalogos/EstadoVacanteConstantes.cs` + test paridad.
- [x] 3.9 Bloque `20000000-…` en `docs/decisiones-implementacion.md` (sección "Mapa de bloques GUID").

## Phase 4 — Web read

- [ ] 4.1 `src/SGV.Web/Integration/Vacantes/{IVacanteApiClient,VacanteApiClient,VacanteListItemViewModel}.cs`.
- [ ] 4.2 `src/SGV.Web/Pages/Organizacion/Vacantes/{Index.cshtml,Index.cshtml.cs}` (filtros segmento, estado recuperable).
- [ ] 4.3 `AddHttpClient<IVacanteApiClient,VacanteApiClient>` en `src/SGV.Web/Program.cs`.
- [ ] 4.4 RED `VacantesIndexSmokeTests` (Index 200 con `SgvWebApplicationFactory`).

## Phase 5 — Web write

- [ ] 5.1 `VacanteInputModel`, `VacanteDetailViewModel` en `src/SGV.Web/Integration/Vacantes/`.
- [ ] 5.2 `Create.cshtml(.cs)` (catálogos, `Forbid()` sin rol, PRG).
- [ ] 5.3 `Edit.cshtml(.cs)` (prellenar estado+observaciones; invoca `ActualizarObservaciones`).
- [ ] 5.4 `Details.cshtml(.cs)` (historial cronológico).
- [ ] 5.5 Modificar `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` (grupo "Vacantes", "Nueva" gated por `esAdministrador || User.IsInRole(GestorVacantes)`).
- [ ] 5.6 RED `VacantesCreateEditForbidTests` (sin rol→Forbid).
- [ ] 5.7 GREEN cobertura segmento no mezclado en Index.

## PB-1 a PB-5 — Mapeo y confirmación

| PB | Decisión | Tareas | Estado |
|----|----------|--------|--------|
| PB-1 | Mutaciones: `Administrador` o `GestorVacantes` | 3.4, 4.2, 5.2, 5.3, 5.5 | ✅ Confirmado |
| PB-2 | Creación solo desde módulo Vacantes (sin botón en `Puestos/Details`) | 5.2 | ✅ Confirmado |
| PB-3 | `Motivo` opcional al cerrar | 3.2, 1.6, 5.3 | ✅ Confirmado |
| PB-4 | Details muestra `HistorialEstadoVacante` | 1.5, 5.4 | ✅ Confirmado |
| PB-5 | Default `abiertas` | 3.4, 4.2 | ✅ Confirmado |

## OQ resuelta

- **OQ-1 (Aprobada)**: `Vacante.ActualizarObservaciones` → 1.1, 1.6, 3.1, 5.3.
- **OQ-2 (Resuelta)**: `EstadoVacanteConstantes` con test paridad → 3.8.
- **OQ-3 (Resuelta)**: `CambiarEstadoVacanteRequest` con `Observaciones` opcional → 1.6.