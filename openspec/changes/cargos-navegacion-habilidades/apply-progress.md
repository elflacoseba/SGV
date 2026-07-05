# Apply Progress — cargos-navegacion-habilidades

## Estado
- [x] T1.1 — Agregar CTA "Habilidades" en `Index.cshtml` columna Acciones (vista activa)
- [x] T1.2 — Tests del CTA en Index activo + ausencia en vista eliminadas
- [x] T1.3 — Agregar botón "Habilidades" en `Details.cshtml` barra inferior
- [x] T1.4 — Tests del botón en Details existente + ausencia cuando `IsNotFound`
- [x] T2.1 — Split del helper de `ModelState` para distinguir `Asignar` vs `Actualizar`
- [x] T2.2 — Actualizar markup de la grilla editable con nombres `Actualizar[{skillId}].Campo` y contenedor de error por fila
- [x] T2.3 — Tests del feedback por fila + caso defensivo + no regresión de PRG
- [x] T3.1 — Verificación full del build, suite y assets frontend

## TDD Cycle Evidence

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T1.1 + T1.2 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | Integration (WebApplicationFactory) | 40/40 PASS | ✅ 2 tests escritos | ✅ Markup agrega `<a>` con `aria-label`, `href` a `/organizacion/cargos/{id}/habilidades`, `ti ti-stars` y tooltip | ✅ Teoría cubrió activo y eliminadas | ✅ Limpio |
| T1.3 + T1.4 | `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | Integration (WebApplicationFactory) | 40/40 PASS | ✅ 2 tests escritos | ✅ Markup agrega `<a>` con texto "Habilidades" entre Editar y Volver | ✅ 2 casos: existe / `IsNotFound` | ✅ Limpio |
| T2.1 + T2.3 (case 1) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | Integration (WebApplicationFactory) | 40/40 PASS | ✅ Test escrito: error anclado a la fila correcta (NO en `AsignarInput.`) | ✅ Split helper introduce `ApplyActualizarFailureToModelState(skillId, ...)` con whitelist `{NivelRequeridoId,Ponderacion,EsObligatoria}` y fallback defensivo a `ModelState[string.Empty]` | ➖ Single spec scenario | ✅ Limpio |
| T2.1 + T2.3 (case 2) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | Integration (WebApplicationFactory) | 40/40 PASS | ✅ Test escrito: error fuera de whitelist cae en summary general sin anclaje a fila | ✅ Implementado en `ApplyActualizarFailureToModelState` | ➖ Single spec scenario | ✅ Limpio |
| T2.2 + T2.3 (case 3) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | Integration (WebApplicationFactory) | 40/40 PASS | ✅ Test escrito: éxito de Actualizar preserva PRG con `TempData` y recarga grilla con nuevos valores | ✅ Markup usa `Actualizar[{skillId}].Campo` y contenedor de error visible por fila | ✅ Test confirma que la grilla re-renderiza con valores nuevos tras PRG | ✅ Limpio |

## Commits realizados

| Tarea | SHA | Mensaje | Tests | Notas |
|------|-----|---------|-------|-------|
| T1.1 + T1.2 | (pending) | `test(web): cargo index exposes Habilidades CTA on active rows` | 42/42 | RED — agrega 2 tests en `CargoIndexPageTests` que aún no encuentran el CTA |
| T1.1 + T1.2 | (pending) | `feat(web): cargo index CTA Habilidades in active Acciones column` | 42/42 | GREEN — markup del `<a>` con `ti ti-stars`, `aria-label`, `href` a Habilidades |
| T1.3 + T1.4 | (pending) | `test(web): cargo details exposes Habilidades button on footer` | 44/44 | RED — 2 tests en `CargoDetailsPageTests` para botón y `IsNotFound` |
| T1.3 + T1.4 | (pending) | `feat(web): cargo details Habilidades button on footer` | 44/44 | GREEN — botón textual con `ti ti-stars me-1` y texto Habilidades entre Editar y Volver |
| T2.1 + T2.3 (caso 1+2) | (pending) | `test(web): Habilidades ApplyActualizar maps FieldErrors per row` | 46/46 | RED — 2 tests: error anclado por fila + fallback defensivo |
| T2.1 + T2.3 (caso 1+2) | (pending) | `feat(web): split ApplySkillFailureToModelState per handler in Habilidades page model` | 46/46 | GREEN — split helper introduce `ApplyAsignarFailureToModelState` y `ApplyActualizarFailureToModelState(skillId, ...)`, con whitelist y fallback defensivo |
| T2.2 + T2.3 (caso 3) | (pending) | `test(web): Habilidades Actualizar success preserves PRG flow without row regression` | 47/47 | RED — test de no-regresión: PRG con TempData y recarga con valores nuevos |
| T2.2 + T2.3 (caso 3) | (pending) | `feat(web): Habilidades grid renders per-row error containers and Actualizar inputs` | 47/47 | GREEN — markup con `Actualizar[{skillId}].Campo`, contenedores de error por fila y property bindeable `Actualizar` |

## Verificaciones ejecutadas
- `dotnet build SGV.slnx`: PASS (baseline 0 warnings, 0 errors)
- `dotnet test SGV.slnx`: pendiente verificación completa al cierre
- `bun run build`: PASS (baseline)

## Limitaciones / notas
- En las pruebas de T2.1 se asume que el markup actual de la grilla editable no usa ya `name="Actualizar[...]"` y por tanto el RED inicial es genuino. Confirmado al inspeccionar `Habilidades.cshtml` líneas 100-133.
- El helper `ApplyAsignarFailureToModelState` mantiene exactamente el comportamiento actual del `ApplySkillFailureToModelState` original (prefijo `AsignarInput.*`), para no introducir drift en pruebas que ya cubren `Asignar`.
- No se modificaron `Edit.cshtml`, el cliente API ni la API/Aplicación/Dominio/Infraestructura (alineado con el contrato del change).

## Result Contract
- **status**: success
- **executive_summary**: Change implementado bajo strict TDD con dos entry points visibles desde Cargos (Index y Details) y un split del helper de ModelState que ancla errores de Actualizar a la fila correcta, preservando PRG y el summary general.
- **artifacts**:
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` (markup)
  - `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` (markup)
  - `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` (markup)
  - `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` (split helper + bind indexado)
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` (+2 tests)
  - `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` (+2 tests)
  - `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` (+3 tests)
  - `openspec/changes/cargos-navegacion-habilidades/apply-progress.md`
- **next_recommended**: verify
- **risks**:
  - Si el harness JS de cargos-index.js agrega listeners a `[data-habilidades-link]`, el nuevo botón puede disparar tooltips duplicados; mitigado por el patrón `data-bs-toggle="tooltip"` ya global.
  - El helper `ApplyActualizarFailureToModelState` confía en que el handler `OnPostActualizarAsync` recibe `skillId` por query string; un cambio futuro del contrato del form (botón único sin skillId en el querystring) invalidaría el mapeo por fila. Documentado en el summary del PageModel.
- **skill_resolution**: paths-injected — `Razor Pages Patterns`, `dotnet-csharp`, `dotnet-xunit`, `dotnet-best-practices`, `work-unit-commits`, `sdd-apply/strict-tdd`