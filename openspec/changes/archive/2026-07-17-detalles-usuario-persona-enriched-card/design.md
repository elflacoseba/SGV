# Design: Card enriquecida de Persona en el detalle readonly del usuario

## Archivos afectados

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs` | Modificar | Inyectar `IPersonaApiClient` en primary constructor; agregar `PersonaDto? PersonaVinculada`, `string? PersonaDisplay`, helper espejo de Edit, llamada tras `GetByIdAsync` exitoso |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | Modificar | Reemplazar líneas 78-81 por card read-only (sin botones Quitar/Cambiar ni modal) + fallback plano cuando `PersonaVinculada is null` |
| `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` | Modificar | Agregar 4 tests: enriquecido, 404, transporte, ausencia de controles de selección |
| `openspec/specs/usuario-web-listado-detalle-baja/spec.md` | Delta ya escrito | `REQ-ULD-04` MODIFIED con escenarios de enriquecimiento y fallback |

## Decisiones de arquitectura

| Decisión | Alternativa descartada | Razón |
|---|---|---|
| **Espejo 1-a-1 de `Edit.TryLoadPersonaVinculadaAsync`** (misma firma, guarda `Guid.Empty`, `try/catch` sobre `TransportFailureClassifier.IsTransportFailure`, `LogWarning`) | Helper compartido en `UsuarioFormHelpers` o `IPersonaApiClient.GetByIdOrNullAsync` | PR #168 ya validó el patrón. Refactor compartido es scope-creep; choca con el non-goal "no tocar `_Form` ni parcial compartido". |
| **Rama enriched conserva `data-usuario-persona-card`**; rama fallback usa atributo neutral `data-usuario-details-persona` | Mantener `data-usuario-persona-card` en ambas ramas | Spec REQ-ULD-04 exige árbol DOM de Edit en enriquecido; atributo reservado al flujo enriquecido para no contaminar selectores JS de Edit/Index. |
| **Nunca `data-usuario-persona-quitar` / `data-usuario-persona-buscar` / `#usuario-persona-buscador-modal`** | Reusar `_PersonaBuscadorModal` para homogeneidad | Spec "Detalle sin controles de selección" lo prohíbe; la card es estrictamente read-only. |
| **404/transporte del API de Persona NO marca `IsNotFound`** | Reusar `IsRecoverable` de Edit | Distinto contrato: en Details, 404 de Persona ≠ 404 de Usuario. El usuario sí existe; sólo se degrada la presentación. |

## Flujo de datos

```
OnGetAsync(id):
  Usuario   = usuarioApiClient.GetByIdAsync(id)        # null → IsNotFound=true, return
  Display   = FormatPersonaDisplay(Usuario.Apellidos, Nombres)
  Vinculada = TryLoadPersonaVinculadaAsync(PersonaId)  # Guid.Empty → return
             # personaApiClient.GetByIdAsync:
             #   dto   → Vinculada = dto
             #   null  → Vinculada = null + LogWarning
             #   throw → Vinculada = null + LogWarning (NO IsNotFound)
Details.cshtml: Vinculada is not null → card enriquecida
               Vinculada is null     → bloque plano data-usuario-details-persona
```

## Cambios concretos

### `Details.cshtml.cs`

- Primary constructor: insertar `IPersonaApiClient personaApiClient` entre los dos existentes.
- Props nuevas: `PersonaDto? PersonaVinculada { get; private set; }` y `string? PersonaDisplay { get; private set; }`.
- Helper `TryLoadPersonaVinculadaAsync(Guid, CancellationToken)`: copia de `Edit.cshtml.cs` 205-229, mensaje ajustado a `"Failed to enrich linked persona {PersonaId} for detail page; falling back to PersonaDisplay."`.
- `OnGetAsync`: tras `GetByIdAsync` exitoso, setear `PersonaDisplay` y llamar `TryLoadPersonaVinculadaAsync`.
- `FormatPersonaDisplay(...)`: copia de `Edit.cshtml.cs` 395-400.
- NO tocar `IsNotFound`, flow de `OnGetAsync`, `BuildIndexUrl`/`BuildEditUrl`, ni handlers.

