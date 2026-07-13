# Tasks: Eliminar catálogos UO/Cargo sin consumidor en Edit de Puestos (#120)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~90 (≈ 80 tests + 10 prod/doc) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR (`fix/120-uo-catalog-no-truncation`) |
| Delivery strategy | single-pr-default |
| Chain strategy | size-exception (no aplica, single-pr) |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Edit no carga UO/Cargo; sí carga superiores; doc actualizada | PR 1 → `develop` | tests + refactor + doc en un PR |

## Fase 1: RED — Tests que fallan

- [x] **1.1** Agregar `Edit_GET_NoInvocaCatalogoUnidadesOrganizativas` en `PuestoEditPageTests.cs`.
  - **Type**: red · **Files**: `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` · **Depends**: — · **Acceptance**: test compila y falla con `QueryCalls.Count == 0` y `GetAllActivasCalls.Count == 0` en GET autenticado. · **Estimate**: S

- [x] **1.2** Agregar `Edit_GET_NoInvocaCatalogoCargos` en el mismo archivo.
  - **Type**: red · **Files**: `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` · **Depends**: 1.1 · **Acceptance**: compila y falla con `FakeCargoApiClient.GetAllCalls.Count == 0`. · **Estimate**: S

- [x] **1.3** Agregar `Edit_GET_CargaPuestosSuperiores` (anti-regresión).
  - **Type**: red · **Files**: `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` · **Depends**: 1.1 · **Acceptance**: compila y falla con `FakePuestosApiClient.GetAllCalls.Count == 1` y `PuestoSuperiorOptions.Count > 0`. · **Estimate**: S

## Fase 2: GREEN — Implementación que pasa los tests

- [x] **2.1** Refactor `LoadCatalogsAsync` y constructor de `EditModel` en `Edit.cshtml.cs`.
  - **Type**: green · **Files**: `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` · **Depends**: 1.1, 1.2, 1.3 · **Acceptance**: quitar `unidadesTask`/`cargosTask`, sus ramas post-`WhenAll` y los parámetros `IUnidadOrganizativaApiClient`/`ICargoApiClient` del ctor; 3 tests de Fase 1 pasan; `UnidadOrganizativaOptions`/`CargoOptions` quedan en `[]`. · **Estimate**: S

- [x] **2.2** Validar compilación del proyecto Web.
  - **Type**: green · **Files**: — · **Depends**: 2.1 · **Acceptance**: `dotnet build SGV.slnx` sin warnings/errors nuevos en `SGV.Web`. · **Estimate**: S

## Fase 3: REFACTOR — Limpieza

- [x] **3.1** Actualizar XML-doc de `LoadCatalogsAsync` (solo `PuestoSuperiorOptions`).
  - **Type**: refactor · **Files**: `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` · **Depends**: 2.2 · **Acceptance**: comentario actualizado, comportamiento idéntico, suite focalizada sigue verde. · **Estimate**: S

- [x] **3.2** Agregar sección "Patrón catálogo vs listado — Unidades Organizativas" en `decisiones-implementacion.md`.
  - **Type**: refactor · **Files**: `docs/decisiones-implementacion.md` · **Depends**: 2.2 · **Acceptance**: sección presente (catálogo `GetAllActivasAsync` solo Create, listado `QueryAsync` para Index, Edit no carga catálogos). · **Estimate**: S

## Fase 4: VERIFICATION

- [x] **4.1** Correr `dotnet test SGV.slnx --filter "FullyQualifiedName~PuestoEdit"`.
  - **Type**: verification · **Files**: — · **Depends**: 3.1, 3.2 · **Acceptance**: 3 tests nuevos verdes + tests previos válidos. · **Estimate**: S

- [x] **4.2** Correr `dotnet build SGV.slnx` y `dotnet test SGV.slnx --no-build` (3 veces, regla del repo).
  - **Type**: verification · **Files**: — · **Depends**: 4.1 · **Acceptance**: build limpio y tres corridas sin regresiones. · **Estimate**: S

## Notas de orden TDD

- `1.1`-`1.3` en commit RED aislado (solo tests que fallan).
- `2.1` en commit GREEN junto al borrado del dead code.
- `3.1`/`3.2` en commits REFACTOR, un propósito por commit.
- `4.1`/`4.2` son gates de aceptación, sin commits.