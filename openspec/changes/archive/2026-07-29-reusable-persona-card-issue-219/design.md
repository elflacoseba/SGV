# Design: reusable-persona-card (issue #219)

## Technical Approach

Factorizar la card de persona en un único partial `_PersonaCard.cshtml` (`SGV.Web/Pages/Shared/Partials/`) con `@model PersonaDto?` y modos `readonly`/`editable` vía `ViewDataDictionary`, consumido por las 4 vistas (`Usuarios/Details`, `Usuarios/_Form`, `Ocupaciones/Details`, `Ocupaciones/_Form`). Un helper estático `PersonaFormatHelper.FormatDocumento` (`SGV.Web/Helpers/`) elimina las 3 copias `@functions`. El PageModel de `Ocupaciones/Details` gana carga de `PersonaDto` vía `IPersonaApiClient.GetByIdAsync` con fallback silencioso, espejo del patrón `TryLoadPersonaVinculadaAsync` de `Usuarios/Details`. No hay cambios de dominio, persistencia ni API; es pure shell-web. Mapea a specs `persona-card-partial` (PER-CARD-01..09) y `persona-format-helper` (PERFMT-01..04).

## Architecture Decisions

| Decisión | Opción | Tradeoff / Alternativa rechazada | Razón |
|---|---|---|---|
| Forma del componente | Partial Razor + `ViewDataDictionary` | Tag Helper (tipado fuerte, sobrediseño); Blazor (rompe stack Inspinia) | SGV.Web usa partials con ViewData (`_PersonaBuscadorModal`, `_PageTitle`); mismo patrón |
| Contrato `data-*` del JS | Seguir el JS vigente: `data-usuario-persona-{display,card,display-text,quitar,empty,display-input,buscar,modal}` + Bootstrap `data-bs-toggle/data-bs-target` | Nombres inventados del spec PER-CARD-05 (`data-usuario-persona-cambiar`, `-persona-id`, `-modal-id`, `data-display-container-id`) | `usuario-persona-buscador.js` selecciona por los atributos reales; renombrar rompería el binding. **PER-CARD-05 requiere enmienda** (ver Open Questions) |
| Formato de documento | Preservar `"{tipo} {numero}"` (espacio) del código existente | `"{TipoDoc}: {NumeroDoc}"` (colon, spec PERFMT-01) | El `<dd>Documento</dd>` server-side usa espacio hoy; cambiar a colon viola PER-CARD-09 (sin regresión visual). El colon del JS `personaDisplay` es otra display distinta (parenthetical). **PERFMT-01 requiere enmienda** |
| Fallback readonly `PersonaDto=null` | ViewData `FallbackDisplay`+`FallbackUrl` renderiza el `<div class="card-body py-2"><a>` plano | Partial vacío puro | `Ocupaciones/Details` ya tiene `PersonaNombre`; `Usuarios/Details` ya tiene `PersonaDisplay` con link — preserva la rama fallback existente |
| Carga de Persona en Ocupaciones | Método `TryLoadPersonaAsync` + `TransportFailureClassifier.IsTransportFailure` → `ViewModel.Persona=null`, log warning | Excepción propagada / `IsNotFound` | `GetByIdAsync` ya devuelve null en 404; igualar `Usuarios/Details.TryLoadPersonaVinculadaAsync`. No marcar IsNotFound (la ocupación sí existe) |
| Visibilidad del helper | `@using SGV.Web.Helpers` en `_ViewImports.cshtml` | `@using` local en cada partial | Resolución global; la partial y los consumers lo ven sin repetir |

## Data Flow

    Usuarios/Details.cshtml.cs ──PersonaVinculada──┐
    Ocupaciones/Details.cshtml.cs ──ViewModel.Persona──┤
    Usuarios/_Form / Ocupaciones/_Form ──PersonaDto?.──┤
                                                      ▼
                              _PersonaCard.cshtml  (Mode vía ViewData)
                                      │  PersonaFormatHelper.FormatDocumento
                                      ▼
                          <card> + data-usuario-persona-* ──► usuario-persona-buscador.js

