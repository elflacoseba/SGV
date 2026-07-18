# Design: Fix popups de usuarios (SweetAlert2 + RIS-002 cause-root)

## Resumen arquitectónico

Este change cierra tres reclamos de `Seguridad/Usuarios` con dos PRs stacked-to-main que se mergean en orden estricto (PR 1 antes que PR 2, enforced por la regla "stacked-to-main" del proposal). El primer PR corrige en raíz el bug RIS-002 que sembraba manualmente `ClaimTypes.NameIdentifier = UserNameOrEmail` en `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-47` y reintroducía un `LastOrDefault` defensivo en `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:108-110`. El segundo PR reemplaza los Bootstrap modals nativos de `Index.cshtml:259-306` y `Details.cshtml:172-183` por SweetAlert2 (espejo de `src/SGV.Web/wwwroot/js/pages/cargos-index.js:1-85` y `puestos-index.js:1-85`), preservando las invariantes REQ-UCB-01..10 y REQ-ULD-05 ya descritas en el proposal y borrando `_ConfirmarAccionUsuarioModal.cshtml`.

La decisión técnica clave es **mantener el JWT como única fuente de verdad del principal** (PR 1) y **mantener `data-usuario-*-form` / `data-usuario-*-button` como contrato observable** entre la vista y el JS (PR 2) — así el cambio de implementación interna no rompe el contrato de los tests web existentes ni del harness JS, y la cobertura actual de `IndexPageTests.cs:152-173`, `DetailsPageTests.cs:191-216` y `BloquearDesbloquearEliminarGatewayTests:90-115` sigue siendo válida.

El cambio no toca `SGV.Aplicacion`, `SGV.Infraestructura`, `SGV.Api`, migraciones EF ni la composición de `plugins.config.js:22-28`. Los handlers backend `UsuarioServicioComandos.cs:166-173` (AutoBloqueo) y `:249-256` (AutoEliminacion) ya existen como fence server-side y permanecen intactos; el guard UI pasa a ser simplemente "el botón no se renderiza" en vez de "el botón se renderiza y dispara un 403".

## Fix RIS-002 (PR 1 — `fix/seguridad-usuarios-ris-002-cause-root`)

### Decisión cause-root vs workaround duplicado

**Choice**: cause-root — quitar la siembra manual de `NameIdentifier` en `AuthSessionFactory.cs:37-47` y dejar que el JWT (vía `AddValidatedTokenClaims` en `AuthSessionFactory.cs:70-90`) sea el único emisor.

**Alternativas consideradas**:
- *Workaround duplicado*: replicar el patrón `LastOrDefault` de `CookiePrincipalRevalidator.cs:110` en cada consumidor (`IndexModel.CurrentUserId`, `DetailsModel.CurrentUserId`, futuros endpoints). Descartado: duplica una invariante en 2+ lugares y propaga el bug a cada consumidor nuevo.
- *Renombrar el claim sembrado manualmente* (`nameid-sgv:userNameOrEmail`) y leerlo por tipo específico. Descartado: introduce una asimetría entre el principal de Web y el de API (que sí lee `ClaimTypes.NameIdentifier` en `src/SGV.Api/Seguridad/UsuarioActualHttpContext.cs:20` y `src/SGV.Api/Program.cs:139,245`); cualquier reconciliación cruzada quedaría propensa a drift.

**Rationale**: el JWT ya emite `NameIdentifier = "admin-test"` (verificado en `tests/SGV.Tests/Web/Common/AdminJwtTestHelper.cs:76` y `AuthSessionFactoryTests.cs:42`). Siembra manual sólo agrega ruido que termina ganándole a `FindFirstValue` por orden de inserción. El fix es de una línea (quitar la entrada `ClaimTypes.NameIdentifier` de la lista `claims` en `AuthSessionFactory.cs:39`); `ClaimTypes.Name` se mantiene porque `UserNameOrEmail` sigue siendo el display name.

### Cambios en código

