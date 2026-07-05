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

## Remediación post-verify (R1-R7)
- [x] R1 — Refactor del markup: `name="Actualizar[@skill.SkillId].NivelRequeridoId|Ponderacion|EsObligatoria"` literal (alineado con `design.md` sección 4)
- [x] R2 — Handler `OnPostActualizarAsync` lee los valores desde `Request.Form` por el prefijo `Actualizar[skillId].` (Opción A del orquestador)
- [x] R2 — `OnPostAsignarAsync` también lee los valores desde `Request.Form` manualmente; `AsignarInput` pierde `[BindProperty]` para evitar la interferencia del binder con las keys indexadas de la grilla
- [x] R2 — Tests existentes de `Actualizar` ajustados a la convención `Actualizar[xxx].Campo` (RED → GREEN)
- [x] R3 — Test de 2 filas (`PostActualizar_TwoRows_BackendPonderacionFieldError_AnchorsOnlyToEditedRow`) añadido para demostrar sin ambigüedad que el error de `Ponderacion` se ancla a la fila correcta (skill-A) y NO a la fila B
- [x] R3 — Anti-drift test ajustado: asserts `name="Actualizar[xxx].NivelRequeridoId"` (acepta literal, interpolación Razor o variable local) y prohíbe binding simple
- [x] R4 — `apply-progress.md` sincronizado con la implementación real (este archivo)
- [x] R5 — `dotnet build SGV.slnx`, `dotnet test SGV.slnx` y `bun run build` re-verificados verdes
- [x] R6 — Commits `test(web)` (RED) y `feat(web)` (GREEN) bajo strict TDD; conventional commits, sin `Co-Authored-By`

## TDD Cycle Evidence

### Aplicación inicial (T1.x, T2.x)

| Tarea | Test File | Layer | RED | GREEN | REFACTOR |
|------|-----------|-------|-----|-------|----------|
| T1.1 + T1.2 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | Integration (WebApplicationFactory) | ✅ 2 tests | ✅ Markup agrega `<a>` con `aria-label`, `href`, `ti ti-stars` y tooltip | ✅ Limpio |
| T1.3 + T1.4 | `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | Integration (WebApplicationFactory) | ✅ 2 tests | ✅ Markup agrega `<a>` con texto "Habilidades" entre Editar y Volver | ✅ Limpio |
| T2.1 + T2.3 (case 1) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | Integration (WebApplicationFactory) | ✅ Test escrito: error anclado a la fila correcta (NO en `AsignarInput.`) | ✅ Split helper introduce `ApplyActualizarFailureToModelState(skillId, ...)` con whitelist `{NivelRequeridoId,Ponderacion,EsObligatoria}` y fallback defensivo a `ModelState[string.Empty]` | ✅ Limpio |
| T2.1 + T2.3 (case 2) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | Integration (WebApplicationFactory) | ✅ Test escrito: error fuera de whitelist cae en summary general sin anclaje a fila | ✅ Implementado en `ApplyActualizarFailureToModelState` | ✅ Limpio |
| T2.2 + T2.3 (case 3) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` | Integration (WebApplicationFactory) | ✅ Test escrito: éxito de Actualizar preserva PRG con `TempData` y recarga grilla con nuevos valores | ✅ Markup usa `Actualizar[{skillId}].Campo` y contenedor de error visible por fila | ✅ Limpio |

### Remediación post-verify (R1-R6)

| Tarea | Test File | Layer | RED | GREEN | REFACTOR |
|------|-----------|-------|-----|-------|----------|
| R2 — Tests Actualizar ajustados a `Actualizar[xxx].Campo` (5 tests) + R3 — Test de 2 filas (1 test) + R3 — Anti-drift test endurecido (1 test) | `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs`, `CargoHabilidadesAntiDriftTests.cs` | Integration (WebApplicationFactory) + Source-string regex | ✅ 5 tests Actualizar ajustados a keys indexadas (fallaban antes del GREEN porque la markup seguía usando binding simple) + 1 test nuevo de 2 filas (fallaba porque el helper `ApplyActualizarFailureToModelState` con whitelist sólo había sido ejercitado con 1 fila) + 1 test anti-drift endurecido | ✅ Markup pasa a `name="Actualizar[@skill.SkillId].Campo"`; handler `OnPostActualizarAsync` extrae valores desde `Request.Form` con prefijo `Actualizar[skillId].`; `OnPostAsignarAsync` también se hidrata manualmente y `AsignarInput` pierde `[BindProperty]` para evitar ghost ModelState entries | ✅ Documentado en código + este `apply-progress.md` |

