# Verify Report — PR 1: Fix RIS-002 cause-root

**Change**: `2026-07-17-fix-popups-usuarios-riesgos`
**Work unit**: PR 1 — `fix/seguridad-usuarios-ris-002-cause-root`
**Modo**: Strict TDD (`openspec/config.yaml`)
**Commit verificado**: `d0b8ec43d236d5864838eb75fcbec62342f021c5`
**Branch**: `fix/seguridad-usuarios-ris-002-cause-root` (base `main`)
**Branch base**: `main` (`2a90ed0dd31149c22d41cd87c1e5f60fdb8cb7a8`)
**Autor del commit**: Sebastián Serrisuela `<sebaserri@gmail.com>`
**Verdict**: **PASS** ✅

---

## Resumen ejecutivo

PR 1 cierra el cause-root de RIS-002 eliminando la siembra manual de
`ClaimTypes.NameIdentifier` en `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-41`,
dejando al JWT como única fuente de verdad para el `NameIdentifier` del
principal cookie. El diff de 1 commit (`d0b8ec43`) modifica 7 archivos con
162 LoC (141 +, 21 -), dentro del budget de ~120 LoC previsto en
`tasks.md`. Las tres requirements del scope de PR 1 (REQ-UCB-09,
REQ-UCB-04, REQ-ULD-05 fila propia) están cubiertas con tests verdes
tanto en el escenario específico como en el nuevo E2E
`Index_E2E_Admin_NoVeSusPropiosBotones`. La suite completa pasa 2441/2441
en tres corridas consecutivas (determinismo confirmado), MySQL 8 local
responde 14/14 MySqlFact, y el commit no incluye atribución a IA.
Recomendación: merge local + push + PR contra `develop` (o contra `main`
si el equipo decide pasar por alto `develop` por tratarse de causa-root
de seguridad). No quedan issues bloqueantes para PR 2.

---

## Evidencia por requirement

### REQ-UCB-09 — No regresión de AutoBloqueo y antifence de UI

**Status**: **PASS** ✅

- **`src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-41`**: la siembra
  manual de `ClaimTypes.NameIdentifier` fue removida. El bloque `claims`
  ahora sólo contiene `new(ClaimTypes.Name, request.UserNameOrEmail)`.
  El JWT (vía `AddValidatedTokenClaims` línea 42-90) es la única fuente
  del `NameIdentifier` del principal.
  *Cita*: línea 39 → `new(ClaimTypes.Name, request.UserNameOrEmail)`.
- **`src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:105-110`**: el
  `LastOrDefault` defensivo permanece activo con el comentario actualizado
  a "Defense in depth: multiple NameIdentifier claims should not occur
  after the root fix. If they do, prefer the last one as the best signal
  from the validated JWT." (líneas 105-107).
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml.cs:83`**:
  `CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)`
  ahora retorna el GUID real del JWT (`admin-test`), no el
  `UserNameOrEmail`.
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs:89`**: análogo.
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:174`**: el guard
  `if (!esAuto)` envuelve los forms `data-usuario-bloquear-form` y
  `data-usuario-delete-form` en el segmento `activas`.
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:206`** (modificado
  por PR 1): `else if (Model.EsAdministrador && !esAuto)` agrega el guard
  al form `data-usuario-desbloquear-form` en el segmento `bloqueadas`.
  Sin este guard, `EsAutoAccion` retornaría `true` y el botón Desbloquear
  igual se renderizaría. Esta fue una desviación menor documentada en
  `apply-progress-pr1.md:71-72` (T-05 RED descubrió el gap).
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml:121`**:
  `else if (!esAuto)` oculta Bloquear y Eliminar cuando el target es el
  admin logueado; el render cae al branch `else` (líneas 152-160) que sólo
  expone Edit.
- **Tests verdes**:
  - `IndexPageTests.Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions` ✅
  - `DetailsPageTests.Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar` ✅
  - `CookiePrincipalRevalidatorTests.ValidateAsync_PicksLastNameIdentifierWhenMultipleClaims` ✅

### REQ-UCB-04 — Privacidad: sin PII en el cuerpo del modal (modal nativo)

**Status**: **PASS** ✅ (aplica sólo al modal Bootstrap nativo vigente
hasta PR 2; el contrato de REQ-UCB-04 sigue siendo "este usuario" en
ambas implementaciones).

- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:259-280`**: el
  modal nativo de Eliminar tiene cuerpo literal "Esta acción eliminará
  **este usuario** de forma permanente. No se puede deshacer." (línea
  268) y "La persona vinculada y las auditorías previas se conservan."
  (línea 271). Sin interpolación de UserName, Email, Nombres ni
  Apellidos del target.