| Archivo | Línea | Cambio |
|---|---|---|
| `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs` | 37-41 | Quitar `new(ClaimTypes.NameIdentifier, request.UserNameOrEmail)`; dejar solo `ClaimTypes.Name` |
| `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs` | 105-111 | Mantener `LastOrDefault` defensivo + actualizar comentario (deja de ser workaround RIS-002 y pasa a ser "defensa en profundidad por si JWT no trae NameIdentifier") |
| `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` | 152-173 | Reescribir: `self.Id = "admin-test"` (alineado con `AdminJwtTestHelper.cs:76`); borrar comentario RIS-002 |
| `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` | 191-216 | Reescribir: cambiar `const string selfId = "admin"` → `"admin-test"`; borrar comentario workaround RIS-002 |
| `tests/SGV.Tests/Seguridad/CookiePrincipalRevalidatorTests.cs` | 94-110 | Mantener el test `ValidateAsync_PicksLastNameIdentifierWhenMultipleClaims` — el `LastOrDefault` defensivo sigue activo; actualizar comentario si describe el bug como vigente |

### Verificación post-fix

- `FindFirstValue(ClaimTypes.NameIdentifier)` retorna `"admin-test"` (string, mismo valor que `AuthSessionFactoryTests.cs:42` ya espera).
- `IndexPageTests.cs:153-173` verde: la fila con `self.Id = "admin-test"` desaparece del render de Bloquear/Eliminar porque `EsAutoAccion("admin-test") == true` cuando `CurrentUserId == "admin-test"`.
- `DetailsPageTests.cs:192-216` verde: análogo.
- `CookiePrincipalRevalidatorTests.cs:94` sigue verde (la guarda defensiva no se rompe).
- Grep de seguridad: `rg -n "NameIdentifier" src/SGV.Web tests/SGV.Tests` debe seguir mostrando los mismos sitios (no aparecen consumidores nuevos). Documentar este grep en la guía de revisión del PR.

## Migración SweetAlert2 (PR 2 — `feat/usuarios-popups-sweetalert2`)

### Estrategia de carga: Opción C (helper `wireUsuarioActions(rootElement)`)

**Choice**: extraer un helper `wireUsuarioActions(root)` que se llama desde `Index.cshtml` y `Details.cshtml`. El helper vive en `wwwroot/js/pages/usuarios-index.js` y se carga vía `<script src="/js/pages/usuarios-index.js"></script>` en el `@section Scripts` de cada vista (mismo patrón que `cargos-index.js:1-85` y `puestos-index.js:1-85`).

**Alternativas consideradas**:
- *Opción A*: `Details.cshtml` carga el mismo `<script src="/js/pages/usuarios-index.js">` que `Index.cshtml` (espejo literal). **Descartada** porque deja al JS acoplado al `window.document` global; si `Details` carga el script y por algún motivo `Index` también se carga en la misma response (e.g. fragment render), `wireUsuarioActions` se ejecuta dos veces y registra handlers duplicados. El helper toma `rootElement` para evitar este problema.
- *Opción B*: inline JS con `data-` attributes distintas y export a un helper compartido. **Descartada** porque rompe la paridad con `cargos-index.js`/`puestos-index.js` (mismo archivo JS por módulo) y complica el harness Node (cada vista tendría un script distinto que cargar en `Path.GetFullPath(...)`).
- *Opción C*: helper compartido `wireUsuarioActions(rootElement)`. **Elegida** porque: (a) `cargos-index.js:74-80` y `puestos-index.js:73-80` ya exponen el patrón `window.wireXxxConfirmation = ...` + auto-wire si `window.Swal` está; (b) el harness Node replica el patrón de `CargoIndexPageTests.cs:701-819` sin agregar un módulo nuevo; (c) `Details` puede llamar `wireUsuarioActions(document)` desde su propio `@section Scripts` sin riesgo de doble-registro porque el script se carga una vez por vista y el helper es idempotente sobre los selectores.

### Estructura de `usuarios-index.js`

Espejo de `cargos-index.js:1-85`. Tres funciones + bootstrap condicional + `module.exports`:

