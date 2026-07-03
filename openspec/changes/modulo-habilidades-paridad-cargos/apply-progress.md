# Apply Progress: módulo de Habilidades en SGV.Web con paridad completa con Cargos

**Change**: `modulo-habilidades-paridad-cargos`
**Mode**: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
**Delivery**: Stacked-to-main, 4 PRs (Slice 1/A, Slice 2, Slice 3A, Slice 3B)

Estado inicial: baseline limpio, sin cambios previos. `dotnet build SGV.slnx --configuration Release` produce 0 warnings / 0 errors.

## Estrategia

- Test runner: `dotnet test SGV.slnx`.
- Reglas: cada task declara su test xUnit; flujo test primero (rojo) → implementación (verde) → refactor. Los commits se hacen por work-unit cohesivo, con prefijo conventional y sin Co-Authored-By.
- Estrategia stacked-to-main: cada PR commitea sobre el HEAD local y se valida antes de pasar al siguiente.

## Resumen por PR

| PR | Estado | Tasks # | Commits | Verif build | Verif tests |
|----|--------|---------|---------|-------------|-------------|
| PR 1 — Slice 1/A (Backend + tests xUnit) | ✅ Completado | #1.1 a #1.11 | a90e0e50, b8c49dc8 | 0 warnings / 0 errors | 191/191 backend nuevos |
| PR 2 — Slice 2 (Cliente + shell) | ✅ Completado | #2.1 a #2.5 | a66199de | 0 warnings / 0 errors | 21/21 verde |
| PR 3 — Slice 3A (Index + JS + tests listado) | Pendiente | #3.1 a #3.3 | — | — | — |
| PR 4 — Slice 3B (Create/Edit/Details + _Form + tests + anti-drift) | Pendiente | #3.4 a #3.9 | — | — | — |

## PR 1 — Detalle de progreso

### Tasks completadas

- [x] **#1.1** `HabilidadListQuery.cs` + `HabilidadSegmentoListado` (Activas/Eliminadas)
- [x] **#1.2** Firma `QueryAsync` agregada a `IHabilidadRepository`
- [x] **#1.3** `QueryAsync` implementado en `HabilidadRepository` (server-side sort + filter + paginación + 4 tests `[MySqlFact]`)
- [x] **#1.4** `INivelHabilidadServicioConsulta` (ListAsync + GetByIdAsync)
- [x] **#1.5** `NivelHabilidadServicioConsulta` (mapping a DTO) + tests de ListAsync/GetByIdAsync
- [x] **#1.6** Firma `QueryAsync(HabilidadListQuery, ct)` agregada a `IHabilidadServicioConsulta`
- [x] **#1.7** `QueryAsync` implementado en `HabilidadServicioConsulta` retornando `PagedResult<HabilidadDto>`
- [x] **#1.8** `NivelHabilidadRepository.ListAllAsync` ahora ordena por `Orden` ascendente (antes era `Codigo`)
- [x] **#1.9** `[Authorize]` global en `SkillsController` + `[Authorize(Roles = Administrador)]` en Create/Update/Delete/Reactivate + `GetConsulta` + tests anti-drift `GetAll_JsonResponse_NoExponeNivelIdEnHabilidadDto`
- [x] **#1.10** `NivelesHabilidadController` (paralelo a `NivelesCargoController`) + tests
- [x] **#1.11** Tests de discoverability Swagger para `/api/v1/skills/consulta` y `/api/v1/niveles-habilidad`

### Archivos creados / modificados

