# Exploration: reusable-persona-card (issue #219)

## Current State

### Duplication confirmed

El issue #219 identifica correctamente 3 implementaciones distintas de "card de persona":

| Vista | Implementación actual | Email | Teléfono | Estado | Quitar/Cambiar |
|-------|----------------------|-------|----------|--------|----------------|
| `Usuarios/Details` (L88-134) | Card inline completa en Razor | ✅ | ✅ | ✅ | ❌ (readonly) |
| `Usuarios/_Form` (L22-117) | Card editable completa en Razor | ✅ | ✅ | ✅ | ✅ |
| `Ocupaciones/Details` | Solo texto `PersonaNombre` plano | ❌ | ❌ | ❌ | ❌ |
| `Ocupaciones/_Form` (L9-66) | Card simplificada (falta Email, Teléfono, Estado, Quitar/Cambiar) | ❌ | ❌ | ❌ | ❌ |

La lógica `FormatDocumento` está duplicada en 3 lugares con idéntico bytecode:
- `Usuarios/Details.cshtml` L256: `FormatDocumento(PersonaDto?)`
- `Usuarios/_Form.cshtml` L225: `FormatDocumento(PersonaDto?)`
- `Ocupaciones/_Form.cshtml` L129: `FormatearDocumento(PersonaDto?)` (nombre diferente, lógica igual)

### Gap en Ocupaciones/Details

`Ocupaciones/Details.cshtml.cs` **no inyecta** `IPersonaApiClient` ni llama `GetByIdAsync`. El `OcupacionDetailsViewModel` solo tiene `OcupacionDto` — no carga `PersonaDto`. La vista actual solo muestra `PersonaNombre` como texto plano.

### Dependencias existentes

- `PersonaDto` — existe en `SGV.Contracts.Personas.Consultas.Dtos`
- `IPersonaApiClient.GetByIdAsync(Guid)` — existe, usado en `Usuarios/Details.cshtml.cs`
- `usuario-persona-buscador.js` — existe en `wwwroot/js/pages/`
- `_PersonaBuscadorModal.cshtml` — existe en `Pages/Seguridad/Usuarios/`
- `SGV.Web/Helpers/` — **no existe**, sería directorio nuevo

## Affected Areas

| Archivo | Impacto | Cambio |
|---------|---------|--------|
| `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` | **Nuevo** | Partial unificada con modos `readonly` y `editable` |
| `src/SGV.Web/Helpers/PersonaFormatHelper.cs` | **Nuevo** | Helper estático `FormatDocumento(PersonaDto?)` — elimina las 3 copias |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | Modificado | Reemplaza card inline por `@await Html.PartialAsync("_PersonaCard", ...)` modo readonly |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Modificado | Reemplaza card editable por la partial — elimina `@functions { FormatDocumento }` |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | Modificado | Reemplaza texto `PersonaNombre` por card completa en modo readonly |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | Modificado | Inyecta `IPersonaApiClient`, carga `PersonaDto` en `OnGetAsync`, expone en ViewModel |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionDetailsViewModel.cs` | Modificado | Agrega `PersonaDto? Persona` property |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | Modificado | Reemplaza card simplificada por partial en modo editable — ahora gana Email, Teléfono, Estado, Quitar/Cambiar |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs` | Sin cambios | Ya tiene `PersonaDto? PersonaVinculada` |
| `src/SGV.Web/Pages/Personas/Details.cshtml` | **Sin cambios** | Excluido explícitamente por el issue |

## Approaches

### 1. Partial view con ViewDataDictionary (enfoque recomendado)

Crear `_PersonaCard.cshtml` en `Pages/Shared/Partials/` que acepte `PersonaDto?` como `@model` y parámetro `Mode` (`"readonly"` | `"editable"`) vía `ViewDataDictionary`. Parámetros opcionales: `ShowDetailButton`, `ShowStatusBadge`, `ShowQuitarCambiar`, `DisplayContainerId`, `PersonaIdInputName`, `ModalId`.

**Pros**:
- Un solo archivo reemplaza 3+ implementaciones inconsistentes
- ViewData permite defaults sensatos y backward-compat sin quebrar call sites existentes
- Modalidad `readonly`/`editable` controlable por el consumer sin duplicar markup
- Compatible con el JS existente `usuario-persona-buscador.js` y sus `data-*` attributes

**Cons**:
- ViewDataDictionary con strings es menos tipado que un modelo dedicado
- Acopla la partial al contrato de JS existente (`data-usuario-persona-*`)

**Complexity**: Medium

### 2. Componente Tag Helper dedicado

Reemplazar el partial view por un Tag Helper que genere el HTML de la card.

**Pros**:
- Tipado fuerte con propiedades del Tag Helper
- Más testable que un partial view con strings

**Cons**:
- Sobrediseño para un problema de deduplicación, no de arquitectura
- Tag Helper no es el patrón predominante en SGV.Web (el proyecto usa Razor partials)

**Complexity**: High

### 3. Componente Blazor (no viable)

**Pros**: -
**Cons**: SGV.Web usa Razor Pages con Inspinia, no Blazor; agregar Blazor rompe la consistencia del stack
**Complexity**: N/A

## Recommendation

**Enfoque 1 — Partial view con ViewDataDictionary**, por las siguientes razones:
1. Sigue el patrón existente de SGV.Web (partials con `@model` y `ViewData`)
2. Minimiza la superficie de cambio: 4 consumers migran a la misma API de partial
3. El issue ya define el diseño completo con los parámetros ViewData correctos
4. `PersonaFormatHelper.FormatDocumento` extraído a clase estática elimina la duplicación de una sola vez
5. El trabajo de `Ocupaciones/Details.cshtml.cs` de agregar `IPersonaApiClient.GetByIdAsync` es necesario independientemente — la card unificada solo lo hace más visible y reutilizable

## Risks

| Riesgo | Likelihood | Mitigation |
|--------|------------|------------|
| Romper el binding JS de `usuario-persona-buscador.js` en `_Form` de Usuarios y Ocupaciones al cambiar el markup | Low | La issue ya define los `data-*` attributes necesarios; validar con tests de integración web existentes |
| Degradación de `Ocupaciones/Details` si `IPersonaApiClient.GetByIdAsync` falla en productivo | Medium | La issue ya especifica fallback a `PersonaNombre` si el fetch falla — implementar con el mismo patrón de `Usuarios/Details.cshtml.cs` |
| Crear carpeta `Helpers/` que no existe en el proyecto | Low | Es simplemente mkdir; la convención de namespaces de C# lo tolera sin cambios en otros archivos |
| Tests de regresión en `Usuarios/Details` y `Ocupaciones/Details` si el render de la card cambia visualmente | Low | La issue exige paridad visual — verificar con tests de integración web |

## Ready for Proposal

**Sí.** La issue #219 está excepcionalmente bien detallada con diseño completo, tabla de componentes, criterios de aceptación y plan de implementación ordenado. No requiere preguntas de clarificación adicionales antes de proceder a proposal.

**Change name sugerido**: `reusable-persona-card`

**Artifact store**: `hybrid` (OpenSpec + Engram)

**Próximo paso natural**: `sdd-propose` → luego `sdd-spec` → `sdd-design` → `sdd-tasks` → `sdd-apply` → `sdd-verify`