```
function wireUsuarioBloquearConfirmation(root, swal)   // REQ-UCB-01
function wireUsuarioDesbloquearConfirmation(root, swal) // REQ-UCB-02
function wireUsuarioDeleteConfirmation(root, swal)      // REQ-ULD-05
if (typeof window !== 'undefined') { window.wireUsuario* = ...; if (window.Swal && window.document) wireUsuario*(window.document, window.Swal) }
if (typeof module !== 'undefined' && module.exports) module.exports = { wireUsuarioBloquearConfirmation, wireUsuarioDesbloquearConfirmation, wireUsuarioDeleteConfirmation }
```

`wireUsuarioActions(root)` queda como agregado que invoca las 3 funciones con guardas (cada una hace early-return si no encuentra los forms esperados, mismo patrón que `Details.cshtml:225-228`).

### Configuración exacta de `Swal.fire`

| Acción | `title` / `titleText` | `text` | `icon` | `confirmButtonText` | `cancelButtonText` | Otros |
|---|---|---|---|---|---|---|
| Bloquear (REQ-UCB-01) | `title: 'Bloquear usuario'` | `'Esta acción afecta este usuario. ¿Desea continuar?'` | `'warning'` | `'Bloquear'` | `'Cancelar'` | `showCancelButton: true`, `showCloseButton: false`, `focusCancel: true`, `allowEscapeKey: true`, `allowOutsideClick: true`, `reverseButtons: true`, `customClass: { confirmButton: 'btn btn-secondary', cancelButton: 'btn btn-light' }` |
| Desbloquear (REQ-UCB-02) | `title: 'Desbloquear usuario'` | `'Esta acción afecta este usuario. ¿Desea continuar?'` | `'warning'` | `'Desbloquear'` | `'Cancelar'` | igual que Bloquear, `confirmButton: 'btn btn-success'` |
| Eliminar (REQ-ULD-05) | `title: 'Eliminar usuario'` | `'Esta acción eliminará este usuario de forma permanente. No se puede deshacer.'` | `'warning'` | `'Eliminar definitivamente'` | `'Cancelar'` | igual, `confirmButton: 'btn btn-danger'` |

**Decisiones de accesibilidad justificadas**:
- **`title` (no `titleText`)**: SweetAlert2 v11.26.x ya envuelve `title` en `<h2 aria-label>` automáticamente cuando se usa `title`. `titleText` se reservaría para casos donde se quiere sobreescribir el aria-label; acá no hace falta.
- **`showCloseButton: false`**: el spec canónico REQ-UCB-05 no lo exige (el patrón vigente es el `X` de Bootstrap que no es estrictamente AA). El botón `Cancelar` cubre el cierre explícito.
- **`focusCancel: true`**: REQ-UCB-05 menciona "foco inicial en un control lógico del modal (el botón de cierre o el `Cancelar`)" — el `X` no existe (decidimos `showCloseButton: false`), por lo tanto `focusCancel` es la única opción coherente. Esto replica implícitamente el patrón de cargos/puestos (que tampoco fuerzan foco explícito — SweetAlert2 por defecto es `focusConfirm`, pero el spec lo pide en Cancelar).
- **`allowEscapeKey: true`, `allowOutsideClick: true`**: ambos true — el `result.dismiss` será `'cancel' | 'backdrop' | 'esc'`, y el handler hace `if (result.isConfirmed) { form.requestSubmit() }`, descartando los tres por construcción.
- **`returnFocus`**: SweetAlert2 ya devuelve foco al elemento que disparó `fire()` (el botón Bloquear/Eliminar/Desbloquear). No hace falta `customReturnFocus` porque el botón disparador vive en la misma vista y no se desmonta durante el alert.
- **`customClass`**: replica las clases Bootstrap (`btn btn-secondary`/`btn-success`/`btn-danger`) para mantener consistencia visual con Inspinia y no introducir un tema alternativo. El proyecto ya carga `wwwroot/scss/plugins/_sweetalert2.scss` (verificado en `plugins.config.js:22-28` y `wwwroot/scss/app.scss:96`) que sobreescribe estilos default; las clases custom se aplican encima.
- **`reverseButtons: true`**: consistente con cargos/puestos — invierte el orden visual (Cancelar a la izquierda, Confirmar a la derecha en LTR). Reduce clicks accidentales sobre la acción destructiva porque el botón queda más lejos del disparador.

