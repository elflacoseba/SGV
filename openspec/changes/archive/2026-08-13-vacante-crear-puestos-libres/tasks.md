# Tasks: vacante-crear-puestos-libres

**Total**: 19 tasks / 5 work units. **Estrategia**: single PR con `size:exception` retroactivo (ver nota abajo).

> **Size exception (autorizado por usuario 2026-08-13)**: budget 400 excedido en 1.77× (707 reales vs 400). Origen: `PuestoRepositoryListarDisponiblesTests.cs` (391 líneas, 55% del total) — subestimación de tests `[MySqlFact]` con setup topológico por escenario. **Decisión**: avanzar con verify + PR simple hacia `develop`.

## Review Workload Forecast

- **Decision needed before apply: No**
- **Chained PRs recommended: No**
- **400-line budget risk: Low**
- **Estimated changed lines**: 310
- **Estimated test lines**: 220

## Work-unit commits

1. `feat(puestos): ListarDisponiblesAsync — repo + servicio` (WU-1)
2. `test(persistencia): 7 [MySqlFact]` (WU-2)
3. `feat(api): GET /api/v1/puestos/disponibles` (WU-3)
4. `feat(web): Vacantes/Create consume disponibles` (WU-4)
5. `chore(sdd): verify — build + test` (WU-5)

## Phase 1: Backend foundation (WU-1)

- [x] **T-01** [Backend] Agregar `ListarDisponiblesAsync` a `IPuestoRepository.cs`.
- [x] **T-02** [Test] Agregar `ListarDisponiblesAsync` a `FakePuestoRepository` (`PuestoServicioConsultaTests.cs:317`).
- [x] **T-03** [Test] Stub `NotSupportedException` en 2 `FakePuestoWriteRepository` (`PuestoServicioComandosTests.cs:510`, `OcupacionServicioComandosTests.cs:1294`).
- [x] **T-04** [Backend] Implementar `ListarDisponiblesAsync` en `PuestoRepository.cs`: `AsNoTracking` + `IsActive && !IsDeleted` + `!Ocupaciones.Any(o => !o.IsDeleted && o.FechaFin == null)` + `!Vacantes.Any(v => !v.IsDeleted && v.FechaCierre == null)` + `Include(UO, Cargo)` + `OrderBy(Nombre).ThenBy(Codigo)` + `MapToDomain`.
- [x] **T-05** [Backend] Agregar `ListarDisponiblesAsync` a `IPuestoServicioConsulta` + delegador puro en `PuestoServicioConsulta`.
- [x] **T-06** [Test] 3 `[Fact]` en `PuestoServicioConsultaTests.cs`: delegacion+mapeo, vacío, shape relaciones.

## Phase 2: Persistencia `[MySqlFact]` (WU-2)

- [x] **T-07** [Test] Crear `PuestoRepositoryListarDisponiblesTests.cs` con 7 `[MySqlFact]` (prefijo `ListarDisponibles_MySql_`): InactivoOSoftDeleted, ConOcupacionVigente, ConVacanteAbierta, CasoCombinadoPorOcupacion, ConOcupacionVigenteAunSiSoftDeleted, ConVacanteAbiertaAunSiSoftDeleted, SoloDisponiblesOrdenadosPorNombre. Setup `TestSgvDbContextFactory` + `RepositoryTestData.CreatePuesto`, token único, `try/finally`.

## Phase 3: API layer (WU-3)

- [x] **T-08** [Backend] Agregar `[HttpGet("disponibles")] GetDisponibles(CancellationToken)` en `PuestosController.cs` con `[ProducesResponseType]` 200/401. `[Authorize]` heredado.
- [x] **T-09** [Test] 3 `[Fact]` en `PuestosControllerTests.cs`: `GetDisponibles_ReturnsOkWithDtoArray`, `…_WithoutCredentials_ReturnsUnauthorized`, `GetAll_NoModificaShape_GetDisponiblesTambien`.