## Commits realizados

### Aplicación inicial

| Tarea | SHA | Mensaje | Tests | Notas |
|------|-----|---------|-------|-------|
| T1.1 + T1.2 | `4ca00d27` | `test(web): cargo index exposes Habilidades CTA on active rows` | 40/40 → 42/42 RED | 2 tests en `CargoIndexPageTests`: activo expone CTA, eliminadas no |
| T1.1 + T1.2 | `1deb4398` | `feat(web): cargo index CTA Habilidades in active Acciones column` | 42/42 GREEN | Markup del `<a>` con `ti ti-stars`, `aria-label`, `href` a Habilidades entre Detalle y Editar |
| T1.3 + T1.4 | `40e7de01` | `test(web): cargo details exposes Habilidades button on footer` | 42/42 → 44/44 RED | 2 tests en `CargoDetailsPageTests`: botón presente / ausente cuando IsNotFound |
| (docs) | `7ecf552b` | `docs(sdd): import change 'cargos-navegacion-habilidades' artifacts` | — | Importación de los artefactos SDD (proposal, design, exploration, tasks, spec) |
| T1.3 + T1.4 | `93114206` | `feat(web): cargo details Habilidades button on footer` | 44/44 GREEN | Botón textual `btn-primary` con `ti ti-stars me-1` y texto "Habilidades" entre Editar y Volver |
| T2.1 + T2.2 + T2.3 | `41adc2f2` | `test(web): Habilidades ApplyActualizar maps FieldErrors per row` | 44/44 → 44/44 RED | 3 tests: per-row anchor (case 1), defensive fallback (case 2), no-regression PRG (case 3). Falla porque el helper sigue mapeando a `AsignarInput.*` y el markup no tiene contenedores per-row |
| T2.1 + T2.2 + T2.3 | `c8668b42` | `feat(web): split ApplySkillFailureToModelState per handler in Habilidades page model` | 44/44 → 47/47 GREEN | Helper split: `ApplyAsignarFailureToModelState` (mantiene `AsignarInput.*`) + `ApplyActualizarFailureToModelState(skillId, ...)` con whitelist `{NivelRequeridoId,Ponderacion,EsObligatoria}` y fallback a `ModelState[string.Empty]`. Markup de la grilla renderiza contenedores `invalid-feedback d-block` por fila consultando `ModelState[$"Actualizar[{skillId}].Campo"]`. |
| (docs) | `16710409` | `docs(sdd): update apply-progress for cargos-navegacion-habilidades` | — | Sincronización inicial del apply-progress |

### Remediación post-verify (R1-R7)

| Tarea | SHA | Mensaje | Tests | Notas |
|------|-----|---------|-------|-------|
| R2 + R3 — Tests | `c2fb846d` | `test(web): Habilidades Actualizar tests use Actualizar[xxx].Campo form keys` | 47/47 → 47/47 RED (todos los Actualizar ajustados + 1 nuevo de 2 filas + anti-drift endurecido) | Helper compartido `BuildActualizarForm` para evitar duplicación. Test de 2 filas cubre anclaje por fila entre skill-A (Liderazgo) y skill-B (Comunicación), incluyendo validación de "no aparece en la fila B" |
| R1 + R2 — Implementación | `1d64e805` | `feat(web): Habilidades Actualizar reads values from Actualizar[xxx] form prefix` | 47/47 → 48/48 GREEN | Markup con `name="Actualizar[@skill.SkillId].Campo"` literal; `OnPostActualizarAsync` extrae valores desde `Request.Form` por prefijo y valida en línea; `OnPostAsignarAsync` también se hidrata manualmente; `AsignarInput` pierde `[BindProperty]` para que el binder no confunda las keys indexadas con propiedades del input model |

> Nota sobre los commits de remediación: la remediación post-verify combina R1 (markup), R2 (handler refactor + ajuste de tests) y R3 (test de 2 filas + endurecimiento del anti-drift) en dos commits bajo strict TDD: uno RED (`test(web)`) y uno GREEN (`feat(web)`). El plan original del orquestador sugería cuatro commits; consolidarlos en dos mantiene la disciplina RED → GREEN sin tests rojos intermedios y refleja la fuerte cohesión entre el markup, el PageModel y los tests que es característica de esta parte del change.

## Verificaciones ejecutadas

