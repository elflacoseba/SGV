# Verify Report: fix-persona-card-empty-state-issue-224

> Change: `fix-persona-card-empty-state-issue-224`
> Issue: [#224](https://github.com/elflacoseba/SGV/issues/224)
> Branch: `feat/fix-persona-card-empty-state-issue-224`
> Base: `develop` (`05dc634b`)
> Verifier: sdd-apply (sub-agent)
> Date: 2026-07-30

## Resumen del cambio

Fix del `TypeError: Cannot set properties of null (setting 'value')` en
`src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` cuando la partial
`_PersonaCard.cshtml` se renderiza en el caso 6 (`Mode=editable` + `PersonaDto=null`
+ sin `FallbackDisplay`).

Cambios:

- **USBJS-01**: lookup del empty state corregido de `display.querySelector` a
  `display.parentElement.querySelector` (bug latente que afectaba casos 4/5).
- **USBJS-02**: `choose()` aborta limpiamente con `console.warn` cuando faltan
  los elementos del contrato (`displayInput`, `cardText`, `card`, `empty`).
- **USBJS-03**: handler Quitar aplica el mismo patrón defensivo.
- **Regression guard**: test .NET nuevo
  `EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes`
  que blinda el contrato negativo del caso 6.

## Tabla de validaciones

| Validación | Comando | Resultado | Esperado |
|------------|---------|-----------|----------|
| Build .NET | `dotnet build SGV.slnx` | 0 errors, 92 warnings (todos pre-existentes) | ✅ 0 errors, 0 warnings nuevos |
| Suite .NET (filtro: tests PersonaCard/OcupacionBuscador/UsuarioHabilidades) | `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaCardPartialTests\|FullyQualifiedName~OcupacionBuscador\|FullyQualifiedName~UsuarioHabilidadesPage"` | Passed: 26, Failed: 0 | ✅ |
| Suite .NET (test nuevo Task 1) | `dotnet test SGV.slnx --filter "FullyQualifiedName~EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes"` | Passed: 1, Failed: 0 | ✅ |
| Suite .NET (completa) | `dotnet test SGV.slnx` | Passed: 3226 / 3228 (2 pre-existing fails documentados abajo) | ⚠️ 2 pre-existing |
| Bundle JS | `cd src/SGV.Web && bun run build` | `Finished 'build' after 2.91 s`, 0 errors | ✅ |
| Diff stats vs develop | `git diff --stat develop` | 3 files, 64 insertions(+), 3 deletions(-) | ✅ ≤ 400 líneas |

### Resultado del suite completa (3228 tests)

- **3226 PASSED**
- **2 FAILED (pre-existentes, no relacionados con este change)**:

1. `SGV.Tests.Persistencia.CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo`
   — Test `[MySqlFact]` que requiere seed data en `sgv_test.Cargos`. La tabla está vacía en este entorno (no hay seeds de catálogo). Falla idénticamente en `develop` (verificado en run con `git checkout 05dc634b`). **No relacionado con #224**.

2. `SGV.Tests.Api.UsuariosEndToEndMySqlFactTests.Bloquear_AnotherUser_Returns200WithBloqueadoTrue`
   — Test `[MySqlFact]` intermitente (pasa individualmente, falla en suite completa). Estado compartido de MySQL entre tests. Verificado que en `develop` también puede fallar intermitentemente. **No relacionado con #224**.

> **Verdict de la suite**: los 2 tests fallidos son **pre-existing** y se
> reproducen en `develop` sin los cambios de este change. El change no introduce
> ninguna regresión nueva.

### Smoke tests manuales

Los 4 smoke tests definidos en el `design.md` §5 no se pudieron ejecutar
manualmente en este entorno automatizado (no hay navegador con DevTools
disponible desde el shell del sub-agente). Se documentan a continuación los
pasos esperados y la lógica que valida el fix.

| Smoke | Pantalla / flujo | Pasos | Resultado esperado | Cobertura |
|-------|-------------------|-------|--------------------|-----------|
| Smoke 1 | `Ocupaciones/Create` con empty state | 1. Ir a `/organizacion/ocupaciones/crear`. 2. Ver empty state visible (caso 6). 3. Click "Buscar Persona" → modal abre. 4. Buscar y seleccionar persona. 5. DevTools Console. | 0 TypeError. `console.warn` con `modalId` y `displayContainerId`. Modal NO se oculta (USBJS-02 `caso_6_choose_warns_and_aborts_without_typeerror`). `hiddenInput.value` queda con `persona.id`. | USBJS-02 L43-53 |
| Smoke 2 | `Ocupaciones/Edit` con `PersonaId=Guid.Empty` + fetch fallido | 1. Cargar `/organizacion/ocupaciones/editar/{id}` con persona borrada. 2. Fetch JS falla → fallback empty state. 3. Seleccionar persona del modal. | 0 TypeError. Mismo comportamiento que Smoke 1. | USBJS-02 L43-53 (rama `isEditableFallback`) |
| Smoke 3 | `Usuarios/_Form` con persona precargada (caso 4) | 1. Ir a `/seguridad/usuarios/crear`. 2. Persona precargada en `PersonaId`. 3. Click "Buscar Persona" → seleccionar OTRA persona. 4. DevTools Console. | 0 TypeError, 0 warnings inesperados. Modal cierra. `cardText.textContent` actualizado. `displayInput.value` actualizado. | USBJS-02 L62-73 (`caso_4_choose_runs_normally_no_warnings`) |
| Smoke 4 | `Usuarios/_Form` con persona precargada → Quitar | 1. Mismo setup que Smoke 3. 2. Pulsar botón "Quitar". 3. DevTools Console. | 0 TypeError, 0 warnings inesperados. Card oculto. Empty visible. `hiddenInput.value = ''`. | USBJS-03 L83-88 + L96-101 (caso normal) |

**Limitación del entorno**: este sub-agente ejecuta en shell sin acceso a
navegador. Los smoke tests fueron validados **por inspección de código** y por
ejecución de la suite .NET del contrato markup:

- El test nuevo `EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes`
  PASS valida el **contrato markup** que causa el TypeError en el caso 6.
- Los 19 tests de `PersonaCardPartialTests` PASS (incluyendo los que cubren casos 1-5)
  validan que los **flujos existentes no se rompen**.

> Recomendación: el maintainer debe ejecutar los 4 smoke tests manualmente
> en navegador antes de mergear. Los pasos están documentados en este
> `verify-report.md` y en `tasks.md` Task 6.

## Hashes de los commits

```
15265de1f2207ab41b5891ac213259c1a7dec2f0  docs(frontend): note defensivo del bug #224 en decisiones-implementacion.md
ed7f293089b6b65e2d3f87793764675438dc5de0  fix(js): abort Quitar handler when card contract missing (#224, USBJS-03)
914f578932dcc42083162486136a98ac10efa137  fix(js): abort choose() when card contract missing (#224, USBJS-02)
d2e08e791f62f94a297526203847a11c70268295  fix(js): read empty state from display.parentElement (#224, USBJS-01)
a56335227bf7894283913f7b0d0c09ae41e929cd  test(web): add regression guard for case 6 card contract (#224)
```

## Mapeo de escenarios de la spec a evidencia

| Spec USBJS | Escenario | Evidencia |
|-----------|-----------|-----------|
| USBJS-01 | `caso_6_empty_visible_lookup_returned` | Test .NET `EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona` (L457-475) + commit `d2e08e7` (lookup a `parentElement`) |
| USBJS-01 | `caso_4_empty_hidden_lookup_returned` | Test .NET existente L384-411 cubre caso 4; commit `d2e08e7` mantiene compatibilidad |
| USBJS-01 | `caso_5_empty_hidden_lookup_returned` | Test .NET existente L413-449 cubre caso 5; commit `d2e08e7` mantiene compatibilidad |
| USBJS-02 | `caso_6_choose_warns_and_aborts_without_typeerror` | Commit `914f578` (null-guards en `choose()`) + Smoke 1 esperado |
| USBJS-02 | `caso_6_choose_still_updates_hidden_input_and_current_persona_id` | Commit `914f578` L59-60 (updates antes del guard) |
| USBJS-02 | `caso_4_choose_runs_normally_no_warnings` | Commit `914f578` + Smoke 3 esperado |
| USBJS-02 | `caso_5_choose_runs_normally_no_warnings` | Commit `914f578` + tests .NET caso 5 |
| USBJS-03 | `caso_6_quitar_button_not_bound` | Test .NET `EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona` L472 (`Assert.DoesNotContain("data-usuario-persona-quitar")`) |
| USBJS-03 | `caso_4_quitar_handler_runs_normally` | Commit `ed7f293` (null-guards en handler Quitar) + Smoke 4 esperado |
| USBJS-03 | `defensive_quitar_warns_when_contract_elements_missing` | Commit `ed7f293` L222-228 (guard con `console.warn`) |

## Riesgos residuales

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Smoke tests manuales no se ejecutaron en este entorno (no hay navegador) | Baja | Pasos documentados en este reporte + `tasks.md` Task 6. El maintainer debe ejecutarlos antes de mergear. |
| Test `[MySqlFact]` `ListAllAsync_RetornaCargosOrdenadosPorCodigo` falla por falta de seeds (pre-existing) | Baja | Falla idéntica en `develop` sin mis cambios. El catálogo `Cargos` está vacío en el entorno local; no es regresión del change. |
| Test `[MySqlFact]` `Bloquear_AnotherUser_Returns200WithBloqueadoTrue` flaky en suite completa (pre-existing) | Baja | Pasa individualmente; falla por orden de tests con estado MySQL compartido. No relacionado con #224. |
| Cambio `hiddenInput.value` antes del guard en `choose()` rompe handler externo | Baja | El `change` event NO se dispara en aborto (USBJS-02). Documentado en comentario del código (L56-58). |

## Verdict

**PASS WITH WARNINGS**

- ✅ Build, bundle, suite relevante (tests PersonaCard), test nuevo Task 1, contrato markup.
- ✅ Diff dentro de budget (64 ins / 3 del = 67 net lines, ≤ 400).
- ⚠️ 2 tests `[MySqlFact]` pre-existing fallan (verificado en `develop`); no son regresión.
- ⚠️ 4 smoke tests manuales no ejecutados en este entorno; documentados para el maintainer.

El change cumple los criterios de aceptación del `proposal.md` y los escenarios
de la spec `usuario-persona-buscador-js`. Procede a PR + archive.

## Próximos pasos

1. `git push origin feat/fix-persona-card-empty-state-issue-224`
2. `gh pr create --base develop ...` (PR único; squash final opcional)
3. Mantainer ejecuta los 4 smoke tests manuales antes de mergear.
4. Mergear PR.
5. Mover change a `archive/2026-07-30-fix-persona-card-empty-state-issue-224/`.
6. Cerrar issue #224 con referencia al PR.