| Archivo | Acción |
|---------|--------|
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadListQuery.cs` | Creado |
| `src/SGV.Aplicacion/Habilidades/Consultas/INivelHabilidadServicioConsulta.cs` | Creado |
| `src/SGV.Aplicacion/Habilidades/Consultas/NivelHabilidadServicioConsulta.cs` | Creado |
| `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadServicioConsulta.cs` | Modificado (QueryAsync) |
| `src/SGV.Aplicacion/Habilidades/Consultas/HabilidadServicioConsulta.cs` | Modificado (impl QueryAsync) |
| `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs` | Modificado (QueryAsync firma) |
| `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` | Modificado (impl QueryAsync + ApplySort) |
| `src/SGV.Infraestructura/Persistencia/Repositorios/NivelHabilidadRepository.cs` | Modificado (OrderBy Orden) |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificado (registro `INivelHabilidadServicioConsulta`) |
| `src/SGV.Api/Controllers/SkillsController.cs` | Modificado (`[Authorize]` + roles + `GetConsulta`) |
| `src/SGV.Api/Controllers/NivelesHabilidadController.cs` | Creado |
| `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadListQueryTests.cs` | Creado (3 tests) |
| `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` | Modificado (7 nuevos tests QueryAsync) |
| `tests/SGV.Tests/Aplicacion/Habilidades/NivelHabilidadServicioConsultaTests.cs` | Creado (4 tests) |
| `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` | Modificado (FakeQueryAsync) |
| `tests/SGV.Tests/Aplicacion/Comun/TestFakes.cs` | Modificado (FakeQueryAsync) |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modificado (FakeQueryAsync + FakeNivelHabilidadServicio) |
| `tests/SGV.Tests/Api/SkillsControllerTests.cs` | Modificado (auth headers + 12 nuevos tests) |
| `tests/SGV.Tests/Api/NivelesHabilidadControllerTests.cs` | Creado (4 tests) |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modificado (3 nuevos tests + allow /consulta) |
| `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` | Modificado (3 nuevos tests QueryAsync) |
| `tests/SGV.Tests/Persistencia/NivelHabilidadRepositoryTests.cs` | Modificado (rename + assert Orden) |

### Verificación ejecutada

- `dotnet build SGV.slnx --configuration Release` → 0 warnings, 0 errors.
- `dotnet test SGV.slnx --filter "Habilidad|NivelHabilidad|SkillsController|NivelesHabilidad|Swagger"` → 191/191 verde.
- `dotnet test SGV.slnx --filter "Api|Persistencia|Aplicacion"` → 908/920 verde. Los 12 fallos son de `OcupacionRepositoryTests` (issue #59, preexistente, documentado y fuera de scope).

### Commits

1. `feat(skills): consulta segmentada y catalogo de niveles ordenado por Orden` — incluye tipos, repos y servicios de aplicación + tests.
2. `feat(skills): authorize skills endpoints and add consulta and niveles-habilidad` — incluye controllers + auth + swagger + tests + ajuste de preflight MySQL.

## PR 2 — Detalle de progreso

### Tasks completadas

- [x] **#2.1** `HabilidadListItemViewModel` + `HabilidadListQuery` (web) + `HabilidadDeleteResult` (web) + `HabilidadInputModel` (con longitudes del dominio)
- [x] **#2.2** `IHabilidadApiClient` con 8 métodos (GetAll/GetById/Delete/Create/Update/GetNivelesHabilidad/Query/Reactivar)
- [x] **#2.3** `HabilidadApiClient` HTTP tipado + tests (ruta /api/v1/skills, /api/v1/niveles-habilidad, BuildQueryUri con status=eliminadas)
- [x] **#2.4** `AddHttpClient<IHabilidadApiClient, HabilidadApiClient>` registrado en `Program.cs` (BaseUrl + 10s timeout + ApiBearerTokenHandler) + `ProductionRegistration_ResolvesHabilidadApiClient` + `WithOverrides_HabilidadApiClient_SwapsToFakeImplementation`
- [x] **#2.5** Entrada colapsable `Habilidades` en `_Sidenav.cshtml` (icono `ti ti-star`, submenú `Listado` + `Nueva`, variable `habilidadesActive` por `StartsWithSegments`) + test `Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule`

### Archivos creados / modificados

| Archivo | Acción |
|---------|--------|
| `src/SGV.Web/Integration/Habilidades/HabilidadListItemViewModel.cs` | Creado |
| `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` | Creado |
| `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` | Creado |
| `src/SGV.Web/Integration/Habilidades/HabilidadInputModel.cs` | Creado (también cubre #3.4) |
| `src/SGV.Web/Program.cs` | Modificado (registro DI) |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modificado (entrada Habilidades) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadWebSeamTests.cs` | Creado (7 tests) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` | Creado (10 tests) |
| `tests/SGV.Tests/Web/Habilidad/FakeHabilidadApiClient.cs` | Creado (fake en memoria) |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modificado (overrides para IHabilidadApiClient) |
| `tests/SGV.Tests/Web/CargoWebTests.cs` | Modificado (nuevo test Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule) |

### Verificación ejecutada

- `dotnet build SGV.slnx --configuration Release` → 0 warnings, 0 errors.
- `dotnet test SGV.slnx --filter "HabilidadWebSeamTests|HabilidadApiClientTests|CargoWebTests"` → 21/21 verde.

### Commits

3. `feat(web): add habilidades HTTP client shell entry and sidenav` — incluye cliente tipado, VMs, DI, sidenav y tests.

## TDD Cycle Evidence

> Tabla consolidada al final del apply (cuando todas las tasks estén completadas).

## Próximo paso lógico

`sdd-verify` cuando todos los PRs estén verdes y registrados.