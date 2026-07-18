# Proposal: Card enriquecida de Persona en el detalle readonly del usuario

## Intent

PR #168 agregó en `/seguridad/usuarios/editar/{id}` una card enriquecida con Legajo/Documento/Email/Teléfono/Estado de la persona vinculada. Hoy `/seguridad/usuarios/detalle/{id}` muestra sólo el Guid crudo en un `<a href="/personas/detalle/{PersonaId}">`. Replicar la card en Details para consistencia con Edit, conservando la naturaleza readonly.

## Scope

### In Scope

1. `Details.cshtml` — sustituir líneas 78-81 (Guid + link) por card read-only con el mismo árbol DOM que `_Form.cshtml` (líneas 27-106), omitiendo botones y modal; preservar el `<a>` como título clickable.
2. `Details.cshtml.cs` — inyectar `IPersonaApiClient`; agregar `PersonaDto? PersonaVinculada` y `string? PersonaDisplay`; agregar `TryLoadPersonaVinculadaAsync(Guid, CancellationToken)` espejo de `Edit.cshtml.cs` (líneas 205-229) y llamarlo desde `OnGetAsync` tras cargar `Usuario`; 404/transporte → `null` con `LogWarning(...)`; **NO** marcar `IsNotFound`.
3. `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` — sumar 4 tests sobre `WebIntegrationFixture.CreateUsuarioLeaseAsync(usuarioApiClient, personaApiClient, adminRole)` y `FakePersonaApiClient.WithPersonaList(dto)`.

### Out of Scope

`SGV.Api`, nuevos endpoints, partial compartido Edit/Details, `Index.cshtml`, `_Form.cshtml`, los specs `usuario-web-crear-editar` y `usuario-web-selector-persona-buscador`, y los botones Quitar/Cambiar/Buscar o `_PersonaBuscadorModal` en Details.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `usuario-web-listado-detalle-baja` — `REQ-ULD-04` MODIFIED: la sección "Persona vinculada" del detalle debe mostrar la card enriquecida (mismo árbol DOM que la card preseleccionada de Edit según `REQ-UCE-08`) cuando `IPersonaApiClient.GetByIdAsync` devuelve `PersonaDto`, y fallback plano "Apellidos, Nombres" ante 404 o fallo de transporte. Sigue read-only.

## Approach

1. **PageModel** — espejo de Edit: inyectar `IPersonaApiClient`, derivar `PersonaDisplay` del DTO, invocar `TryLoadPersonaVinculadaAsync` tras cargar `Usuario`. 404 y `TransportFailureClassifier.IsTransportFailure(ex)` non-blocking; `LogWarning(ex, "Failed to enrich linked persona {PersonaId} for detail page; falling back to PersonaDisplay.", personaId)` espejado.
2. **Razor** — reemplazar el `<dt>/<dd>` por `<div class="card border mb-3">` con título clickable; si enriquecido, `<dl class="row mb-0">` con Documento/Email/Teléfono/Estado vía `FormatDocumento(PersonaDto?)` (réplica de `_Form.cshtml` 197-227). Fallback: `<span data-usuario-persona-display-text>@Model.PersonaDisplay</span>`. Sin botones ni modal.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Pages/Seguridad/Usuarios/Details.cshtml` | Modified | Card read-only (Documento/Email/Teléfono/Estado o fallback plano) en lugar de Guid crudo |
| `Pages/Seguridad/Usuarios/Details.cshtml.cs` | Modified | DI `IPersonaApiClient`, props, helper espejo de Edit, non-blocking desde `OnGetAsync` |
| `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` | Modified | 4 tests: enriquecido, 404, transporte, ausencia de Quitar/Cambiar/Buscar |
| `openspec/specs/usuario-web-listado-detalle-baja/spec.md` | Modified | MODIFIED `REQ-ULD-04` con escenarios de enriquecimiento y fallback |

## Risks

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Latencia extra por fetch a Persona | Baja | Non-blocking; fallo NO marca `IsNotFound` |
| Inconsistencia visual Edit vs Details | Baja | Mismo árbol DOM que `_Form.cshtml` 27-83 |
| Pérdida del link al detalle de Persona | Baja | Test: `<a>` se conserva como título |
| Falsa inyección de botones del modal | Media | Test negativo `DoesNotContain` sobre data-attributes y modal |

## Rollback Plan

`git revert` del PR. Sin migraciones ni schema; el revert devuelve el Guid crudo con link. Specs vuelven a la versión previa vía `sdd-archive` revirtiendo el delta sobre el main spec.

## Success Criteria

- [ ] Details muestra la card enriquecida cuando el API devuelve DTO completo.
- [ ] Details cae al fallback "Apellidos, Nombres" cuando el API devuelve `null` (404).
- [ ] Details cae al fallback y `IsNotFound=false` ante `HttpRequestException`.
- [ ] Details NO renderiza `data-usuario-persona-quitar`, `data-usuario-persona-buscar`, ni `#usuario-persona-buscador-modal`.
- [ ] El `<a href="/personas/detalle/{PersonaId}">` se preserva como título.
- [ ] `dotnet test SGV.slnx` verde. PR único, ~150-200 LoC, dentro del budget 400.