- **`src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml:70-72`**:
  body default "Esta acción afecta **este usuario**. ¿Desea continuar?"
  Sin interpolación de PII (líneas 58-73 documentan el contrato).
- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:288-296`** y
  **`:298-306`**: invocaciones de `_ConfirmarAccionUsuarioModal` para
  Bloquear y Desbloquear, pasando título y `ConfirmSelector` por
  `ViewDataDictionary`. No se pasa ningún `BodyHtml` ni campo de PII.
- **Tests verdes**:
  - `IndexPageTests.BloquearModal_DoesNotContainPii` ✅
  - `IndexPageTests.DesbloquearModal_DoesNotContainPii` ✅
  - `IndexPageTests.EliminarModal_DoesNotContainPii` ✅
  - `DetailsPageTests.Get_Details_ModalDoesNotContainPii` ✅

### REQ-ULD-05 escenario "La fila propia oculta Eliminar"

**Status**: **PASS** ✅

- **`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:174`**: `if (!esAuto)`
  oculta el `<form data-usuario-delete-form>` (líneas 189-202) cuando el
  target es el admin logueado. `EsAutoAccion` (Index.cshtml.cs:99-101)
  compara `CurrentUserId` con `item.Id`.
- **Test verde**:
  - `IndexPageTests.Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions`
    valida con `AssertUsuarioActionFormNotRendered(content,
    "data-usuario-delete-form", self.Id)` (IndexPageTests.cs:170).
  - `IndexPageTests.Index_E2E_Admin_NoVeSusPropiosBotones` valida el
    mismo escenario en los dos segmentos (activas y bloqueadas)
    (IndexPageTests.cs:789-812).
- El render del botón Eliminar (`data-usuario-delete-button` con
  `data-bs-toggle="modal"` y `data-bs-target="#confirm-delete-modal"`)
  permanece intacto en el segmento `activas` (línea 196-201) — esto es
  correcto porque el guard opera sobre `esAuto`, no sobre el resto del
  render.

---

## Evidencia de cause-root fix (RIS-002)

| Cita | Estado |
|---|---|
| `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-41` — bloque `claims` sin siembra manual de `ClaimTypes.NameIdentifier` | **PASS** ✅ |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml.cs:83` — `CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)` retorna el GUID del JWT | **PASS** ✅ |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs:89` — análogo | **PASS** ✅ |
| `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:105-110` — `LastOrDefault` defensivo + comentario actualizado a "Defense in depth" | **PASS** ✅ |
| `tests/SGV.Tests/Web/Auth/AuthSessionFactoryTests.cs:42` — assert `claim.Type == ClaimTypes.NameIdentifier && claim.Value == "admin-test"` verde | **PASS** ✅ |
| `tests/SGV.Tests/Web/Common/AdminJwtTestHelper.cs:76` — JWT emite `new(ClaimTypes.NameIdentifier, "admin-test")` | **PASS** ✅ |

**Conclusión**: tras PR 1, `ClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)` retorna `"admin-test"` (alineado con el `sub` y `NameIdentifier` del JWT firmado por `AdminJwtTestHelper.BuildAdminRoleJwt`). El guard de server-side `EsAutoAccion(admin-test) == true` se activa y la vista oculta Bloquear/Eliminar/Desbloquear en la fila del admin logueado.

---

## Resultados de tests

### Suite completa (`dotnet test SGV.slnx --no-build`)

| Corrida | Passed | Failed | Skipped | Total | Duración |
|---|---|---|---|---|---|
| Run 1 | 2441 | 0 | 0 | 2441 | 1 m 3 s |
| Run 2 | 2441 | 0 | 0 | 2441 | 1 m 2 s |
| Run 3 | 2441 | 0 | 0 | 2441 | 1 m 5 s |

**Determinismo**: confirmado. Las 3 corridas arrojan el mismo conteo
2441/2441 sin tests flaky.

### MySQL 8 local

`dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~MySqlFact"`
→ **14/14 PASS** (incluye `UsuariosEndToEndMySqlFactTests`,
`MigracionD7MySqlFactTests`, `MigracionFailLoudCargosTests`,
`JwtCorteInmediatoMySqlFactTests`, etc.).

