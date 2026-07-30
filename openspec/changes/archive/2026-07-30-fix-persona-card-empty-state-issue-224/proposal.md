# Proposal: fix-persona-card-empty-state-issue-224

## Intent

Cerrar el follow-up del bug #224: `TypeError` en `usuario-persona-buscador.js` cuando la partial `_PersonaCard.cshtml` se renderiza en estado vacío puro (caso 6: `Mode=editable` + `PersonaDto=null` + sin `FallbackDisplay`). El bug vive enteramente en el JS — la partial NO se modifica. La decisión técnica #8 del change #219 (`apply-progress.md` L125-128) explícitamente excluyó este fix para no mezclar el scope Razor/C# con uno de JavaScript; esta propuesta retoma ese pendiente.

## Background

El change `reusable-persona-card` (#219) introdujo la partial unificada `_PersonaCard.cshtml` con 6 ramas de render. El caso 6 (`editable + DTO null + sin FallbackDisplay`) emite solo el contenedor display vacío + el empty state con el botón "Buscar Persona"; **no** emite `data-usuario-persona-card`, `data-usuario-persona-display-input` ni `data-usuario-persona-display-text`.

El JS `usuario-persona-buscador.js` fue escrito antes de la partial y asumía que todos los elementos del contrato `data-*` existen siempre en el DOM. La función `choose()` (L54-71) y el handler Quitar (L215-228) escriben directamente sobre `displayInput.value`, `cardText.textContent`, `card.hidden` y `empty.hidden` sin validar null. Cuando el caso 6 se renderiza, esos nodos no existen → `TypeError`.

Adicionalmente se detectó un **bug latente** en el lookup de `empty` (L32): el JS lo busca con `display.querySelector('[data-usuario-persona-empty]')` pero la partial emite ese atributo **fuera** del `displayContainerId` (L242), en `display.parentElement`. Este bug latente afecta también los casos 4 y 5 (la transición Quitar→Buscar no funciona correctamente).

## Scope

### In Scope

- Fix de `choose()` (L54-71): agregar null-guards para `displayInput`, `cardText`, `card` y `empty`; log warning + return early si faltan (no TypeError).
- Fix del handler Quitar (L215-228): mismo tratamiento de null-guards.
- Refactor del lookup de `empty` (L32): cambiar de `display.querySelector` a `display.parentElement.querySelector` (consistente con la partial L242 y con el patrón ya usado para `displayInput`).
- Mejora defensiva del lookup de `displayInput` (L29): ya usa `parentElement`, pero se valida null antes de escribir.
- Test .NET nuevo en `PersonaCardPartialTests.cs` que documente el contrato del caso 6: los 4 atributos mutables (`data-usuario-persona-card`, `data-usuario-persona-display-input`, `data-usuario-persona-display-text`, `data-usuario-persona-quitar`) **no** se emiten en caso 6.
- Actualización de `docs/decisiones-implementacion.md` § "Frontend / JS compartido" con nota del fix, patrón defensivo y referencia a la decisión de no agregar Vitest.
- Smoke test manual documentado en `verify-report.md` con pasos reproducibles.

### Out of Scope / Non-Goals

- **NO** agregar Vitest, Jest, Mocha o Playwright al `package.json` (decisión de la exploración §6; la verificación manual es suficiente para este nivel de riesgo).
- **NO** modificar `_PersonaCard.cshtml` (el bug vive en el JS, no en el markup).
- **NO** modificar otros archivos JS del repo (auditoría en exploración §5 confirma que el patrón defectuoso es único a este archivo).
- **NO** agregar tests E2E con browser headless.
- **NO** cambiar el comportamiento de los casos 1-5 (solo se arregla el bug latente del lookup de `empty` y se blinda el caso 6).
- **NO** introducir un nuevo contrato `data-*` en la partial.
- **NO** cambiar el bundle pipeline (gulp/bun).

## Capabilities

### New Capabilities

- `usuario-persona-buscador-js` (NEW): spec delta que documenta el contrato entre el JS y el DOM emitido por `_PersonaCard.cshtml`. 3 requisitos:
  - REQ-USBJS-01: lookup de `empty` desde `display.parentElement`.
  - REQ-USBJS-02: `choose()` aborta limpiamente si faltan elementos del contrato (log warning, no excepción).
  - REQ-USBJS-03: handler Quitar aborta limpiamente en las mismas condiciones.

### Modified Capabilities

Ninguno. Este change no modifica requisitos de specs canónicas existentes.

## Approach

Fix localizado en un único archivo JS (`usuario-persona-buscador.js`): refactor del lookup de `empty` y null-guards defensivas en `choose()` y handler Quitar. Tests .NET de markup que refuerzan el contrato del caso 6. Smoke test manual documentado. No se introduce infraestructura de testing JS.

**Testing approach (Opción A — pragmática)**:

1. Tests .NET en `PersonaCardPartialTests.cs` que verifican que el caso 6 **no emite** los 4 atributos mutables. Estos son tests RED→GREEN legítimos: el test verifica que la partial no emite esos atributos, lo cual es exactamente lo que causa el crash del JS. El test protegerá contra regresiones si alguien accidentalmente agrega esos atributos al caso 6.
2. Smoke test manual documentado: cargar `/organizacion/ocupaciones/crear`, abrir modal, seleccionar persona, verificar consola sin TypeError.
3. Suite .NET completa (`dotnet test SGV.slnx`) sigue pasando.

Esta decisión respeta: (a) el principio del repo "Cada test debe aportar valor real", (b) la decisión previa de #219 que excluyó este bug, (c) `strict_tdd: true` se cumple a nivel del contrato markup del caso 6.

## Affected Areas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | Modificado | Null-guards en `choose()` y handler Quitar; fix del lookup de `empty` |
| `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs` | Modificado | 1 test nuevo para contrato caso 6 |
| `docs/decisiones-implementacion.md` | Modificado | Nota del fix y patrón defensivo en § Frontend/JS |
| `openspec/changes/fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md` | Nuevo | Spec delta con 3 requisitos |

## Risks

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Regresión en Usuarios Create/Edit | Media | Tests .NET existentes (`PersonaCardPartialTests`, `OcupacionBuscadorModalTests`) verifican markup. Smoke test manual cubre comportamiento JS. |
| Bug latente del lookup de `empty` afecta otros cases | Baja | El refactor usa `parentElement` (consistente con comentario L236-237 de la partial). El comportamiento esperado no cambia: `empty` visible sin persona, hidden con persona. |
| Fix no se ejecuta por bundling issue | Baja | `bun run build` valida que el bundle se genera sin errores. |
| Strict TDD sin test rojo→verde sobre el JS | Baja | Decisión explícita (Opción A). El test .NET del contrato markup es RED→GREEN verificable. |
| Otros call sites del script con mismo bug | Baja | Auditoría completa (exploración §5) confirma que el patrón es único a este archivo. |

## Rollback Plan

Revertir los cambios en `usuario-persona-buscador.js` a su estado en `develop` (`05dc634b1`) elimina las null-guards y restaura el lookup de `empty` original. El rollback es atómico sobre un único archivo. Los tests .NET no necesitan revertirse porque el nuevo test es aditivo (verifica ausencia de atributos en caso 6, comportamiento que ya existe).

## Dependencies

Ninguna dependencia externa nueva. El fix es autocontenido en el archivo JS. No se requieren migraciones de DB ni cambios en API/Contracts.

## Success Criteria

- [ ] `choose()` aborta limpiamente con `console.warn` cuando `displayInput`, `cardText`, `card` o `empty` son null (sin TypeError).
- [ ] Handler Quitar aborta limpiamente en las mismas condiciones.
- [ ] Lookup de `empty` lee desde `display.parentElement` (consistente con la partial L242).
- [ ] `Ocupaciones/Create` con empty state permite seleccionar persona del modal sin errores en consola.
- [ ] `Ocupaciones/Edit` con `PersonaId = Guid.Empty` y fetch fallido permite seleccionar persona sin errores.
- [ ] `Usuarios/_Form` no sufre regresión (comportamiento existente intacto).
- [ ] Suite .NET completa pasa: `dotnet test SGV.slnx`.
- [ ] `bun run build` genera el bundle sin errores.

---

## Proposal question round

Las siguientes preguntas refinan la propuesta antes de pasar a specs. Respondé, corregí o skippeá.

1. **Testing JS**: La issue #224 sugiere Playwright como opción de testing E2E. ¿Querés que agreguemos Playwright al `package.json` o preferís mantener la Opción A (smoke test manual + test .NET del contrato markup)?

2. **Scope del test .NET**: El test nuevo en `PersonaCardPartialTests.cs` verifica que el caso 6 no emite los 4 atributos mutables. ¿Alcanza o preferís que el test también verifique que SÍ se emiten los atributos del empty state (`data-usuario-persona-display`, `data-usuario-persona-empty`)?

3. **Criterio de aceptación extra**: ¿Querés agregar un criterio explícito sobre la verificación en `Usuarios/Create` además de `Ocupaciones/Create` y `Ocupaciones/Edit`?

*(Si preferís no responder, la propuesta avanza con las respuestas por defecto: Opción A sin Playwright, test de atributos mutables negativos, cobertura de los 4 call sites.)*