## Phase 4: Web integration (WU-4)

- [x] **T-10** [Frontend] Agregar `PuestosDisponiblesBase`/`PuestosDisponiblesRoot` a `VacanteApiRoutes.cs`.
- [x] **T-11** [Frontend] Agregar `ListarPuestosDisponiblesAsync` a `IVacanteApiClient.cs`. `ListarPuestosAsync` intacto.
- [x] **T-12** [Frontend] Implementar `ListarPuestosDisponiblesAsync` en `VacanteApiClient.cs` (espejo de `ListarPuestosAsync`).
- [x] **T-13** [Test] Crear `VacanteApiClientListarPuestosDisponiblesTests.cs` con 4 tests (espejo del existente): ruta `/api/v1/puestos/disponibles`, 500→`HttpRequestException`, token pre-cancelado, transport fails (`[Theory]`).
- [x] **T-14** [Test] Extender `FakeVacanteApiClient.cs` con `ListarPuestosDisponiblesResult`/`Calls`/`Exception` + método.
- [x] **T-15** [Frontend] Cambiar línea 232 de `Create.cshtml.cs`: `ListarPuestosAsync` → `ListarPuestosDisponiblesAsync`. `try/catch` y markup sin cambios.
- [x] **T-16** [Test] Adaptar `Get_Create_WhenMutationRole_RendersFormWithCatalogs` con `ListarPuestosDisponiblesResult` + `Assert.Empty(apiClient.ListarPuestosCalls)`. Migrar 4 tests hermanos (líneas 76, 106, 156, 202, 244) y `ListarPuestosException` → `ListarPuestosDisponiblesException` (línea 124). Agregar `Get_Create_DropdownSoloIncluyeDisponibles`.

## Phase 5: Verification (WU-5)

- [x] **T-17** [Wiring] `dotnet build SGV.slnx` + `dotnet test SGV.slnx`. Si MySQL local: 7 `[MySqlFact]` verdes. Si no: skipean limpio.
- [x] **T-18** [Wiring] Revisar diff contra AC del `proposal.md`.
- [x] **T-19** [Docs] Evaluar entrada en `docs/decisiones-implementacion.md` (probablemente N/A).

## Resumen

WU-1 ~95 / WU-2 ~140 / WU-3 ~55 / WU-4 ~135 / WU-5 0–5. **Total ~310 líneas**.

## Test coverage matrix

| AC | Test(s) |
| --- | --- |
| AC-1 filtro 2 NOT EXISTS | T-07 (Ocupación/Vacante/combinado) + T-09 `GetDisponibles_ReturnsOkWithDtoArray` |
| AC-1 soft-deleted/inactivos | T-07 `…_InactivoOSoftDeleted` |
| AC-1 cobertura+finalizada→incluido | T-07 `…_ConOcupacionVigenteAunSiSoftDeleted`, `…_ConVacanteAbiertaAunSiSoftDeleted` |
| AC-2 dropdown consume disponibles | T-16 (adaptado + nuevo) |
| AC-3 `[MySqlFact]` × 4+ | T-07 (7 métodos) |
| AC-4 N1 + constraint intactos | Regresión `Crear_PuestoConOcupacionActiva_DevuelveConflictoPuestoOcupado`, `Crear_PuestoSinOcupacion_Exito` |
| AC-5 `GET /api/v1/puestos` sin cambios | T-09 `GetAll_NoModificaShape_GetDisponiblesTambien` |
| AC-6/7 build+test verde | T-17 |
| AC-8 `ListarPuestosAsync` intacto | T-13 preexisting + T-16 `Assert.Empty(apiClient.ListarPuestosCalls)` |

## Open questions (resueltas)

1. `PuestosControllerTests` **existe**; T-09 se agrega al final.
2. Convención `[MySqlFact]`: `ListarDisponibles_MySql_<Contexto>_<Comport>`, 1 método por escenario.
3. `FakePuestoRepository.ListarDisponiblesAsync` devuelve datos sin filtro (filtro vive en repo real con `[MySqlFact]`).