### Cambios en `Index.cshtml`

| Líneas | Cambio |
|---|---|
| 184-185 | Quitar `data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"` del botón Bloquear (queda solo `data-usuario-bloquear-button`) |
| 197-198 | Quitar `data-bs-toggle="modal" data-bs-target="#confirm-delete-modal"` del botón Eliminar |
| 217 | Quitar `data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"` del botón Desbloquear |
| 259-280 | Borrar `<div id="confirm-delete-modal">` (modal nativo de Eliminar) |
| 288-306 | Borrar las dos invocaciones de `_ConfirmarAccionUsuarioModal` |
| 308-361 | Borrar el `<script>` inline con la IIFE y los handlers de Bootstrap |
| `<head>` | Agregar `<link rel="stylesheet" href="/plugins/sweetalert2/sweetalert2.min.css" />` (espejo `Cargos/Index.cshtml:10`) |
| `@section Scripts` | Agregar `<script src="/plugins/sweetalert2/sweetalert2.all.min.js"></script>` + `<script src="/js/pages/usuarios-index.js"></script>` |

### Cambios en `Details.cshtml`

| Líneas | Cambio |
|---|---|
| 116 | Quitar `data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"` |
| 135-136 | Quitar `data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"` |
| 173-191 | Borrar las dos invocaciones de `_ConfirmarAccionUsuarioModal` |
| 193-230 | Borrar el `<script>` inline |
| `<head>` + `@section Scripts` | Mismo mirror que Index (link + script sweetalert2 + script usuarios-index) |

### Archivo a borrar

`src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` (83 líneas). Sin reemplazo — la confirmación es SweetAlert2, no necesita markup modal. Cualquier consumidor futuro (e.g. Persona) que necesite confirmación Bootstrap replicaría el patrón de cargos.

### Manejo del `<form data-usuario-delete-form>` en Index

En `Index.cshtml:189-202`, el form de Eliminar tiene `formaction="?handler=Delete"` en el propio `<button>` (no en el form). El PR 2 debe quitar el `formaction="?handler=Delete"` y el `type="submit"` del botón, dejando solo `data-usuario-delete-button type="button"` para que `wireUsuarioDeleteConfirmation` controle el submit vía `form.requestSubmit(button)` (espejo de `cargos-index.js:25-30`). El form mantiene `method="post"` y `@Html.AntiForgeryToken()`.

## Harness JS para tests

### Patrón

Espejo de `CargoIndexPageTests.cs:701-819` y `PuestoIndexPageTests.cs:647-794`. Cada test:

1. Resuelve el path absoluto de `wwwroot/js/pages/usuarios-index.js` con `Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/usuarios-index.js"))`.
2. Escribe un harness Node en `%TEMP%/usuario-{kind}-{guid}.cjs` que:
   - `require()` el script y desestructura la función bajo test.
   - Construye un `root` mock con `querySelectorAll` que retorna un array de forms sintéticos (uno por `data-usuario-*-form`).
   - Construye un `Swal` mock que captura la config y devuelve `Promise.resolve({ isConfirmed: <bool> })` o `Promise.resolve({ dismiss: 'cancel' })` según el caso.
   - Invoca el wire, dispara el `click`, espera microtasks, serializa el resultado a stdout.
3. Lanza `node "<harnessPath>"` con `ProcessStartInfo`, captura stdout/stderr, hace `WaitForExitAsync`.
4. Deserializa y asserta.

### Helpers a implementar en `IndexPageTests.cs`

```
private enum UsuarioConfirmationKind { Bloquear, Desbloquear, Eliminar }
private static async Task<UsuarioScriptExecutionResult> ExecuteUsuarioConfirmationScriptAsync(UsuarioConfirmationKind kind, string dismiss = null)
```

