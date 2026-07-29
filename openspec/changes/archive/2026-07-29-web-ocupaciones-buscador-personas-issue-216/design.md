# Diseño: Buscador de Personas en Ocupación (#216)

> Change: `2026-07-29-web-ocupaciones-buscador-personas-issue-216` · Issue #216 · Modo artefactos: **Both** · Review budget: **400 líneas**.
> Specs: NEW `ocupacion-web-selector-persona-buscador` (7 REQs, 15 escenarios) · MODIFIED `usuario-web-selector-persona-buscador` (1 ADDED REQ-USB-12 + 2 MODIFIED REQ-USB-03/REQ-USB-10).
> Precedente directo: `archive/2026-07-17-buscador-personas-modal` (mismo modal, mismo JS, misma logica de ViewData; única diferencia: `soloSinUsuario`).

## Resumen

Se reemplaza el `<select name="Input.PersonaId">` de `_Form.cshtml` de Ocupaciones (populado vía `IPersonaApiClient.GetAllAsync()`) por la card + modal reutilizable `_PersonaBuscadorModal.cshtml`, ya vigente en Usuarios. La diferencia crítica es que en Ocupaciones una persona puede tener múltiples ocupaciones, por lo que NO se aplica el filtro `soloSinUsuario=true`. Para habilitarlo sin romper Usuarios, se introduce el atributo `data-solo-sin-usuario` en el modal raíz, leído por `usuario-persona-buscador.js` con default `true` (preserva comportamiento vigente de Usuarios). `IOcupacionForm` reemplaza `PersonaOptions` por `PersonaDisplay` (string formateado) y `PersonaVinculada` (`PersonaDto?`); en Edit el PageModel enriquece la card vía `GetByIdAsync`. Sin tocar backend, persistencia, migraciones ni el partial compartido.

## Decisiones arquitectónicas

1. **Path del partial `_PersonaBuscadorModal.cshtml`**: se invoca con path absoluto `~/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml` desde `_Form.cshtml` de Ocupaciones. NO se duplica ni se mueve el partial (cumple el scope "no incluye mover el partial"). Precedente: `PersonaOcupaciones.cshtml` ya invoca `_CrossList.cshtml` con path absoluto.
2. **Estrategia de testing JS**: **opción (a) — DOM contract test** parseando el HTML renderizado por `SgvWebApplicationFactory`. Se asemeja al patrón vigente en `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs`, que valida el modal markup y el BFF vía `Assert.Matches`/`Assert.Contains` sobre el HTML y `FakePersonaApiClient.QueryCalls`. Se agregan aserciones sobre el atributo `data-solo-sin-usuario="false"` del modal en el HTML de Ocupaciones. El query param flag se cubre ya en `FakePersonaApiClientTests` y `PersonaApiClientBasicTests` (transport contract). No se introduce jsdom.
3. **Eliminación de `PersonaOptions`**: verificado vía grep — único consumidor es `Ocupaciones/_Form.cshtml`, `IOcupacionForm` y `OcupacionFormPageModel`. Ningún listado, autocomplete u otro PageModel lo referencia. Se elimina limpio de interfaz, PageModel y `LoadCatalogsAsync`.
4. **Contrato `IOcupacionForm`**: expone `PersonaDisplay` (`string?`) y `PersonaVinculada` (`PersonaDto?`). `PersonaDisplay` formatea `Apellido, Nombre (TipoDoc: NroDoc)` caendo a `Legajo` si no hay documento (espejo de la función `personaDisplay` del JS y de `FormatDocumento` del `_Form` de Usuarios).
5. **Carga de `PersonaVinculada` en Edit**: `OnGetAsync` de `EditModel` resuelve `Input.PersonaId` desde `OcupacionDto` y luego invoca `personaApiClient.GetByIdAsync(Input.PersonaId!.Value)`. Falla suave: si la persona no existe o hay error de transporte, `PersonaVinculada = null` y `PersonaDisplay` cae al fallback plano (sin excepción, sin `IsRecoverable`). En `Create`, `OnGetAsync` invoca `GetByIdAsync` si `personaId` vino por query string (precarga cruzada), caendo a estado vacío si el id es inválido (REQ/OCC-PER-BUSC-05).
6. **Modalidad de invocación del modal**: el partial se invoca desde `_Form.cshtml` (no desde `Create.cshtml`/`Edit.cshtml`), porque el contrato de ViewData (`HiddenInputId`, `DisplayContainerId`) vive en el mismo scope del form. La inclusión del script `usuario-persona-buscador.js` se hace vía `@section Scripts` en `Create.cshtml` y `Edit.cshtml` (espejo de Usuarios, no se incluye en el partial para no duplicar entre Create y Edit).
7. **Backwards-compat del JS**: lectura case-insensitive de `data-solo-sin-usuario` contra `"true"`; valores `"false"`, `"False"`, ausente o no-parseable → defaultea `true` (preserva Usuarios sin cambios en markup ni tests). Una sola línea hardcodeada (js:154) se reemplaza por lectura + conditional.