## Out-of-scope

NO modifica: agregados de dominio, validación N1, constraint `ActivePuestoIdUnique`, `GET /api/v1/puestos`, `IVacanteApiClient.ListarPuestosAsync`, otros dropdowns, migraciones.

## Próximo paso

`sdd-apply` arranca por WU-1 (T-01→T-06). Orchestrator confirma `Decision needed before apply: No`. Si la línea base crece >400 durante apply, requiere `size:exception` retroactivo.

## Apply Progress WU-1

- **Commit**: `959611fa` — `feat(puestos): ListarDisponiblesAsync en repo + servicio + fakes (WU-1)`
- **Diff**: 10 files changed, 202 insertions(+), 0 deletions(-)
- **Tests added**: 3 (`ListarDisponiblesAsync_DelegaEnRepositorioYDevuelveDtos`, `…_CuandoNoHayDisponibles_RetornaListaVacia`, `…_DevuelveDtosConResumenRelaciones`)
- **Tests passing**:
  - `PuestoServicioConsultaTests`: 15/15 (12 existentes + 3 nuevos)
  - `PuestoServicioComandosTests`: 22/22
  - `OcupacionServicioComandosTests`: 44/44
  - `VacanteServicioComandosTests`: 32/32
  - `SGV.slnx` (suite completa): **3503/3503** passed, 0 failed, 0 skipped
- **Build**: `dotnet build SGV.slnx --nologo` → 0 errors, 76 warnings (warnings preexistentes, no introducidos por WU-1)
- **Desviaciones**: ninguna. Las nav collections `PuestoEntity.Ocupaciones` y `PuestoEntity.Vacantes` ya existían (líneas 30 y 32 de `PuestoEntity.cs`); se usó el path `p.Ocupaciones.Any(...)` / `p.Vacantes.Any(...)` del design — NO se recurrió al fallback `Context.Set<OcupacionEntity>().Where(...)`.
- **Extras (blast-radius no listados en tasks.md)**: 2 fakes de `IPuestoServicioConsulta` (en `ApiWebApplicationFactory.cs:315` y `PuestosControllerTests.cs:689`) actualizados con `ListarDisponiblesAsync` para preservar ABI del interface. Sin tests WU-3 los invocan aún; los stubs son `Task.FromResult(_data)` con un solo puesto, no rompen ni relajan comportamiento.
- **Tree SHA (evidence_revision)**: `f7acb61c34f8eff85768150d3fc7fc29c7e6e572`

## Apply Progress WU-2

- **Commit**: `aede56c9` — `test(persistencia): 7 [MySqlFact] para ListarDisponiblesAsync (WU-2)`
- **Diff**: 1 file created (`tests/SGV.Tests/Persistencia/PuestoRepositoryListarDisponiblesTests.cs`), 2 files modified (`openspec/changes/vacante-crear-puestos-libres/tasks.md`, tree). 0 src changes.
- **Tests added**: 7 — `ListarDisponibles_MySql_InactivoOSoftDeleted_ExcluyeAmbos`, `…_ConOcupacionVigente_Excluye`, `…_ConVacanteAbierta_Excluye`, `…_CasoCombinadoOcupacionYVacante_ExcluidoPorOcupacion`, `…_OcupacionFinalizada_NoExcluye`, `…_VacanteCubierta_NoExcluye`, `…_SoloDisponibles_OrdenadosPorNombreYCodigo`.
- **Tests passing**:
  - `PuestoRepositoryListarDisponiblesTests`: 7/7 (todos los nuevos)
  - `PuestoRepository` (filter `:~PuestoRepository`): 31/31 (24 previos + 7 nuevos)
  - `PuestoServicioConsultaTests`: 15/15 (intacto desde WU-1)
  - `SGV.slnx` (suite completa): **3510/3510** passed, 0 failed, 0 skipped