### Aplicación inicial
- `dotnet build SGV.slnx`: PASS (0 warnings, 0 errors) — `2026-07-04 22:25`
- `dotnet test SGV.slnx`: **1380/1392 PASS, 12 pre-existentes `OcupacionRepositoryTests` (issue #59)** — `2026-07-04 22:28`. Los 12 fallos son todos `SGV.Tests.Persistencia.OcupacionRepositoryTests.*` por el bug conocido de migración `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`, **fuera del alcance** de este change (no se modificó persistencia, migraciones ni Dominio).
- `bun run build`: PASS (3.01 s) — `2026-07-04 22:29`

### Remediación post-verify
- `dotnet build SGV.slnx`: PASS (0 warnings, 0 errors) — `2026-07-04 23:08`
- `dotnet test SGV.slnx`: **1381/1393 PASS, 12 pre-existentes `OcupacionRepositoryTests` (issue #59)** — `2026-07-04 23:09`. Total sube a 1393 tests por el +1 del test de 2 filas. Los 12 fallos siguen siendo los mismos `OcupacionRepositoryTests`, fuera del alcance de este change.
- `dotnet test --filter "FullyQualifiedName~CargoHabilidades"`: 24/24 PASS — confirma que el alcance del change (Index, Details, Habilidades PageModel + tests + anti-drift) está completamente verde.
- `bun run build`: PASS (2.94 s) — `2026-07-04 23:10`. El bundle frontend de Inspinia sigue compilando sin warnings nuevos.

## Limitaciones / notas

### Aplicación inicial
- En las pruebas de T2.1 se asume que el markup actual de la grilla editable no usa ya `name="Actualizar[...]"` y por tanto el RED inicial es genuino. Confirmado al inspeccionar `Habilidades.cshtml` líneas 100-133.
- El helper `ApplyAsignarFailureToModelState` mantiene exactamente el comportamiento actual del `ApplySkillFailureToModelState` original (prefijo `AsignarInput.*`), para no introducir drift en pruebas que ya cubren `Asignar`.
- No se modificaron `Edit.cshtml`, el cliente API ni la API/Aplicación/Dominio/Infraestructura (alineado con el contrato del change).
- La propiedad bindeable `Actualizar` (dictionary) propuesta en `design.md` sección 4 NO se incorporó en la primera pasada: el binding ASP.NET Core con keys tipo `[guid]` introducía validación fantasma de `AsignarInput.*` cuando coexistía con la `[BindProperty] AsignarInput`.

### Remediación post-verify
- **Decisión técnica (Opción A)**: la remediación del verify implementó la convención `Actualizar[xxx].Campo` del design usando extracción manual desde `Request.Form` con el prefijo del skill activo, en lugar del dictionary `[BindProperty] Actualizar { get; set; }` que el design proponía originalmente. Esta decisión se documenta inline en `OnPostActualizarAsync` y se sostiene sobre dos razones técnicas:
  1. El binder de Razor Pages interpreta keys tipo `Actualizar[guid].Campo` como un Dictionary-path contra CUALQUIER propiedad compleja del modelo. Cuando intentamos poblar `AsignarInput.SkillId` con `[BindProperty]`, el binder pobló esa propiedad con el GUID extraído de los corchetes y dejó el resto de `AsignarInput` vacío, generando ghost entries de ModelState (`SkillId`, `NivelRequeridoId`) que cortocircuitaban `OnPostActualizarAsync` con `[Required]` antes de llegar al upsert.
  2. La extracción manual evita esa interferencia: `OnPostActualizarAsync` lee `Request.Form[$"Actualizar[{skillId}].NivelRequeridoId"]` directamente, valida con `Guid.TryParse`/`decimal.TryParse` en línea, y empuja errores a `ModelState` bajo la MISMA convención indexada para que el contenedor per-row del markup los muestre. Sin dictionary binding, sin ghost entries, sin DataAnnotations conflictivas.
- **`AsignarInput` pierde `[BindProperty]`**: como efecto secundario de la decisión anterior, `AsignarInput` ya no se bindea automáticamente. `OnPostAsignarAsync` ahora también lo hidrata manualmente desde `Request.Form["AsignarInput.Campo"]` y valida en línea (mismo patrón que Actualizar). Esto preserva el comportamiento del flujo Asignar sin requerir DataAnnotations sobre el input model — el cual sigue intacto en `src/SGV.Web/Integration/Organizacion/CargoHabilidadInputModels.cs` por compatibilidad con el contrato del repositorio de integración.
- **`CargoHabilidadActualizarInputModel` queda como clase spare**: la firma del handler ya no toma este input model, pero el archivo `CargoHabilidadInputModels.cs` lo conserva para no romper la superficie pública del repositorio de integración. No se usa en runtime; podría eliminarse en una limpieza futura fuera del scope de este change.
- **Anti-drift test actualizado**: el regex ahora acepta tres formas equivalentes en el markup fuente (`Actualizar[<guid>].NivelRequeridoId` literal, `Actualizar[@skill.SkillId].NivelRequeridoId` con interpolación Razor, o `Actualizar[@nivelKey].NivelRequeridoId` con variable local) porque las tres producen el mismo HTML renderizado `name="Actualizar[<guid>].NivelRequeridoId"`. El test sigue siendo anti-regresión al bloquear binding simple (`name="NivelRequeridoId"` plano) en cualquier `<select>` o `<input>` del archivo.
- **Test de 2 filas (SUGGESTION del verify)**: `PostActualizar_TwoRows_BackendPonderacionFieldError_AnchorsOnlyToEditedRow` blinda sin ambigüedad el anclaje por fila frente al escenario multi-fila. Recorta el HTML renderizado entre las anclas textuales `Liderazgo` (fila A) y `Comunicación` (fila B) y exige que el mensaje del backend aparezca en ese slice; adicionalmente recorta entre `Comunicación` y `</tbody>` y exige que NO aparezca en ese slice (pertenece sólo a la fila A). El mensaje también debe estar en `validation-summary-errors`.
- **No regresiones**: los tests existentes de `Asignar` (`PostAsignar_*`) y los demás flujos (`Quitar`, errores recuperables, anti-drift) siguen verdes sin cambios. El único test añadido es el de 2 filas; los 5 tests de Actualizar fueron ajustados a la nueva convención de form keys.

## Result Contract

### Aplicación inicial
- **status**: success
- **executive_summary**: Change implementado bajo strict TDD con dos entry points visibles desde Cargos (Index y Details) y un split del helper de ModelState que ancla errores de Actualizar a la fila correcta, preservando PRG y el summary general.
- **next_recommended**: verify (cumplido)

### Remediación post-verify
- **status**: success
- **executive_summary**: La remediación del verify alinea la markup y el PageModel con `design.md` sección 4: los inputs de la grilla editable ahora usan `name="Actualizar[@skill.SkillId].Campo"` literal y `OnPostActualizarAsync` extrae esos valores directamente desde `Request.Form`. El helper `ApplyActualizarFailureToModelState` sigue intacto y ancla errores bajo la misma convención indexada. El test de 2 filas añadido blinda el comportamiento multi-fila, el anti-drift test endurecido prohíbe binding simple, y el conteo de tests subió de 1380 a 1381 PASS (sin regresiones; los 12 fallos siguen siendo los pre-existentes `OcupacionRepositoryTests` issue #59).
- **artifacts** (delta de remediación):
  - `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml` (markup con `Actualizar[xxx].Campo`)
  - `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs` (PageModel con extracción manual + `AsignarInput` sin `[BindProperty]`)
  - `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs` (+1 test de 2 filas, 5 tests Actualizar ajustados, helper `BuildActualizarForm`)
  - `tests/SGV.Tests/Web/Cargo/CargoHabilidadesAntiDriftTests.cs` (regex endurecido, anti-binding simple)
  - `openspec/changes/cargos-navegacion-habilidades/apply-progress.md` (este archivo, sincronizado)
- **next_recommended**: verify
- **risks**:
  - Si un futuro refactor reactiva `[BindProperty]` sobre `AsignarInput` SIN quitar el binding manual en `OnPostAsignarAsync`, el binder volverá a interferir con las keys indexadas de la grilla y reintroducirá ghost ModelState entries. Documentado inline en la propiedad `AsignarInput`.
  - El anti-drift test acepta tres formas equivalentes de la convención indexada (literal, interpolación, variable local). Si alguien introduce una forma adicional (por ejemplo `name="Actualizar@(skill.SkillId).NivelRequeridoId"` con paréntesis en lugar de corchetes), el test seguirá pasando pero el HTML renderizado будет невалидным. Mitigación: los tests de integración web (que envían form data con corchetes reales) validan el camino end-to-end.
  - La extracción manual desde `Request.Form` confía en que el `skillId` viene por la query string (consistente con el helper). Un cambio futuro del contrato del form (botón único sin `skillId` en la query) invalidaría el mapeo por fila. Documentado en el summary del PageModel.
- **skill_resolution**: paths-injected — `Razor Pages Patterns`, `dotnet-csharp`, `dotnet-xunit`, `dotnet-best-practices`, `work-unit-commits`, `sdd-apply/strict-tdd`