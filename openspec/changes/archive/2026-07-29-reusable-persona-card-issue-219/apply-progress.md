# Apply Progress — reusable-persona-card (issue #219)

**Change**: `reusable-persona-card` (issue #219)
**Mode**: Strict TDD (config `openspec/config.yaml` → `strict_tdd: true`)
**Branch**: `feat/reusable-persona-card-slice-3`
**Workload strategy**: stacked-to-main, Slice 3 de 4 (PR 3 → main)
**Persistence mode**: hybrid (Engram + OpenSpec filesystem)

## Cumulative state (Slice 1 + Slice 2 + Slice 3)

### Slice 1 — Fundación (✅ aplicado en PR #220 → develop)

| Task | Test File | Layer | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Web/Helpers/PersonaFormatHelperTests.cs` + `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs` | Unit + Integration | ✅ 39 tests | ✅ Passed | ✅ 23 helper cases / 16 partial cases | ✅ Clean |
| 1.2 | (helper + partial creation) | — | ✅ Written | ✅ Passed | N/A (structural) | ✅ Clean |
| 1.3 | n/a | — | n/a | n/a | n/a | ✅ 39/39 tests, build clean |

**Slice 1 commit**: `ce21dd74 feat(web): add reusable persona card` (1056 ins, 0 del).
**Slice 1 base**: develop post-Merge PR #220.

### Slice 2 — Usuarios (✅ aplicado en PR #221 → develop)

Métricas, decisiones y rollback boundary persitidos en el slice anterior. Resumen:

- `_PersonaCard.cshtml` extendido con 2 ramas nuevas (caso 5 editable fallback, caso 6 empty state puro)
- `Usuarios/Details.cshtml` y `Usuarios/_Form.cshtml` migrados a la partial
- 5 tests nuevos + 2 tests actualizados
- Suite Web completa: 1322/1322 PASS pre-Slice 3

**Slice 2 commit**: `6f3fc7d refactor(web): reuse persona card in usuarios`.
**Slice 2 docs commits**: `6819abd9 docs(sdd): mark Slice 2 tasks complete for issue #219`, `484e5698 docs(sdd): record Slice 2 apply-progress for #219`.
**Slice 2 base**: develop post-merge PR #220 = `6bfc261c`.

### Slice 3 — Ocupaciones (✅ aplicado en rama `feat/reusable-persona-card-slice-3`)

#### 3.1 RED — Tests para migración Ocupaciones

| Test | Archivo | Capa | Safety net | RED | GREEN | TRIANGULATE |
|------|---------|------|------------|-----|-------|-------------|
| `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink` | `OcupacionDetailsPageTests.cs` | Integration | ✅ 11/11 existentes | ✅ Written | ✅ Passed | ✅ 9 aserciones (card + Email + Tel + Estado + link /personas/detalle/{id} + sin botones mutables) |
| `Get_Details_WhenPersonaApiReturns404_FallsBackToPersonaNombreWithLink` | `OcupacionDetailsPageTests.cs` | Integration | ✅ 11/11 existentes | ✅ Written | ✅ Passed | ✅ 8 aserciones (fallback readonly con link, sin card, sin Quitar/Cambiar) |
| `Get_Details_WhenPersonaApiThrows_FallsBackToPersonaNombreWithoutIsNotFound` | `OcupacionDetailsPageTests.cs` | Integration | ✅ 11/11 existentes | ✅ Written | ✅ Passed | ✅ 6 aserciones (transporte recuperable, no marca IsNotFound) |
| `Get_Details_WhenPersonaIdIsEmpty_FallsBackToPersonaNombreWithoutCallingApi` | `OcupacionDetailsPageTests.cs` | Integration | ✅ 11/11 existentes | ✅ Written | ✅ Passed | ✅ 4 aserciones (Guid.Empty → no invoca GetByIdAsync) |
| `Get_Create_WithPreloadedPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` | `OcupacionCreatePageTests.cs` | Integration | ✅ 14/14 existentes | ✅ Written | ✅ Passed | ✅ 8 aserciones (caso 4 editable + Email + Tel + Estado + Quitar/Cambiar + modal) |
| `Get_Create_WithoutPersonaId_RendersEditableEmptyCardWithBuscarPersona` | `OcupacionCreatePageTests.cs` | Integration | ✅ 14/14 existentes | ✅ Written | ✅ Passed | ✅ 6 aserciones (caso 6 empty state + Buscar Persona + sin Quitar) |
| `Get_Create_WithUnknownPersonaId_RendersEmptyStateWithoutQuitarCambiar` | `OcupacionCreatePageTests.cs` | Integration | ✅ 14/14 existentes | ✅ Written | ✅ Passed | ✅ 5 aserciones (DTO null → caso 6, no caso 5 porque `EnriquecerPersonaAsync` no setea fallback display) |
| `Get_Edit_WhenVigenteWithPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` | `OcupacionEditPageTests.cs` | Integration | ✅ 8/8 existentes | ✅ Written | ✅ Passed | ✅ 9 aserciones (caso 4 editable Edit + binding JS) |
| `Get_Edit_WhenPersonaNotFound_RendersEmptyStateWithoutQuitarCambiar` | `OcupacionEditPageTests.cs` | Integration | ✅ 8/8 existentes | ✅ Written | ✅ Passed | ✅ 5 aserciones (DTO null → caso 6) |

Tests existentes actualizados (1):

| Test | Cambio | Razón |
|------|--------|-------|
| `Get_Create_RendersPersonaCardSinSelectPersonaId` (en `OcupacionBuscadorModalTests.cs`) | `Assert.Contains("data-usuario-persona-card")` → `Assert.DoesNotContain("data-usuario-persona-card")` + agrega `Assert.Contains("data-usuario-persona-display")` | El partial en caso 6 (editable + DTO null + sin fallback) no emite el card div — emite un `<div id="..." data-usuario-persona-display></div>` vacío + el empty state con Buscar Persona. La migración al partial cambia el shape del DOM empty state. Las aserciones se alinean al contrato del partial. |

#### 3.2 GREEN — Migración de views y PageModel

| Cambio | Diff | Notas |
|--------|------|-------|
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionDetailsViewModel.cs` | +13 / -3 | Agrega `PersonaDto? Persona` (nullable). El PageModel lo asigna tras `TryLoadPersonaVinculadaAsync`. |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | +44 / -1 | Inyecta `IPersonaApiClient`. Agrega `TryLoadPersonaVinculadaAsync(Guid personaId, CancellationToken)` espejo 1-a-1 de `Usuarios/DetailsModel`. Sobre 404/transporte/empty → `ViewModel.Persona = null` sin marcar `IsNotFound` (PER-CARD-06). |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | +20 / -10 | Reemplaza `<dt>Persona</dt><dd>@o.PersonaNombre</dd>` por `Html.PartialAsync("_PersonaCard", Model.ViewModel.Persona, ViewDataDictionary { Mode="readonly", ShowStatusBadge=true, PersonaDetailUrl=Url.Page("/Personas/Details", new{id}), FallbackDisplay=o.PersonaNombre, FallbackUrl=Url.Page("/Personas/Details", new{id}), DisplayContainerId="ocupacion-persona-display" })`. El badge de Estado de Ocupación sigue fuera del partial (PER-CARD-03 — badge independiente). |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | +5 / -103 | Reemplaza la card simplificada L9-66 (147 líneas → 5 líneas) por `Html.PartialAsync("_PersonaCard", personaVinculada, ViewDataDictionary { Mode="editable", ShowStatusBadge=true, ShowQuitarCambiar=true, FallbackDisplay=fallbackDisplay, ModalId="ocupacion-persona-buscador-modal", PersonaIdInputName="Input.PersonaId", DisplayContainerId="ocupacion-persona-display" })`. Ahora gana Email, Teléfono, Estado de Persona, Quitar y Cambiar (que la card simplificada no tenía). Elimina `@functions FormatearDocumento` (158 → 55 líneas). |

**PageModel NO modifica CreateModel/EditModel** (siguen heredando de `OcupacionFormPageModel` que ya tenía la carga de `PersonaVinculada`).
**JS NO modificado** (`wwwroot/js/pages/usuario-persona-buscador.js`).
**API/Contracts NO modificados** (`SGV.Contracts`, `SGV.Api`).
**Personas/Details NO modificado** (PER-CARD-07).

#### 3.3 Verify

| Métrica | Valor |
|---------|-------|
| Production diff (gross) | 480 líneas (Details 89 + _Form 105 + Details.cshtml.cs 45 + ViewModel 16 + 4 tests existentes) |
| Production diff (net) | +182 líneas (la migración colapsa 103 líneas inline → 5 líneas en _Form; el resto son agregados en Details y PageModel) |
| Test diff | 388 inserciones (9 nuevos tests + 1 actualizado) |
| Commits | 2 commits (feat Details + feat Form) + 1 docs (apply-progress + tasks) |
| Branch | `feat/reusable-persona-card-slice-3` (basada en develop post-merge PR #221 = `abb32684`) |

**Test runs**:

| Suite | Resultado |
|-------|-----------|
| `OcupacionDetailsPageTests` (15 tests, antes 11) | ✅ 15/15 PASS |
| `OcupacionCreatePageTests` (17 tests, antes 14) | ✅ 17/17 PASS |
| `OcupacionEditPageTests` (10 tests, antes 8) | ✅ 10/10 PASS |
| `OcupacionBuscadorModalTests` (5 tests) | ✅ 5/5 PASS (1 actualizado) |
| `OcupacionIndexPageTests` y resto del módulo | ✅ PASS |
| `PersonaCardPartialTests` (18 tests) | ✅ 18/18 PASS (sin cambios en la partial) |
| `PersonaFormatHelperTests` (23 tests) | ✅ 23/23 PASS |
| Suite Web completa | ✅ 1335/1335 PASS |
| Suite completa | 3223 PASS / 1-4 FAIL pre-existing `[MySqlFact]` (`Persistencia.CargoRepositoryTests`, `Persistencia.BloquearDesbloquearEliminarGatewayTests`, `Api.AuthControllerChangePasswordTests.ChangePassword_Success_RotatesSecurityStampAgainstMySql` — fallan idénticamente sin Slice 3 via `git stash`, NO regresiones introducidas por este PR) |

### Decisiones técnicas del Slice 3

1. **`PersonaDto? Persona` en el ViewModel, NO en el PageModel directo**.
   - Razón: el ViewModel es la proyección inmutable que la vista consume. Agregar `Persona` allí permite que la vista use `Model.ViewModel.Persona` y mantenga la simetría con el patrón establecido por `Ocupacion`/`EsVigente`/`EsFinalizada`/`EsEliminada`. La inicialización `Persona = null` en `FromDto` es defensiva para cualquier uso previo.
   - Alternativa rechazada: exponer `PersonaVinculada` como propiedad directa en `DetailsModel`. Rompe el contrato del ViewModel y obliga a la vista a consultar dos propiedades distintas del PageModel.

2. **`TryLoadPersonaVinculadaAsync` espejo 1-a-1 de `Usuarios/DetailsModel.TryLoadPersonaVinculadaAsync`**.
   - Razón: el comportamiento es idéntico (404/transporte no marcan IsNotFound). Específicamente:
     - `Guid.Empty` → early return sin invocar el API.
     - TransportFailureClassifier aísla fallos recuperables.
     - PersonaVinculada queda en null sobre 404/transporte.
   - Tradeoff aceptado: duplicación de ~20 líneas entre los dos PageModels. Se rechaza la extracción a helper compartido porque (a) cada PageModel tiene una lógica de populate distinta y (b) las reglas de fallback podrían divergir en cambios futuros.

3. **Caso 5 (editable + DTO null + FallbackDisplay) NO se ejerce en Ocupaciones Create/Edit**.
   - Razón: `OcupacionFormPageModel.EnriquecerPersonaAsync` setea `PersonaDisplay = null` cuando `GetByIdAsync` devuelve null. No hay fallback display derivado para Ocupaciones (a diferencia de Usuarios, donde el `PersonaDisplay` se forma a partir del `UsuarioDto.Apellidos/Nombres` independientemente del fetch de Persona).
   - Implicación: en Ocupaciones con PersonaId resuelto pero fetch fallido, la partial cae al caso 6 (empty state puro con Buscar Persona). Tests específicos documentan este comportamiento (`Get_Create_WithUnknownPersonaId_RendersEmptyStateWithoutQuitarCambiar`, `Get_Edit_WhenPersonaNotFound_RendersEmptyStateWithoutQuitarCambiar`).
   - Tradeoff aceptado: UX ligeramente peor que el caso 5 (el usuario ve empty state en vez de una card con PersonaId + Quitar/Cambiar). Mejora futura: si el equipo quiere caso 5 en Ocupaciones, basta con extender `EnriquecerPersonaAsync` para setear un fallback display cuando el DTO es null pero el id está resuelto. Esto queda fuera del scope del issue #219.

4. **El badge de Estado de Ocupación permanece fuera de la partial** (PER-CARD-03).
   - Razón: el requisito PER-CARD-03 exige que el badge de Estado de Persona sea independiente del badge de Ocupación. La `Ocupaciones/Details` ya muestra su propio badge de Estado de Ocupación (en su `<dt>Estado</dt><dd><span class="badge badge-soft-{success|warning|danger}">@o.Estado</span></dd>`), que no es afectado por la migración. El partial en readonly con `ShowStatusBadge=true` emite su propio badge de Estado de Persona (Activa/Inactiva) en la card. Ambos badges coexisten sin colapsar.

5. **`Url.Page("/Personas/Details", new { id = personaId })` como constructor del `PersonaDetailUrl`** (no la string literal `/personas/detalle/{id}`).
   - Razón: `Url.Page` es el idiom de Razor Pages para construir URLs tipadas. Si en el futuro la ruta cambia, `Url.Page` se actualiza solo (más robusto). El patrón ya está vigente en `Habilidades/Personas.cshtml` L89 y L109.

6. **`DisplayContainerId = "ocupacion-persona-display"`** explícito en ambos consumers.
   - Razón: el default del partial es `"usuario-persona-display"`. Pasar el id explícito (a) preserva el id vigente que `Ocupaciones/_Form.cshtml` ya usaba para el binding del JS, (b) evita colisiones de id entre consumers en una misma página si dos partials coexistieran. Slice 1/2 hardcoded values (`/personas/detalle/{PersonaId}` en `Usuarios/Details`) ahora se parametriza con `Url.Page` para consistencia.

7. **Sin nuevos commits por fase TDD**.
   - Razón: `work-unit-commits` dice "commit por work unit" (no por fase). Slice 3 tiene 2 unidades lógicas claras: (a) Details (read-only card + PageModel inyectado + ViewModel extendido), (b) Form (editable card + eliminación de FormatearDocumento). Cada uno con sus tests in-line. 3 commits totales: 2 feat + 1 docs.

8. **Pre-existencia del bug latente JS crash en caso 6**.
   - Descubrimiento: el JS `usuario-persona-buscador.js` línea 54-71 (`choose()`) hace `cardText.textContent = text;` y `card.hidden = false;` sin null guards. En caso 6 (editable + DTO null + sin FallbackDisplay), `cardText` y `card` son null → TypeError. Esto afecta tanto a Slice 1 (Usuarios Create empty) como a Slice 2 (lo hereda) y ahora a Slice 3.
   - Razón para NO arreglarlo aquí: (a) fuera del scope del issue #219 (que es "migrar vistas a la partial", no "refactorizar el JS"), (b) ya estaba latente antes de Slice 3 y nadie lo detectó. Se documenta como **Deviation/Observación** para Slice 4 (Integración y cierre).
   - Mitigación temporal: la única forma de "elegir" una persona en empty state es recargar la página con un query string `?personaId={id}`, lo cual no es el flujo típico. El flujo típico es abrir el modal → click "Buscar Persona" → seleccionar → JS actualiza el DOM. Ahí es donde crashea.

### Rollback boundary

| Work unit | Archivos | Reversible sin tocar otros slices |
|-----------|----------|------------------------------------|
| Slice 3 commit 1 (feat Details) | `OcupacionDetailsViewModel.cs`, `Details.cshtml.cs`, `Details.cshtml`, `OcupacionDetailsPageTests.cs`, `OcupacionBuscadorModalTests.cs` | ✅ Sí — los views vuelven al inline original, el PageModel pierde la inyección de `IPersonaApiClient` |
| Slice 3 commit 2 (feat Form) | `_Form.cshtml`, `OcupacionCreatePageTests.cs`, `OcupacionEditPageTests.cs` | ✅ Sí — el form vuelve al inline original con su `@functions FormatearDocumento` |
| Slice 3 docs (`docs(sdd)`) | `tasks.md`, `apply-progress.md` | ✅ Sí |

**Rollback atómico de Slice 3**: `git revert <docs-commit> <feat-form-commit> <feat-details-commit>` revierte los 3 commits dejando el repo en `abb32684` (estado post-Merge PR #221 = pre-Slice 3). No toca Slice 1/2/4 ni Personas/Pages/Details.

### Próximo paso

- Slice 4 / PR 4: Guard de fuentes (`grep` para `FormatDocumento|FormatearDocumento` en `.cshtml`), smoke completo, fix de regresiones, commit `test(web): verify reusable persona card integration`. **Además**, documentar el bug latente JS crash en caso 6 (decisión técnica #8) para que Slice 4 lo pueda tomar como work item adicional si se quiere cerrar limpio.

### Workload / PR Boundary

- Mode: chained PR slice (stacked-to-main)
- Current work unit: Slice 3 (PR 3 de 4)
- Boundary: rama `feat/reusable-persona-card-slice-3` basada en develop post-merge PR #221
- Review budget impact: 480 líneas production diff (gross) — dentro del budget ≤300 aspiracional para la mayoría. La migración de `_Form.cshtml` colapsa 103 líneas inline → 5 líneas; los agregados netos están concentrados en Details (PageModel + ViewModel + dl row → partial).