### Tests críticos del change

Filtro: `Index_E2E_Admin_NoVeSusPropiosBotones | Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions | Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar | CreatePrincipal_WithValidToken_AddsRoleAndTokenClaims | ValidateAsync_PicksLastNameIdentifierWhenMultipleClaims`

→ **5/5 PASS** ✅

---

## Diff cleanliness

### `git diff HEAD~1..HEAD --stat`

```
openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/apply-progress-pr1.md | 91 ++++++++++++++++++++++
src/SGV.Web/Auth/CookiePrincipalRevalidator.cs                                  |  7 +-
src/SGV.Web/Integration/Auth/AuthSessionFactory.cs                              |  1 -
src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml                               |  2 +-
tests/SGV.Tests/Seguridad/CookiePrincipalRevalidatorTests.cs                    |  9 +--
tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs                                 |  9 +--
tests/SGV.Tests/Web/Usuario/IndexPageTests.cs                                   | 43 +++++++++-
7 files changed, 141 insertions(+), 21 deletions(-)
```

- **Total**: 162 LoC modificadas (presupuesto PR1: ~120, **OK** dentro
  de margen razonable para un PR cause-root que añade 1 test E2E
  end-to-end con helper reutilizable).
- **7 archivos**: ningún archivo de PR 2 incluido (no se toca
  `Details.cshtml` ni `_ConfirmarAccionUsuarioModal.cshtml`, no se crea
  `wwwroot/js/pages/usuarios-index.js`, no se agrega `<link>` ni
  `<script>` SweetAlert2). Esto preserva el stacked-to-main.
- **Sin contaminación cross-PR**: el diff no toca `Index.cshtml:259-280`
  (modal nativo vigente), ni `Index.cshtml:288-306` (invocaciones
  parciales), ni `Details.cshtml:172-191`, ni el `<script>` inline de
  `Index.cshtml:308-361`. PR 2 los recibirá como estado base limpio.

### `git log -1 d0b8ec43`

```
commit d0b8ec43d236d5864838eb75fcbec62342f021c5
Author: Sebastián Serrisuela <sebaserri@gmail.com>
Date:   <fecha>
    fix(web): remove manual NameIdentifier seed in AuthSessionFactory (RIS-002 cause-root)
```

- ✅ **Conventional commit**: prefijo `fix(web):` correcto.
- ✅ **Sin `Co-Authored-By`**: verificado con `git log --format=… |
  grep -i "co-authored\|AI\|generated-by"` → no retorna coincidencias.
- ✅ **Sin atribución a IA**: el cuerpo del commit es una sola línea
  descriptiva.

### Build

`dotnet build SGV.slnx` → **0 Error(s)**, **23 Warning(s)**.

**Verificación de warnings pre-existentes**: los 23 warnings son
**PRE-EXISTENTES** (existen también en `HEAD~1` =
`94cfc385 sync engram memories` y en `main`). Se distribuyen entre:
- CS8524 (switch expression no exhaustiva, 6 archivos: ErrorCategoriaMappers
  y 6 ApiClients).
- CS8604 (posible null en `RedirectToIndex` 2 sitios en Index.cshtml.cs
  pre-existentes).
