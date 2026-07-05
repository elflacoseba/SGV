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
| T1.1 + T1.2 | `4ca00d27` | `test(web): cargo index exposes Habilidades CTA on active rows` | 40/40 → 42/42 RED | 2 tests en `CargoIndexPageTests`: activo expone CTA, eliminadas no |
| T1.1 + T1.2 | `1deb4398` | `feat(web): cargo index CTA Habilidades in active Acciones column` | 42/42 GREEN | Markup del `<a>` con `ti ti-stars`, `aria-label`, `href` a Habilidades entre Detalle y Editar |
| T1.3 + T1.4 | `40e7de01` | `test(web): cargo details exposes Habilidades button on footer` | 42/42 → 44/44 RED | 2 tests en `CargoDetailsPageTests`: botón presente / ausente cuando IsNotFound |
| (docs) | `7ecf552b` | `docs(sdd): import change 'cargos-navegacion-habilidades' artifacts` | — | Importación de los artefactos SDD (proposal, design, exploration, tasks, spec) |
| T1.3 + T1.4 | `93114206` | `feat(web): cargo details Habilidades button on footer` | 44/44 GREEN | Botón textual `btn-primary` con `ti ti-stars me-1` y texto "Habilidades" entre Editar y Volver |
| T2.1 + T2.2 + T2.3 | `41adc2f2` | `test(web): Habilidades ApplyActualizar maps FieldErrors per row` | 44/44 → 44/44 RED | 3 tests: per-row anchor (case 1), defensive fallback (case 2), no-regression PRG (case 3). Falla porque el helper sigue mapeando a `AsignarInput.*` y el markup no tiene contenedores per-row |
| T2.1 + T2.2 + T2.3 | `c8668b42` | `feat(web): split ApplySkillFailureToModelState per handler in Habilidades page model` | 44/44 → 47/47 GREEN | Helper split: `ApplyAsignarFailureToModelState` (mantiene `AsignarInput.*`) + `ApplyActualizarFailureToModelState(skillId, ...)` con whitelist `{NivelRequeridoId,Ponderacion,EsObligatoria}` y fallback a `ModelState[string.Empty]`. Markup de la grilla renderiza contenedores `invalid-feedback d-block` por fila consultando `ModelState[$"Actualizar[{skillId}].Campo"]`. |

> Nota sobre la cantidad de commits: el plan original proponía 4 commits para T2 (RED+GREEN para T2.1 y RED+GREEN para T2.2). Sin embargo, T2.1 (helper split) y T2.2 (markup) están fuertemente acoplados: el helper inyecta errores en keys `Actualizar[xxx].Campo` que el markup tiene que renderizar para que los tests pasen. Combinarlos en un único GREEN mantiene la disciplina RED→GREEN sin dejar tests rojos intermedios. Documentado aquí para que el orquestador y la verificación lo tengan presente.

## Verificaciones ejecutadas
- `dotnet build SGV.slnx`: PASS (0 warnings, 0 errors) — `2026-07-04 22:25`
- `dotnet test SGV.slnx`: **1380/1392 PASS, 12 pre-existentes `OcupacionRepositoryTests` (issue #59)** — `2026-07-04 22:28`. Los 12 fallos son todos `SGV.Tests.Persistencia.OcupacionRepositoryTests.*` por el bug conocido de migración `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`, **fuera del alcance** de este change (no se modificó persistencia, migraciones ni Dominio).
- `bun run build`: PASS (3.01 s) — `2026-07-04 22:29`

## Limitaciones / notas
- En las pruebas de T2.1 se asume que el markup actual de la grilla editable no usa ya `name="Actualizar[...]"` y por tanto el RED inicial es genuino. Confirmado al inspeccionar `Habilidades.cshtml` líneas 100-133.
- El helper `ApplyAsignarFailureToModelState` mantiene exactamente el comportamiento actual del `ApplySkillFailureToModelState` original (prefijo `AsignarInput.*`), para no introducir drift en pruebas que ya cubren `Asignar`.
- No se modificaron `Edit.cshtml`, el cliente API ni la API/Aplicación/Dominio/Infraestructura (alineado con el contrato del change).
- La propiedad bindeable `Actualizar` (dictionary) propuesta en `design.md` sección 4 NO se incorporó: el binding ASP.NET Core con keys tipo `[guid]` introducía validación fantasma de `AsignarInput.*` cuando coexistía con la `[BindProperty] AsignarInput`. Se optó por mantener el binding simple del parámetro `CargoHabilidadActualizarInputModel input` en `OnPostActualizarAsync` y delegar el mapeo de keys `Actualizar[xxx].Campo` al helper `ApplyActualizarFailureToModelState`. Esto preserva la firma del handler, evita regresiones en los tests existentes y cumple el requisito de "feedback por fila + summary" sin añadir complejidad de binding.

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