`dismiss` cubre `null` (isConfirmed=true), `"cancel"`, `"backdrop"`, `"esc"`. El mock de Swal devuelve `{ isConfirmed: false, dismiss: <dismiss> }` cuando se quiere simular descarte (espejo de `puestos-index.js:23-31`: el handler verifica `result.isConfirmed`, así que cualquier `dismiss` distinto de confirmar no dispara `form.requestSubmit()`).

### Tests E2E del lado Razor

Asserts sobre el HTML renderizado por `lease.Client.GetAsync("/seguridad/usuarios")` y `/seguridad/usuarios/detalle/{id}`:

| Asserción | Antes (vigente) | Después (PR 2) |
|---|---|---|
| Script SweetAlert2 presente | ausente | `Assert.Contains("/plugins/sweetalert2/sweetalert2.all.min.js", content)` |
| Script `usuarios-index.js` presente | ausente | `Assert.Contains("/js/pages/usuarios-index.js", content)` |
| ID modal nativo Eliminar | presente | `Assert.DoesNotContain("id=\"confirm-delete-modal\"", content)` |
| ID modal nativo Bloquear | presente | `Assert.DoesNotContain("id=\"confirm-bloquear-modal\"", content)` |
| ID modal nativo Desbloquear | presente | `Assert.DoesNotContain("id=\"confirm-desbloquear-modal\"", content)` |
| Botón Bloquear sin `data-bs-toggle` | `Assert.Contains("data-bs-toggle=\"modal\"", content)` | `Assert.DoesNotContain("data-bs-toggle=\"modal\"", content)` para Bloquear |
| Botón Eliminar sin `data-bs-toggle` | idem | idem para Eliminar |
| `data-usuario-*-button` sigue presente | sí | sí (sin cambios) |
| `data-usuario-*-form` sigue presente | sí | sí (sin cambios) |

Tests existentes a actualizar para reflejar la nueva realidad:
- `IndexPageTests.cs:573-655` (presencia de `data-bs-toggle`/`data-bs-target`): invertir a `DoesNotContain`.
- `IndexPageTests.cs:619-655` (modal nativo renderizado): invertir a `DoesNotContain` y reemplazar con asserts del script.
- `IndexPageTests.cs:689-717` (PII en modal): el bloque "modal" deja de existir; mover el assert al body del alert simulado vía JS — o mantener `Assert.DoesNotContain("agarcía", content)` global sobre toda la response.
- `IndexPageTests.cs:719-786` (Desbloquear modal): análogo.
- `DetailsPageTests.cs:218-306` (modal Bloquear + PII): análogo.

### Tests de integración con factory

`SgvWebApplicationFactory` + override de `IUsuarioApiClient` ya cubre el roundtrip POST Bloquear/Desbloquear/Eliminar. No se agregan tests nuevos para el flujo confirmado — los tests `IndexPageTests.cs:294-419` y `:421-470` siguen cubriendo el POST con antiforgery válido + feedback. El cambio es puramente UX (el form sigue siendo POST `?handler=Bloquear`, etc., lo único que cambia es quién dispara el submit).

## Decisiones arquitectónicas (ADRs inline)

### D1: cause-root vs workaround replicado (PR 1)

- **Choice**: cause-root — quitar la siembra manual de `NameIdentifier` en `AuthSessionFactory.cs:39`.
- **Rationale**: el JWT ya emite el GUID correcto (`AdminJwtTestHelper.cs:76`); siembra manual sólo agrega ruido. Workaround replicado (e.g. `LastOrDefault` en cada `CurrentUserId`) propaga el bug a cada consumidor nuevo.
- **Tradeoff aceptado**: si en el futuro el JWT dejara de incluir `NameIdentifier` por un cambio upstream, `FindFirstValue` retornaría null y el sistema entero se rompería. La guarda defensiva en `CookiePrincipalRevalidator.cs:108-111` cubre el caso "no NameIdentifier" (logueado como warning, preserva cookie). El `ClaimTypes.Name` sembrado manualmente se mantiene como display name.

### D2: estrategia de carga Index/Details (PR 2)