- **Build**: `dotnet build SGV.slnx --nologo` → 0 errors, 96 warnings (no introducidos por WU-2; warnings preexistentes en `CrearPersonaRequestValidatorTests`, `VacantesConcurrenciaTests`, `BloquearDesbloquearEliminarGatewayTests` y demás archivos ajenos a este change).
- **MySQL availability**: local `localhost:3306` con `root` sin password — bootstrap OK. `MySqlFact` corrió los 7 contra la DB `sgv_test` (sin skip).
- **Desviaciones**: la lista de escenarios del `tasks.md` original (línea 32) nombraba 7 escenarios bautizados como `ConOcupacionVigenteAunSiSoftDeleted` y `ConVacanteAbiertaAunSiSoftDeleted`; los nombres canónicos adoptados (alineados con el `spec.md` y el `design.md`) son `OcupacionFinalizada_NoExcluye` y `VacanteCubierta_NoExcluye`. La cobertura es idéntica: ambos verifican que el sistema de soft-delete de Ocupacion/Vacante libera al Puesto. Sin impacto sobre el WU-3.
- **Patrones aplicados**: `try/finally` por test, `SeedAsync`/`CleanupAsync` en orden topológico (Vacante → Ocupacion → Puesto → Cargo → UnidadOrganizativa → EstadoVacante → Persona) para evitar "association severed" por FK RESTRICT. Suffix único `Guid.NewGuid().ToString("N")[..8]` por test. Aserciones filtran por Id al comparar subsets (no se confía en `Assert.Single` contra la coleccion completa porque la DB de tests es compartida).
- **Tree SHA (evidence_revision)**: `46f7c36f59451cb0d3fd97485793654c81ff58bc` (sha256 tree: `7f57d8a0cb12c77fe93c0b574c3576a82def5901f5aef147ef116e4c58542df7`).

## Apply Progress WU-3

- **Commit**: `625c6a57` — `feat(api): GET /api/v1/puestos/disponibles (WU-3)`
- **Diff**: 3 files changed, 80 insertions(+), 1 deletion(-)
  - `src/SGV.Api/Controllers/PuestosController.cs` — agregado action `GetDisponibles` entre `GetAll` y `GetById` (el literal `disponibles` se resuelve antes que `{id:guid}` por la route table).
  - `tests/SGV.Tests/Api/PuestosControllerTests.cs` — 3 nuevos `[Fact]` (`GetDisponibles_ReturnsOkWithDtoArray`, `GetDisponibles_WithoutCredentials_ReturnsUnauthorized`, `GetAll_NoModificaShape_GetDisponiblesTambien`).
  - `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` — `FakePuestoServicio.ListarDisponiblesAsync` reemplazó `Task.FromResult(_data)` por `Task.FromResult<IReadOnlyList<PuestoDto>>([])` (filtro excluye todo en el escenario por defecto). El `SortCapturingFake` quedó intacto (ya devolvía un DTO seed en T-08).
- **Tests added**: 3 — `GetDisponibles_ReturnsOkWithDtoArray`, `GetDisponibles_WithoutCredentials_ReturnsUnauthorized`, `GetAll_NoModificaShape_GetDisponiblesTambien`.
- **Tests passing**:
  - `PuestosControllerTests`: **38/38** (35 previos + 3 nuevos)
  - `SGV.slnx` (suite completa): **3513/3513** passed, 0 failed, 0 skipped
- **Build**: `dotnet build SGV.slnx --nologo` → 0 errors, 76 warnings (preexistentes, no introducidos por WU-3).
- **Desviaciones**: el tests `GetAll_NoModificaShape_GetDisponiblesTambien` explota la nueva divergencia de la `FakePuestoServicio` por defecto (`ListAsync` → seed, `ListarDisponiblesAsync` → `[]`). Si en el futuro `GetAll` se cambiara por accidente a delegar en `ListarDisponiblesAsync`, `GetAll` devolvería `[]` y este test fallaría con un mensaje claro (`Assert.NotEmpty(all)` antes de `Assert.Empty(disponibles)`).
- **Sin cambios** en dominio, repo, servicio, web, ni `PuestoRepository.cs` (WU-1 ya cerrado). Cero cambios en migraciones.
- **Tree SHA (evidence_revision)**: `8add5d6c07f58b71f184192849944eb74a36757d` (commit `625c6a57`).

