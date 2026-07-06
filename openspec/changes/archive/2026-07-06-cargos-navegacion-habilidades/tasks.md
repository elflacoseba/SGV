# Tasks — cargos-navegacion-habilidades

> **Reconciliación archive-time (2026-07-06)**: todos los checkboxes de implementación marcados a continuación como `[x]` reflejan el estado real al cierre, con `apply-progress.md` como prueba (full TDD evidence con commits `4ca00d27`, `1deb4398`, `40e7de01`, `93114206`, `41adc2f2`, `c8668b42`, `c2fb846d`, `1d64e805`; PR #87 mergeado a `develop`; `dotnet build SGV.slnx` verde; `dotnet test SGV.slnx` 1381/1393 PASS con 12 fallos pre-existentes `OcupacionRepositoryTests` por issue #59 fuera del alcance; `bun run build` verde). Esta reconciliación fue autorizada por el maintainer bajo override explícito porque el `tasks.md` persistido no se sincronizó al cierre del apply slice. Source of truth: `apply-progress.md`.

## Review Workload Forecast

| PR | Tareas | Líneas est. | Riesgo budget 400 | Chained PRs | Decisión previa |
|---|---|---|---|---|---|
| PR único (cargos-navegacion-habilidades) | T1.1, T1.2, T1.3, T1.4, T2.1, T2.2, T2.3, T3.1 | 123-245 | Low | No | No |
| Total | 8 tareas | 123-245 | — | No | No |

El change entra en una sola PR con un diff estimado de 123-245 líneas, dentro del budget de 400. No requiere split.

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

Workload estimado por bloque (alineado con `design.md` sección 9):

| Bloque | Archivos afectados | Líneas estimadas |
|---|---|---:|
| Bloque A — Entry points (W-UX) | `Index.cshtml`, `Details.cshtml`, `CargoIndexPageTests.cs`, `CargoDetailsPageTests.cs` | 56-105 |
| Bloque B — Feedback por fila (W1) | `Habilidades.cshtml`, `Habilidades.cshtml.cs`, `CargoHabilidadesPageTests.cs` | 87-175 |
| Bloque C — Verificación | Sin archivo productivo; cobertura por pipeline | 0 (sin nuevo código) |

Notas sobre el forecast:

- Single PR es suficiente porque el diff proyectado (incluyendo markup, helpers y tests web) está claramente bajo el budget de 400.
- No hay migración de BD ni cambio de contrato HTTP: el blast radius queda restringido a `SGV.Web` Razor Pages y tests web.
- El split del helper de `ModelState` introduce cohesión entre markup y PageModel, por eso T2.1 y T2.2 son codependientes y se commitean en una sola unidad de trabajo.

## Phase 1 — Entry points desde Cargos (W-UX)

- [x] **T1.1 — Agregar CTA "Habilidades" en `Index.cshtml` columna Acciones (vista activa)**
  - **Capa**: Web (markup Razor Pages)
  - **Archivos**: `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`
  - **Descripción**: Insertar un cuarto botón icon-only (`btn btn-primary btn-icon btn-sm rounded-circle`, ícono `ti ti-stars`) entre los existentes **Detalle** y **Editar**, dentro de `div.d-flex.justify-content-center.gap-1` y solo cuando `!Model.IsDeletedView`, con `aria-label="Gestionar habilidades de {Nombre}"`, `data-bs-toggle="tooltip"`, `data-bs-title="Habilidades"` y `href` construido con `@Url.Page("/Organizacion/Cargos/Habilidades", new { id = item.Id })`.
  - **Criterios**: spec `cargo-skill-ui-tabla-editable` Req 6 — escenario "Fila activa expone enlace a habilidades" y escenario "Vista eliminadas no expone enlace a habilidades".
  - **Dependencias**: —
  - **Líneas est.**: ~8-16 (markup nuevo dentro del bloque activo; sin tocar `Index.cshtml.cs` salvo que se opte por `Build*RouteValues(...)`).
  - **Strict TDD**: RED primero en `CargoIndexPageTests` mediante un test que verifique presencia del enlace en vista activa y `href` correcto por `id` (T1.2). GREEN al agregar el markup.

- [x] **T1.2 — Tests del CTA en Index activo + ausencia en vista eliminadas**
  - **Capa**: Tests web (xUnit + `SgvWebApplicationFactory`)
  - **Archivos**: `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`
  - **Descripción**: Cubrir con un test por escenario (activo y eliminadas) que el HTML renderizado por la página contiene o no el `<a>` hacia `Habilidades`. Preferir 1 `[Theory]` o como máximo 2 `[Fact]` aprovechando el setup existente del fixture.
  - **Criterios**: spec `cargo-skill-ui-tabla-editable` Req 6 (ambos escenarios).
  - **Dependencias**: T1.1 (el markup que el test verifica).
  - **Líneas est.**: ~12-24.
  - **Strict TDD**: el test es el RED inicial de T1.1; la implementación (markup) cierra el ciclo GREEN.

- [x] **T1.3 — Agregar botón "Habilidades" en `Details.cshtml` barra inferior**
  - **Capa**: Web (markup Razor Pages)
  - **Archivos**: `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml`
  - **Descripción**: Insertar botón textual `btn btn-primary` con `ti ti-stars me-1` y texto `Habilidades`, ubicado entre `Editar` y `Volver al listado`, dentro del bloque condicional `!Model.IsNotFound`, con `href` vía `@Url.Page("/Organizacion/Cargos/Habilidades", new { id = Model.Cargo!.Id })`. No tocar `Details.cshtml.cs`.
  - **Criterios**: spec `cargo-skill-ui-tabla-editable` Req 7 — escenario "Detalle existente muestra botón de habilidades" y escenario "Detalle inexistente no muestra botón".
  - **Dependencias**: —
  - **Líneas est.**: ~6-12.
  - **Strict TDD**: RED primero en `CargoDetailsPageTests` (T1.4). GREEN al insertar el markup.

- [x] **T1.4 — Tests del botón en Details existente + ausencia cuando `IsNotFound`**
  - **Capa**: Tests web
  - **Archivos**: `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs`
  - **Descripción**: Dos casos: (a) detalle existente renderiza el botón y el `href` apunta al `id` del cargo; (b) cuando `IsNotFound == true` el botón no aparece en el HTML. Reutilizar el fixture web existente.
  - **Criterios**: spec `cargo-skill-ui-tabla-editable` Req 7 (ambos escenarios).
  - **Dependencias**: T1.3.
  - **Líneas est.**: ~10-18.
  - **Strict TDD**: este test es el RED inicial de T1.3.

## Phase 2 — Feedback de validación por fila (W1)

- [x] **T2.1 — Split del helper de `ModelState` para distinguir `Asignar` vs `Actualizar`**
  - **Capa**: Web (PageModel)
  - **Archivos**: `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs`
  - **Descripción**: Reemplazar `ApplySkillFailureToModelState(...)` por dos helpers especializados:
    - `ApplyAsignarFailureToModelState(...)` — comportamiento actual (mapea a `AsignarInput.*`).
    - `ApplyActualizarFailureToModelState(skillId, ...)` — mapea cada `FieldErrors["Campo"]` a `Actualizar[{skillId}].Campo`, con `Campo ∈ {NivelRequeridoId, Ponderacion, EsObligatoria}`. Mantener `return Page()` en ambos handlers.
  - Introducir (si hace falta) propiedad bindeable `Actualizar` indexada por `skillId` para leer los valores editados sin perderlos en re-render.
  - Caso defensivo: si una key no encaja en el whitelist o no se asocia a la fila activa, agregar a `ModelState[string.Empty]` para que quede en el summary general sin anclaje.
  - **Criterios**: design.md sección 4 + spec Req 3 modificado — escenarios "Error de validación anclado a la fila correcta", "Error defensivo fuera de la fila activa" y "Éxito de edición preserva el flujo editable".
  - **Dependencias**: T2.2 opcional (se puede desarrollar antes y agregar markup al final).
  - **Líneas est.**: ~24-48.
  - **Strict TDD**: RED primero en `CargoHabilidadesPageTests`: un test que, dado un `FieldErrors["Ponderacion"] = ["Fuera de rango"]`, verifica que `OnPostActualizarAsync` con `RequestForm["skillId"] = X` NO inyecta la key bajo `AsignarInput.*`. El assert inicial ya es la luz roja antes del split.

- [x] **T2.2 — Actualizar markup de la grilla editable con nombres `Actualizar[{skillId}].Campo` y contenedor de error por fila**
  - **Capa**: Web (markup Razor Pages)
  - **Archivos**: `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml`
  - **Descripción**: En cada fila de la tabla editable, cambiar los nombres de los inputs a `Actualizar[{skillId}].NivelRequeridoId`, `Actualizar[{skillId}].Ponderacion` y `Actualizar[{skillId}].EsObligatoria`. Debajo de cada control agregar contenedor de error visible (clase Bootstrap `invalid-feedback d-block` o `text-danger`) que renderice `ModelState[$"Actualizar[{skillId}].Campo"]`. Mantener el `validation-summary` general arriba de la página (sin cambios).
  - Inputs manuales: replicar visualmente el patrón de error `text-danger` cuando el control no es bindeable por `asp-validation-for`.
  - Checkbox de `EsObligatoria`: contenedor debajo del bloque `form-check`.
  - **Criterios**: spec Req 3 modificado (escenarios "Render de columnas", "Error de validación anclado a la fila correcta", "Error defensivo fuera de la fila activa" y "Éxito de edición preserva el flujo editable").
  - **Dependencias**: T2.1 (necesita que las keys existan en `ModelState` para que aparezcan; sin esto el markup no muestra nada).
  - **Líneas est.**: ~28-55.
  - **Strict TDD**: GREEN consolidado — los tests de T2.3 ya cubren el comportamiento por fila; este cambio de markup los debe dejar verdes sin regresiones.

- [x] **T2.3 — Tests del feedback por fila + caso defensivo + no regresión de PRG**
  - **Capa**: Tests web (xUnit + `SgvWebApplicationFactory`)
  - **Archivos**: `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs`
  - **Descripción**: Tres casos como máximo, alineados con `design.md` sección 6:
    1. `OnPostActualizarAsync` con `FieldErrors = { "Ponderacion": ["Fuera de rango"] }` y `skillId = X` → el HTML renderizado contiene el mensaje junto al input `Ponderacion` de la fila `X` y además dentro del `validation-summary`.
    2. `OnPostActualizarAsync` con un `FieldErrors` que NO pertenece a `{NivelRequeridoId, Ponderacion, EsObligatoria}` → el mensaje aparece solo en el summary general, sin anclaje en ninguna fila.
    3. `OnPostActualizarAsync` exitoso → la página hace PRG (redirect) con `TempData` y la grilla recargada mantiene los nuevos valores.
  - **Criterios**: spec Req 3 modificado (todos los escenarios relevantes).
  - **Dependencias**: T2.1 + T2.2.
  - **Líneas est.**: ~35-72.
  - **Strict TDD**: los casos 1 y 2 comienzan como RED (escribirse ANTES de implementar T2.1 y T2.2); el caso 3 es RED→GREEN para cubrir no-regresión del flujo PRG.

## Phase 3 — Verificación final

- [x] **T3.1 — Verificación full del build, suite y assets frontend**
  - **Capa**: Web (build + test + assets)
  - **Archivos**: sin cambios de fuente; ejecución de comandos sobre la solución y `src/SGV.Web`.
  - **Descripción**: Ejecutar en este orden:
    1. `dotnet build SGV.slnx` debe finalizar sin warnings nuevos y sin errores.
    2. `dotnet test SGV.slnx` debe quedar en verde, con foco en los nuevos casos de `CargoIndexPageTests`, `CargoDetailsPageTests` y `CargoHabilidadesPageTests`.
    3. `bun run build` dentro de `src/SGV.Web` debe quedar verde (assets frontend de Inspinia/Gulp compilables).
  - **Criterios**: `success_criteria` del change — `dotnet test SGV.slnx` verde y `bun run build` verde; no hubo modificaciones de `Edit.cshtml` ni del contrato HTTP del subrecurso.
  - **Dependencias**: T1.* + T2.* completas.
  - **Líneas est.**: 0 (comandos y reportes).
  - **Strict TDD**: N/A — esta tarea es la guardia final del ciclo RED→GREEN y del presupuesto de review.

## Work Units (commits sugeridos, strict TDD RED → GREEN cuando aplica)

| Tarea | Commits sugeridos |
|---|---|
| T1.1 + T1.2 | `test(web): cargo index exposes Habilidades CTA on active rows` (RED) → `feat(web): cargo index CTA Habilidades in active Acciones column` (GREEN) |
| T1.3 + T1.4 | `test(web): cargo details exposes Habilidades button on footer` (RED) → `feat(web): cargo details Habilidades button on footer` (GREEN) |
| T2.1 + T2.3 (casos 1 y 2) | `test(web): Habilidades ApplyActualizar maps FieldErrors per row` (RED) → `feat(web): split ApplySkillFailureToModelState per handler in Habilidades page model` (GREEN) |
| T2.2 + T2.3 (caso 3) | `test(web): Habilidades Actualizar success preserves PRG flow without row regression` (RED de no-regresión) → `feat(web): Habilidades grid renders per-row error containers and Actualizar inputs` (GREEN) |
| T3.1 | `chore(verify): full test suite plus bun build stay green` (sin cambio de fuente; opcional como commit vacío para sellar la verificación) |

Cada commit incluye su evidencia local (`dotnet build`, `dotnet test` focal o `bun run build`) cuando aplique. Los tests RED se redactan junto a su markup GREEN en el mismo work unit para mantener la disciplina del repositorio (`strict_tdd: true`).

## Riesgos por tarea y mitigación

| Tarea | Riesgo | Mitigación |
|---|---|---|
| T1.1 / T1.2 | Ensanchar visualmente la columna Acciones de `Index.cshtml` | Mantener `btn-icon btn-sm rounded-circle` y posición intermedia entre Detalle y Editar; verificar el render en `Index` activo tras el cambio. |
| T1.3 / T1.4 | Filtrar el botón por error en `!Model.IsNotFound` y romper el flujo de detalle existente | Condicionar el botón exactamente al mismo branch que ya protege `Editar`; cubrir el escenario `IsNotFound == true` en T1.4. |
| T2.1 | Confundir las dos rutas de mapeo y dejar errores de `Actualizar` cayendo en `AsignarInput.*` | Mantener dos métodos separados, uno por handler; tests de T2.3 verifican que las keys de `ModelState` son las correctas por fila. |
| T2.2 | Drift entre `name=` del input manual y la key `ModelState[$"Actualizar[{skillId}].Campo"]` | Convención única escrita explícita en markup; tests HTML de T2.3 verifican la presencia del contenedor en la fila correcta. |
| T2.3 | Sobre-generar tests redundantes entre las tres clases web | Limitar a 5-7 casos nuevos totales (presupuesto de `design.md` sección 6); preferir `[Theory]` cuando aplique. |
| T2.1 / T2.2 | Romper PRG/`return Page()` por redirect accidental en error | Conservar éxito con `RedirectToPage` y `TempData`; fallos recuperables con `return Page()` (ver `design.md` secciones 4 y 7). |
| T3.1 | Olvidar el pipeline frontend tras tocar `SGV.Web` | Cierre obligatorio con `bun run build` antes de declarar la tarea como completada. |

## Próximo paso sugerido

Una vez que las tareas T1.1-T3.1 estén todas marcadas `[x]`, correr `sdd-apply` para implementar el change. No requiere chain strategy (single PR, dentro del budget de 400 líneas).

## Result Contract

- **status**: success
- **executive_summary**: `tasks.md` descompone el change `cargos-navegacion-habilidades` en 8 tareas agrupadas en tres fases (entry points W-UX, feedback por fila W1, verificación). Single PR bajo budget (123-245 líneas estimadas), con disciplina strict TDD (RED → GREEN por work unit) y commits paralelos a markup + tests.
- **artifacts**:
  - `openspec/changes/cargos-navegacion-habilidades/tasks.md`
- **next_recommended**: apply
- **risks**:
  - Drift entre keys `Actualizar[{skillId}].Campo` y los nombres reales de inputs/contenedores si T2.2 se hace antes que T2.1.
  - Sobregeneración de tests web si T2.3 se extiende más allá del presupuesto de 5-7 casos.
  - Pérdida del patrón PRG/`return Page()` si se introduce un redirect accidental en fallos de `Actualizar`.
- **skill_resolution**: paths-injected — `sdd-tasks`, `work-unit-commits`, `Razor Pages Patterns`
- **task_summary**:
  - **total**: 8
  - **completed**: 8
  - **pending**: 0
  - **allComplete**: true
  - **reconciliation_note**: contadores ajustados en archive-time a partir de `apply-progress.md` bajo override del maintainer.