Ocupaciones/Details: `IOcupacionApiClient.ObtenerPorIdAsync` → `OcupacionDetailsViewModel.FromDto` → `IPersonaApiClient.GetByIdAsync(PersonaId)` (catch transporte → `Persona=null`) → parcial readonly con `FallbackDisplay=PersonaNombre`.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Web/Helpers/PersonaFormatHelper.cs` | Create | `public static string FormatDocumento(PersonaDto?)` en `namespace SGV.Web.Helpers`; lógica idéntica a las 3 copias (espacio) + caso `Legajo` (PERFMT-02) |
| `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` | Create | `@model PersonaDto?` + `@using SGV.Web.Helpers`; ramas readonly/editable por `ViewData["Mode"]` (default `readonly`); params `ShowStatusBadge`, `ShowQuitarCambiar`, `PersonaDetailUrl`, `FallbackDisplay`, `FallbackUrl`, `ModalId`, `PersonaIdInputName`(editable) |
| `src/SGV.Web/Pages/_ViewImports.cshtml` | Modify | Agregar `@using SGV.Web.Helpers` |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | Modify | Reemplaza card inline L88-144 por `@await Html.PartialAsync("_PersonaCard", Model.PersonaVinculada, vd{Mode=readonly, ShowStatusBadge=true, PersonaDetailUrl=..., FallbackDisplay=Model.PersonaDisplay, FallbackUrl=...})`; elimina `@functions` L248-285 |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Modify | Reemplaza card editable L26-114 por `PartialAsync("_PersonaCard", PersonaVinculada, vd{Mode=editable, ModalId="usuario-persona-buscador-modal", PersonaIdInputName="Input.PersonaId", ShowQuitarCambiar=true})`; conserva hidden `PersonaDisplay`; elimina `@functions` L224-253 |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | Modify | Inyecta `IPersonaApiClient` (constructor primario); `OnGetAsync` llama `TryLoadPersonaAsync` tras `FromDto`; fallback silencioso |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionDetailsViewModel.cs` | Modify | Agrega `PersonaDto? Persona { get; set; }` + `PersonaNombre` ya existe en `Ocupacion` |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | Modify | Reemplaza `<dd>@o.PersonaNombre</dd>` L46 por `@await Html.PartialAsync("_PersonaCard", Model.ViewModel?.Persona, vd{Mode=readonly, ShowStatusBadge=true, PersonaDetailUrl="/personas/detalle/"+o.PersonaId, FallbackDisplay=o.PersonaNombre})` |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | Modify | Reemplaza card L23-55 por partial editable (`ModalId="ocupacion-persona-buscador-modal"`, `PersonaIdInputName="Input.PersonaId"`, `ShowQuitarCambiar=true`) — **gana** Email/Teléfono/Estado/Quitar/Cambiar; elimina `@functions` L128-157 |

## Interfaces / Contracts

`PersonaFormatHelper.FormatDocumento(PersonaDto?)` — `static`, sin IO: null→`""`; ambos vacíos→`""`; sólo tipo→tipo; sólo número→número; ambos→`"{tipo} {numero}"`; sin documento pero con `Legajo`→`Legajo`.