- **Choice**: helper `wireUsuarioActions(root)` llamado desde ambas vistas, script único `usuarios-index.js`.
- **Rationale**: paridad con `cargos-index.js`/`puestos-index.js`, harness Node simple, evita doble-registro de handlers.
- **Tradeoff aceptado**: cualquier futuro consumidor (e.g. Persona, Habilidad) que quiera confirmación tiene su propio script. No hay un "shared bundle" de confirmaciones — sigue siendo 1:1 entre módulo y archivo JS.

### D3: configuración SweetAlert2 (PR 2)

- **Choice**: `focusCancel: true`, `showCloseButton: false`, `allowEscapeKey/OutsideClick: true`, `reverseButtons: true`, `customClass` con clases Bootstrap.
- **Rationale**: REQ-UCB-05 menciona foco en Cancelar; el patrón de cargos/puestos no fuerza foco, pero el spec canónico pide foco en Cancelar para usuarios. `customClass` mantiene consistencia visual sin introducir tema nuevo.
- **Tradeoff aceptado**: `showCloseButton: false` significa que usuarios con mouse pueden tener un click extra si esperaban la X. El botón Cancelar cubre el 100% de los flujos de descarte.

### D4: contrato de tests estable

- **Choice**: mantener `data-usuario-bloquear-form`, `data-usuario-bloquear-button`, `data-usuario-desbloquear-form`, `data-usuario-desbloquear-button`, `data-usuario-delete-form`, `data-usuario-delete-button` como contrato observable entre vista y JS.
- **Rationale**: los tests web existentes (`IndexPageTests.cs:46-47, 109-110, 126-131, 147-149`, `DetailsPageTests.cs:44-47, 66-68, 87-89, 125-128, 174-176, 213-215`) y los tests de harness futuros assertean sobre estos atributos. Renombrarlos implicaría actualizar ~30 asserts y perder la trazabilidad histórica.
- **Tradeoff aceptado**: el naming es inconsistente con `data-cargo-*-form`/`data-puesto-*-form` (usuarios tiene 3 acciones, los otros 2). No vale refactorizar.

### D5: rollback

- **PR 1**: `git revert <sha> + re-siembra NameIdentifier en AuthSessionFactory.cs:39 + restaurar comentario CookiePrincipalRevalidator.cs:105-108`. Riesgo bajo: el fix es de una línea.
- **PR 2**: `git revert <sha> + restaurar _ConfirmarAccionUsuarioModal.cshtml + restaurar modal nativo + JS inline + agregar data-bs-toggle/data-bs-target`. Riesgo medio: requiere re-borrar lo eliminado y re-agregar lo agregado.
- **Datos**: `Persons`/`Auditorias`/`AspNetUsers` intactos. Cero migraciones.

## Plan de tareas por PR

### PR 1 — `fix/seguridad-usuarios-ris-002-cause-root` (~120 líneas)

| ID | Tarea | Archivos |
|---|---|---|
| T-01 | Quitar siembra manual de `NameIdentifier` | `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-41` |
| T-02 | Actualizar comentario del workaround defensivo | `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:105-111` |
| T-03 | Adaptar test Index auto-fence (`self.Id = "admin-test"`) | `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs:152-173` |
| T-04 | Adaptar test Details auto-fence (`selfId = "admin-test"`) | `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs:191-216` |
| T-05 | Actualizar test defensivo de `CookiePrincipalRevalidator` (mantener, ajustar comentario) | `tests/SGV.Tests/Seguridad/CookiePrincipalRevalidatorTests.cs:94-110` |
| T-06 | Nuevo E2E: `Index_E2E_Admin_NoVeSusPropiosBotones` con asserts de ausencia | `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` (nuevo test) |
| T-07 | Validar `dotnet test SGV.slnx` + grep de seguridad `rg -n NameIdentifier` | — |

### PR 2 — `feat/usuarios-popups-sweetalert2` (~450 líneas)

