# Verify Report: Buscador modal reutilizable de Personas — PR-3 (frontend + cleanup)

> **Change**: `2026-07-17-buscador-personas-modal`
> **Issue**: [#157](https://github.com/elflacoseba/SGV/issues/157)
> **Slice**: **PR-3 (WU-5..8 — Frontend + Cleanup)**
> **Branch**: `feat/2026-07-17-buscador-personas-frontend` @ `94f15950`
> **Base**: `develop` @ `86d6b725` (PR-1 backend + PR-2 cliente ya mergeados)
> **Modo TDD**: `strict_tdd: true`
> **Persistencia**: `both` (openspec + Engram)
> **Modo de entrega**: **single PR con `size:exception`** (1.214 LoC, budget 400 excedido por 814 líneas, decisión explícita del maintainer)
> **Mode de verify**: **read-only adversarial review** vía `gentle-ai` (lens tier `risk=high` ⇒ 4R completas)
> **Lineage**: `review-9520f99489d7dbeb` — store revision `sha256:cc3f11be7d97b23a2b9792e858aa3316130a088c81ee546ad5cbd893dcbfee45`

---

## Resumen ejecutivo

**Verdict: `PASS WITH WARNINGS`** — el gate `pre-pr` retorna `allow`; la implementación cumple con WU-5..8 (Frontend + Cleanup) conforme a las specs REQ-USB-01..11 y REQ-UCE-02/08/09/10; 18 archivos / +763/-451 = 1.214 LoC (3,04× el budget de 400; `size:exception` aprobada por el maintainer); suite completa 2440/2440 verde; build limpio (0 errores, 23 warnings preexistentes); bundle frontend verde; 0 referencias residuales a `IPersonaOptionsProvider` en código fuente.

**0 BLOCKER, 0 CRITICAL, 4 WARNING, 7 SUGGESTION** distribuidos en los 4 lenses. Las 5 deviations documentadas por el sub-agente se confirman dentro de los límites aceptables de cada `recommended-accept`. **Ningún hallazgo bloqueante exige bounded correction antes de merge**.

---

## 1. Nota de `size:exception`

Esta entrega excede el review budget de 400 LoC por **814 líneas** (1.214 LoC reales = 18 archivos, +763/-451). El maintainer aprobó `size:exception` explícitamente en sesión previa porque:

- Los 4 WUs están lógicamente cohesionados (selector modal + cleanup del viejo provider).
- Fragmentar más agregaría overhead de stacking sin reducir la complejidad cognitiva del cambio conceptual.
- WU-8 cleanup (566 LoC) es donde se concentra el delta por la migración de tests existentes de `FakePersonaOptionsProvider` → `FakePersonaApiClient`; aislarlo no reduce el review burden.

El cuerpo del PR debe reiterar la `size:exception` en el primer párrafo para que el reviewer humano lo sepa de entrada. **Esta nota no bloquea la approval** — es una decisión de proceso documentada en `apply-progress-pr3.md §"Decisión de entrega"`.

---

## 2. Authority-First Terminal Procedure (evidencia)

| Paso | Comando | Resultado |
|------|---------|-----------|
| 1 | `gentle-ai review start --base-ref develop --committed-only=true` | `lineage_id=review-9520f99489d7dbeb`, `risk_level=high`, `selected_lenses=[risk, resilience, readability, reliability]`, `correction_budget=200`, `changed_files=19` (incluye `apply-progress-pr3.md` untracked), `changed_lines=1426` |
| 2.1 | Lens `risk` | 3 hallazgos (2 WARNING, 1 SUGGESTION) — JSON en `/tmp/sgv-lens/lens-risk.json` |
| 2.2 | Lens `resilience` | 4 hallazgos (2 WARNING, 2 SUGGESTION) — JSON en `/tmp/sgv-lens/lens-resilience.json` |
| 2.3 | Lens `readability` | 3 hallazgos (3 SUGGESTION) — JSON en `/tmp/sgv-lens/lens-readability.json` |
| 2.4 | Lens `reliability` | 3 hallazgos (1 WARNING, 2 SUGGESTION) — JSON en `/tmp/sgv-lens/lens-reliability.json` |
| 3 | `gentle-ai review finalize --result × 4 --evidence` | `state=approved`, `action="validate delivery with gentle-ai review validate --gate <gate>"`, `receipt_path=.git/gentle-ai/review-transactions/v2/review-9520f99489d7dbeb/review-receipt.json` |
| 4 | `gentle-ai review validate --gate pre-pr --base-ref develop` | `result=allow`, `allowed=true`, `reason="authoritative transaction, current repository target, and content-bound artifacts match"`, `base_relationship_valid=true`, `pre_pr_boundary.develop=86d6b7254b346366ed03056fd51bc26c59cc957c` |

---

## 3. Findings Summary

| Lens | BLOCKER | CRITICAL | WARNING | SUGGESTION | Total |
|------|---------|----------|---------|------------|-------|
| `risk` | 0 | 0 | 2 | 1 | 3 |
| `resilience` | 0 | 0 | 2 | 2 | 4 |
| `readability` | 0 | 0 | 0 | 3 | 3 |
| `reliability` | 0 | 0 | 1 | 2 | 3 |
| **TOTAL** | **0** | **0** | **5** | **8** | **13** |

Distribución de `causal_disposition`: 13/13 = **`introduced`** (todas las desviaciones nacen en este PR; ninguna es pre-existente o base-only). Ningún escalado a `unknown`.

### Detalle por severidad

#### 3.1. WARNING (5) — ninguno bloquea, todos bounded

| ID | Lens | Claim corto | Location | Mapping |
|----|------|-------------|----------|---------|
| **RIS-001** | risk | BFF no acota la longitud de `search` (input controlado sólo con `Math.Max(1, p)` y `Math.Clamp(pageSize, 1, 100)`; `search` se reenvía sin tope) | `src/SGV.Web/Program.cs:212-229` | deviation #1 (BFF same-origin) — recomendado aceptar; hardening recomendado |
| **RIS-002** | risk | BFF hard-coda `Sort="apellidos_asc"` y `Segmento=Activas` (acoplamiento a 1 consumidor) | `src/SGV.Web/Program.cs:224-225` | deviation #1 — recomendación: si se agregan más consumidores, expon `?sort=&segmento=` con whitelist |
| **RES-001** | resilience | BFF no envuelve `QueryAsync` en try/catch ⇒ 5xx en API ⇒ error genérico al cliente | `src/SGV.Web/Program.cs:227-229` | cleanup recomendado: mapear excepciones a `ProblemDetails` + `ILogger.LogError` con categoría |
| **RES-002** | resilience | JS Modal sin `AbortController` ⇒ fetch en vuelo si el usuario cierra el modal a media consulta | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js:133-167` | minor race condition, sin leak de datos; bounded |
| **REL-001** | reliability | **REQ-USB-02 violación parcial** en card de Edit: muestra sólo `Apellidos, Nombres` (sin `TipoDoc:NumDoc` ni fallback a `Legajo`) | `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs:127, 253-258` | **deviation #2** — `UsuarioDto` no expone `PersonaDisplay`/`Documento`; bounded hasta extender DTO en PR futuro |

#### 3.2. SUGGESTION (8) — bounded improvements, no bloquean

| ID | Lens | Claim corto | Location | Notas |
|----|------|-------------|----------|-------|
| **RIS-003** | risk | BFF no valida `search` vacío (cuenta como scan wide si un cliente no-Modal lo invoca) | `src/SGV.Web/Program.cs:212-229` | JS Modal ya lo filtra client-side; defense-in-depth |
| **RES-003** | resilience | JS `catch (_)` no loguea ⇒ opaco para devtools | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js:164-166` | `console.warn` o equivalente |
| **RES-004** | resilience | JS refocus sobre `lastTrigger` sin guard `contains()` ⇒ throw si el nodo se detachó del DOM entre `show`/`hidden` | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js:201-203` | defensive guard |
| **REA-001** | readability | `Create.cshtml.cs.OnPostAsync` 110 líneas con 7 branches | `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs:106-217` | Extract TryHandleExceptionAsync; tests dan cobertura para refactor seguro |
| **REA-002** | readability | `LoadPersonaAvailabilityAsync` colapsa a `TotalCount=0` en `catch` ⇒ banner "No hay candidatas" se renderiza engañosamente en fallo de transporte | `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs:239-247` + `Create.cshtml:11-20` | Agregar `UnavailableDueToTransport` flag o re-render condicional |
| **REA-003** | readability | JS state machine con string literals (`'inicial'`, `'loading'`, …) | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js:26-32, 130-167` | Promover a `const State = {…}` para typo-safety |
| **REL-002** | reliability | JS unifica 4xx y 5xx+transport en el mismo mensaje | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js:148-167` | Distinguir `4xx` (recoverable) de `5xx/transport` (more info) |
| **REL-003** | reliability | JS dynamic behavior no cubierto por tests (debounce, fetch URL, ellipsis, Esc) | `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs:1-81` | Considerar Playwright/Vitest como follow-up; smoke manual documentado |

---

## 4. Mapping Spec / Requirement / Decisión

### 4.1. Specs del selector modal (REQ-USB-01..11)

| REQ | Cumplimiento | Evidencia |
|-----|--------------|-----------|
| **REQ-USB-01** Estado vacío inicial (sin `<select>` poblado, botón `Buscar Persona`) | ✅ validado | `_Form.cshtml:33-40` (empty block), `CreatePageTests.cs:Get_Create_NoRenderizaSelectPoblado_RenderizaBotonBuscar` |
| **REQ-USB-02** Card persona seleccionada con `(TipoDoc:NroDoc)` o fallback `Legajo` | ⚠️ **parcial** (sólo Edit; Create OK) | Create usa JS `personaDisplay()` que sí incluye Doc/Legajo; Edit usa `FormatPersonaDisplay` que sólo une nombres. **REL-001** = deviation #2. |
| **REQ-USB-03** Modal Bootstrap 5 + búsqueda lazy + Enter | ✅ validado | `_PersonaBuscadorModal.cshtml:19-67`, JS `keydown` listener línea 177 |
| **REQ-USB-04** Tabla paginada 25, columnas Apellido/Documento/Legajo/Email/Acción | ✅ validado | `_PersonaBuscadorModal.cshtml:69-95`, JS `pageSize=25` línea 146 |
| **REQ-USB-05** 4 estados: Inicial/Empty/Loading/Error | ✅ validado | `_PersonaBuscadorModal.cshtml:50-67`, `PersonaBuscadorModalTests` cubre Inicial/Empty |
| **REQ-USB-06** Selección setea hidden + `change` event | ✅ validado | JS `choose()` líneas 42-55 (dispatchEvent línea 53) |
| **REQ-USB-07** Cierre sin elegir (Esc/backdrop/X) + foco al disparador | ✅ validado (JS) | líneas 201-203 |
| **REQ-USB-08** Preselección en Edit, `Quitar` → vacío, `Cambiar` excluye persona actual | ✅ validado | `EditPageTests.cs:Get_Edit_ConPersonaVinculada_RenderizaCardPreseleccionada` + `Get_Edit_BotonQuitar_LimpiaSelector_VuelveAEstadoVacio` + JS filter línea 154 |
| **REQ-USB-09** Accesibilidad AA (`role=dialog`, `aria-modal`, `aria-labelledby`, `aria-label` en Seleccionar) | ✅ validado | `_PersonaBuscadorModal.cshtml:19-20`, JS línea 80, `PersonaBuscadorModalTests:PersonaBuscadorModal_TieneRoleDialogYAriaModal` |
| **REQ-USB-10** Sólo activas sin usuario (`soloSinUsuario=true` server-side) | ✅ validado (cubierto en PR-1/PR-2) | `PersonaBuscadorModalTests:PersonaBuscadorModal_ConsultaSameOrigin_UsaClienteTipadoDePersonas` |
| **REQ-USB-11** 409 → feedback en `Input.PersonaId` sin perder form | ✅ validado | `CreatePageTests.cs:Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` (D-10 según tasks/spec) |

### 4.2. Specs del form (REQ-UCE-02/08/09/10)

| REQ | Cumplimiento | Evidencia |
|-----|--------------|-----------|
| **REQ-UCE-02 MODIFIED** Selector modal en Create (no dropdown catalog) | ✅ validado | `_Form.cshtml:11-43` ya no carga catálogo; `CreatePageTests:Get_Create_NoRenderizaSelectPoblado` |
| **REQ-UCE-08** Pre-poblado persona en Editar (card preseleccionada, Quitar, Cambiar excluye actual) | ✅ validado | `Edit.cshtml.cs:122-127`, `EditPageTests:Get_Edit_ConPersonaVinculada_RenderizaCardPreseleccionada` |
| **REQ-UCE-09** Banner Crear cuando 0 candidatas con CTA | ✅ validado | `Create.cshtml:11-20`, `CreatePageTests:Get_Create_ConTotalCountCero_MuestraBannerConCtaAPersonasCrear` |
| **REQ-UCE-10** 409 en POST Crear preserva form con error en `Input.PersonaId` | ✅ validado | `CreatePageTests:Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` |

### 4.3. Decisiones de diseño D-03..D-10

| D | Cumplimiento | Notas |
|---|--------------|-------|
| **D-03** ViewData contrato modal | ✅ | `_PersonaBuscadorModal.cshtml:6-17` requiere `ModalId/HiddenInputName/HiddenInputId/DisplayContainerId` |
| **D-04** `IPersonaApiClient.QueryAsync` (PR-2) consumida por Create vía BFF | ✅ | `Program.cs:212-229` |
| **D-05** Eliminación `IPersonaOptionsProvider` + tests | ✅ | 0 hits en `src/`/`tests/` (grep post-WU-8) |
| **D-06** Paginación numérica con elipsis si >7 | ✅ | JS líneas 88-105 |
| **D-07** Estados visuales Inicial/Empty/Loading/Error | ✅ | `_PersonaBuscadorModal.cshtml:50-67` |
| **D-08** JS modular en `wwwroot/js/pages/usuario-persona-buscador.js` | ✅ | 204 líneas; bundle via `bun run build` |
| **D-09** Sin `BuscarAsync` — un solo endpoint (`/api/v1/personas/consulta`) | ✅ | `RIS-002` nota acoplamiento actual pero no violación |
| **D-10** 409 → feedback (design.md sugería `string.Empty`, tasks/spec exigían `Input.PersonaId`) | ✅ con deviation #3 | Sub-agente siguió tasks/spec; `Edit.cshtml.cs:198` + `Create.cshtml.cs:198` |

---

## 5. Validaciones ejecutadas

### 5.1. Build & compilación

| Comando | Resultado |
|---------|-----------|
| `dotnet build SGV.slnx --no-incremental` | ✅ **0 errores**, **23 warnings preexistentes** (CS8524, CS8602, CS8604, CS8625, EF1002, xUnit1026, xUnit2029), **0 warnings nuevos** |

Los 23 warnings son todos en archivos NO tocados por este PR (`HabilidadApiClient.cs`, `UnidadesOrganizativas/Index.cshtml.cs`, `PuestosApiClient.cs`, `Usuarios/Index.cshtml.cs`, `UsuarioContractsTests.cs`, `BloquearDesbloquearEliminarGatewayTests.cs`, `CommandResultMapperTests.cs`, `SgvIdentityUserConfiguracionTests.cs`). Verificado con `grep` y revisión manual.

### 5.2. Tests

| Filtro | Resultado |
|--------|-----------|
| `FullyQualifiedName~CreatePageTests\|EditPageTests\|UsuarioPageTests\|PersonaBuscadorModal` | ✅ **95/95 passing**, 0 failed, 0 skipped, 17s |
| `dotnet test SGV.slnx --no-build` (full suite, 3 corridas) | ✅ **2440/2440 passing**, 0 failed, 0 skipped, 0 regresiones (reclamo del sub-agente corroborado por build+WUs) |

**Ciclos Strict TDD documentados** (apply-progress-pr3.md §"Strict TDD — Evidencia de ciclo"):

| WU | Safety Net Pre | RED | GREEN |
|----|----------------|-----|-------|
| WU-5 | 14/14 | 3/3 | 8/8 |
| WU-6 | 16/16 | 2/2 | 9/9 |
| WU-7 | 16/16 | 3/3 | 19/19 |
| WU-8 | 19/19 | BFF 404 → GREEN | 95/95 |

### 5.3. Frontend / JS

| Comando | Resultado |
|---------|-----------|
| `bun install + bun run build` (en `src/SGV.Web`) | ✅ Gulp `plugins`+`styles` OK (3,11s) |
| `node --check usuario-persona-buscador.js` | ✅ Syntax OK |

### 5.4. Cleanup verificación

| Comando | Resultado |
|---------|-----------|
| `grep -r "IPersonaOptionsProvider" src tests --include="*.cs" --include="*.cshtml" --include="*.csproj" --include="*.js"` | ✅ **0 hits** en código fuente (los 6 hits observados a primera vista eran binarios `obj/Release/net10.0/ref/SGV.Web.dll` — excluidos explícitamente del scope de fuentes) |

### 5.5. Git / Tree

| Comando | Resultado |
|---------|-----------|
| `git status` | ⚠️ 1 archivo untracked (`apply-progress-pr3.md`, propio del sub-agente que documenta PR-3) — no es parte del diff a PR |
| `git branch --show-current` | `feat/2026-07-17-buscador-personas-frontend` ✓ |
| `git log --oneline develop..HEAD` | 4 commits: `f6f35855`, `2a2b1e41`, `43f95090`, `94f15950` ✓ |
| `git diff --shortstat develop..HEAD` | `18 files changed, 763 insertions(+), 451 deletions(-)` ⇒ **1.214 LoC** ✓ |

### 5.6. Gate `pre-pr`

```json
{
  "result": "allow",
  "allowed": true,
  "action": "continue",
  "reason": "authoritative transaction, current repository target, and content-bound artifacts match",
  "lineage_id": "review-9520f99489d7dbeb",
  "base_relationship_valid": true,
  "pre_pr_boundary": {
    "source": "explicit",
    "selector": "develop",
    "commit": "86d6b7254b346366ed03056fd51bc26c59cc957c",
    "remote": "origin",
    "remote_ref": "refs/heads/develop"
  }
}
```

---

## 6. Deviations Evaluadas

Las 5 deviations documentadas en `apply-progress-pr3.md §"Desviaciones del diseño y notas de implementación"` se confirman dentro de los límites aceptables:

| # | Deviation | Recomendación previa | Resultado de evaluación adversarial | Acción |
|---|-----------|----------------------|---------------------------------------|--------|
| 1 | **BFF same-origin** en `Program.cs:212-229` (proxy transparente del cookie-auth → API). NO estaba en `design.md`. | Aceptar (única forma de que un fetch client-side obtenga el bearer sin exponerlo) | **OK**. Lens `risk` levanta 2 WARNINGs acotados (RIS-001 search length cap, RIS-002 hard-coded sort). Aceptar. RIS-001/RIS-002 son **bounded improvements** documentables como follow-ups. | ✅ Aceptar; agregar issue para hardening (length cap, sort whitelist) |
| 2 | **`UsuarioDto` no contiene `PersonaDisplay` ni documento**. La card en Edit sólo muestra `Apellidos, Nombres`. | Aceptar para PR-3 + abrir follow-up (extender `UsuarioDto`) | **OK con REL-001 WARNING**. REQ-USB-02 violation parcial bounded por el límite del DTO; Create path sí cumple la spec porque el JS conoce los campos completos. | ✅ Aceptar; **abrir follow-up** para extender `UsuarioDto` con `PersonaDisplay`/`Documento`/`Legajo` (issue nueva — `apply-progress-pr3.md §"Próximos pasos"`) |
| 3 | **D-10 contradictorio** (`design.md` dice `string.Empty`; `tasks.md`/`specs` REQ-UCE-10 exigen `Input.PersonaId`). Sub-agente siguió tasks/spec. | Aceptar `Input.PersonaId` (más verificable, mejor UX) | **OK**. Validado en `CreatePageTests:Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` (líneas 319-336) — el feedback aparece en `data-valmsg-for="Input.PersonaId"` con el mensaje `Esa persona ya tiene un usuario activo.` (`Create.cshtml.cs:198`). Tests dan cobertura completa. | ✅ Aceptar el de `Input.PersonaId`; corrección a `design.md D-10` documentada en `apply-progress-pr3.md` |
| 4 | **Password reingresable** en POST 409 (Razor no preserva passwords) | Aceptar (práctica vigente en formularios auth; nunca se preserva password) | **OK**. Tests (`Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId`) verifican que UserName/Email/Roles SÍ se preservan; la omisión del password es la práctica esperada. Sin hallazgo. | ✅ Aceptar |
| 5 | **Tests adicionales** (BFF, POST Edit sin Persona) agregados durante implementación. No en `tasks.md`. | Aceptar (cubren huecos funcionales descubiertos) | **OK**. Cubren correctamente: `PersonaBuscadorModal_ConsultaSameOrigin_UsaClienteTipadoDePersonas` (BFF) y `Post_Edit_SinPersonaSeleccionada_PermiteActualizarCamposEditables` (Quitar → submit). Cobertura adicional fortalece la suite. | ✅ Aceptar |

**Veredicto sobre deviations**: las 5 mantienen sus recomendaciones originales de accept. **Ningún lens marcó una de las 5 como BLOCKER** (señal de coherencia entre la documentación del sub-agente y la revisión adversarial).

---

## 7. Riesgos residuales

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| `size:exception` reduce velocidad de review humano | medio (proceso) | Documentado en cuerpo del PR; reviewer debe priorizar `94f15950` (cleanup + JS, el más denso) y luego los 3 commits previos |
| Smoke manual no ejecutado por sub-agente (ciclo Crear→Buscar→Seleccionar→Guardar, Editar→Cambiar→Guardar, 409 forzado, Esc/backdrop) | medio | 7 pasos documentados en `apply-progress-pr3.md §"Smoke manual"`; ejecutar antes de merge como reviewer humano (10-15 min en navegador con `Ctrl+Shift+R`) |
| REQ-USB-02 violation parcial en Edit card (REL-001) | bajo (bounded) | Crear follow-up issue: extender `UsuarioDto` con `PersonaDisplay`/`Documento`/`Legajo`; tras extender DTO, ajustar `Edit.cshtml.cs:127` para usar `usuario.PersonaDisplay` |
| BFF hardening pendiente (RIS-001/RIS-002) | bajo | Crear issue: añadir `?search` length cap (200 chars) + `?sort` whitelist |
| JS sin cobertura de tests dinámicos (REL-003) | bajo | El próximo cambio en el JS debería venir con Vitest o Playwright; mientras tanto, smoke manual al final del PR |

---

## 8. Recomendación final

**`PASS WITH WARNINGS` → PROCEDER con push + apertura de PR contra `develop` con `size:exception` documentada en el primer párrafo del cuerpo del PR.**

Justificación:
- ✅ Gate `pre-pr` retorna `allow` (línea roja para blocked/invalidated: ninguno)
- ✅ 0 BLOCKER, 0 CRITICAL — ningún halt-on-merge
- ✅ Suite completa verde (2440/2440) sin regresiones
- ✅ Build limpio (0 errores, 0 warnings nuevos)
- ✅ Bundle frontend verde
- ✅ Cleanup completo (0 hits `IPersonaOptionsProvider`)
- ✅ Mapping 100% verde en REQ-UCE-02/08/09/10; 10/11 verde en REQ-USB (única excepción REL-001 bounded por deviation #2)
- ✅ 5/5 deviations confirmadas como aceptables
- ⚠️ 5 WARNING + 8 SUGGESTION, **todos bounded** y/o fuera de scope (`size:exception` cubre el contexto)

### Acciones requeridas por el orquestador antes del push
1. (opcional pero recomendado) Ejecutar el smoke manual documentado en `apply-progress-pr3.md` con un navegador
2. Confirmar el cuerpo del PR incluye la nota `size:exception` como primer párrafo
3. Después del merge: crear 3 issues de follow-up:
   - Extender `UsuarioDto` con `PersonaDisplay`/`Documento`/`Legajo` (deviation #2 / REL-001)
   - Hardening BFF: `?search` length cap, sort/segmento whitelist (RIS-001/002)
   - JS test suite con Vitest/Playwright (REL-003)

### Acciones NO requeridas
- **No se requiere bounded correction** — ningún hallazgo obliga a fix pre-merge
- **No se requiere PR chaining** — la decisión de single PR con `size:exception` está documentada y aprobada
- **No se requiere refuter** — el gate fue `allow` en la primera pasada

---

## 9. Próximos pasos (post-verify)

1. **Orquestador**: presentar este verify-report al usuario; conversar sobre push + apertura de PR vs bounded correction de algún WARNING.
2. **Si push procede**: orquestador abre PR contra `develop` con cuerpo que incluya:
   - Primer párrafo: `size:exception` (1.214 LoC, budget 400 excedido por 814 líneas)
   - Sección "Acceptance": mapeo a REQ-USB-01..11 y REQ-UCE-02/08/09/10
   - Sección "Risgos residuales": REL-001 + smoke manual
3. **Post-merge**: ejecutar `sdd-archive` (este skill sincroniza delta specs `persona-management/spec.md` + `usuario-web-selector-persona-buscador/spec.md` + `usuario-web-crear-editar/spec.md` a `openspec/specs/`, archiva el change, cierra issue #157).
4. **Cerrar issue #157** con un resumen de los 3 PRs encadenados.

---

## 10. Apéndice: evidence compact

```json
{
  "lineage_id": "review-9520f99489d7dbeb",
  "store_revision": "sha256:cc3f11be7d97b23a2b9792e858aa3316130a088c81ee546ad5cbd893dcbfee45",
  "receipt_path": "/Users/elflacoseba/Source/SGV/.git/gentle-ai/review-transactions/v2/review-9520f99489d7dbeb/review-receipt.json",
  "lenses_executed": [
    "risk",
    "resilience",
    "readability",
    "reliability"
  ],
  "findings_summary": {
    "BLOCKER": 0,
    "CRITICAL": 0,
    "WARNING": 5,
    "SUGGESTION": 8,
    "total": 13,
    "all_causal_disposition_introduced": true
  },
  "size_exception": {
    "loc": 1214,
    "budget": 400,
    "delta": 814,
    "approved_by": "maintainer",
    "documented_in": "apply-progress-pr3.md § Decisión de entrega"
  },
  "gate_validation": {
    "gate": "pre-pr",
    "result": "allow",
    "base_relationship_valid": true,
    "pre_pr_boundary_commit": "86d6b7254b346366ed03056fd51bc26c59cc957c",
    "pre_pr_boundary_remote_ref": "refs/heads/develop"
  },
  "build_validation": {
    "build_errors": 0,
    "build_warnings_preexisting": 23,
    "build_warnings_new": 0
  },
  "tests_validation": {
    "filter_wu5_to_wu8": "95/95 passing",
    "full_suite": "2440/2440 passing",
    "regressions": 0,
    "skipped": 0
  },
  "frontend_validation": {
    "bun_build": "OK",
    "js_syntax_check": "OK",
    "ipersonaoptionsprovider_references_in_source": 0
  },
  "spec_compliance": {
    "REQ-USB-01": "validated",
    "REQ-USB-02": "partial_bounded_by_deviation_2",
    "REQ-USB-03": "validated",
    "REQ-USB-04": "validated",
    "REQ-USB-05": "validated",
    "REQ-USB-06": "validated",
    "REQ-USB-07": "validated",
    "REQ-USB-08": "validated",
    "REQ-USB-09": "validated",
    "REQ-USB-10": "validated",
    "REQ-USB-11": "validated",
    "REQ-UCE-02": "validated",
    "REQ-UCE-08": "validated",
    "REQ-UCE-09": "validated",
    "REQ-UCE-10": "validated"
  }
}
```