Partial ViewData keys (todos opcionales salvo `PersonaIdInputName` en editable):
```csharp
new ViewDataDictionary(ViewData) {
    ["Mode"] = "readonly" | "editable",          // default "readonly"
    ["ShowStatusBadge"] = true,
    ["ShowQuitarCambiar"] = true,                // editable only
    ["PersonaDetailUrl"] = "/personas/detalle/" + id,  // readonly link
    ["FallbackDisplay"] = PersonaNombre,         // cuando PersonaDto null
    ["FallbackUrl"] = detailUrl,
    ["ModalId"] = "usuario-persona-buscador-modal",
    ["PersonaIdInputName"] = "Input.PersonaId"   // editable only
}
```
Atributos JS emitidos (binding `usuario-persona-buscador.js` preservado): `data-usuario-persona-display`, `-card`, `-display-text`, `-empty`, `-display-input`, `-quitar`, `-buscar`+`data-bs-toggle="modal"`+`data-bs-target="#{ModalId}"`. readonly omite `-quitar`/`-buscar` y el hidden editable.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `PersonaFormatHelper.FormatDocumento` (PERFMT-01..02): completo, tipo ausente, número ausente, null, sólo Legajo, sin ambos | `dotnet-xunit` `[Theory]`+`InlineData` sobre `SGV.Web.Helpers` |
| Unit | Cero `@functions FormatDocumento`/`FormatearDocumento` en `.cshtml` (PERFMT-03) | Test de árbol de fuentes con `grep` assert en `tests/SGV.Tests` |
| Integration (Web) | `Ocupaciones/Details` admin renderiza card completa con Email/Teléfono/Estado (PER-CARD-02/06) y fallback a `PersonaNombre` ante transporte fallido | `SgvWebApplicationFactory` + mock `IPersonaApiClient` lanza → assert HTML contiene `PersonaNombre` sin Email |
| Integration (Web) | `Usuarios/Details` y `Usuarios/_Form` sin regresión visual: asserts vigentes sobre card pasan (PER-CARD-09); binding JS `data-*` presente | Extender smoke tests `Web/Usuario` existentes |
| Regression | `_Form` Ocupaciones ahora emite Quitar/Cambiar + Email/Teléfono/Estado | Nuevo smoke test en `Web/Ocupaciones` |

No E2E (`testing.layers.e2e.available: false`). Persistencia no se toca → sin `[MySqlFact]`. Validar `bun run build` si la partial .cshtml carga assets (no requiere).

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, ni process-integration boundary. Es deduplicación de markup Razor + un helper estático en la shell web.

## Migration / Rollout

Sin migración de datos ni feature flags (pure refactor de shell + un fetch read-only ya existente en otro PageModel). Rollout atómico en un PR. **Rollback**: (1) revertir los 4 `.cshtml` + `_ViewImports.cshtml` + `Details.cshtml.cs` + `OcupacionDetailsViewModel.cs`, (2) eliminar `_PersonaCard.cshtml` y `PersonaFormatHelper.cs`, (3) `dotnet build SGV.slnx && dotnet test SGV.slnx`. El cambio no altera esquema ni contratos wire → reversible_sin_state.

## Open Questions

- [ ] **PERFMT-01** (spec `persona-format-helper`) exige `"{TipoDocumento}: {NumeroDocumento}"` (colon), pero las 3 copias vigentes usan `"{tipo} {numero}"` (espacio) y PER-CARD-09 prohíbe regresión visual. **Decisión de diseño: preservar espacio.** ¿Confirmar enmienda del spec a espacio, o aceptar regresión visual y adoptar el colon uniformemente? (Recomiendo enmendar PERFMT-01 a espacio.)
- [ ] **PER-CARD-05** lista atributos inexistentes (`data-usuario-persona-cambiar`, `-persona-id`, `-modal-id`, `data-display-container-id`). El JS real usa `data-usuario-persona-buscar` + Bootstrap `data-bs-target` y `data-usuario-persona-modal` en el modal root. **Decisión de diseño: seguir el JS real.** ¿Confirmar enmienda de PER-CARD-05 a los atributos vigentes?
- [ ] `Ocupaciones/Details` readonly: ¿el título de la card debe linkear a `/personas/detalle/{PersonaId}` (igual que `Usuarios/Details`) o dejar texto plano? Diseño asume link (paridad). Confirmar.
- [ ] Badge de Estado en `ShowStatusBadge`: en `Ocupaciones/Details` además de Estado de Persona, conviven el Estado de la Ocupación. Confirmar que `ShowStatusBadge` refiere solo al Estado de la Persona (no colisiona con el badge de Ocupación`.