| ID | Tarea | Archivos |
|---|---|---|
| T-08 | Crear `usuarios-index.js` con 3 funciones + bootstrap + exports | `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` (nuevo) |
| T-09 | Editar `Index.cshtml`: agregar `<link>`+`<script>` en head/Scripts, quitar `data-bs-toggle`/`data-bs-target`, borrar modal nativo, borrar parcial, borrar JS inline | `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:1-362` |
| T-10 | Editar `Details.cshtml`: agregar `<link>`+`<script>`, quitar `data-bs-toggle`/`data-bs-target`, borrar parcial, borrar JS inline | `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml:1-231` |
| T-11 | Borrar `_ConfirmarAccionUsuarioModal.cshtml` | `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` |
| T-12 | Invertir asserts de presencia de modal nativo a ausencia + asserts de presencia de scripts SweetAlert2/usuarios-index | `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs:573-786`, `DetailsPageTests.cs:218-306` |
| T-13 | Implementar harness Node con helper `ExecuteUsuarioConfirmationScriptAsync` y `enum UsuarioConfirmationKind { Bloquear, Desbloquear, Eliminar }` | `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` (nuevos tests + helper) |
| T-14 | Validar `bun run build` en `src/SGV.Web` | — |
| T-15 | Validar `dotnet test SGV.slnx` completo | — |

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Cambio en siembra de `NameIdentifier` rompe otro consumidor de Web que asume `UserNameOrEmail` | Baja (sólo `IndexModel.CurrentUserId` y `DetailsModel.CurrentUserId` lo leen) | `rg -n "NameIdentifier" src/SGV.Web tests/SGV.Tests` antes de mergear PR 1; documentado en guía de revisión |
| SweetAlert2 ausente en runtime si Bun no procesa el bundle | Baja (ya integrado desde change `2026-06-26`, ver `plugins.config.js:22-28` y `package.json:46`) | `bun run build` en `src/SGV.Web` valida que el bundle incluye el CSS+JS; tests E2E assertean presencia del `<script src="/plugins/sweetalert2/...">` |
| PR 2 mergeado antes que PR 1 reintroduce confusión sobre auto-fence | Media si el orden se invierte | Stacked-to-main enforced por la regla del proposal; el E2E `Index_E2E_Admin_NoVeSusPropiosBotones` (T-06) es gate de PR 1 y se re-ejecuta en CI de PR 2 |
| Tests JS acoplados a implementación interna (e.g. aserciones sobre `__pendingBloquearTrigger`) | Media si el harness no sigue el patrón | El harness exporta vía `module.exports` (no toca globals) y assertea sobre el contrato observable (`submitCount`, `swalConfig.*`) — no sobre el orden de ejecución ni nombres de variables internas |
| Foco de teclado en SweetAlert2 al ser invocado programáticamente | Baja (SweetAlert2 v11.26.x maneja foco por defecto) | El spec canónico REQ-UCB-05 línea 117 dice "foco inicial en un control lógico del modal (el botón de cierre o el Cancelar)" — `focusCancel: true` cubre ambos casos donde no hay X |
| Backend `AutoBloqueo`/`AutoEliminacion` se debilita por un cambio futuro | Baja (no se toca en este change, sigue siendo fence server-side) | Documentado en propuesta como "out of scope"; comentario explícito en `Index.cshtml.cs:131-138` y `:200-204` sigue presente |
| `bun run build` falla por nueva dependencia no declarada | Nula (no se agrega ninguna dependencia) | `package.json:46` ya tiene `sweetalert2 ^11.26.3`; el lockfile en `bun.lock:1104` ya lo resuelve |
| Regresión en accesibilidad al pasar de Bootstrap modal (nativo `<dialog>`-like) a SweetAlert2 (DOM inyectado) | Baja | REQ-UCB-05 cubre AA; el patrón de cargos/puestos ya validó SweetAlert2 en producción; los tests E2E assertean presencia de `aria-labelledby` implícito vía `title` |

## Próxima fase

`sdd-tasks` con justificación breve: el design ya está lo suficientemente concreto como para descomponer en tareas T-01..T-15 con criterios de aceptación verificables (cada tarea tiene al menos un assert observable asociado en los tests). El orden de tareas refleja la dependencia secuencial (PR 1 antes que PR 2) y respeta `strict_tdd: true` (T-03, T-04, T-06, T-12, T-13 son tareas de test que deben escribirse antes que T-01/T-08).
