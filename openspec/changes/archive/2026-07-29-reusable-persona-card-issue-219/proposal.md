# Proposal: reusable-persona-card (issue #219)

## Intent

Unificar la card de persona usada en 4 vistas distintas (`Usuarios/Details`, `Usuarios/_Form`, `Ocupaciones/Details`, `Ocupaciones/_Form`) en un único partial view reutilizable con modos `readonly` y `editable`, eliminando la duplicación de `FormatDocumento()` en 3 lugares y la brecha de funcionalidad donde `Ocupaciones/Details` solo muestra texto plano.

## Scope

### In Scope
- `_PersonaCard.cshtml` parcial unificada en `Pages/Shared/Partials/` con modos `readonly`/`editable` vía `ViewDataDictionary`
- `PersonaFormatHelper.cs` helper estático en `Helpers/` que centraliza `FormatDocumento(PersonaDto?)`
- Migración de `Usuarios/Details.cshtml` → partial modo readonly
- Migración de `Usuarios/_Form.cshtml` → partial modo editable
- Migración de `Ocupaciones/Details.cshtml` → partial modo readonly (con carga de `PersonaDto` en el PageModel)
- Migración de `Ocupaciones/_Form.cshtml` → partial modo editable
- Eliminación de `@functions { FormatDocumento }` duplicados en `Usuarios/Details.cshtml` y `Usuarios/_Form.cshtml`
- Fallback silencioso en `Ocupaciones/Details.cshtml.cs`: si `IPersonaApiClient.GetByIdAsync` falla, degrada a solo `PersonaNombre`

### Out of Scope
- `Personas/Details.cshtml` — **sin cambios**, excluido explícitamente por el issue
- `PersonaFormatHelper` no se introduce en `SGV.Api` ni en otros proyectos que no sea `SGV.Web`
- No se modifica el comportamiento de `_PersonaBuscadorModal` ni de `usuario-persona-buscador.js`
- No se introduce Tag Helper, Blazor, ni componente de navegación nuevo
- No se agrega validación visual automatizada (Percy, captchas, etc.)

## Capabilities

### New Capabilities
- `persona-card-partial`: Partial view `_PersonaCard.cshtml` que renderiza una card de persona en modo `readonly` o `editable` según `ViewData["Mode"]`. Acepta `PersonaDto?` como `@model`. Soporta parámetros `ShowDetailButton`, `ShowStatusBadge`, `ShowQuitarCambiar`, `DisplayContainerId`, `PersonaIdInputName`, `ModalId`, `PersonaDisplay`.
- `persona-format-helper`: Helper estático `PersonaFormatHelper.FormatDocumento(PersonaDto?)` que retorna el texto formateado de documento (`"TipoDoc: NumeroDoc"`) y se usa desde la partial.

### Modified Capabilities
- Ninguna. Los requisitos existentes de `persona-management`, `usuario-web-selector-persona-buscador` y `web-ocupaciones-crear-editar` no cambian de comportamiento — solo el markup se factoriza desde implementaciones inline hacia la partial compartida.

## Approach

Partial view con `ViewDataDictionary` (enfoque recomendado por la exploración). Un único archivo `_PersonaCard.cshtml` recibe `PersonaDto?` como `@model` y un `ViewDataDictionary` con los parámetros de modo y display. El helper `PersonaFormatHelper` centraliza la lógica de formateo de documento en una clase estática, eliminando las 3 copias existentes. La migración es incremental: cada vista existente se actualiza para invocar la partial preservando el markup visual resultante; no se reestiliza.

