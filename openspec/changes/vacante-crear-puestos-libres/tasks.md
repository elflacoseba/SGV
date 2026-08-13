# Tasks: vacante-crear-puestos-libres

**Total**: 19 tasks / 5 work units. **Estrategia**: single PR — bajo budget.

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

- [ ] **T-08** [Backend] Agregar `[HttpGet("disponibles")] GetDisponibles(CancellationToken)` en `PuestosController.cs` con `[ProducesResponseType]` 200/401. `[Authorize]` heredado.
- [ ] **T-09** [Test] 3 `[Fact]` en `PuestosControllerTests.cs`: `GetDisponibles_ReturnsOkWithDtoArray`, `…_WithoutCredentials_ReturnsUnauthorized`, `GetAll_NoModificaShape_GetDisponiblesTambien`.

## Phase 4: Web integration (WU-4)

- [ ] **T-10** [Frontend] Agregar `PuestosDisponiblesBase`/`PuestosDisponiblesRoot` a `VacanteApiRoutes.cs`.
- [ ] **T-11** [Frontend] Agregar `ListarPuestosDisponiblesAsync` a `IVacanteApiClient.cs`. `ListarPuestosAsync` intacto.
- [ ] **T-12** [Frontend] Implementar `ListarPuestosDisponiblesAsync` en `VacanteApiClient.cs` (espejo de `ListarPuestosAsync`).
- [ ] **T-13** [Test] Crear `VacanteApiClientListarPuestosDisponiblesTests.cs` con 4 tests (espejo del existente): ruta `/api/v1/puestos/disponibles`, 500→`HttpRequestException`, token pre-cancelado, transport fails (`[Theory]`).
- [ ] **T-14** [Test] Extender `FakeVacanteApiClient.cs` con `ListarPuestosDisponiblesResult`/`Calls`/`Exception` + método.
- [ ] **T-15** [Frontend] Cambiar línea 232 de `Create.cshtml.cs`: `ListarPuestosAsync` → `ListarPuestosDisponiblesAsync`. `try/catch` y markup sin cambios.
- [ ] **T-16** [Test] Adaptar `Get_Create_WhenMutationRole_RendersFormWithCatalogs` con `ListarPuestosDisponiblesResult` + `Assert.Empty(apiClient.ListarPuestosCalls)`. Migrar 4 tests hermanos (líneas 76, 106, 156, 202, 244) y `ListarPuestosException` → `ListarPuestosDisponiblesException` (línea 124). Agregar `Get_Create_DropdownSoloIncluyeDisponibles`.

## Phase 5: Verification (WU-5)

- [ ] **T-17** [Wiring] `dotnet build SGV.slnx` + `dotnet test SGV.slnx`. Si MySQL local: 7 `[MySqlFact]` verdes. Si no: skipean limpio.
- [ ] **T-18** [Wiring] Revisar diff contra AC del `proposal.md`.
- [ ] **T-19** [Docs] Evaluar entrada en `docs/decisiones-implementacion.md` (probablemente N/A).

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