### `Details.cshtml`

Reemplazar líneas 78-81 por `@if (Model.PersonaVinculada is not null) { ... } else { ... }`.

- **Enriquecida**: copiar árbol de `_Form.cshtml` 32-86 sin los botones Quitar/Cambiar. Título = `<h6><a href="/personas/detalle/@Model.Usuario.PersonaId">@personaVinculada.Apellidos, @personaVinculada.Nombres</a></h6>`. Replicar `FormatDocumento` como `@functions` local (espejo de `_Form.cshtml` 197-227).
- **Fallback**: `<div class="card-body py-2" data-usuario-details-persona><a href="/personas/detalle/@Model.Usuario.PersonaId">@Model.PersonaDisplay</a></div>`. Sin `data-usuario-persona-card`, sin botones, sin modal.
- NO modificar banner "Cuenta bloqueada", forms Bloquear/Desbloquear/Eliminar, `@section scripts`, ni `usuarios-index.js`.

### `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs`

Reusar `WebIntegrationFixture.CreateUsuarioLeaseAsync(usuarioApiClient, personaApiClient, adminRole: true)` (`WebIntegrationFixture.cs` 121-128) y `FakePersonaApiClient.WithPersonaList(dto)`. Tests nuevos al final:

| # | Test | Asserts clave |
|---|---|---|
| 1 | `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedCard` | `LEG-7777`, `DNI 30123456`, email, teléfono, badge `Activa`, `data-usuario-persona-card`, `<a href="/personas/detalle/{pid}">` como título |
| 2 | `Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` | `FakePersonaApiClient` vacío; `Apellidos, Nombres` plano, `data-usuario-details-persona`, ausencia de `data-usuario-persona-card`. NO assert sobre `no está disponible` |
| 3 | `Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` | `QueryException = new HttpRequestException`; fallback como (2) + `DoesNotContain("no está disponible")` |
| 4 | `Get_Details_NoControlesSeleccionPersona` | `DoesNotContain` sobre `data-usuario-persona-quitar`, `data-usuario-persona-buscar`, `usuario-persona-buscador-modal`. Corre con y sin DTO |

`BuildUsuario` ya existe (línea 317); agregar overload `BuildUsuario(string id, Guid personaId)` para fijar `PersonaId` en tests de enriquecimiento.

## Branching y PR

Branch `feat/detalles-usuario-persona-enriched-card` desde `develop`. PR único. Conventional commit `feat(web): persona enriched card on usuario detail`. Body referencia PR #168 como antecedente del patrón de enriquecimiento.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Latencia del fetch a Persona en GET | Non-blocking con `LogWarning` (espejo de Edit). Tests usan `FakePersonaApiClient`. |
| Inconsistencia visual Edit ↔ Details | Mismo árbol DOM (espejo de `_Form.cshtml` 32-86). Test de enriquecido asserta clases y campos clave. |
| Pérdida del link al detalle de Persona | El `<a href="/personas/detalle/{PersonaId}">` se preserva como título en ambas ramas; test asserta presencia. |
| Falsa inyección de botones Quitar/Cambiar/Buscar | Test negativo `Get_Details_NoControlesSeleccionPersona` en ambas ramas. |

## Verificación previa al PR

1. `dotnet build SGV.slnx` verde.
2. `dotnet test SGV.slnx` verde. Los 4 tests nuevos cubren enriquecimiento, 404, transporte y ausencia de controles. No requiere `MySqlFact` (change UI puro).
3. Manual opcional: abrir `/seguridad/usuarios/detalle/{id}` con persona activa → card enriquecida; con persona inexistente o API caída → fallback plano.

## Threat Matrix

`N/A — el change no introduce routing nuevo, no ejecuta shell/subprocess, no automatiza VCS/PR, no clasifica archivos ejecutables ni integra procesos externos. El único límite HTTP es `IPersonaApiClient.GetByIdAsync`, ya gobernado por `TransportFailureClassifier.IsTransportFailure`.`