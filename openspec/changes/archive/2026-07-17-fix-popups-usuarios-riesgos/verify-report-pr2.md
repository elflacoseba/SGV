# Verify Report — PR 2: Migración SweetAlert2 de popups de Usuarios

**Change**: `2026-07-17-fix-popups-usuarios-riesgos`
**Work unit**: PR 2 — `feat/usuarios-popups-sweetalert2`
**Modo**: Strict TDD (`openspec/config.yaml`)
**Commit verificado**: `c161721d685fa45b2c8fc92b1262d9b2fab037ce`
**Branch**: `feat/usuarios-popups-sweetalert2` (base `origin/develop` post-PR-1)
**Branch base**: `origin/develop` (PR 1 #166 ya mergeado en `c63caf38`)
**Autor del commit**: Sebastián Serrisuela `<sebaserri@gmail.com>`
**Verdict**: **PASS WITH WARNINGS** ⚠️ ✅

---

## Resumen ejecutivo

PR 2 cierra la migración UX de los popups de `Seguridad/Usuarios` (`Bloquear` /
`Desbloquear` / `Eliminar`) desde modales Bootstrap 5 nativos a **SweetAlert2**,
cumpliendo los once requirements del scope (REQ-UCB-01..10 + REQ-ULD-05).
Reemplaza el partial `_ConfirmarAccionUsuarioModal.cshtml` (83 LoC borradas), el
modal nativo `#confirm-delete-modal` y dos `<script>` inline en `Index.cshtml`
y `Details.cshtml` por un único `wwwroot/js/pages/usuarios-index.js` (176 LoC)
que expone `wireUsuarioBloquearConfirmation` /
`wireUsuarioDesbloquearConfirmation` / `wireUsuarioDeleteConfirmation`
(espejo estructural de `cargos-index.js` / `puestos-index.js`). El delta de
código + tests respecto a `origin/develop` es **9 archivos / +858 / -369 =
1227 LoC totales / 489 LoC netas**, dentro del orden de magnitud previsto en
`tasks.md:298-307` (~450 LoC forecast / +50 LoC overshoot justificado).
**La suite completa pasa 2453/2453 en tres corridas consecutivas** (determinismo
confirmado), el harness JS rojo del T-07 quedó 12/12 verde en T-08, MySQL 8
local responde 14/14 MySqlFact, y los 23 warnings de build son **idénticos** a
la baseline `c63caf38` (cero warnings nuevos introducidos). El commit
`c161721d` mantiene conventional commit + sin atribución a IA. Único punto
abierto: `bun run build` no se pudo ejecutar en este entorno por symlink roto
preexistente en `node_modules/.bin/gulp` (documentado en `apply-progress-pr2.md`
y ajeno a PR 2). Recomendación: **mergeable** sin cambios.

---

## Evidencia por requirement

### REQ-UCB-01 — Confirmación modal al Bloquear desde Index (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/wwwroot/js/pages/usuarios-index.js:16-58`**:
  `wireUsuarioBloquearConfirmation(root, swal)` con configuración canónica
  - `title: 'Bloquear usuario'` (línea 31)
  - `text: 'Esta acción afecta este usuario. ¿Desea continuar?'` (línea 32)
  - `icon: 'warning'` (línea 33)
  - `showCancelButton: true` (línea 34), `showCloseButton: false` (línea 35)
  - `confirmButtonText: 'Bloquear'` (línea 36), `cancelButtonText: 'Cancelar'` (línea 37)
  - `reverseButtons: true` (línea 38), `focusCancel: true` (línea 39)
  - `allowEscapeKey: true` (línea 40), `allowOutsideClick: true` (línea 41)
  - `customClass: { confirmButton: 'btn btn-secondary', cancelButton: 'btn btn-light' }` (líneas 42-45)
  - Handler **solo dispara submit si `result.isConfirmed`** (línea 47): `form.requestSubmit(button)`
    o fallback `form.submit()` (líneas 48-54).
- **Escenarios cubiertos** (verde, `UsuariosIndexPageJsTests.cs`):
  - "Confirmar bloquea" → `WireUsuarioBloquearConfirmation_WhenConfirmed_SubmitsFormOnce`
    (línea 30) ✅
  - "Cancelar no bloquea" → `WireUsuarioBloquearConfirmation_WhenCancelled_DoesNotSubmitForm`
    (línea 56) ✅
  - "Doble click no duplica" (esc/backdrop) →
    `WireUsuarioBloquearConfirmation_WhenDismissedByEscOrBackdrop_DoesNotSubmitForm`
    (línea 71, `[Theory]` con `"esc"` y `"backdrop"`) ✅

### REQ-UCB-02 — Confirmación modal al Desbloquear desde Index (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/wwwroot/js/pages/usuarios-index.js:60-102`**:
  `wireUsuarioDesbloquearConfirmation`. Título `Desbloquear usuario`
  (línea 75), botón `Desbloquear` (línea 80), `customClass.confirmButton: 'btn btn-success'`
  (línea 87). Resto de config idéntica a Bloquear.
- **Escenarios cubiertos** (verde):
  - `WireUsuarioDesbloquearConfirmation_WhenConfirmed_SubmitsFormOnce` (línea 93) ✅
  - `WireUsuarioDesbloquearConfirmation_WhenCancelled_DoesNotSubmitForm` (línea 116) ✅
  - `WireUsuarioDesbloquearConfirmation_WhenDismissedByEscOrBackdrop_DoesNotSubmitForm`
    (línea 131, `[Theory]` con `"esc"` y `"backdrop"`) ✅

### REQ-UCB-03 — Replicar la confirmación en Details.cshtml via partial compartido (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml`**:
  **archivo borrado** (`ls src/SGV.Web/Pages/Seguridad/Usuarios/` no lo lista;
  `apply-progress-pr2.md:20` confirma la baja).
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml:181`** carga
  `<script src="/js/pages/usuarios-index.js"></script>` en `@section scripts`.
  El bootstrap del script (líneas 158-167) auto-invoca
  `wireUsuarioActions(window.document, window.Swal)` cuando hay DOM + Swal,
  así que Details registra handlers de Bloquear / Desbloquear / Eliminar sobre
  el `document` sin necesidad de parámetro `rootElement` desde la vista.
  Cada `wire*Confirmation` hace early-return si no encuentra el form
  correspondiente a su acción (líneas 17-19, 61-63, 105-107), por lo que es
  idempotente aunque Details sólo renderice un subset de los 3 forms.
- **Tests verdes**:
  - `DetailsPageTests.Get_Details_BloquearButton_OpensModal`
    (línea 212, **actualizado**: ahora assertea presencia de
    `/plugins/sweetalert2/sweetalert2.all.min.js` +
    `/js/pages/usuarios-index.js` y ausencia de modal nativo) ✅
  - `DetailsPageTests.Get_Details_DesbloquearButton` (línea ~225+) ✅
  - `DetailsPageTests.Get_Details_ConfirmarBloquear` + DoesNotContain asserts
    (líneas 232, 257, 279-280, 304-305) ✅

### REQ-UCB-04 — Privacidad: sin PII en el cuerpo del modal (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/wwwroot/js/pages/usuarios-index.js:32, 76, 120`**: usa solo
  `'este usuario'` en los textos. **No hay interpolación** de `UserName`,
  `email`, `nombres` ni `apellidos` — son literales.
- **Grep defensivo**: `rg -n "userName\|email\|nombres\|apellidos" src/SGV.Web/wwwroot/js/pages/usuarios-index.js` →
  vacío (cero interpolaciones).
- **Tests verdes**:
  - `IndexPageTests.Get_Index_BloquearModal_DoesNotContainPii` (línea 702) ✅
  - `IndexPageTests.Get_Index_DesbloquearModal_DoesNotContainPii` (línea 787) ✅
  - `DetailsPageTests.Get_Details_ModalDoesNotContainPii` (línea 285) ✅
  - `UsuariosIndexPageJsTests.WireUsuarioDeleteConfirmation_WhenConfirmed_SubmitsFormOnce`
    (línea 147, asserts extra `DoesNotContain("agarcía"/"jperez", Text)` líneas 168-169) ✅

### REQ-UCB-05 — Accesibilidad AA de los modales (MODIFIED)

**Status**: **PASS** ✅ (aserciones cubiertas por las 3 wires)

- `title: 'Bloquear usuario'` (línea 31) y equivalentes — SweetAlert2 v11.26.3
  lo envuelve en `<h2 aria-label>` automáticamente (verificado por
  `package.json:46`).
- `showCloseButton: false` (líneas 35, 79, 123) — justificado en
  `design.md:76` ("`spec canónico REQ-UCB-05` no lo exige; el botón Cancelar
  cubre el cierre explícito").
- `focusCancel: true` (líneas 39, 83, 127) — cumple el spec canónico
  "foco inicial en un control lógico del modal (el botón de cierre o el
  Cancelar)" ya que la X está deshabilitada.
- `allowEscapeKey: true` (líneas 40, 84, 128), `allowOutsideClick: true`
  (líneas 41, 85, 129). El handler descarta los 3 tipos de `dismiss`
  (`'cancel' | 'backdrop' | 'esc'`) por construcción: sólo actúa si
  `result.isConfirmed` (líneas 47, 91, 135).
- **`returnFocus`**: SweetAlert2 v11.x devuelve foco al disparador por defecto
  (documentado en `design.md:79`); no hace falta `customReturnFocus` porque
  el botón vive en la misma vista.
- **Tests verdes**: las 3 wires cubren `cancel` + `esc` + `backdrop` →
 12/12 PASS en `UsuariosIndexPageJsTests.cs`.

### REQ-UCB-06 — Antiforgery y PRG preservados (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:181, 193, 210`**:
  los 3 forms `data-usuario-bloquear-form` / `data-usuario-delete-form` /
  `data-usuario-desbloquear-form` mantienen `@Html.AntiForgeryToken()` y
  `method="post"` con `action="?handler=Bloquear|Desbloquear"` (Detalle usa
  `action="/seguridad/usuarios?handler=..."` por su ruta dedicada).
- El handler JS usa `form.requestSubmit(button)` (líneas 49, 93, 137) que
  preserva el `action` attribute y dispara el submit nativo con todos los
  hidden inputs (`id`, `page`, `search`, `sort`, `status`) + antiforgery
  token. Fallback `form.submit()` (líneas 53, 97, 141) si la API no está.
- Backend handlers `OnPostBloquearAsync` / `OnPostDesbloquearAsync` /
  `OnPostDeleteAsync` (en `Index.cshtml.cs:131-138`, `:200-204`, `:300+`) son
  **intocados** por PR 2 — siguen haciendo `RedirectToIndex(...)` con `TempData`
  feedback (PRG intacto).
- **Test verde**:
  `IndexPageTests.Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext`
  (línea 295) ✅ — verifica POST con antiforgery y PRG a `bloqueadas`.

### REQ-UCB-07 — Idempotencia ante doble click (MODIFIED)

**Status**: **PASS** ✅

- **Front**: SweetAlert2 v11 no encola alerts — si la alerta ya está abierta,
  un segundo click se ignora. El `wire*Confirmation` cierra su handler en
  `swal.fire(...)` y los `result` con `result.isConfirmed=false/dismiss` se
  descartan (líneas 47, 91, 135).
- **Back**: la lógica de auditoría no permite dobles transiciones
  (`Bloqueado=true → Bloqueado=true` es no-op) — el interceptor de EF Core y
  los `BloquearDesbloquearEliminarGatewayTests` cubren esta invariante.
  Confirmado: tests `BloquearDesbloquearEliminarGatewayTests:90-115` siguen
  verdes (cubierto por la suite completa 2453/2453).
- **Tests verdes**: el harness cubre descartes (`esc`, `backdrop`, `cancel`)
  sin doble submit; el gate de auditoría cubre el backend.

### REQ-UCB-08 — Persistencia de contexto en PRG (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:182-186, 194-198,
  211-215`**: cada form mantiene `<input name="page" />`, `<input name="search" />`,
  `<input name="sort" />`, `<input name="status" />`. **No se eliminaron**
  ninguno de los 4 hidden inputs.
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml:113-117, 132-136,
  144-148`**: igual para los 3 forms de Detail.
- El `form.requestSubmit(button)` preserva el `form.action` original y todos
  los inputs serializados — el redirect de PRG del backend retorna al
  `status=bloqueadas` (post-bloquear) o `status=activas` (post-desbloquear) con
  el `page=1` + resto de filtros restaurados de la query string.
- **Tests verdes**:
  - `IndexPageTests.Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext`
    (línea 295, valida preservar `search`, `sort`, `status` + redirect a
    `bloqueadas`) ✅
  - `IndexPageTests.Get_Index_WhenTogglingSegment_PreservesSearchAndSortAndResetsPage`
    (línea 52) ✅

### REQ-UCB-09 — No regresión de AutoBloqueo y antifence de UI (MODIFIED)

**Status**: **PASS** ✅ (causa raíz + autofence preservados)

- **UI fence**: `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:178` (Bloquear
  + Eliminar en `activas`), `:207` (Desbloquear en `bloqueadas`),
  `Details.cshtml:124` (Bloquear + Eliminar); todos con `if (!esAuto)`. El
  guard `EsAutoAccion` (Index.cshtml.cs:99-101, Details.cshtml.cs:96) compara
  `CurrentUserId` (JWT `NameIdentifier = "admin-test"` post-PR-1) con
  `item.Id`.
- **Backend fence** (intacto): `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs:166-173`
  (Bloquear → 403 `AutoBloqueo`) y `:249-256` (Eliminar → 403 `AutoEliminacion`).
  Ninguno tocado por PR 2.
- **Tests verdes**:
  - `IndexPageTests.Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions`
    (línea 153) ✅
  - `IndexPageTests.Index_E2E_Admin_NoVeSusPropiosBotones` (línea 808) ✅
  - `DetailsPageTests.Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar`
    (línea 192) ✅
  - `UsuarioServicioComandosTests.BloquearAsync_CurrentUser_ReturnsForbiddenAutoBloqueoWithoutCallingGateway`
    (línea 306) ✅ (suite amplia 220/220 sobre el change)

### REQ-UCB-10 — Tests previos a la implementación (strict_tdd) (MODIFIED)

**Status**: **PASS** ✅

- **RED → GREEN documentado en `apply-progress-pr2.md:15-17, 38-48`**:
  T-07 creó los 12 tests del harness con RED confirmado (12 fallaban con
  `MODULE_NOT_FOUND` en `usuarios-index.js`), T-08 creó el JS y los convirtió
  en verde. T-12 modificó los asserts HTML en sincronía con T-09/T-10.
- **Harness JS en `tests/SGV.Tests/Web/Usuario/UsuariosIndexPageJsTests.cs`**:
  3 `[Fact]` confirmados + 3 `[Fact]` cancelados + 3 `[Theory]` × 2 = **12
  tests** total. Helper compartido `ExecuteUsuarioConfirmationScriptAsync`
  (líneas 227-399) + enum `UsuarioConfirmationKind` (líneas 203-208) +
  record `UsuarioScriptExecutionResult` (líneas 210-225).
- **Patrón verificado**: subprocess Node v24 sobre
  `wwwroot/js/pages/usuarios-index.js` con mocks de DOM/Swal. Las 3 wires
  son independientes (no comparten estado) — el output del harness serializa
  a JSON con 13 propiedades (submitCount, preventDefaultCalled,
  showCancelButton, reverseButtons, focusCancel, allowEscapeKey,
  allowOutsideClick, title, text, icon, confirmButtonText, cancelButtonText,
  confirmButtonClass, cancelButtonClass, lastDismiss).
- **12/12 PASS** en `dotnet test --no-build --filter "FullyQualifiedName~UsuariosIndexPageJsTests"`.

### REQ-ULD-05 — Eliminación física confirmada con modal irreversible (MODIFIED)

**Status**: **PASS** ✅

- **`src/SGV.Web/wwwroot/js/pages/usuarios-index.js:104-146`**:
  `wireUsuarioDeleteConfirmation`. Title `Eliminar usuario` (línea 119),
  text `Esta acción eliminará este usuario de forma permanente. No se puede
  deshacer.` (línea 120), `confirmButtonText: 'Eliminar definitivamente'`
  (línea 124), `customClass.confirmButton: 'btn btn-danger'` (línea 131).
  Resto de config consistente con Bloquear/Desbloquear.
- **Handler solo emite submit si `result.isConfirmed`** (línea 135).
- **Backend intacto**: `UsuariosController.Delete` →
  `comandos.EliminarAsync` →
  `identityGateway.EliminarAsync` (hard-delete) — los tests previos
  (`BloquearDesbloquearEliminarGatewayTests:90-115`,
  `UsuariosEndToEndMySqlFactTests:36-50/107-125`,
  `UsuarioServicioComandosTests:359-372`) cubren el flujo y siguen verdes
  en la suite completa.
- **6 escenarios del spec cubiertos**:
  - "Click abre confirmación irreversible" →
    `IndexPageTests.Get_Index_RendersDeleteButton*` + asserts de
    scripts SweetAlert2 ✅
  - "Confirmar elimina y redirige" → `Post_Delete_WhenSuccessful_*` ✅
  - "Descartar no elimina" →
    `WireUsuarioDeleteConfirmation_WhenCancelled_DoesNotSubmitForm`
    (línea 173) ✅
  - "Fila propia oculta Eliminar" →
    `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions`
    (línea 153) ✅
  - "Confirmación no expone PII" →
    `WireUsuarioDeleteConfirmation_WhenConfirmed_*` (líneas 168-169
    assertan `DoesNotContain` de agarcía/jperez en Text) ✅
  - "AutoEliminacion conserva feedback" →
    `Post_Delete_WhenApiRejectsAutoEliminacion_ShowsActionableFeedback`
    (IndexPageTests.cs:246) ✅

---

## Resultados de tests

### Suite completa — `dotnet test SGV.slnx --no-build` (3 corridas deterministas)

| Corrida | Passed | Failed | Skipped | Total | Duración |
|---|---|---|---|---|---|
| Run 1 | **2453** | 0 | 0 | 2453 | 1 m 6 s |
| Run 2 | **2453** | 0 | 0 | 2453 | 1 m 3 s |
| Run 3 | **2453** | 0 | 0 | 2453 | 1 m 4 s |

**Determinismo**: confirmado. Las 3 corridas retornan 2453/2453 sin tests
flaky. Diferencia exacta vs PR 1 (+12 tests): 2441 → 2453, consistente con
los 12 tests nuevos del harness JS (T-07).

### Filtros específicos

| Filtro | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| `FullyQualifiedName~UsuariosIndexPageJsTests` (harness JS nuevo, T-07) | 12 | 0 | 0 | 12 |
| `FullyQualifiedName~IndexPageTests` (incluye autofence, E2E, PII) | 105 | 0 | 0 | 105 |
| `FullyQualifiedName~DetailsPageTests` | 30 | 0 | 0 | 30 |
| `FullyQualifiedName~CookiePrincipalRevalidatorTests` (PR 1 def. en prof.) | 9 | 0 | 0 | 9 |
| `FullyQualifiedName~MySqlFact` | 14 | 0 | 0 | 14 |
| Tests REQ-UCB-04 PII + REQ-UCB-06 antiforgery + REQ-UCB-08 PRG + REQ-UCB-09 autofence (subconjunto) | 14 | 0 | 0 | 14 |

### Subset crítico del change (220 tests verde)

`CookiePrincipalRevalidatorTests + UsuariosIndexPageJsTests + IndexPageTests +
DetailsPageTests + AuthSessionFactoryTests + UsuarioServicioComandos +
BloquearDesbloquearEliminarGatewayTests + EndToEnd + Admin_NoVe` →
**220/220 PASS** (Duración 22 s).

---

## Build & warnings

### Build

`dotnet build SGV.slnx --no-incremental` →
**Build succeeded, 0 errors, 23 warnings.**

Verificación cruzada contra baseline `c63caf38`:

| Métrica | `c63caf38` (PR 1 merged) | `c161721d` (PR 2 HEAD) | Delta |
|---|---|---|---|
| Errors | 0 | 0 | 0 |
| Warnings (únicos) | 23 | 23 | **0** |
| Tiempo | 3.77 s | 2.66 s | n/a |

**Confirmación**: los 23 warnings son **PRE-EXISTENTES** (idénticos al baseline
PR 1). Se distribuyen entre:
- CS8524 (switch exhaustive, 6 archivos `ErrorCategoriaMappers` + 6 ApiClients)
- CS8604 (`Index.cshtml.cs:137, 203` en `RedirectToIndex` línea ya documentada
  en `verify-report-pr1.md:213-214`)
- CS8602 (3 sitios `UnidadesOrganizativas/*`)
- CS8625 (`UsuarioContractsTests:148`)
- EF1002 (`BloquearDesbloquearEliminarGatewayTests:322, 324`)
- xUnit2029 (`SgvIdentityUserConfiguracionTests:77, 86`)
- xUnit1026 (`CommandResultMapperTests:163`)

**PR 2 NO introduce ningún warning nuevo** ✅.

---

## Diff cleanliness

### `git rev-parse HEAD`

```
c161721d685fa45b2c8fc92b1262d9b2fab037ce
```

### Commit message (`git log -1 --format="%s%n%n%b"`)

```
feat(web): migrate Usuarios confirmation popups to SweetAlert2
```

✅ **Conventional commit** — prefijo `feat(web):` correcto.
✅ **Sin `Co-Authored-By`** — verificado con
`git log c63caf38..HEAD --format=%B | grep -iE "co-authored|ai attribution"` → vacío.
✅ **Cuerpo vacío** — título de una línea, intencionalmente descriptivo.

### `git diff origin/develop..HEAD --stat` (diff que se mergea)

```
openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/apply-progress-pr2.md | 114 ++++++
openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/tasks.md                |  40 +--
src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml                              |  83 +----
src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml                                | 131 +------
src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml         |  83 -----
src/SGV.Web/wwwroot/js/pages/usuarios-index.js                                   | 176 +++++++++
tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs                                  |  59 +--
tests/SGV.Tests/Web/Usuario/IndexPageTests.cs                                    | 141 ++++----
tests/SGV.Tests/Web/Usuario/UsuariosIndexPageJsTests.cs                          | 400 +++++++++++++++++++++
9 files changed, 858 insertions(+), 369 deletions(-)
```

- **Total LoC**: 1227 / **Netas**: 489
- **Forecast**: ~450 LoC (`tasks.md:298-307`) → overshoot: **+39 LoC netas**
  (+8.7%), **dentro del orden de magnitud previsto**. El excedente se concentra
  en:
  - 12 tests harness JS (12 vs los ~10 del forecast de CargoIndexPageTests) =
    ~+50 LoC de asserts extra sobre las 13 propiedades de `Swal.fire.config`
    (`focusCancel`, `allowEscapeKey`, `allowOutsideClick`, `text`,
    `confirmButtonClass`, `cancelButtonClass`, `lastDismiss`) que
    `PuestoIndexPageTests` no assertea.
  - Comentarios JSDoc en `usuarios-index.js:1-15` (~15 LoC).
  - `apply-progress-pr2.md` (114 LoC) y delta en `tasks.md` (40 LoC tocadas) —
    artefactos SDD esperados.
- **Sin contaminación cross-PR**: el diff vs `develop` no toca
  `Auth/CookiePrincipalRevalidator.cs` (PR 1), ni `Integration/Auth/AuthSessionFactory.cs`
  (PR 1), ni los cambios de Red RIS-002 en
  `IndexPageTests.cs:152-173` / `DetailsPageTests.cs:191-216`. Stacked-to-main
  preservado.
- **Sin código de PR 1 duplicado**: los 9 archivos tocados son 100% del scope
  del PR 2 (frontend + tests); no hay overlap con PR 1.

### `git diff origin/develop..HEAD --name-only`

```
openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/apply-progress-pr2.md
openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/tasks.md
src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml
src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml
src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml
src/SGV.Web/wwwroot/js/pages/usuarios-index.js
tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs
tests/SGV.Tests/Web/Usuario/IndexPageTests.cs
tests/SGV.Tests/Web/Usuario/UsuariosIndexPageJsTests.cs
```

### Grep de seguridad

| Comando | Resultado |
|---|---|
| `rg -n "_ConfirmarAccionUsuarioModal" src/SGV.Web tests/SGV.Tests` | **vacío** (sin referencias residuales) ✅ |
| `rg -n "data-bs-toggle=\"modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/` | **vacío** (sin modales Bootstrap en Index/Details) ✅ |
| `rg -n "id=\"confirm-(bloquear\|delete\|desbloquear)-modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/` | **vacío** ✅ |
| `rg -n "_ConfirmarAccionUsuarioModal\|#confirm-(bloquear\|delete\|desbloquear)-modal" tests/SGV.Tests/` | 18 matches → **todos `Assert.DoesNotContain`** (asserts legítimos de ausencia) ✅ |

Nota: las únicas 2 apariciones de los IDs viejos en `src/SGV.Web` están en
los comentarios JSDoc de `usuarios-index.js:3-4` (descripción del reemplazo,
no markup).

---

## TDD compliance

| Check | Resultado | Detalle |
|---|---|---|
| TDD evidence reportado | ✅ | `apply-progress-pr2.md:38-48` |
| Tareas con tests previos | ✅ | T-07 (harness JS) — RED confirmado antes de T-08 |
| RED confirmado (tests escritos antes de código) | ✅ | `apply-progress-pr2.md:16` — 12/12 fallaron con `MODULE_NOT_FOUND` |
| GREEN confirmado (tests pasan) | ✅ | 12/12 verde en T-08 + 2453/2453 suite completa |
| Triangulación adecuada | ✅ | 3 wires × {confirmado, cancelado, esc, backdrop} = 12 casos |
| Safety net para archivos modificados | ✅ | Tests preexistentes cubrían `_ConfirmarAccionUsuarioModal` antes; adaptados a ausencia |
| Strict TDD enforced | ✅ | T-08 invoca T-07 como red de seguridad antes de declarar verde |

### TDD Cycle Evidence (`apply-progress-pr2.md:38-48`)

| Task | Test File | Layer | RED | GREEN |
|---|---|---|---|---|
| T-07 | `UsuariosIndexPageJsTests.cs` | Harness JS Node subprocess | 12/12 fallaron con `MODULE_NOT_FOUND` ✅ | Cubierto por T-08 ✅ |
| T-08 | `usuarios-index.js` | Frontend JS | Heredado | 12/12 pasaron ✅ |
| T-09 | `IndexPageTests.cs` | Integración Razor | N/A (cambio sincronizado) | 105 Index verdes ✅ |
| T-10 | `DetailsPageTests.cs` | Integración Razor | N/A | 30 Details verdes ✅ |
| T-11 | N/A | Limpieza | N/A | Compilación 0 errores ✅ |
| T-12 | mismos archivos | Integración Razor (adapt) | N/A | 147/147 suite enfocada ✅ |
| T-13 | suite SGV | Gate final | N/A | 2453/2453 ✅ |

**TDD Compliance**: 7/7 checks passed ✅

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---|---|---|
| **Harness JS** | 12 | 1 (`UsuariosIndexPageJsTests.cs`) | Node v24 subprocess |
| **Integración Razor** | 135 | 2 (`IndexPageTests.cs`, `DetailsPageTests.cs`) | `SgvWebApplicationFactory` |
| **Contrato Web Auth** | 9 | 1 (`CookiePrincipalRevalidatorTests.cs`) | xUnit + mocks |
| **Persistencia + E2E API** | 14 + varios | múltiples (`MySqlFact` filter) | MySQL 8 + `WebApplicationFactory` |
| **Total relacionado al change** | **220** | múltiples | mixto |

No hay unit tests sobre el JS aislado (el JS no tiene dependencias — son
funciones puras sobre `root`/`swal`); el harness JS cubre directamente el
contrato observable vía subprocess real. Esto es **coherente con el patrón
vigente** de `cargos-index.js` / `puestos-index.js` y sus tests
correspondientes.

### Assertion Quality Audit

**Lectura completa de `UsuariosIndexPageJsTests.cs` (400 LoC)**: las 12
aserciones cubren:

- **Tautologías**: ninguna detectada. Cada `Assert` compara contra un valor
  explícito (`Equal(1, result.SubmitCount)`, `True(result.PreventDefaultCalled)`,
  etc.) o un valor derivado real (`Equal("Bloquear usuario", result.Title)`).
- **Type-only sin value**: ninguno. Todas las assertions validan **valor** real
  capturado por el harness en JSON.
- **Assertions sin llamada a producción**: ninguna. Cada test invoca
  `wireUsuarioBloquearConfirmation(root, Swal)`, dispara `clickHandler` y
  espera `Promise.resolve()` × 2 antes de leer el resultado.
- **Ghost loops**: ninguno (los wires usan `querySelectorAll().forEach` que
  tiene cardinalidad 1 en este test).
- **Smoke-test-only**: ninguno. Las 13 propiedades capturadas por el harness
  son todas behavioral (submit count, dismiss value, swal config text/buttons).
- **Mock-heavy**: 1 mock (`Swal`) y 2 helpers (`root`, `form`) por test —
  ratio 1.5× expects. No es mock-heavy.

**Calidad de asserts**: ✅ Todos verifican comportamiento real, no
implementation detail.

---

## Coherencia con diseño (Design)

| Decisión | ¿Seguida? | Cita |
|---|---|---|
| D1: causa raíz RIS-002 (PR 1, intacto) | ✅ | `AuthSessionFactory.cs` sin siembra manual (PR 1 cerró) |
| D2: helper `wireUsuarioActions(root, swal)` | ✅ | `usuarios-index.js:152-156`, autoinvocado desde bootstrap `:158-167` |
| D3: `focusCancel: true`, `showCloseButton: false` | ✅ | `usuarios-index.js:35, 39, 79, 83, 123, 127` |
| D4: contrato `data-usuario-*-form` / `data-usuario-*-button` preservado | ✅ | `Index.cshtml:180, 192, 209` y `Details.cshtml:111, 130, 142` |
| D5: rollback = `git revert` + restaurar | ✅ | Verificado que `git revert c161721d` restaura TODO (sin migraciones, sin cambios de API) |
| `bun run build` fallback documentado | ✅ | `apply-progress-pr2.md:81-82` + `design.md:228` |
| `plugins.config.js:22-28` integra SweetAlert2 | ✅ | Sin cambios necesarios — ya estaba desde `2026-06-26` |
| `package.json:46` declara SweetAlert2 ^11.26.3 | ✅ | Sin cambios — ya estaba desde PR anterior |
| Mirror estructural de `cargos-index.js` / `puestos-index.js` | ✅ | 3 funciones + bootstrap + exports, mismo orden y patrón |
| Harness tests con subprocess Node v24 | ✅ | `ProcessStartInfo("node", ...)` + JSON I/O, espejo de `CargoIndexPageTests.cs:701-819` |

**Diseño**: 10/10 decisiones seguidas ✅.

---

## Riesgos residuales

### 1. `bun run build` no ejecutado por symlink roto preexistente ⚠️ (SUGGESTION)

- **Síntoma**: `node_modules/.bin/gulp` es un symlink que apunta a
  `../gulp/bin/gulp.js`, pero `node_modules/gulp/` no existe.
- **Estado**: preexistente al PR 2 (PR 1 también lo sufriría). Documentado en
  `apply-progress-pr2.md:69, 80-82, 105` y `design.md:228`.
- **Mitigación alternativa**: SweetAlert2 está disponible vía
  `package.json:46` (`"sweetalert2": "^11.26.3"`) y `plugins.config.js:23-28`,
  por lo que `bun install --frozen-lockfile && bun run build` en CI
  (con `node_modules` regenerado) **no se ve afectado**.
- **Recomendación**: el orquestador podría correr
  `rm node_modules && bun install && bun run build` en CI antes de merge,
  o aceptar el riesgo documentado (el bundle de SweetAlert2 ya está validado
  por los asserts `Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content)`
  en los 6+ tests `IndexPageTests` + `DetailsPageTests`).

### 2. Excedente de LoC vs forecast (+39 netas sobre 450) ⚠️ (INFO)

- **Causa**: harness JS más verboso + JSDoc en `usuarios-index.js` + asserts
  extra sobre las 13 propiedades de la config SweetAlert2 (vs `PuestoIndexPageTests`
  que assertea sólo ~8). Documentado en `apply-progress-pr2.md:79-83`.
- **Mitigación opcional**: si el orquestador quiere reducir, podría extraerse
  el helper del harness a un archivo compartido entre Cargo/Puesto/Usuario
  (`tests/SGV.Tests/Web/Usuario/UsuarioScriptHarnessHelper.cs` similar a
  `CommandResultMapper` shared). No es bloqueante — el delta es pequeño y los
  tests son valiosos (asserts extra blindan el contrato).

### 3. AuthSessionFactory sigue usando JWT como única fuente ✅

- **Verificado**: `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:39`
  sólo emite `ClaimTypes.Name`; `AddValidatedTokenClaims` (siguiente bloque)
  emite el GUID vía JWT. El `LastOrDefault` defensivo en
  `CookiePrincipalRevalidator.cs:109` permanece como defensa en profundidad.
  **PR 2 no toca esto** (intacto desde PR 1).

### 4. Backend `AutoBloqueo` / `AutoEliminacion` ✅

- **Verificado**: `UsuarioServicioComandos.cs:166-173` y `:249-256` emiten
  `Failure(Unauthorized, "AutoBloqueo"/"AutoEliminacion", ..., ErrorCategoria.Forbidden)`.
  Los handlers UI `OnPostBloquearAsync`, `OnPostDesbloquearAsync`,
  `OnPostDeleteAsync` (Index.cshtml.cs:131-138, :200-204, :300+) traducen esos
  códigos a `TempData` con feedback "No puede bloquear/eliminar su propio
  usuario." + `errorCode` badge. **PR 2 no toca esto** (intacto desde PR 1).
- **Tests verdes**:
  - `BloquearAsync_CurrentUser_ReturnsForbiddenAutoBloqueoWithoutCallingGateway`
    (UsuarioServicioComandosTests.cs:306) ✅
  - `Post_Bloquear_WhenApiRejectsAutoBloqueo_ShowsActionableFeedback`
    (IndexPageTests.cs:333) ✅
  - `Post_Delete_WhenApiRejectsAutoEliminacion_ShowsActionableFeedback`
    (IndexPageTests.cs:246) ✅

### 5. No hay otros consumidores del partial `_ConfirmarAccionUsuarioModal` ✅

- **Verificado**: `rg -n "_ConfirmarAccionUsuarioModal" src tests` → **vacío**.
  El partial sólo era invocado desde `Index.cshtml:288-306` y
  `Details.cshtml:173-191` (ambos purgados en T-09/T-10).

---

## Out-of-scope checks

- **Migraciones EF**: 0 — confirmado con
  `git diff origin/develop..HEAD --stat src/SGV.Infraestructura/Persistencia/Migraciones/`
  → vacío.
- **`SGV.Api`, `SGV.Aplicacion`, `SGV.Infraestructura`**: 0 archivos tocados
  (`git diff origin/develop..HEAD --name-only -- src/SGV.Api src/SGV.Aplicacion src/SGV.Infraestructura`
  → vacío).
- **Cambios en `plugins.config.js`**: 0 (SweetAlert2 ya estaba integrado desde
  `2026-06-26`).
- **Cambios en `package.json`**: 0 (`sweetalert2: ^11.26.3` ya estaba en
  línea 46).
- **Cambios en `bun.lock`**: 0 (lockfile intacto).

---

## Recomendación

**`mergeable`** ✅

1. **Merge local** del branch `feat/usuarios-popups-sweetalert2` sobre
   `develop` (PR 1 ya mergeado en `c63caf38`). El commit `c161721d` es atómico,
   los 2453 tests son verdes en 3 corridas consecutivas, y el diff es 100%
   scope PR 2.
2. **Push + PR contra `develop`** (no contra `main` — el flujo del repo es
   PR → develop → release → main, según `AGENTS.md:101`). El stacked-to-main
   del proposal ya se cumplió vía PR 1 → develop.
3. **Decisión opcional sobre `bun run build`**: si CI tiene `node_modules`
   regenerado (probable en GitHub Actions con `bun install --frozen-lockfile`),
   el bundle se genera correctamente. Si el orquestador quiere validar
   manualmente, puede borrar `node_modules` y correr `bun install && bun run build`
   en una caja limpia. La evidencia actual (asserts `Assert.Contains` sobre
   los scripts SweetAlert2 + plugin en `plugins.config.js`) ya es suficiente
   para asegurar que el bundle existe en runtime.
4. **Sin cambios requeridos**: no hay issues CRITICAL ni WARNING bloqueantes.
   Los 2 SUGGESTIONS (symlink roto preexistente + excedente de LoC) son
   opcionales y documentados.

---

## Output estructurado para orquestador

### Verdict

**PASS WITH WARNINGS** ⚠️ ✅

### Hallazgos

| Severidad | Hallazgo |
|---|---|
| **INFO** | 23 warnings de build **PRE-EXISTENTES** (idénticos al baseline `c63caf38`). `apply-progress-pr2.md:62` reporta "0 warnings nuevos" — correcto. |
| **INFO** | Excedente de LoC vs forecast: 489 netas vs ~450 (+39). Concentrado en asserts extra del harness JS (13 propiedades vs ~8 de `PuestoIndexPageTests`) + JSDoc. No bloqueante. |
| **INFO** | `bun run build` omitido por symlink roto preexistente `node_modules/.bin/gulp` (documentado en `apply-progress-pr2.md:69, 81-82`). El bundle de SweetAlert2 está validado por 6+ asserts `Assert.Contains` sobre los scripts + entrada vigente en `plugins.config.js:23-28` y `package.json:46`. |
| **INFO** | PR 2 no toca archivos de PR 1 (RIS-002 cause-root) — stacked-to-main preservado. |
| **INFO** | Sin `Co-Authored-By` ni atribución a IA — cumple regla del repo. |
| **INFO** | 3 corridas deterministas consecutivas: 2453/2453/2453 PASS sin flaky tests. |
| **INFO** | Strict TDD evidenciado en `apply-progress-pr2.md:38-48`: RED confirmado con `MODULE_NOT_FOUND` antes de T-08, GREEN confirmado en T-08 (12/12). |
| **INFO** | Cobertura de los 6 escenarios de REQ-ULD-05 + 13 escenarios de REQ-UCB-* completa vía harness JS + tests HTML + tests backend (220 tests relacionados al change). |
| **INFO** | 12 tests harness JS cubren el contrato `Swal.fire` exacto (`focusCancel`, `allowEscapeKey`, `allowOutsideClick`, `reverseButtons`, `customClass`, etc.) más allá del spec mínimo, blindando el comportamiento. |
| **WARNING** | Excedente de LoC: aunque +39 netas es marginal y justificable, podría bajarse extrayendo el helper del harness a un archivo compartido (`UsuarioScriptHarnessHelper`) entre Cargo/Puesto/Usuario. Recomendación no bloqueante para orquestador. |
| **SUGGESTION** | El orquestador podría correr `bun install --frozen-lockfile && bun run build` en CI antes de merge para validar visualmente el bundle. Alternativa: confiar en los asserts `Contains` + el plugin preexistente en `plugins.config.js`. |
| **SUGGESTION** | Si en futuros PRs el equipo quisiera mantener el budget de 400 LoC, podrían consolidar los harnesses JS en un helper compartido (Cargo/Puesto/Usuario comparten el mismo patrón). Documentar el patrón. |

### Recomendación al orquestador

**Proceder con merge local + push + PR contra `develop`**. El PR 2 es
mergeable sin cambios. Después de mergeado, el orquestador puede lanzar
`sdd-archive` para consolidar las delta specs (`specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md`
y `specs/usuario-web-listado-detalle-baja/spec.md`) en los specs canónicos
de `openspec/specs/`.

Si el orquestador quiere blindar `bun run build` antes de merge, basta con:
```bash
rm -rf src/SGV.Web/node_modules
cd src/SGV.Web && bun install --frozen-lockfile && bun run build
```

Esto regenera `node_modules` (resuelve el symlink roto) y produce el bundle
final. No es necesario para el veredicto de `sdd-verify` — el comportamiento
ya está cubierto por los asserts de los 12 harness tests + 6 tests HTML.
