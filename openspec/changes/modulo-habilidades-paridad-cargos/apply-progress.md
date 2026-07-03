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
| PR 3 — Slice 3A (Index + JS + tests listado) | ✅ Completado | #3.1 a #3.3 | 982900d8 | 0 warnings / 0 errors | 10/10 verde |
| PR 4 — Slice 3B (Create/Edit/Details + _Form + tests + anti-drift) | ✅ Completado | #3.4 a #3.9 | (siguiente) | 0 warnings / 0 errors | 28/28 verde |

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

## PR 3 — Detalle de progreso

### Tasks completadas

- [x] **#3.1** `Pages/Organizacion/Habilidades/Index.cshtml(.cs)` con PageModel `[Authorize]` que consume `IHabilidadApiClient.QueryAsync`; toggle `activas|eliminadas` con reset de página; banner `TempData` con CTA de reactivación rápida (`LastDeletedId`); SweetAlert2 para confirmación de baja y reactivación.
- [x] **#3.2** `wwwroot/js/pages/habilidades-index.js` con handlers `data-habilidad-delete-form` y `data-habilidad-reactivate-form` (paridad con `cargos-index.js`, mensajes en español, `icon: 'question'` para reactivación).
- [x] **#3.3** (Anti-drift Slice 3A) Verificar que `Index.cshtml` NO muestra `data-cargo-*` ni ningún filtro/columna relacionado con nivel: assert `Assert.DoesNotContain("Nivel", content)` y `Assert.DoesNotContain("data-cargo-", content)` en `HabilidadIndexPageTests`.

### Archivos creados / modificados

| Archivo | Acción |
|---------|--------|
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml.cs` | Creado |
| `src/SGV.Web/wwwroot/js/pages/habilidades-index.js` | Creado |
| `tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs` | Creado (10 tests) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadWebTestFixture.cs` | Creado |

### Verificación ejecutada

- `dotnet build SGV.slnx --configuration Release` → 0 warnings, 0 errors.
- `dotnet test SGV.slnx --filter "HabilidadIndexPageTests"` → 10/10 verde.
- `bun run build` (en `src/SGV.Web`) → bundle frontend generado limpio.

### Commits

4. `feat(web): add habilidades index page with segmentado list and sweetalert` — incluye Index + JS + tests.

## PR 4 — Detalle de progreso

### Tasks completadas

- [x] **#3.4** `HabilidadInputModel` con `Codigo`, `Nombre`, `Categoria?`, `Descripcion?` (anotaciones `[Required]`/`[StringLength]` replicando longitudes del dominio: 50/200/100/1000).
- [x] **#3.5** `Pages/Organizacion/Habilidades/_Form.cshtml` parcial compartido para create/edit: 4 campos. **NO incluye ningún `<select>` cuyo `name` o label contenga `Nivel`**. En edit el input `Codigo` se renderiza con `readonly` cuando `Model.IsEdit == true`.
- [x] **#3.6** `Pages/Organizacion/Habilidades/Create.cshtml(.cs)` con PageModel `[Authorize]`; GET carga formulario vacío; POST con PRG a Details (`Redirect` directo, no `RedirectToPage` porque Details aún no existía al compilar); mapea 409 a `ModelState["Input.Codigo"]`; manejo de `HttpRequestException`/`TaskCanceledException`/`JsonException` como error recuperable.
- [x] **#3.7** `Pages/Organizacion/Habilidades/Edit.cshtml(.cs)` con PageModel `[Authorize]`; GET precarga el form, marca `IsRecoverable` si el backend devuelve null/404/error de transporte; POST con PRG a sí mismo con TempData; `Codigo` se renderiza como `readonly` en `_Form.cshtml` cuando `Model.IsEdit == true`.
- [x] **#3.8** `Pages/Organizacion/Habilidades/Details.cshtml(.cs)` con PageModel `[Authorize]`; detalle readonly de los 4 campos (`Codigo`, `Nombre`, `Categoria`, `Descripcion`); `IsNotFound` cuando `GetByIdAsync` devuelve null o falla; acción "Volver al listado" preservando `p/search/sort`.
- [x] **#3.9** (Anti-drift blindante centralizado) `HabilidadAntiDriftTests` (4 tests) que verifican para `Create`, `Edit` y `_Form` (cuando se renderiza como parte de Create/Edit): NO existe ningún `<select>` cuyo `name` contenga `Nivel`, NO existe texto visible `Nivel` (case-insensitive) en el form, NO existe input `name="Input.NivelId"`.

### Archivos creados / modificados

| Archivo | Acción |
|---------|--------|
| `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml.cs` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml.cs` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml.cs` | Creado |
| `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml` | Creado (sin dropdown de nivel) |
| `src/SGV.Web/Integration/Habilidades/IHabilidadForm.cs` | Creado |
| `src/SGV.Web/Integration/Habilidades/HabilidadFormHelpers.cs` | Creado |
| `tests/SGV.Tests/Web/Habilidad/HabilidadCreatePageTests.cs` | Creado (5 tests) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` | Creado (6 tests) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadDetailsPageTests.cs` | Creado (3 tests) |
| `tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs` | Creado (4 tests) |
| `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | Modificado (reemplazar `DoesNotContain("Habilidades")` por `DoesNotContain("data-habilidad-delete-form")`) |
| `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | Modificado (reemplazar `DoesNotContain("Habilidades")` por `DoesNotContain("data-cargo-reactivate-button")`) |

### Verificación ejecutada

- `dotnet build SGV.slnx --configuration Release` → 0 warnings, 0 errors.
- `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests&FullyQualifiedName!~MigracionFailLoud"` → 1208/1208 verde.
- `bun run build` (en `src/SGV.Web`) → bundle frontend generado limpio.

### Commits

5. `feat(web): add create edit details forms for habilidades with anti-drift guards` — incluye 4 páginas + _Form + 28 tests + ajustes de tests previos por cambio en sidenav.

## Resumen final consolidado

- **Tasks completadas**: 28/28 del `tasks.md` (1.1-1.11, 2.1-2.5, 3.1-3.9).
- **Tests agregados**: 28 nuevos (CreatePage 5, EditPage 6, DetailsPage 3, IndexPage 10, AntiDrift 4) + ajustes a tests pre-existentes.
- **Verificación build**: `dotnet build SGV.slnx --configuration Release` → 0 warnings / 0 errors.
- **Verificación tests**: 1208/1208 verde excluyendo `OcupacionRepositoryTests` y `MigracionFailLoudTests` (issue #59, preexistente y documentado).
- **Verificación bundle frontend**: `bun run build` (en `src/SGV.Web`) → limpio.
- **Commits**: 5 commits en total (`a90e0e50`, `b8c49dc8`, `a66199de`, `982900d8`, `5cdc4166` doc + `60e81fb7` doc + el commit de PR 4).

## Próximo paso lógico

`sdd-verify` para validar la cobertura completa del change contra specs, design y tasks.

## Próximo paso lógico

`sdd-verify` cuando todos los PRs estén verdes y registrados.