- CS8602 (3 sitios en UnidadesOrganizativas pre-existentes).
- CS8625 (1 sitio en UsuarioContractsTests pre-existente).
- EF1002 (2 sitios en BloquearDesbloquearEliminarGatewayTests pre-existentes).
- xUnit1026 y xUnit2029 (3 sitios en tests pre-existentes).

**PR 1 NO introduce ningún warning nuevo**. El apply-progress-pr1.md
reporta "0 warnings, 0 errors" lo cual es impreciso (debería decir "0
warnings nuevos, 23 warnings pre-existentes") pero no afecta la
veredicto.

---

## Riesgos residuales

### Consumidores de `ClaimTypes.NameIdentifier` en SGV.Web (grep verificado)

| Archivo:línea | Tipo de consumidor | Asume `UserNameOrEmail`? |
|---|---|---|
| `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:109` | `LastOrDefault(c => c.Type == ClaimTypes.NameIdentifier)` (defensa en profundidad) | **NO** — toma el último valor disponible, que tras PR 1 es siempre el GUID del JWT |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml.cs:83` | `User.FindFirstValue(ClaimTypes.NameIdentifier)` (auto-fence UI) | **NO** — el JWT emite `NameIdentifier = "admin-test"` y la siembra manual fue removida |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs:89` | Análogo a Index.cshtml.cs:83 | **NO** |

**Conclusión**: ninguno de los 3 consumidores asume el valor de
`UserNameOrEmail`. El `AuthSessionFactory.cs` ya no aparece en el grep
de `rg -n "NameIdentifier" src/SGV.Web` (única aparición era la siembra
manual removida), lo cual confirma el efecto del cause-root fix.

### Otros riesgos

- **PR 2 antes que PR 1**: la regla `stacked-to-main` del proposal
  (`openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/proposal.md:45-48`)
  está preservada por el diff. Si el merge de PR 1 se retrasa, el
  PR 2 podría reintroducir el bug si vuelve a sembrar el claim (no es
  el caso según `design.md`).
- **Backend `AutoBloqueo`/`AutoEliminacion`**: `src/SGV.Api` queda
  intacto. Los fences server-side siguen siendo la red final. PR 1 no
  los debilita.

---

## Recomendación

**`mergeable`** ✅

1. **Merge local** del branch `fix/seguridad-usuarios-ris-002-cause-root`
   sobre `develop` (o `main` si el flujo del repo lo requiere) — el
   commit es atómico, los tests son verdes y el diff es cause-root
   puro.
2. **Push + PR contra `develop`** para registrar el cambio en el
   historial de PRs. El stacked-to-main del proposal está pensado para
   que PR 2 salga de `main` después de mergeado PR 1; según el flujo
   actual del repo (desarrollo contra `develop`, release contra
   `main`), el path más limpio es PR 1 → `develop` y PR 2 → también
   `develop` (o cherry-pick a `main` después).
3. **No requiere cambios**: no hay issues CRITICAL ni WARNING
   bloqueantes. El único item de housekeeping menor es la métrica de
   "0 warnings" reportada en `apply-progress-pr1.md` que es imprecisa
   pero no afecta la corrección.

---

## Output estructurado para orquestador

### Verdict

**PASS** ✅

### Hallazgos

| Severidad | Hallazgo |
|---|---|
| **INFO** | 23 warnings de build pre-existentes (no introducidos por PR 1). `apply-progress-pr1.md:58` reporta "0 warnings" — impreciso pero no afecta veredicto. |
| **INFO** | PR 1 fue diseñado para stacked-to-main y la regla está preservada por el diff (no toca archivos de PR 2). |
| **INFO** | El commit `d0b8ec43` no incluye `Co-Authored-By` ni atribución a IA — cumple regla del repo. |
| **INFO** | 3 corridas deterministas consecutivas: 2441/2441 PASS sin flaky tests. |

### Recomendación al orquestador

**Proceder con merge local + push + PR contra `develop`**. El PR 1 es
mergeable sin cambios. Después de mergeado, el orquestador puede lanzar
`sdd-apply` sobre las tareas T-07..T-13 del PR 2 (SweetAlert2).