Para `Ocupaciones/Details.cshtml.cs` se inyecta `IPersonaApiClient`, se carga `PersonaDto` en `OnGetAsync` y se expone en `OcupacionDetailsViewModel.PersonaDto?`. En caso de fallo de la llamada HTTP se degrada silenciosamente al texto `PersonaNombre` ya existente, sin mostrar error al usuario.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` | Nuevo | Partial unificada con modos readonly/editable |
| `src/SGV.Web/Helpers/PersonaFormatHelper.cs` | Nuevo | Helper estático `FormatDocumento(PersonaDto?)` |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | Modificado | Reemplaza card inline por partial modo readonly |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Modificado | Reemplaza card editable por partial; elimina `@functions { FormatDocumento }` |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | Modificado | Reemplaza texto `PersonaNombre` por partial modo readonly |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | Modificado | Inyecta `IPersonaApiClient`, carga `PersonaDto` en `OnGetAsync`, fallback a `PersonaNombre` en caso de fallo |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionDetailsViewModel.cs` | Modificado | Agrega `PersonaDto? Persona` |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | Modificado | Reemplaza card simplificada por partial modo editable |
| `src/SGV.Web/Pages/Personas/Details.cshtml` | Sin cambios | Excluido por el issue |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Romper binding JS de `usuario-persona-buscador.js` al cambiar markup de la card | Low | La issue define los `data-*` attributes necesarios; tests de regresión funcional validan el comportamiento |
| Degradación silenciosa en `Ocupaciones/Details` si la API no responde | Medium | Comportamiento confirmado por el usuario; fallback a `PersonaNombre` preserva la experiencia legacy |
| Duplicación residual de `FormatDocumento` si no se eliminan todas las copias | Low | Plan de implementación ordenado (borrar helpers inline al final del paso 8) |
| Tests de regresión fallidos por cambio de markup | Low | Smoke tests de binding y rendering sobre las 4 vistas migraincorporadas |

## Rollback Plan

1. Revertir los 4 archivos de vista (`Usuarios/Details.cshtml`, `Usuarios/_Form.cshtml`, `Ocupaciones/Details.cshtml`, `Ocupaciones/_Form.cshtml`) a su estado previo con los helpers inline y markup original.
2. Eliminar `_PersonaCard.cshtml` y `PersonaFormatHelper.cs`.
3. Restaurar la inyección de `IPersonaApiClient` y `PersonaDto` en `OcupacionDetailsViewModel` al estado original.
4. Ejecutar `dotnet build` y `dotnet test` para confirmar que los 4 archivos de vista compilan y los tests pasan.

## Dependencies

- `SGV.Contracts.Personas.Consultas.Dtos.PersonaDto` — existente, referenciado por `SGV.Web`
- `IPersonaApiClient.GetByIdAsync(Guid)` — existente, usado en `Usuarios/Details.cshtml.cs`
- `SGV.Web/Integration/ApiClients/IPersonaApiClient` — interfaz existente en la capa web
- `usuario-persona-buscador.js` — sin cambios; la partial solo consume los `data-*` attributes que el JS ya espera
- `_PersonaBuscadorModal.cshtml` — sin cambios; se sigue incluyendo por separado en cada consumer page

## Success Criteria

- [ ] `_PersonaCard.cshtml` existe en `src/SGV.Web/Pages/Shared/Partials/` y acepta `PersonaDto?` como `@model`
- [ ] `PersonaFormatHelper.FormatDocumento(PersonaDto?)` existe y es invocado desde la partial
- [ ] `Usuarios/Details.cshtml` renderiza la card en modo readonly sin cambios visuales
- [ ] `Usuarios/_Form.cshtml` renderiza la card en modo editable con botones Quitar/Cambiar y binding correcto
- [ ] `Ocupaciones/Details.cshtml` muestra card completa con datos de persona en modo readonly
- [ ] `Ocupaciones/Details.cshtml.cs` carga `PersonaDto` vía `IPersonaApiClient`; si falla, degrada a `PersonaNombre`
- [ ] `Ocupaciones/_Form.cshtml` muestra card editable con Email, Teléfono, Estado y botones Quitar/Cambiar
- [ ] `OcupacionDetailsViewModel` expone `PersonaDto? Persona`
- [ ] `Personas/Details.cshtml` no se modifica
- [ ] No existen duplicaciones de `FormatDocumento` ni `FormatearDocumento` en ninguna vista
- [ ] `dotnet build SGV.slnx` compila sin errores
- [ ] `dotnet test SGV.slnx` pasa sin regresiones en las vistas afectadas