## Apply Progress WU-4

- **Commit**: `9a711e17` — `feat(web): Vacantes/Create consume puestos disponibles (WU-4)`
- **Diff**: 7 files changed, 226 insertions(+), 9 deletions(-)
  - `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs` — agregadas constantes `PuestosDisponiblesBase` (`api/v1/puestos/disponibles`) y `PuestosDisponiblesRoot` (`/api/v1/puestos/disponibles`); comentario explica el porqué (T-10).
  - `src/SGV.Web/Integration/Vacantes/IVacanteApiClient.cs` — nuevo `ListarPuestosDisponiblesAsync(CancellationToken)`; `ListarPuestosAsync` intacto (T-11).
  - `src/SGV.Web/Integration/Vacantes/VacanteApiClient.cs` — implementación espejo de `ListarPuestosAsync` apuntado a `PuestosDisponiblesRoute`; `ThrowIfCancellationRequested` + `GetAsync` + `EnsureSuccessStatusCode` + `ReadFromJsonAsync<IReadOnlyList<PuestoDto>>` + `?? []` (T-12).
  - `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` — línea 232 (`LoadPuestosAsync`): `ListarPuestosAsync` → `ListarPuestosDisponiblesAsync`; `try/catch`, markup `Create.cshtml` y variables (`Puestos`, `PuestosReady`) sin cambios (T-15).
  - `tests/SGV.Tests/Web/Vacantes/FakeVacanteApiClient.cs` — propiedades `ListarPuestosDisponiblesResult` / `ListarPuestosDisponiblesCalls` / `ListarPuestosDisponiblesException` + método `ListarPuestosDisponiblesAsync` que respeta el contrato del fake (counter, exception, result) (T-14).
  - `tests/SGV.Tests/Web/Vacantes/VacanteApiClientListarPuestosDisponiblesTests.cs` (nuevo, T-13) — 4 tests espejo de `VacanteApiClientListarPuestosTests`:
    1. `ListarPuestosDisponiblesAsync_WhenApiReturnsOk_ReturnsDtoArray` — 200 + ruta `/api/v1/puestos/disponibles`.
    2. `ListarPuestosDisponiblesAsync_WhenApiReturns500_ThrowsHttpRequestException` — non-JSON 500 → `HttpRequestException`.
    3. `ListarPuestosDisponiblesAsync_WhenTokenPreCanceled_ThrowsOperationCanceledException` — pre-canceled token sin pegada al handler.
    4. `ListarPuestosDisponiblesAsync_WhenHttpRequestFails_PropagatesTransportFailure` — `[Theory]` × 3 filas de `HttpClientExceptionScenarios.TransportExceptionData` (`TaskCanceled`, `HttpRequest`, `DnsFailure`).
  - `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` (T-16) — 5 tests adaptados al contrato nuevo + 1 nuevo:
    - `Get_Create_WhenMutationRole_RendersFormWithCatalogs`: usa `ListarPuestosDisponiblesResult` + `Assert.Single(ListarPuestosDisponiblesCalls)` + `Assert.Empty(ListarPuestosCalls)`.
    - `Get_Create_OmiteDropdownDeEstado`: usa `ListarPuestosDisponiblesResult` + asserts de Calls.
    - `Get_Create_WhenMutationRole_LoadsPuestoChangeDismissScript`: usa `ListarPuestosDisponiblesResult` + asserts de Calls.
    - `Get_Create_WhenPuestoCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave`: `ListarPuestosException` → `ListarPuestosDisponiblesException`.
    - `Post_Create_WhenSuccessful_RedirectsToDetails`: usa `ListarPuestosDisponiblesResult` (1 call, GET only) + `Assert.Empty(ListarPuestosCalls)`.
    - `Post_Create_WhenApiReturnsFieldValidationError_ShowsFieldErrorAndPreservesInput`: `ListarPuestosResult` → `ListarPuestosDisponiblesResult` + asserts (2 calls: GET + ApplyFailureAsync).
    - `Post_Create_WhenApiReturnsConflict_ShowsMessageAndPreservesInput`: `ListarPuestosResult` → `ListarPuestosDisponiblesResult` + asserts (2 calls: GET + ApplyFailureAsync).
    - **NUEVO** `Get_Create_DropdownSoloIncluyeDisponibles`: setea ambos `ListarPuestosResult` (Puesto Ocupado) y `ListarPuestosDisponiblesResult` (Puesto Libre); verifica que el HTML contiene el Id del Libre y NO contiene el Id del Ocupado. Guardarraíl contra regresiones que re-introduzcan el consumo del endpoint general.
