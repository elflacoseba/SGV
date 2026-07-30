# Apply Progress — reusable-persona-card (issue #219)

**Change**: `reusable-persona-card` (issue #219)
**Mode**: Strict TDD (config `openspec/config.yaml` → `strict_tdd: true`)
**Branch**: `feat/reusable-persona-card-slice-2`
**Workload strategy**: stacked-to-main, Slice 2 de 4 (PR 2 → main)
**Persistence mode**: hybrid (Engram + OpenSpec filesystem)

## Cumulative state (Slice 1 + Slice 2)

### Slice 1 — Fundación (✅ aplicado en PR #220 → develop)

| Task | Test File | Layer | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Web/Helpers/PersonaFormatHelperTests.cs` + `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs` | Unit + Integration | ✅ 39 tests | ✅ Passed | ✅ 23 helper cases / 16 partial cases | ✅ Clean |
| 1.2 | (helper + partial creation) | — | ✅ Written | ✅ Passed | N/A (structural) | ✅ Clean |
| 1.3 | n/a | — | n/a | n/a | n/a | ✅ 39/39 tests, build clean |

**Slice 1 commit**: `ce21dd74 feat(web): add reusable persona card` (1056 ins, 0 del).
**Slice 1 base**: develop post-Merge PR #220.

### Slice 2 — Usuarios (✅ aplicado en rama `feat/reusable-persona-card-slice-2`)

#### 2.1 RED — Tests parcial + migración Usuarios

| Test | Archivo | Capa | Safety net | RED | GREEN | TRIANGULATE |
|------|---------|------|------------|-----|-------|-------------|
| EditableWithPersonaNullAndFallbackDisplay_EmitsEditableFallbackCardWithQuitarCambiar | `PersonaCardPartialTests.cs` | Integration | ✅ 16/16 existentes | ✅ Written | ✅ Passed | ✅ 4 aserciones sobre contrato JS (Quitar/Cambiar/binding/empty hidden) |
| EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona | `PersonaCardPartialTests.cs` | Integration | ✅ 16/16 existentes | ✅ Written | ✅ Passed | ✅ 5 aserciones (empty state visible, sin card, sin Quitar, sin hidden editable) |
| Get_Details_WhenPersonaApiReturnsDto_RendersPartialPersonaDisplayContainer | `DetailsPageTests.cs` | Integration | ✅ 6/6 Details existentes | ✅ Written | ✅ Passed | ✅ 4 aserciones (id contenedor + binding + sin Quitar/Cambiar readonly) |
| Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding | `EditPageTests.cs` | Integration | ✅ 6/6 Edit existentes | ✅ Written | ✅ Passed | ✅ 11 aserciones (binding JS + modal + Input.PersonaId + Quitar/Cambiar) |
| Get_Edit_WhenPersonaIdIsEmpty_FallsBackToEditableFallbackCard | `EditPageTests.cs` | Integration | ✅ 6/6 Edit existentes | ✅ Written | ✅ Passed | ✅ 9 aserciones (Guid.Empty.HasValue=true → fallback card editable) |

Tests existentes actualizados (2):

| Test | Cambio | Razón |
|------|--------|-------|
| `Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` | `data-usuario-details-persona` → `data-usuario-persona-display` | El selector interno del Details.cshtml inline se reemplaza por el contenedor del partial |
| `Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` | Mismo reemplazo | Misma razón |

**No se modificaron aserciones visuales** — los tests de binding/rendering JS siguen cubriendo `data-usuario-persona-card`, `data-usuario-persona-display`, `data-usuario-persona-quitar`, `data-usuario-persona-buscar`, `data-usuario-persona-display-input` etc.

#### 2.2 GREEN — Migración de views

| Cambio | Diff | Notas |
|--------|------|-------|
| `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` | +132 / -34 | Nuevos casos 5 (editable fallback) y 6 (empty state puro). Empty state siempre se emite en editable (hidden o visible según contexto). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | +10 / -113 | Reemplaza card inline L79-145 por `Html.PartialAsync` con ViewData readonly. Borra `@functions { FormatDocumento }` L248-285. 285 → 202 líneas. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | +6 / -148 | Reemplaza card inline L26-115 por `Html.PartialAsync` editable. Conserva `<input type="hidden" asp-for="Input.PersonaId" />` (binding model). Borra `@functions { FormatDocumento }` L224-253. 254 → 151 líneas. |

**PageModels NO modificados** (`Details.cshtml.cs`, `Edit.cshtml.cs`, `Create.cshtml.cs`).
**JS NO modificado** (`wwwroot/js/pages/usuario-persona-buscador.js`).
**API/Contracts NO modificados** (`SGV.Contracts`, `SGV.Api`).

#### 2.3 Verify

| Métrica | Valor |
|---------|-------|
| Production diff (gross) | 895 líneas (Details 248 + _Form 411 + _PersonaCard 236) |
| Production diff (net) | +92 líneas (extensión partial compensa eliminaciones inline) |
| Test diff | 218 inserciones (5 nuevos tests + 2 actualizados) |
| Commits | 2 commits (2508a0d feat(web) extend partial + 6f3fc7d refactor(web) migrate) + 1 docs (6819abd9) |
| Branch | `feat/reusable-persona-card-slice-2` (basada en develop post-merge PR #220) |

**Test runs**:

| Suite | Resultado |
|-------|-----------|
| `PersonaCardPartialTests` (18 tests, antes 16) | ✅ 18/18 PASS |
| `PersonaFormatHelperTests` (23 tests) | ✅ 23/23 PASS |
| `Web.Usuario` (200 tests, antes 197) | ✅ 200/200 PASS |
| `Web.Ocupaciones` (sin cambios — Slice 3) | ✅ PASA (no tocado) |
| Suite Web completa | ✅ 1322/1322 PASS |
| Suite completa | 3210 PASS / 1-4 FAIL (pre-existing `[MySqlFact]` en `Persistencia.CargoRepositoryTests`, `Persistencia.BloquearDesbloquearEliminarGatewayTests`, `Api.AuthControllerChangePasswordTests.ChangePassword_Success_RotatesSecurityStampAgainstMySql` — fallan idénticamente sin Slice 2 via `git stash`, NO regresiones introducidas por este PR) |

### Decisiones técnicas del Slice 2

1. **Extensión del partial con 2 ramas nuevas** (caso 5 y caso 6).
   - Razón: el comportamiento histórico de `Usuarios/_Form` cuando el fetch del API falla pero `Input.PersonaId` tiene valor (Guid.Empty o Guid real) requería una card con PersonaDisplay + Quitar/Cambiar, no un empty state. La Slice 1 partial no cubría este caso.
   - Alternativa rechazada: duplicar markup en `_Form.cshtml` (viola la razón de ser del partial).
   - Tradeoff aceptado: el partial crece +82 líneas netas; a cambio, los consumers son trivialmente simples (un PartialAsync con ViewData).

2. **Empty state siempre se emite en editable** (con `hidden="hidden"` o null según contexto).
   - Razón: el JS usa `display.parentElement.querySelector('[data-usuario-persona-empty]')` para gestionar visibilidad tras Quitar. Si el elemento no existe, `empty.hidden = false` lanza TypeError. El test del usuario lo verifica explícitamente.

3. **Guid.Empty.HasValue = true** en C# nullable value types.
   - Razón: `Guid? x = Guid.Empty; x.HasValue` devuelve `true`. Esto significa que la rama `isEditableFallback` se activa cuando `Input.PersonaId = Guid.Empty`, NO la rama empty state puro. El comportamiento es consistente con el _Form histórico (que también mostraba fallback card para Guid.Empty).
   - Implicación: la rama `case 6` (empty state puro) sólo se ejerce en Create (`Input.PersonaId` es `null` por default), no en Edit.

4. **Selección de `data-usuario-details-persona` removida en Details fallback**.
   - Razón: ese atributo era un selector interno del Details.cshtml inline (marcaba el `<div class="card-body py-2">` del fallback). Al migrar al partial, el contenedor pasa a ser `data-usuario-persona-display` (binding JS vigente). Las aserciones visuales del fallback se actualizaron al nuevo selector — siguen siendo semánticamente equivalentes (verifican que el fallback muestra PersonaDisplay + link, sin card enriquecida, sin botones mutables).

5. **Sin nuevos commits por fase TDD**.
   - Razón: el `work-unit-commits` skill dice "commit por work unit" (no por fase). Slice 2 tiene 2 work units lógicos: extender el partial (foundation), migrar los views (consumers). Cada uno con sus tests in-line. 3 commits totales (feat + refactor + docs). La fase TDD se documenta arriba como tabla, no como commits separados.

### Rollback boundary

| Work unit | Archivos | Reversible sin tocar otros slices |
|-----------|----------|------------------------------------|
| Slice 2 commit 1 (`2508a0d`) | `_PersonaCard.cshtml`, `PersonaCardPartialTests.cs` | ✅ Sí — sólo toca la partial y sus tests |
| Slice 2 commit 2 (`6f3fc7d`) | `Details.cshtml`, `_Form.cshtml`, `DetailsPageTests.cs`, `EditPageTests.cs` | ✅ Sí — los views vuelven al inline original |
| Slice 2 docs (`6819abd9`) | `tasks.md` | ✅ Sí |

**Rollback atómico de Slice 2**: `git revert 6819abd9 6f3fc7d 2508a0d` revierte los 3 commits dejando el repo en `6bfc261c` (estado post-Merge PR #220 = pre-Slice 2). No toca Slice 3/4 ni Persona/Pages/Details.

### Próximo paso

- Slice 3 / PR 3: Migrar Ocupaciones (`Ocupaciones/Details.cshtml` + `Ocupaciones/_Form.cshtml`). Branch: `feat/reusable-persona-card-slice-3`. Pre-requisito: Slice 2 mergeado a develop.
- Slice 4 / PR 4: Guard de fuentes (`grep` para `FormatDocumento|FormatearDocumento` en `.cshtml`), smoke completo, fix de regresiones, commit `test(web): verify reusable persona card integration`.

### Workload / PR Boundary

- Mode: chained PR slice (stacked-to-main)
- Current work unit: Slice 2 (PR 2 de 4)
- Boundary: rama `feat/reusable-persona-card-slice-2` basada en develop post-merge PR #220
- Review budget impact: 895 líneas production diff (gross) — excede el target aspiracional de ≤250 pero la mayoría es la eliminación de markup inline duplicado en Details y _Form. Las adiciones netas son +92 líneas en producción (extensión partial compensa eliminaciones).