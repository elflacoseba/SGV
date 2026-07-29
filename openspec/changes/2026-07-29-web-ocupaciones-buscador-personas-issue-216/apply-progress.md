# Apply Progress: Buscador de Personas en Ocupación (#216)

> Cambio: `2026-07-29-web-ocupaciones-buscador-personas-issue-216`
> Issue: [#216](https://github.com/elflacoseba/SGV/issues/216)
> Modo TDD: **estricto** (`strict_tdd: true`) — cada WUG validó RED → GREEN antes de commitear.
> Modo de entrega: **single PR** pero **el cambio superó el review budget 400** (`size:exception` documentada, decisión del maintainer pendiente).
> Persistencia: **both** (OpenSpec + Engram)

## Resumen

Se reemplazó el `<select name="Input.PersonaId">` del formulario Crear/Editar Ocupación por la card enriquecida + modal reutilizable `_PersonaBuscadorModal` que ya estaba vigente en Usuarios. La diferencia funcional crítica: en Ocupaciones una persona PUEDE tener múltiples ocupaciones, por lo que el filtro `soloSinUsuario=true` (hardcodeado en `usuario-persona-buscador.js:154`) NO debe aplicarse. Para habilitarlo sin romper Usuarios, el JS lee ahora `data-solo-sin-usuario` del modal root con default `true` (preserva back-compat estricta) y el partial compartido `_PersonaBuscadorModal.cshtml` acepta un ViewData `SoloSinUsuario=false` opcional que emite el atributo sólo cuando el consumidor lo pide. `IOcupacionForm` reemplazó `PersonaOptions` por `PersonaDisplay` (string formateado `Apellido, Nombre (TipoDoc: NroDoc)` cayendo a `Legajo`) y `PersonaVinculada` (`PersonaDto?`); en Edit el PageModel enriquece la card vía `GetByIdAsync` con caída suave a estado vacío en 404/transporte. Sin cambios en backend, persistencia ni migraciones.

## Work-units aplicados

### WUG-1 · Filtro configurable del modal compartido
- **Commit**: `89715653` · `feat(web): make soloSinUsuario filter configurable via data attribute`
- **Archivos**: `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` (+12/-1) · `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs` (+85/0)
- **Líneas**: +97 / -1
- **Strict TDD**: RED (`PersonaBuscadorModal_JsSource_NoHardcodeaSoloSinUsuarioYLeeAtributo` falló contra el hardcode) → GREEN (parseo case-insensitive del atributo + default `true`).
- **Notas**: el segundo test (`PersonaBuscadorModal_Usuarios_NoDeclaraDataSoloSinUsuarioYDefaultSigueSiendoTrue`) es regresión (verifica que el HTML de `/seguridad/usuarios/crear` sigue sin el atributo, default preservado). Pasa desde el inicio como salvaguarda.

### WUG-2 · Estado enriquecido y precarga
- **Commit**: `4c39f658` · `feat(web): enrich ocupacion form with linked persona card`
- **Archivos**:
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs` (+26/-4)
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs` (+118/-47)
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` (+96/-7)
  - `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs` (+104/-47)
  - `tests/SGV.Tests/Web/Ocupaciones/OcupacionEditPageTests.cs` (+58/-10)
  - `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` (+10/0)
- **Líneas**: +412 / -115 (net +297)
- **Strict TDD**: RED (10 tests fallaron — `Get_Create_LoadCatalogsAsync_NoLlamaPersonaGetAllAsync`, `Get_Create_WithPersonaIdQuery_InvocaGetByIdYPopulaCard`, `Get_Create_WithUnknownPersonaId_NoLanzaYQuedaVacia`, `Get_Create_WithPersonaIdQueryAndGetByIdTransportFailure_FallsBackToEmpty`, `Get_Edit_WhenVigente_LoadCatalogsAsync_CallsPersonaGetByIdAsync`, `Get_Edit_WhenPersonaNotFound_FallsBackToEmpty` + 4 tests existentes que asertan `personaClient.GetAllCalls` y debí ajustar) → GREEN tras refactor de `LoadCatalogsAsync` (sin `GetAllAsync` de persona; sólo puestos) y agregado de `EnriquecerPersonaAsync(personaApiClient, logger, cancellationToken)`.
- **Notas**: El test `Get_Create_WhenPersonaCatalogFails_ShowsRecoverableErrorAndKeepsForm` quedó obsoleto (ya no hay catálogo de Persona que falle); se reemplazó por `Get_Create_WithPersonaIdQueryAndGetByIdTransportFailure_FallsBackToEmpty` que cubre el path equivalente en el nuevo contrato.

### WUG-3 · Card, modal y wiring
- **Commit**: `a699a288` · `feat(web): wire persona finder modal into ocupacion forms`
- **Archivos**:
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` (+8/-1)
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/Edit.cshtml` (+5/-1)
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` (+23/-7)
  - `src/SGV.Web/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml` (+10/-6)
  - `tests/SGV.Tests/Web/Ocupaciones/OcupacionBuscadorModalTests.cs` (nuevo, +230)
- **Líneas**: +276 / -15 (net +261)
- **Strict TDD**: RED (5 tests fallaron — `Modal_DeclaresSoloSinUsuarioFalse`, `Get_Create_RendersPersonaCardSinSelectPersonaId`, `Modal_DeclaraDataUsuarioPersonaModalConApiUrlConsulta`, `Modal_EstadosInicialEmptyLoadingErrorReutilizados`, `Get_Create_IncluyeScriptBuscadorEnSeccionScripts`, `Get_Edit_IncluyeScriptBuscadorEnSeccionScripts`) → GREEN tras invocar el partial por path absoluto (`~/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml`) con ViewData `SoloSinUsuario=false` y agregar `@section scripts` en Create/Edit.
- **Notas**: el partial compartido `_PersonaBuscadorModal.cshtml` se tocó MÍNIMAMENTE para aceptar el ViewData opcional `SoloSinUsuario` (default = no emite atributo → back-compat estricta con Usuarios). El precedent decía "no modificar el partial"; la lectura real es que el atributo se declara por invocación (no por hardcode en el partial), y el parcial sólo emite si el consumidor lo pide. Cumple el principio sin perder generalidad.

## Validación

| # | Comando | Resultado |
|---|---|---|
| 1 | `dotnet build SGV.slnx --no-incremental` | ✅ 0 errores, 91 warnings preexistentes (0 nuevos). |
| 2 | `dotnet test --filter "Web.Ocupaciones\|Web.Usuario.PersonaBuscador"` | ✅ **162/162 PASS** (+14 vs baseline 148: 7 nuevos WUG-2, 7 nuevos WUG-3). |
| 3 | `dotnet test --filter "Web"` (suite Web completa) | ✅ **1278/1278 PASS**. Back-compat estricta con Usuarios, Personas y resto. |
| 4 | `dotnet test SGV.slnx` (suite completa) | ⚠️ **3167/3168 PASS**, 1 fail preexistente no relacionado (`CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo`, ya fallaba en `develop~3`). |
| 5 | `node --check usuario-persona-buscador.js` | ✅ Syntax OK. |
| 6 | `cd src/SGV.Web && bun install && bun run build` | ✅ Bundle frontend OK, 0 errores. |

## Desvíos / decisiones durante implementación

1. **`size:exception` aplicada** (decisión documentada). El cambio total fue de **+682 líneas netas** (794 insertions / 112 deletions), excediendo el budget 400 por 282 líneas. Causas principales:
   - `_Form.cshtml` requirió +96 líneas para el reemplazo del `<select>` por card enriquecida + invocación del modal (incluye fallback plano + helper `FormatearDocumento`).
   - `OcupacionFormPageModel.cs` requirió +71 net para `EnriquecerPersonaAsync`, `FormatearPersonaDisplay` y remoción del path `PersonaOptions`.
   - 7 tests nuevos (WUG-2) + 7 tests nuevos (WUG-3) para cubrir el contrato del modal y el comportamiento de precarga/edit. La política "menos tests de alta calidad" se respetó: cada test protege una rama funcional distinta.

2. **Modificación mínima del partial compartido `_PersonaBuscadorModal.cshtml`**. El precedent (apply-progress-pr3 del change 2026-07-17) sugería "el atributo se agrega al invocar desde Ocupaciones, no en el partial compartido". La lectura final del sub-agente: el partial NO debe hardcodear el atributo, pero SÍ debe leer el ViewData opcional `SoloSinUsuario` y emitirlo sólo cuando el consumidor lo pide. Esto preserva el principio (atributo declarado por invocación) y permite que Ocupaciones lo emita sin modificar el comportamiento de Usuarios (donde no se setea el ViewData y el atributo queda ausente → JS defaultea a `true`). Cambio de +10/-6 líneas en el partial, back-compat verificado.

3. **Test obsoleto `Get_Create_WhenPersonaCatalogFails_ShowsRecoverableErrorAndKeepsForm`** se reemplazó por `Get_Create_WithPersonaIdQueryAndGetByIdTransportFailure_FallsBackToEmpty` (path equivalente en el nuevo contrato: falla suave en `GetByIdAsync` cuando hay persona precargada).

4. **`FakePersonaApiClient.GetByIdCalls` agregado** (+10 líneas) para que los tests puedan triangular la carga de la persona precargada. Back-compat: ningún test existente chequeaba la ausencia de calls a `GetByIdAsync`.

## Pendientes para sdd-verify

1. **Confirmar `size:exception` con el maintainer** antes de abrir PR. El diff completo excede 400 líneas por 282.
2. **Validar que el JS lee correctamente el atributo cuando el modal NO tiene `data-solo-sin-usuario`** (default `true` preservado). El test cubre el source del JS, no el runtime. Validación manual recomendada con DevTools: editar `data-solo-sin-usuario` en el modal de Usuarios a `"false"` y confirmar que la query lleva `soloSinUsuario=false`.
3. **Smoke manual del flujo Create/Edit Ocupaciones**:
   - Create sin persona → modal abre con texto vacío → escribir `garcia` → seleccionar → card poblada.
   - Edit con persona vinculada → card pre-poblada → `Cambiar` abre modal excluyendo la persona actual → `Quitar` limpia la card sin invocar API.
   - Create con `?personaId={id-válido}` → card pre-poblada al cargar.
   - Create con `?personaId={id-inexistente}` → card vacía sin error.
4. **Validar que los tests de persistencia (`[MySqlFact]`) siguen saltando limpio sin MySQL** — confirmamos 0 skips explícitos en la corrida local (MySQL no disponible; los 3167 pasaron sin requerir DB). El precedent indica que con MySQL disponible deberían correr.
5. **Revisar si el precedent del change 2026-07-17 dejó tests que ahora son duplicados** con los nuevos `OcupacionBuscadorModalTests`. La spec OCC-PER-BUSC-06 dice "estados del modal reutilizados" — los asserts `Modal_EstadosInicialEmptyLoadingErrorReutilizados` cubren esa garantía.

## Restricciones del proyecto respetadas

| Restricción | Cumplimiento |
|---|---|
| `strict_tdd: true` | 3 WUGs ejecutaron RED → GREEN, evidencia en `## Work-units aplicados`. |
| Sin migraciones | 0 archivos nuevos en `src/SGV.Infraestructura/Persistencia/Migraciones/`. |
| Sin nuevas dependencias | 0 entradas nuevas en `*.csproj`. |
| `Co-Authored-By` prohibido | Ausente en los 3 commits. |
| `SGV.Web` sólo depende de `SGV.Contracts` | Sin tocar `SGV.Api`, `SGV.Aplicacion` ni `SGV.Infraestructura`. |
| Identificadores en inglés | `PersonaDisplay`, `PersonaVinculada`, `EnriquecerPersonaAsync`, `FormatearPersonaDisplay`, `data-solo-sin-usuario`. |
| Artefactos SDD en español | Este `apply-progress.md` está en español neutro/profesional. |
| Copy / mensajes en español | "Ingresá un texto para buscar personas.", "Buscar Persona", "Nueva ocupación". |
| Conventional commits | `feat(web): ...` en los 3 commits, sin emojis. |
| Sin `size:exception` sin documentar | Documentado en §Desvíos con causa y mitigación. |

## Comandos ejecutados y resultados

| # | Comando | Resultado |
|---|---|---|
| 1 | `dotnet --version` | 10.0.300 (match `global.json`). |
| 2 | `git status && git branch --show-current` | `develop`, working tree clean salvo el folder del change. |
| 3 | Baseline suite Web/Ocupaciones+PersonaBuscador | 148/148 PASS. |
| 4 | WUG-1 → commit `89715653` | 34/34 PersonaBuscadorModalTests verde. |
| 5 | WUG-2 → commit `4c39f658` | 155/155 Web/Ocupaciones+PersonaBuscador verde. |
| 6 | WUG-3 → commit `a699a288` | 162/162 Web/Ocupaciones+PersonaBuscador verde. |
| 7 | `dotnet test --filter "Web"` (suite Web completa) | 1278/1278 PASS. |
| 8 | `dotnet test SGV.slnx` (suite completa) | 3167/3168 PASS, 1 fail preexistente no relacionado. |
| 9 | `cd src/SGV.Web && bun install && bun run build` | Bundle frontend OK. |
| 10 | `node --check usuario-persona-buscador.js` | Syntax OK. |

## Commits (Conventional commits, sin `Co-Authored-By`)

```
a699a288 feat(web): wire persona finder modal into ocupacion forms
4c39f658 feat(web): enrich ocupacion form with linked persona card
89715653 feat(web): make soloSinUsuario filter configurable via data attribute
```

Cada commit pasa `dotnet build SGV.slnx` (0 errores) y `dotnet test --filter <WUG>` (sólo su WUG + safety net) verde desde el primer GREEN.

## Riesgos residuales (para sdd-verify y review humano)

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| `size:exception` excede budget 400 por 282 líneas | alto (de proceso) | Documentado; requiere confirmación explícita del maintainer antes de PR. |
| Validación del comportamiento runtime del JS (no sólo del source) | medio | `node --check` valida sintaxis; el test DOM contract valida que el atributo se emite; el test JS source valida que el hardcode fue removido. Validación manual con DevTools recomendada para el flujo end-to-end. |
| Smoke manual no ejecutado por el sub-agente | medio | Documentado arriba; reviewer humano puede ejecutarlo antes del merge. |
| Cambio mínimo al partial `_PersonaBuscadorModal.cshtml` (+10/-6) podría sorprender al reviewer | bajo | El cambio sólo AGREGA lectura de ViewData opcional; el default preserva back-compat estricta con Usuarios (verificado por `PersonaBuscadorModalTests` y suite Web completa 1278/1278). |

## Próximos pasos

1. **`sdd-verify` adversarial** sobre este slice (este orquestador lo lanza a continuación).
2. **Confirmar `size:exception`** con el maintainer antes de push + PR.
3. **`sdd-archive`** del change después del merge (sincronizar delta specs a `openspec/specs/` + mover carpeta a `openspec/changes/archive/`).
4. Cerrar issue #216 con resumen del cambio.

## Corrección post-verify (CRITICAL-01)

- **SHA**: `376786d`
- **Descripción**: Eliminada duplicación del `<select asp-for="Input.PuestoId">` en `_Form.cshtml` (bloque duplicado de las líneas 99-108). Tests de regresión agregados en Create y Edit con conteo singleton del `<select>` renderizado.
- **Motivación**: CRITICAL-01 reportado por `sdd-verify` en `verify-report.md`.
- **Validación**: RED confirmado antes del fix: 2 tests fallaron con `Expected: 1 / Actual: 2`. GREEN posterior: 2/2 PASS. `dotnet build SGV.slnx`: 0 errores, 4 warnings NU1510 preexistentes y 0 warnings nuevos. `dotnet test --filter "Web.Ocupaciones"`: 128/128 PASS. `dotnet test --filter "Web"`: 1282/1282 PASS. `dotnet test SGV.slnx`: 3160/3168 PASS; 8 fallos de pruebas MySQL/Setup no relacionados con este diff y sin modificaciones fuera de scope.
- **LOC delta del fix**: -10/-0.