- **Tests added**: 7 ejecuciones nuevas (6 de T-13: 3 `[Fact]` + 3 filas de `[Theory]`; 1 de T-16: nuevo `[Fact]`) + 5 tests adaptados (sin cambio de cuenta).
- **Tests passing**:
  - `VacanteApiClientListarPuestosDisponiblesTests` (nuevo): **6/6** ejecuciones.
  - `VacanteApiClientListarPuestosTests` (existente, intacto): **6/6**.
  - `VacantesCreateEditForbidTests`: **13/13** (12 previos adaptados + 1 nuevo).
  - `Web.Vacantes` (filtrado): **44/44**.
  - `SGV.slnx` (suite completa): **3520/3520** passed, 0 failed, 0 skipped.
- **Build**: `dotnet build SGV.slnx --nologo` → 0 errors, 76 warnings (preexistentes, no introducidos por WU-4).
- **Desviaciones**:
  - Las aserciones para los 2 tests POST que re-renderizan por validación/conflicto del backend usan `Assert.Equal(2, apiClient.ListarPuestosDisponiblesCalls.Count)` en lugar de `Assert.Single` porque `LoadPuestosAsync` se invoca 2× (GET inicial + re-render desde `ApplyFailureAsync`). El test de POST exitoso (Redirect) sí conserva `Assert.Single` (1 sola llamada durante GET).
  - El delta de tests publicados en la consigna era "+8 ejecuciones"; el delta real es **+7** (5 métodos nuevos: 4 de T-13 + 1 de T-16; ejecuciones: 6 de T-13 [3 Fact + 3 Theory rows] + 1 de T-16 = 7). El conteo de métodos cuadra exactamente con el spec; el conteo de ejecuciones difiere porque las 3 filas del `[Theory]` se cuentan individualmente en xUnit.
- **Sin cambios** en dominio, repo, servicio, controller, ni migraciones. `IVacanteApiClient.ListarPuestosAsync` y `VacanteApiClient.ListarPuestosAsync` permanecen intactos para otros consumers potenciales.
- **Tree SHA (evidence_revision)**: `752ca352ea2dcd344c68c1b20faf936bf9b64cf5` (commit `9a711e17`).

## Apply Progress WU-5

