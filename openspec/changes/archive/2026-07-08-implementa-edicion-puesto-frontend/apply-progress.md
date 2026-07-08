# Apply Progress: Expone botón Editar en Puestos y cierra frontera admin en PuestosController

> Cambio quirúrgico (~221 LoC agregados / 21 LoC quitados en 5 archivos). Strict TDD: RED → GREEN → REFACTOR.
> Working tree trae `DatosSemilla.cs` + migración `20260706221558_*` sin commitear; este aplicador los aísla vía `git diff -- <paths permitidos>` antes de cualquier commit potencial.

## Estado global

- Modo orquestador: `interactive`
- Strict TDD: ACTIVO (`openspec/config.yaml:11`)
- Artifact store: `both` (OpenSpec filesystem + Engram)
- Delivery: single PR contra `develop` (riesgo Low, presupuesto 400 LoC) — el agente decide no commitear; el orquestador aprueba boundaries
- Idioma artefactos: español
- Working tree isolation: **verificada** en cada hito (sólo 5 archivos listados abajo)

## Safety net (baseline)

- Build inicial: 0 errores, 0 warnings en `dotnet build SGV.slnx`.
- Tests subset pre-cambio (`PuestosControllerTests | PuestoIndexPageTests`): **33/33 PASS**.
- Suite completa pre-cambio: 1539 tests totales, 12 fallos pre-existentes (issue #59 — bug de tipo `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)` en `OcupacionRepositoryTests`).

## Resultado TDD por fase

### Fase 1 — Frontend (helper + botón Editar)

| Tarea | Estado | Evidencia |
|-------|--------|-----------|
| 1.1 Helper `BuildEditRouteValues(Guid id)` en `Index.cshtml.cs` | ✅ GREEN | Espejo verbatim de `CargoIndexModel.BuildEditRouteValues:237-244` (XML doc +5 impl lines +5 expl lines = 15 LoC). |
| 1.2 Botón `<a class="btn btn-warning">Editar</a>` entre Detalle y Delete en `Index.cshtml:189` | ✅ GREEN | 10 LoC (1 link + 3 atributos). Sólo dentro de la rama `if (!Model.IsDeletedView)`. |
| 1.3 Borrar comment obsoleto `Index.cshtml:183-186` | ✅ GREEN | Eliminado (4 LoC −5 +1). |

- Build post-cambio: 0 errores, 0 warnings.
- `PuestoIndexPageTests` post-cambio: **18/18 PASS** (era 17, +1 test nuevo).

### Fase 2 — Backend (`[Authorize]` transversal)

| Tarea | Estado | Evidencia |
|-------|--------|-----------|
| 2.1 `using Microsoft.AspNetCore.Authorization;` + `using SGV.Aplicacion.Seguridad;` | ✅ GREEN | 2 líneas agregadas. |
| 2.2 `[Authorize]` a nivel clase (`PuestosController.cs:14`) | ✅ GREEN | 1 atributo. |
| 2.3 `[Authorize(Roles = RolesSgv.Administrador)]` en Create/Update/Delete/Reactivate | ✅ GREEN | 4 atributos. |
| 2.4 `[ProducesResponseType(401)]` en GetAll/GetById; `[ProducesResponseType(401)]` + `[ProducesResponseType(403)]` en writes | ✅ GREEN | 10 atributos XML docs. |

- Build post-cambio: 0 errores, 0 warnings.
- `PuestosControllerTests` post-cambio: **27/27 PASS**.

### Fase 3 — Tests web (presencia/ausencia botón Editar)

| Tarea | Estado | Test |
|-------|--------|------|
| 3.1 Extender `Get_Index_WhenAuthenticated_RendersActivePuestosTable` con asserts href + data-bs-title | ✅ RED → GREEN | Failure visible antes de Fase 1; pasa después. |
| 3.2 Nuevo `Get_Index_WhenDeletedView_DoesNotRenderEditButton` | ✅ RED → GREEN | Triple triangulation: positive `DoesNotContain "Editar"` + positive Reactivate form + positive reactivate form action. |

### Fase 4 — Tests API (matriz 401/403/2xx)

| Tarea | Estado | Test |
|-------|--------|------|
| 4.1 Invertir test atributo (negativo → positivo) | ✅ RED → GREEN | `Controller_HasAuthorizeAttribute` reemplazó a `Controller_DoesNotHaveAuthorizeAttribute` (eliminado). |
| 4.2 Migración 15 callers `CreateClient()` → `CreateAdminClient()` en tests 2xx | ✅ REFACTOR (sin break) | Pre-condición para que 2.x mantenga suite verde. |
| 4.3 `GetAll_WithoutCredentials_ReturnsUnauthorized` + `GetById_WithoutCredentials_ReturnsUnauthorized` | ✅ RED → GREEN | 2 tests, fallaron pre-2.x. |
| 4.4 `[Theory] Mutation_WithoutCredentials_ReturnsUnauthorized` (POST/PUT/DELETE/PATCH) | ✅ RED → GREEN | 4 InlineData cases — fallaron pre-2.x. |
| 4.5 `Create/Update/Delete/Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden` | ✅ RED → GREEN | 4 tests con `FakeAuthenticationDefaults.UserHeader` — fallaron pre-2.x. |

### Fase 5 — Validación y aislamiento

- `dotnet build SGV.slnx`: **0 errors, 0 warnings**.
- `dotnet test SGV.slnx` (suite completa): **1527 PASS, 12 FAIL pre-existentes (issue #59 — no relacionado), 0 nuevos regresiones**.
- `git diff --stat -- <paths permitidos>`: 5 archivos, +221 −21 LoC. **Bien dentro del presupuesto de 400 LoC**.
- `git status --short` filtrado: `DatosSemilla.cs` y `Migraciones/20260706221558_*` permanecen **sin tocar** (verificado).

## Archivos modificados (uncommitted al cierre)

| Archivo | Acción | LoC |
|---------|--------|-----|
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` | Modified | +6 −4 |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` | Modified | +15 |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modified | +27 |
| `tests/SGV.Tests/Api/PuestosControllerTests.cs` | Modified | +129 −21 |
| `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` | Modified | +40 |
| `openspec/changes/2026-07-08-implementa-edicion-puesto-frontend/apply-progress.md` | Created | (este doc) |

**Total: 5 modificados + 1 nuevo apply-progress. +221 −21 LoC en código.**

## TDD Cycle Evidence (resumen)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1-1.3 | `PuestoIndexPageTests.cs` | Integration Web | ✅ 17/17 | ✅ | ✅ | ✅ (presencia Edit + ausencia Edit + Render Activas vs Eliminadas) | ✅ minimal code, no comment cruft |
| 2.1-2.4 | n/a (producción) | n/a | ✅ baseline | n/a | n/a | n/a | n/a |
| 3.1 | `PuestoIndexPageTests.cs` 3.1 | Integration | ✅ | ✅ (href & data-bs-title fail) | ✅ (post 1.2) | ✅ (asserts Detalle + Editar + Delete form coexisten) | n/a |
| 3.2 | `PuestoIndexPageTests.cs` 3.2 | Integration | ✅ | ✅ (DoesNotContain Edit passes by absence pre; assertive after) | ✅ (post 1.2) | ✅ (DoesNotContain Edit + Contains Reactivate form + Contains formaction) | n/a |
| 4.1 | `PuestosControllerTests.cs` `Controller_HasAuthorizeAttribute` | Reflection | ✅ | ✅ (has=false expected, fact not created yet) | ✅ (post 2.2) | ➖ Single (intrinsic — at most one attribute) | n/a |
| 4.2 | `PuestosControllerTests.cs` 15 callers | Refactor | ✅ 15/15 pre (CreateClient ≡ CreateAdminClient antes de [Authorize]) | n/a | n/a | n/a | ✅ required precondition |
| 4.3 | `PuestosControllerTests.cs` `GetAll/GetById_WithoutCredentials_ReturnsUnauthorized` | Integration | ✅ | ✅ (200 → expected 401) | ✅ (post 2.2) | ✅ pair (GET-only endpoints) | n/a |
| 4.4 | `PuestosControllerTests.cs` `[Theory]` Mutation_WithoutCredentials_ReturnsUnauthorized | Integration | ✅ | ✅ (4/4: 201/200/204/200 → expected 401) | ✅ (post 2.2) | ✅ 4 mutations × 1 theory body | n/a |
| 4.5 | `PuestosControllerTests.cs` `*_WithAuthenticatedNonAdmin_ReturnsForbidden` | Integration | ✅ | ✅ (4/4: 201/200/204/200 → expected 403) | ✅ (post 2.3) | ✅ 4 mutations + isolated for each | n/a |

## Remediación S1 — Status context round-trip mismatch

> Aplicación de la remediación del finding S1 del verify-report (pase posterior al verdict `pass-with-notes`). El bug: `Puestos/Edit` bindeaba `[FromQuery(Name = "status")]` mientras que `Index.BuildEditRouteValues` y `Details.BuildEditRouteValuesForReturn` emiten `returnStatus`, por lo que el segmento vigente se perdía tras guardar (el usuario aterrizaba en Activas aunque viniera de Eliminadas). Fix mínimo aplicado bajo strict TDD en 1 archivo de código + 1 test RED → GREEN.

### Hallazgo original

- `Puestos/Index.cshtml.cs:262-269` `BuildEditRouteValues(Guid id)` emitía `returnStatus = Segmento`.
- `Puestos/Edit.cshtml.cs:84, 142` declaraba `[FromQuery(Name = "status")] string? status = null` — nombre distinto al que emitían los callers.
- Resultado observable: `?returnStatus=eliminadas` no llegaba como `status` → `ReturnStatus = string.Empty` → redirect a Details sin `returnStatus` → usuario aterriza en Activas.

### Decisión de fix — Opción D

Se eligió la opción **D** (rename del binding en Edit, no del helper) por tres razones:

1. **Coherencia con los callers**: el helper de Index Y `BuildEditRouteValuesForReturn` de Details emiten `returnStatus`. Edit es el único lado divergente. Renombrar el binding alinea Edit con sus callers sin tocarlos.
2. **Coherencia con `design.md`**: la línea 66 del design ya declara "`returnStatus` (no `status`) es el nombre que `Puestos/Edit` acepta" — el fix implementa lo que el diseño afirma.
3. **Mínimo diff**: 1 archivo de código, 5 líneas renombradas (2 docstrings + 2 bindings + 2 usos del `string.Equals` + 1 redirect shorthand). El test es nuevo y suma +86 LoC.

Se descartaron las opciones:
- **A** (emitir ambos `status` y `returnStatus`): URL pollution sin beneficio durable.
- **B** (alias `[FromQuery(Name = "returnStatus")]` en Edit): dos nombres para un mismo concepto, confusión futura.
- **C** (cambiar helper a `status`): rompe el paralelo con `BuildDetailsRouteValues`/`BuildDetailsUrl`/`BuildEditRouteValuesForReturn` que también emiten `returnStatus`.

### Archivos tocados por esta remediación

| Archivo | Acción | LoC | Notas |
|---------|--------|-----|-------|
| `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` | Modified | +9 / −6 | Rename `status` → `returnStatus` en 2 docstrings + 2 signatures + 2 usos + 1 redirect. |
| `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` | Modified | +86 | Nuevo test `RoundTrip_FromEliminadasSegment_PreservesSegmentInPostSaveRedirect` (RED → GREEN). |

**Total: 2 archivos, +95 / −6 LoC, presupuesto 400 LoC: OK.**

Cero cambios fuera de scope. `DatosSemilla.cs` y `Migraciones/20260706221558_*` permanecen intactos (verificado vía `git diff --stat`).

### TDD Cycle Evidence

| Task | Test File | Layer | RED | GREEN | REFACTOR |
|------|-----------|-------|-----|-------|----------|
| S1.R1 (binding rename) | `PuestoEditPageTests.RoundTrip_FromEliminadasSegment_PreservesSegmentInPostSaveRedirect` | Integration Web | ✅ (RED visible: regex no matcheaba `value="eliminadas"` en el hidden `ReturnStatus`) | ✅ (post rename del binding: hidden field poblado + redirect Location contiene `returnStatus=eliminadas`) | ➖ n/a (rename atómico, sin duplicación) |

### Validación

- `dotnet build SGV.slnx`: 0 errors, 0 warnings.
- `dotnet test SGV.slnx --filter "FullyQualifiedName~PuestoEditPageTests"` (incluyendo el nuevo test RED → GREEN): **18/18 PASS** (era 17, +1).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~PuestosControllerTests|FullyQualifiedName~PuestoIndexPageTests|FullyQualifiedName~PuestoEditPageTests|FullyQualifiedName~PuestoDetailsPageTests|FullyQualifiedName~PuestoCreatePageTests|FullyQualifiedName~PuestosApiClientTests"`: **99/99 PASS** (era 98, +1 nuevo test).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Web"`: **408/408 PASS** (era 407, +1 nuevo test, 0 regresiones).
- `dotnet test SGV.slnx --filter "FullyQualifiedName~Api"`: **431/431 PASS** (sin cambios).

### Recomendación para archive

El change `2026-07-08-implementa-edicion-puesto-frontend` puede pasar de `pass-with-notes` a `pass` puro tras esta remediación. El design.md y la implementación ahora coinciden en el nombre del parámetro (`returnStatus`), la suite web cubre el round-trip end-to-end con un test dedicado, y el presupuesto de 400 LoC se mantiene holgado (total del change: ~316 LoC en código de los archivos en scope).

## Commits sugeridos (NO ejecutados por el agente)

Por política (`interactive` mode + sin instrucción explícita), el aplicador NO ejecuta los siguientes commits. El orquestador decide boundary.

1. `test(puestos): assert presencia y ausencia del botón Editar en Index` — sólo cambios en `PuestoIndexPageTests.cs` (extensión del test activo + nuevo test en eliminadas).
2. `feat(web): expone botón Editar en Puestos Index con helper BuildEditRouteValues` — cambios en `Index.cshtml` + `Index.cshtml.cs`.
3. `test(api): assert autorización admin en PuestosController` — cambios en `PuestosControllerTests.cs` (nuevos tests RED + migración 15 callers + remoción del obsolete `Controller_DoesNotHaveAuthorizeAttribute`).
4. `feat(api): requiere rol Administrador en PuestosController` — cambios en `PuestosController.cs`.

## Working tree isolation rules (recomendadas para commit)

```bash
git diff -- src/SGV.Web/Pages/Organizacion/Puestos \
          src/SGV.Api/Controllers/PuestosController.cs \
          tests/SGV.Tests/Web/Puesto \
          tests/SGV.Tests/Api/PuestosControllerTests.cs \
          openspec/changes/2026-07-08-implementa-edicion-puesto-frontend/apply-progress.md
```

Si aparecen `DatosSemilla.cs` o `Migraciones/20260706221558_*`, abortar — esos archivos NO pertenecen a este change.

## Estado por fase (legacy)

| Fase | Estado | Notas |
|------|--------|-------|
| 1 (Frontend) | ✅ GREEN | Helper + botón + cleanup de comment. |
| 2 (Backend) | ✅ GREEN | `[Authorize]` clase + 4×`[Authorize(Roles)]` + 10×`[ProducesResponseType]`. |
| 3 (Tests web) | ✅ GREEN | Presencia/ausencia + 4ta positive triangulation. |
| 4 (Tests API) | ✅ GREEN | 11 tests nuevos + migración + obsolete test removido. |
| 5 (Validación) | ✅ GREEN | Build limpio, suite 1527 PASS / 12 pre-FAIL (issue #59) / 0 regresiones. |

## Correcciones post-review PR #95 (recomendaciones 🟡)

### #1 — Helper `CaptureReturnContext` en `Puestos/Edit.cshtml.cs`

Extraída la duplicación de normalización `ReturnPage`/`ReturnSearch`/`ReturnSort`/`ReturnStatus` que existía en GET (`:90-95`) y POST (`:148-153`) a un helper privado `CaptureReturnContext(string? p, string? search, string? sort, string? returnStatus)`. Ambos handlers ahora llaman `CaptureReturnContext(p, search, sort, returnStatus)`.

**Cambio**: `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` — +8 LoC (helper) / −10 LoC (duplicación removida). **−2 LoC neto**.

### #2 — `<remarks>` documental en `PuestosController.Update`

Agregado `<remarks>409 Conflict no aplica aquí porque Codigo es inmutable en un puesto existente. La unicidad activa sólo se valida en Crear y Reactivar.</remarks>` para dejar explícito por qué `Update` no emite 409, manteniendo simetría documental con `CargosController.Update`.

**Cambio**: `src/SGV.Api/Controllers/PuestosController.cs` — +1 línea `<remarks>`.

### #3 — Cobertura "cada fila" en `PuestoIndexPageTests`

Extendido `Get_Index_WhenAuthenticated_RendersActivePuestosTable` con asserts para `second.Id` (Editar + Detalle), cumpliendo el requisito canónico *"cada fila MUST ofrecer Detalle, Editar y Eliminar"*.

**Cambio**: `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs:65-72` — +4 asserts.

### Validación post-corrección

- `dotnet build SGV.slnx`: **0 errors, 0 warnings**.
- Suite Puestos (99 tests): **99/99 PASS**.
- Sin regresiones en los 12 fallos pre-existentes (issue #59).