## Cambios por archivo

| Archivo | Tipo | Descripción | LOC ± |
|--------|------|-------------|------|
| `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | MODIFIED | Lectura de `data-solo-sin-usuario` del modal raíz; conditionaliza `url.searchParams.set("soloSinUsuario", valor)`. Default `true`. | +3 / -1 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs` | MODIFIED | Remueve `PersonaOptions`; agrega `PersonaDisplay` (`string?`) y `PersonaVinculada` (`PersonaDto?`). | +6 / -4 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs` | MODIFIED | Remueve `PersonaOptions` y su carga vía `GetAllAsync` en `LoadCatalogsAsync`. Agrega `PersonaDisplay`, `PersonaVinculada` y helper protegido `EnriquecerPersonaAsync(IPersonaApiClient, Guid, ILogger, CancellationToken)`. | +45 / -12 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs` | MODIFIED | `OnGetAsync` invoca `EnriquecerPersonaAsync` cuando `Input.PersonaId` viene del query string; caída suave a estado vacío si `GetByIdAsync` devuelve `null` o lanza transporte. | +12 / -1 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Edit.cshtml.cs` | MODIFIED | `OnGetAsync` invoca `EnriquecerPersonaAsync(Input.PersonaId!.Value)` tras poblar `Input` desde `OcupacionDto`; falla suave. | +8 / 0 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | MODIFIED | Reemplaza `<select PersonaId>` (líneas 9-18) por card-enriquecida + `input type="hidden" asp-for="Input.PersonaId"` + cards `Quitar`/`Cambiar`/`Buscar Persona` (espejo del col-12 de Usuarios `_Form`). Invoca `_PersonaBuscadorModal` con `data-solo-sin-usuario="false"` vía `ViewDataDictionary`. | +96 / -10 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` | MODIFIED | Agrega `@section Scripts { <script src="/js/pages/usuario-persona-buscador.js"></script> }`. | +3 / 0 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Edit.cshtml` | MODIFIED | Agrega `@section Scripts` idéntico (dentro del `else` de `IsRecoverable`). | +3 / 0 |
| `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs` | MODIFIED | Reemplaza aserciones de `<option>` (`García, Ana`, `Analista`, `personaClient.GetAllCalls`) por aserciones del modal (card, hidden, botón `Buscar`, ausencia de `<select name="Input.PersonaId">`). El test de catálogo caído ajusta: ya no espera `<select>` pero sí hidden + error general. | +18 / -6 |
| `tests/SGV.Tests/Web/Ocupaciones/OcupacionEditPageTests.cs` | MODIFIED | Ajusta aserciones análogas para Edit; agrega aserción de `PersonaDisplay` poblado y `personaClient.GetByIdCalls` único tras precarga. | +20 / -4 |
| `tests/SGV.Tests/Web/Ocupaciones/OcupacionBuscadorModalTests.cs` | NEW | DOM contract test: modal raíz con `data-solo-sin-usuario="false"`, presencia de `data-usuario-persona-modal`, estados Inicial/Empty/Loading/Error, hidden poblado, `usuario-persona-buscador.js` incluido. Espejo de `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs`. | +120 / 0 |

## Estrategia de testing

| Concern | Tipo | Cantidad | Fuente |
|--------|------|---------|--------|
| Modal raíz declara `data-solo-sin-usuario="false"` | DOM contract (HTML parse) | 1 | NEW `OcupacionBuscadorModalTests` |
| `_Form.cshtml` Ocupaciones: ausencia de `<select PersonaId>`, presencia de hidden + botón `Buscar` | DOM contract | 2 (Create + Edit) | extend `OcupacionCreatePageTests` / `OcupacionEditPageTests` |
| `LoadCatalogsAsync` ya NO invoca `personaApiClient.GetAllAsync` | Unit (via fake `GetAllCalls`) | 1 (entre Create/Edit) | extend `OcupacionCreatePageTests` |
| Edit invoca `GetByIdAsync(Input.PersonaId)` y puebla `PersonaVinculada` | Unit (via fake `GetByIdCalls`) | 1 | extend `OcupacionEditPageTests` |
| `PersonaDisplay` formatea `Apellido, Nombre (TipoDoc: NroDoc)` caendo a `Legajo` | Unit | 1 | extend Edit tests con `FakePersonaApiClient` poblado |
| Create con `?personaId` válido precarga card; inválido cae a estado vacío | Integration | 2 | extend `OcupacionCreatePageTests` (reemplaza el test actual de precarga) |
| `Quitar` limpia `PersonaId` sin invocar API | DOM contract (verficación de markup `data-usuario-persona-quitar` + JS ya cubierto) | 1 | NEW `OcupacionBuscadorModalTests` |
| Backwards-compat JS Usuarios | Integration (regresión) | 0 nuevos | `PersonaBuscadorModalTests` existentes cubren markup + BFF; no requieren cambios |
| Flag `soloSinUsuario` transport contract | Unit | 0 nuevos | `FakePersonaApiClientTests` / `PersonaApiClientBasicTests` vigentes |

**Total estimado de tests nuevos**: ~5-6 casos (sin inflar). Cumple política del repo " ante la duda, menos tests, de alta calidad".

## Estimación de LOC

| Bloque | Líneas |
|--------|--------|
| Production (`_Form.cshtml`, PageModels, JS, IOcupacionForm) | ~178 |
| Tests (extensión + NEW `OcupacionBuscadorModalTests`) | ~160 |
| Artefacto `design.md` | (no entra en budget de PR) |
| **Review total aprox.** | **~338** |

Si superase 400 durante implementación (e.g., enriquecimiento del modal fallback), `Decision needed before apply: Yes`. Bajo `ask-always`, se consulta antes de superar.

## Riesgos restantes

1. **Estrategia de testing JS** → Resuelto con **opción (a) DOM contract + assertions sobre `data-solo-sin-usuario`**. Justificación: replica el patrón vigente en `PersonaBuscadorModalTests.cs` que ya cubre el modal HTML y el BFF vía `FakePersonaApiClient.QueryCalls`. El flag `soloSinUsuario` del query param ya está cubierto por tests de transport contract vigentes, por lo que no se duplica. No se introduce jsdom (OVERKILL para este scope). Severidad: MEDIA.
2. **`LoadCatalogsAsync` / `PersonaOptions`** → Verificado via grep: consumidores limitados a Ocupaciones (`IOcupacionForm`, `OcupacionFormPageModel`, `_Form.cshtml`). Ningún listado, autocomplete u otro PageModel los usa. **Eliminación limpia**. El método `LoadCatalogsAsync` sobrevive (PuestoOptions sí se mantiene). Severidad: BAJA.
3. **Path del partial `_PersonaBuscadorModal.cshtml`** → Resuelto con **opción (a) path absoluto** desde `_Form.cshtml` (`~/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml`). Sin duplicar, sin mover. Precedente: `PersonaOcupaciones.cshtml` / `PuestoOcupaciones.cshtml` ya invocan `_CrossList.cshtml` por path absoluto. Severidad: BAJA.

Nota: el riesgo de "card fallback 404 en Edit" (persona inexistente asociada a una ocupación vigente — inconsistencia de datos hipotética) se cubre con caída suave a card plana sin `IsRecoverable`, preservando el flujo de edición.

## Plan de implementación (alto nivel)

1 PR, 4 work-units bajo `strict_tdd` (`test → feat → refactor` por WU). Sin chained PRs (estimación bajo budget):

- **WU-1 (JS fix)**: tests regresión DOM en `PersonaBuscadorModalTests` para `data-solo-sin-usuario` ausente/inválido/"false" → modificación de `usuario-persona-buscador.js` (línea 154).
- **WU-2 (Contrato + PageModel)**: tests `IOcupacionForm` miembros + `LoadCatalogsAsync` no invoca ` GetAllAsync` → extensión `IOcupacionForm`, refactor `OcupacionFormPageModel` (`EnriquecerPersonaAsync`), eliminación `PersonaOptions`.
- **WU-3 (Create/Edit wiring)**: tests de precarga `?personaId` válido/inexistente + Edit enriquece card → ajuste `Create.cshtml.cs` / `Edit.cshtml.cs`.
- **WU-4 (UI + DOM contract)**: tests `OcupacionBuscadorModalTests` (DOM contract, ausencia `<select>`, botón `Buscar`) → cambio `_Form.cshtml` + `@section Scripts` en `Create.cshtml` / `Edit.cshtml`. Reemplazo/retiro de aserciones `Assert.Contains("García, Ana"...)` en `OcupacionCreatePageTests` / `OcupacionEditPageTests`.

Validación por WU: `dotnet build SGV.slnx` + `dotnet test SGV.slnx` selectively por capa afectada; suite completa verde al final de WU-4.