- **Final test count**: **3520** passed / 0 failed / 0 skipped (sin `[MySqlFact]` skipped — MySQL local `localhost:3306` con `root` sin password disponible, los 7 escenarios de T-07 corrieron y pasaron).
- **Build**: `dotnet build SGV.slnx --nologo` → **0 errors, 96 warnings** (todos preexistentes — `xUnit1031`, `EF1002`, `xUnit2002`, `xUnit1026`, `xUnit2013`, `xUnit2029`, `NU1510` — en archivos ajenos al change).
- **Commit chain** (4 WU feature commits + 1 chore de cierre, sobre `2033fd2c` de `origin/develop`):

  | WU | SHA | Mensaje |
  |---|---|---|
  | WU-1 | `ed82bd23` | feat(puestos): ListarDisponiblesAsync en repo + servicio + fakes (WU-1) |
  | WU-2 | `59910788` | test(persistencia): 7 [MySqlFact] para ListarDisponiblesAsync (WU-2) |
  | WU-3 | `625c6a57` | feat(api): GET /api/v1/puestos/disponibles (WU-3) |
  | WU-4 | `9a711e17` | feat(web): Vacantes/Create consume puestos disponibles (WU-4) |
  | WU-5 (cierre) | `fecdd027` | chore(sdd): apply-progress WU-4 (este commit, cierre SDD) |

- **Diff stats** (`origin/develop..HEAD` = `2033fd2c..fecdd027`):
  - 19 archivos cambiados
  - **983 insertions / 9 deletions = 992 líneas totales**
  - Producción: 9 archivos (controller, 2 interfaces, 1 impl servicio, 1 repo, 1 routes contract, 2 web integration, 1 razor page) ≈ 111 líneas
  - Tests: 9 archivos (3 service test files, 1 controller test, 1 factory, 1 fake, 1 nuevo `[MySqlFact]` file, 1 nuevo ApiClient test file, 1 razor page tests) ≈ 711 líneas
  - Specs/meta: `tasks.md` ≈ 170 líneas
  - **Budget 400 líneas: EXCEDIDO (~2.48×)** — `tasks.md` estimaba ~310 líneas; el delta real se concentró en `PuestoRepositoryListarDisponiblesTests.cs` (391 líneas de tests `[MySqlFact]` con setup por escenario topológico y asserts filtrados por Id, siguiendo el precedente de `PuestoRepositoryQueryAsyncTests`).
  - **Implicación**: requerir flag `size:exception` retroactivo si la política es estricta. La sobre-ejecución es marginal al objetivo (más tests en `[MySqlFact]`) y todos pasan, pero el plan de apply subestimó el costo de los tests de persistencia. Recomendación: si vuelve a aplicarse un patrón "1 método por escenario" para `[MySqlFact]`, multiplicar el budget por ~1.5×.

- **AC verification** (cubrir cada AC del `proposal.md` §6 con test(s) verificables y PASSING):

  | AC | Cubierto por | PASS |
  |---|---|---|
  | AC-1: `GET /api/v1/puestos/disponibles` devuelve solo puestos activos sin Ocupación vigente NI Vacante Abierta | T-07 `[MySqlFact]` (`ListarDisponibles_MySql_InactivoOSoftDeleted_ExcluyeAmbos`, `…_ConOcupacionVigente_Excluye`, `…_ConVacanteAbierta_Excluye`, `…_CasoCombinadoOcupacionYVacante_ExcluidoPorOcupacion`) + T-09 `PuestosControllerTests.GetDisponibles_ReturnsOkWithDtoArray` | ✅ |
  | AC-2: dropdown de `Vacantes/Create` consume el nuevo endpoint y NO incluye puestos con Ocupación vigente | T-16 `VacantesCreateEditForbidTests.Get_Create_WhenMutationRole_RendersFormWithCatalogs` (1× a `ListarPuestosDisponiblesAsync`, 0× a `ListarPuestosAsync`) + T-16 `Get_Create_DropdownSoloIncluyeDisponibles` (nuevo — verifica que el HTML contiene Id del Puesto Libre y NO contiene Id del Puesto Ocupado) | ✅ |
  | AC-3: Tests `[MySqlFact]` cubren los 4 escenarios (con/sin Ocupación × con/sin Vacante Abierta) | T-07 cubre los 4 cuadrantes explícitos + 3 escenarios complementarios (soft-deleted, soft-deleted aun-si-activo, ordenados). 7 métodos `[MySqlFact]` en `PuestoRepositoryListarDisponiblesTests.cs`. | ✅ |
  | AC-4: validación backend existente (N1 `PuestoOcupado`, constraint `ActivePuestoIdUnique`) NO se modifica | Sin cambios en `VacanteServicioComandos.CrearAsync`, sin migraciones, sin diff en `PuestoEntity.Ocupaciones`/`Vacantes` nav properties. Regresión cubierta por tests vigentes: `Crear_PuestoConOcupacionActiva_DevuelveConflictoPuestoOcupado`, `Crear_PuestoSinOcupacion_Exito` (suite completa 3520/3520 verde confirma no regresión). | ✅ |
  | AC-5: `GET /api/v1/puestos` mantiene su comportamiento actual (todos los activos) | T-09 `PuestosControllerTests.GetAll_NoModificaShape_GetDisponiblesTambien` (verifica que `GetAll` sigue retornando seed, mientras `GetDisponibles` retorna `[]` — divergencia intencional que protege contra swap accidental). | ✅ |
  | AC-6: `dotnet build SGV.slnx` compila sin errores | T-17 → build verde, 0 errors, 96 warnings preexistentes. | ✅ |
  | AC-7: `dotnet test SGV.slnx` pasa sin regresión | T-17 → **3520/3520 passed, 0 failed, 0 skipped**. Diferencia vs WU-4 (3520/3520) y WU-1 (3503/3503) consistente con tests añadidos. | ✅ |
  | AC-8: `ListarPuestosAsync` en `IVacanteApiClient` permanece funcional | T-13 preexisting (`VacanteApiClientListarPuestosTests` — 6/6 ejecuciones verde, sin cambios) + T-16 `Assert.Empty(apiClient.ListarPuestosCalls)` en 5 tests adaptados — confirma 0 invocaciones del método legacy en el path Create. | ✅ |

- **Docs decision (T-19)**: **NO se requiere entrada en `docs/decisiones-implementacion.md`**.
  - **Razón**: el change introduce un endpoint ortogonal siguiendo patrones ya documentados (CQRS query method en repository/service, `[Authorize]` heredado, `HttpGet` con sub-recurso, `ApiClient` tipado con `[Theory]` de `HttpClientExceptionScenarios`, integración Razor Pages con bridge JWT). NO establece un nuevo patrón arquitectónico, NO toca capas transversales (auditoría, autorización, persistencia, identity), NO requiere migración, NO cambia la validación backend ni la semántica de N1/`ActivePuestoIdUnique`. El filtro es puramente una proyección de query defense-in-depth. El `design.md` ya documenta las decisiones internas (route antes que `{id:guid}`, blast-radius de la interfaz, análisis de índices); no hay una decisión "de proyecto" que merezca entrada transversal.

- **Risk flags post-verification**:
  - **Budget 400 líneas excedido (~2.48×)** — sin impacto funcional pero requiere flag `size:exception` retroactivo si la política es estricta. Origen: subestimación de `PuestoRepositoryListarDisponiblesTests.cs` (391 líneas) que sigue el precedente "1 método por escenario" de `PuestoRepositoryQueryAsyncTests`.
  - **Sin PR creado, sin push** — los 5 commits quedan locales en `develop`. El siguiente paso (sdd-verify) debe validar la propuesta antes de cualquier push.
  - **Spec files uncommitted** (`openspec/specs/puesto-management/spec.md`, `openspec/specs/vacante-web/spec.md`) — preexistentes al WU-1, intencionalmente NO commiteadas en este change. Corresponden archivado posterior o a otro change en curso. NO mezclar.

- **Tree SHA (evidence_revision WU-5)**: `fecdd0270...` (HEAD actual — pendiente tree hash completo al cierre).
- **Next recommended**: `sdd-verify` — ejecutar la fase de verificación SDD con el árbol completo y el `verify-report.md` que consume los apply-progress de WU-1..WU-5.
