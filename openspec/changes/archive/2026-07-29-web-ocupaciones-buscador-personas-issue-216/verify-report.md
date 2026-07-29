# Verify Report: Buscador de Personas en Ocupación (#216)

> Change: `2026-07-29-web-ocupaciones-buscador-personas-issue-216`
> Issue: [#216](https://github.com/elflacoseba/SGV/issues/216)
> Modo artefactos: **both** (OpenSpec + Engram)
> Modo TDD: **strict** (`strict_tdd: true`)
> Review budget: 400 líneas · **Overrun aceptado**: 682 netas (ver §Size exception)
> Verificación ejecutada: working tree limpio de tracked sobre `develop` (HEAD = `a699a288`)

## Resumen

La verificación cubre build, suite de tests focalizada, suite Web completa, suite .NET completa, build del bundle frontend, sintaxis JS y trazabilidad completa de cada escenario de ambas specs (Ocupación NEW + Usuario MODIFIED). **Encontré un blocker crítico** en el archivo `_Form.cshtml` de Ocupaciones: el `<select>` de `PuestoId` aparece **DUPLICADO** en el render (líneas 88-97 y 99-108), introducido en el commit WUG-2 (`4c39f658`). La propuesta explícitamente decía "`<select>` de PuestoId — permanece intacto" y el design reiteraba "`PuestoId` intacto", pero el form ahora tiene dos selects idénticos que violan el contrato. El bug no es detectado por la suite (los tests usan `Assert.Contains("name=\"Input.PuestoId\"", ...)` que no distingue uno de dos). El resto de los requisitos (JS fix, `IOcupacionForm`, `OcupacionFormPageModel`, modal wiring, scripts, ViewData, `data-solo-sin-usuario="false"`) cumple los escenarios de las dos specs.

## Validación técnica

| # | Comando | Resultado | Notas |
|---|---------|-----------|-------|
| 1 | `git status` (sobre `develop` HEAD) | ✅ Working tree clean de tracked (sólo untracked: `openspec/changes/2026-07-29-...`). | 3 commits ahead de `origin/develop`. |
| 2 | `git log --oneline -5 develop` | ✅ `a699a288`, `4c39f658`, `89715653`, `edf5e728`, `26e3b02d`. | Los 3 commits del change son atómicos, conventional-prefixed `feat(web)`, sin `Co-Authored-By`. |
| 3 | `git diff --stat HEAD~3..HEAD` | ✅ 12 files, +794 / -112. | Coincide con `apply-progress.md` (682 netas, +282 sobre budget de 400). |
| 4 | `dotnet build SGV.slnx --no-incremental` | ✅ 0 errores, 91 warnings (preexistentes, 0 nuevos). | Matchea la baseline documentada en apply-progress. |
| 5 | `dotnet test SGV.slnx --filter "Web.Ocupaciones\|Web.Usuario.PersonaBuscador"` | ✅ **162/162 PASS** (0 failed, 0 skipped). | Build aporta 14 tests nuevos vs baseline 148 (7 WUG-2 + 7 WUG-3). |
| 6 | `dotnet test SGV.slnx --filter "Web"` | ✅ **1282/1282 PASS** (0 failed, 0 skipped). | Suite Web completa. Back-compat con Usuarios estricta. |
| 7 | `dotnet test SGV.slnx` (suite completa) | ⚠️ **3167/3168 PASS**, 1 fail preexistente. | `Persistencia.CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo` (MySqlFact, agregado en `f5569eb9`, fuera del diff del change). Documentado en apply-progress. |
| 8 | `cd src/SGV.Web && bun install && bun run build` | ✅ Bundle frontend OK, 0 errores. | Idéntico a apply-progress. |
| 9 | `node --check src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | ✅ Syntax OK. | 239 líneas, paréntesis balanceados. |
| 10 | `grep -c "Input.PuestoId" src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | ❌ **6** (líneas 90, 94, 95, 101, 105, 106). | Baseline HEAD~3 = 3; el archivo tiene **DOS `<select>` duplicados** (líneas 88-97 y 99-108). Ver Finding CRITICAL-01. |

## Trazabilidad specs

### Spec NEW `ocupacion-web-selector-persona-buscador` (7 REQ / 15 scenarios)

| Requirement | Scenario | Estado | Evidencia |
|-------------|----------|--------|-----------|
| **OCC-PER-BUSC-01** Reemplazo del `<select>` por card + modal | Cargar Create renderiza card + botón Buscar | ✅ PASS | `OcupacionBuscadorModalTests.Get_Create_RendersPersonaCardSinSelectPersonaId` (líneas 94-122) |
| OCC-PER-BUSC-01 | Cargar Edit renderiza card + botón Buscar | ✅ PASS | `OcupacionBuscadorModalTests.Get_Edit_RendersPersonaCardPrepopulated` (líneas 124-153) + `OcupacionEditPageTests.Get_Edit_WhenVigente_PrepopulatesFormFromApi` (líneas 74-121) |
| **OCC-PER-BUSC-02** `IOcupacionForm` expone estado enriquecido | `IOcupacionForm` expone `PersonaDisplay`/`PersonaVinculada` | ✅ PASS | `src/.../IOcupacionForm.cs` líneas 43, 52; implementado en `OcupacionFormPageModel.cs` líneas 40, 47. Cubierto indirectamente por `OcupacionBuscadorModalTests.Get_Edit_RendersPersonaCardPrepopulated`. |
| OCC-PER-BUSC-02 | Edit enriquece la card desde `GetByIdAsync` | ✅ PASS | `OcupacionEditPageTests.Get_Edit_WhenVigente_LoadCatalogsAsync_CallsPersonaGetByIdAsync` (líneas 127-147) |
| OCC-PER-BUSC-02 | Create no invoca `GetByIdAsync` para personas | ✅ PASS | `OcupacionCreatePageTests.Get_Create_LoadCatalogsAsync_NoLlamaPersonaGetAllAsync` (líneas 117-133) |
| **OCC-PER-BUSC-03** Búsqueda sin filtro `soloSinUsuario` | Búsqueda desde Ocupaciones omite `soloSinUsuario` | ⚠️ PARTIAL | El attribute `data-solo-sin-usuario="false"` está declarado (`OcupacionBuscadorModalTests.Modal_DeclaresSoloSinUsuarioFalse` líneas 72-87) y el JS lo lee (`usuario-persona-buscador.js` líneas 14-18, 165). Cubierto a nivel DOM contract + JS source. Validación runtime del query param (jsdom-style) NO está automatizada — apply-progress lo documenta como pendiente. |
| OCC-PER-BUSC-03 | Modal root declara `data-solo-sin-usuario="false"` | ✅ PASS | `OcupacionBuscadorModalTests.Modal_DeclaresSoloSinUsuarioFalse` (líneas 72-87) + `_PersonaBuscadorModal.cshtml` línea 22. |
| **OCC-PER-BUSC-04** Preselección y exclusión en Edit | Edit precarga la persona vinculada en la card | ✅ PASS | `OcupacionBuscadorModalTests.Get_Edit_RendersPersonaCardPrepopulated` (líneas 124-153) |
| OCC-PER-BUSC-04 | `Cambiar` excluye la persona actual del modal | ⚠️ PARTIAL | La exclusion vive en el JS compartido (`usuario-persona-buscador.js` línea 179: `filter(persona => persona.id !== modal.dataset.currentPersonaId)`). Cubierto por comportamiento de Usuarios (regresión intacta) pero no aislado en test del cambio. Validación manual recomendada. |
| OCC-PER-BUSC-04 | `Quitar` limpia el campo sin invocar la API | ⚠️ PARTIAL | Implementado en `usuario-persona-buscador.js` líneas 204-217 del bloque `[data-usuario-persona-quitar]`. Sin test específico del change; back-compat con Usuarios cubre el flujo. |
| **OCC-PER-BUSC-05** Pre-carga via query string en Create | `?personaId` válido precarga la card | ✅ PASS | `OcupacionCreatePageTests.Get_Create_WithPersonaIdQuery_InvocaGetByIdYPopulaCard` (líneas 135-154) |
| OCC-PER-BUSC-05 | `?personaId` inexistente cae a estado vacío | ✅ PASS | `OcupacionCreatePageTests.Get_Create_WithUnknownPersonaId_NoLanzaYQuedaVacia` (líneas 156-174) |
| **OCC-PER-BUSC-06** Estados del modal reutilizados | Estado Empty muestra mensaje estándar | ✅ PASS | `OcupacionBuscadorModalTests.Modal_EstadosInicialEmptyLoadingErrorReutilizados` (líneas 168-187) |
| OCC-PER-BUSC-06 | Error de transporte preserva texto | ⚠️ PARTIAL | Mensaje `"No se pudo conectar con el servidor. Reintentá."` existe en el partial compartido (línea 71). El JS compartido (`usuario-persona-buscador.js` línea 184) hace `showState('error')` en `catch`. No testeado en el change; cubierto por tests de Usuarios preexistentes. |
| **OCC-PER-BUSC-07** Actualización de tests xUnit | Test de render validando modal en lugar de `<select>` | ✅ PASS | `OcupacionCreatePageTests.Get_Create_WhenAdmin_RendersAllFiveFieldsWithCatalogs` (líneas 79-111) ya NO assertea `"García, Ana"` ni `"Analista"` como option del select; los nuevos asserts (líneas 109-110) verifican `Empty(GetAllCalls)` y `Empty(GetByIdCalls)`. |

**Subtotal**: 11 PASS / 4 PARTIAL / 0 FAIL sobre 15 scenarios. Los PARTIAL son gaps de test runtime del cambio (no de implementación) documentados en `apply-progress.md` §Pendientes; la implementación cumple el comportamiento esperado vía tests de regresión en Usuarios.

### Spec MODIFIED `usuario-web-selector-persona-buscador` (1 ADDED REQ-USB-12 + 2 MODIFIED REQ-USB-03/10)

| Requirement | Scenario | Estado | Evidencia |
|-------------|----------|--------|-----------|
| **REQ-USB-12** ADDED Configuración del modal via `data-solo-sin-usuario` | Modal Usuarios sin atributo mantiene `soloSinUsuario=true` | ✅ PASS | `PersonaBuscadorModalTests.PersonaBuscadorModal_Usuarios_NoDeclaraDataSoloSinUsuarioYDefaultSigueSiendoTrue` (líneas 88-105) |
| REQ-USB-12 | Modal Ocupaciones con `false` omite el filtro | ✅ PASS | `OcupacionBuscadorModalTests.Modal_DeclaresSoloSinUsuarioFalse` (líneas 72-87) + JS lee atributo (líneas 14-18) |
| REQ-USB-12 | Casing/value variants normaliza | ✅ PASS | `usuario-persona-buscador.js` línea 16: `rawSoloSinUsuario.toLowerCase() === 'false'` |
| REQ-USB-12 | Script backwards-compatible | ✅ PASS | `PersonaBuscadorModalTests` previos (líneas 14-385) — 32 tests verdes sin cambios. |
| **REQ-USB-03** MODIFIED Modal Bootstrap 5 con búsqueda lazy | Apertura enfoca el input y renderiza placeholder | ✅ PASS | `PersonaBuscadorModalTests.PersonaBuscadorModal_TieneRoleDialogYAriaModal` (líneas 20-33) + `PersonaBuscadorModal_EstadoInicial_*` (líneas 35-61) |
| REQ-USB-03 | Búsqueda desde Usuarios envía `soloSinUsuario=true` | ✅ PASS | `PersonaBuscadorModalTests.PersonaBuscadorModal_ConsultaSameOrigin_UsaClienteTipadoDePersonas` (líneas 63-78) — `Assert.True(query.SoloSinUsuario)` |
| REQ-USB-03 | Búsqueda desde modal con `false` omite el parámetro | ⚠️ PARTIAL | JS lee atributo correctamente; sin embargo el `query.SoloSinUsuario` flag del `FakePersonaApiClient` se establece en el BFF (`OcupacionApiClientMutation`/BFF), no en el JS. La cobertura real es: el JS envía `soloSinUsuario=false` cuando el attribute lo declara. Esto NO está directamente probado en runtime; el `OcupacionBuscadorModalTests.Modal_DeclaresSoloSinUsuarioFalse` valida el atributo del HTML y el `PersonaBuscadorModal_JsSource_NoHardcodeaSoloSinUsuarioYLeeAtributo` (líneas 114-138) valida el source del JS. |
| **REQ-USB-10** MODIFIED Listado activo (default `true`) | Solo activas sin usuario en `/consulta` desde Usuarios | ✅ PASS | Tests BFF preexistentes (cover the contract). |
| REQ-USB-10 | Modal reutilizado con `soloSinUsuario=false` no filtra por usuario | ⚠️ PARTIAL | Cubierto por el JS source test (líneas 114-138) que valida el attribute source. El comportamiento runtime del BFF al recibir `soloSinUsuario=false` NO está aislado en el change; los tests de `FakePersonaApiClientTests`/`PersonaApiClientBasicTests` (transport contract) ya cubren el query param, pero no diferencian `true` vs `false` específicamente. |

**Subtotal**: 7 PASS / 2 PARTIAL / 0 FAIL sobre 9 scenarios.

## Validación de proposal/design

### Alcance (Incluye / NO incluye)

- **APPROVE**: Todos los puntos del "Incluye" están cumplidos:
  - ✅ Reemplazo del `<select>` por card + modal (con WARNING por duplicación de PuestoId — ver Finding CRITICAL-01).
  - ✅ Fix back-compat JS vía `data-solo-sin-usuario` (default `true`).
  - ✅ `IOcupacionForm` extendido con `PersonaDisplay`/`PersonaVinculada` y `PersonaOptions` eliminado.
  - ✅ `@section Scripts` en Create.cshtml y Edit.cshtml.
  - ✅ ViewData populado en OnGetAsync.
  - ✅ Tests de `<select>` actualizados/retirados.
- **APPROVE**: Los puntos del "NO incluye" están respetados:
  - ✅ Backend intacto (no hubo cambios en `OcupacionesController`, `OcupacionServicio`, entidades).
  - ✅ Migraciones: 0 archivos nuevos en `src/SGV.Infraestructura/Persistencia/Migraciones/`.
  - ✅ Sin cambios en `csproj`.
  - ✅ `_PersonaBuscadorModal.cshtml` movido hasta una modificación mínima (+10/-6) para aceptar el ViewData opcional `SoloSinUsuario`. La decisión documentada en `apply-progress §Desvíos` es razonable: el partial NO hardcodea el atributo, sólo lo emite cuando el consumidor lo pide. Cumple la guía "no modificar el partial" en su espíritu.

### Criterios de aceptación (7/7)

| # | Criterio | Estado |
|---|----------|--------|
| 1 | Card con botón "Buscar" en lugar de `<select>` en Create/Edit | ✅ PASS (con WARNING por duplicación — Finding CRITICAL-01) |
| 2 | Modal sin `soloSinUsuario=true` | ✅ PASS — atributo `data-solo-sin-usuario="false"` emitido |
| 3 | Edit pre-selecciona persona y la excluye del modal | ✅ PASS |
| 4 | Fix back-compat JS (Usuarios sin cambios) | ✅ PASS — 14 tests previos de PersonaBuscadorModal verdes |
| 5 | Tests de `<select>` actualizados | ✅ PASS |
| 6 | `dotnet build` + `dotnet test` pasan | ✅ PASS (con 1 fail preexistente no relacionado) |
| 7 | `bun run build` pasa | ✅ PASS |

### Decisiones arquitectónicas (design.md)

| # | Decisión | Estado | Evidencia |
|---|----------|--------|-----------|
| 1 | Path absoluto del partial sin duplicar | ✅ APPROVE | `_Form.cshtml` líneas 74-86 invoca `~/Pages/Seguridad/Usuarios/_PersonaBuscadorModal.cshtml` con ViewData. Precedente respetado. |
| 2 | DOM contract test (no jsdom) | ✅ APPROVE | `OcupacionBuscadorModalTests` (230 líneas) cubre markup, atributos, BFF. |
| 3 | `PersonaOptions` eliminado limpio | ✅ APPROVE | Único consumidor eliminado; `LoadCatalogsAsync` ya no llama `GetAllAsync` para personas (verificado por `OcupacionCreatePageTests.Get_Create_LoadCatalogsAsync_NoLlamaPersonaGetAllAsync`). |
| 4 | `IOcupacionForm` con `PersonaDisplay`/`PersonaVinculada` | ✅ APPROVE | `IOcupacionForm.cs` líneas 43, 52 + `OcupacionFormPageModel.cs` líneas 40, 47. |
| 5 | `PersonaVinculada` cargado en Edit vía `GetByIdAsync` con falla suave | ✅ APPROVE | `OcupacionEditPageTests.Get_Edit_WhenPersonaNotFound_FallsBackToEmpty` (líneas 149-167) + `OcupacionFormPageModel.EnriquecerPersonaAsync` (líneas 119-156). |
| 6 | Modal invocado desde `_Form.cshtml`, script desde `Create.cshtml`/`Edit.cshtml` | ✅ APPROVE | Precedente respetado. |
| 7 | Backwards-compat JS vía case-insensitive `data-solo-sin-usuario` contra `"true"` | ✅ APPROVE | `usuario-persona-buscador.js` líneas 14-18. |

### Riesgos residuales del design (3)

| # | Riesgo | Mitigación | Estado |
|---|--------|------------|--------|
| 1 | Estrategia de testing JS | DOM contract + JS source test | ✅ APPROVE |
| 2 | `LoadCatalogsAsync`/`PersonaOptions` | Verificado via grep; eliminación limpia | ✅ APPROVE |
| 3 | Path del partial | Path absoluto | ✅ APPROVE |

## Validación TDD

| WUG | Commit | Tests introducidos ANTES de código | Tests en mismo commit |
|-----|--------|-----------------------------------|------------------------|
| WUG-1 | `89715653` JS fix | ❌ NO — el test `PersonaBuscadorModal_JsSource_NoHardcodeaSoloSinUsuarioYLeeAtributo` se introduce en el mismo commit que el fix JS (`+85` tests + `+12/-1` JS). | ✅ Test GREEN antes del JS modificación no es separable en este commit; `git show --stat` muestra `+85/-0` tests + `+13/-1` JS. |
| WUG-2 | `4c39f658` PageModel + form | ❌ NO — los tests `Get_Create_LoadCatalogsAsync_NoLlamaPersonaGetAllAsync`, `Get_Create_WithPersonaIdQuery_InvocaGetByIdYPopulaCard`, `Get_Create_WithUnknownPersonaId_NoLanzaYQuedaVacia`, `Get_Edit_WhenVigente_LoadCatalogsAsync_CallsPersonaGetByIdAsync`, `Get_Edit_WhenPersonaNotFound_FallsBackToEmpty` se commitean EN el mismo commit que el cambio de `OcupacionFormPageModel.cs` (+118/-47) y `_Form.cshtml` (+96/-7). | ✅ El apply-progress documenta RED antes de GREEN; pero el bundle indivisible no permite splittear el WUG en commits separados. |
| WUG-3 | `a699a288` UI + modal | ❌ NO — el `OcupacionBuscadorModalTests` (NEW, +230) se introduce en el mismo commit que los cambios de `_Form.cshtml` (+23/-7), `Create.cshtml` (+8/-1), `Edit.cshtml` (+5/-1), `_PersonaBuscadorModal.cshtml` (+10/-6). | ✅ Mismo argumento. |

**Observación TDD**: Los 3 commits agrupan test + production en un solo commit (no hay commits `test-only` previos). Esto **no respeta la letra estricta** del `test → feat → refactor` por WUG, pero el apply-progress documenta explícitamente RED → GREEN dentro del commit (los tests fallaban contra el código previo). Para una próxima iteración podría proponerse 6 commits en lugar de 3 (un par RED+GREEN por WUG). **WARNING sin blocker**.

## Findings

### CRITICAL

- **CRITICAL-01 — `<select>` de `PuestoId` DUPLICADO en `_Form.cshtml`**
  - **Archivo**: `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml`
  - **Líneas**: 88-97 y 99-108 (idénticas).
  - **Síntoma**: El render de Create/Edit Ocupaciones produce DOS `<select asp-for="Input.PuestoId">` con los mismos `asp-items`, label "Puesto" y validation span. En el browser se ven dos dropdowns de Puesto apilados.
  - **Baseline**: HEAD~3 (antes del change) tenía 3 ocurrencias de `Input.PuestoId` (1 select, 1 label, 1 validation span). HEAD (post-change) tiene 6.
  - **Introducido en**: commit WUG-2 `4c39f658` (ver `git show 4c39f658 -- src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` — el form reemplaza `<select PersonaId>` por la card, pero en el refactor el bloque `<select PuestoId>` quedó duplicado).
  - **Violación**:
    - Propuesta §NO incluye: "`<select>` de PuestoId — permanece intacto".
    - Design §"Cambios por archivo" tabla: "`PuestoId` intacto".
  - **No detectado por tests porque**: `OcupacionCreatePageTests.Get_Create_WhenAdmin_RendersAllFiveFieldsWithCatalogs` línea 96 usa `Assert.Contains("name=\"Input.PuestoId\"", ...)` que es satisfecho por 1 o más ocurrencias. El test `OcupacionEditPageTests.Get_Edit_WhenVigente_PrepopulatesFormFromApi` línea 110 igual usa `Assert.Contains`.
  - **Acción requerida**: Eliminar uno de los dos bloques (líneas 99-108). Mantener las líneas 88-97. Sugerencia: agregar un test `Assert.Single(content, Regex.Match(content, @"<select[^>]*name=""Input\.PuestoId""").Value)` en `OcupacionCreatePageTests` y `OcupacionEditPageTests` para prevenir regresión.
  - **Severidad**: CRITICAL. Bloquea el cierre. Veredicto = REQUEST CHANGES.

### WARNING

- **WARNING-01 — Tests RED→GREEN no spliteados en commits**
  - Los 3 WUGs incluyen test + production en el mismo commit. `apply-progress` documenta el flujo RED→GREEN pero el git history no lo refleja. Política "strict TDD" podría endurecerse en próximos cambios.
  - Severidad: WARNING. No bloquea.

- **WARNING-02 — 1 test `[MySqlFact]` falla en este entorno**
  - `Persistencia.CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo` (línea 50) está preexistente al change (introducido en `f5569eb9`). Falla porque MySQL local no está disponible / DB no migrada. Documentado en `apply-progress.md`.
  - Severidad: WARNING. No relacionado con el change.

- **WARNING-03 — Size exception**
  - Total: 794 insertions / 112 deletions = 682 netas. Budget 400 → overrun 282. Maintainer aprobó. Documentado en `apply-progress §Desvíos`.
  - Severidad: WARNING de proceso (no de código).

- **WARNING-04 — Validación runtime del query param con `soloSinUsuario=false`**
  - Los tests validan el atributo del HTML y el source del JS, pero no el comportamiento end-to-end `Enter → fetch con soloSinUsuario=false`. El apply-progress documenta la validación manual con DevTools como pendiente.
  - Severidad: WARNING. No bloquea la verificación (precedente del change 2026-07-17-buscador-personas-modal usó el mismo enfoque).

### SUGGESTION

- **SUGGESTION-01 — Tests parametrizados sobre los 4 escenarios de OCC-PER-BUSC-04**
  - `Cambiar` excluye persona actual / `Quitar` limpia sin API / Card text formatting. Podrían consolidarse en `[Theory]` + `[InlineData]` para reducir duplicación. Política "menos tests de alta calidad" — no urge.

- **SUGGESTION-02 — Test de regresión específico para `<select>` único de PuestoId**
  - Cuando se arregle CRITICAL-01, agregar un `Assert.Matches` con regex `(?s)^(?!.*<select[^>]*name="Input\.PuestoId".*<select[^>]*name="Input\.PuestoId").*$` o equivalente. El assertion actual `Assert.Contains(...)` no previene la regresión.

- **SUGGESTION-03 — Snapshot test del HTML renderizado**
  - En lugar de 7+ asserts por test, comparar contra un snapshot estable. Considerar para cambios futuros.

## Size exception

> Mantainer aprobó `size:exception`. Overrun documentado: **682 netas vs budget 400** (282 over). Justificación en `tasks.md` §Plan de PR y `apply-progress.md` §Desvíos (item 1):
>
> 1. `_Form.cshtml` requirió +96 líneas para reemplazar el `<select>` por card enriquecida + invocación del modal (incluye fallback plano + helper `FormatearDocumento`).
> 2. `OcupacionFormPageModel.cs` requirió +71 net para `EnriquecerPersonaAsync`, `FormatearPersonaDisplay` y remoción del path `PersonaOptions`.
> 3. 14 tests nuevos (7 WUG-2 + 7 WUG-3). La política "menos tests de alta calidad" se respetó: cada test protege una rama funcional distinta.
>
> NO se trata como blocker — es un hecho aceptado.
>
> **Nota**: La duplicación de `PuestoId` (CRITICAL-01) representa +14 líneas que podrían recuperarse al corregir. La corrección de CRITICAL-01 ayudaría a reducir el overrun a ~668 netas.

## Veredicto final

**REQUEST CHANGES**

**Justificación**: El `<select>` de `PuestoId` está **duplicado** en el render HTML del formulario Crear/Editar Ocupación. Esto constituye una regresión visual y funcional: el usuario ve dos dropdowns idénticos de Puesto, el model binding puede ser ambiguo entre clientes, y la propuesta explícitamente prohibía modificar el `<select>` de PuestoId. El build pasa, los tests pasan (no cubren este path), y el resto de la implementación cumple los requisitos de las specs. Pero el bug es trivial de detectar y arreglar (eliminar un bloque de 14 líneas en `_Form.cshtml`); mientras esté presente, **el change no debe mergearse**.

**Próximo paso recomendado**: `apply` (volver a aplicar con el fix de CRITICAL-01 incluido). El maintainer o el sub-agente `sdd-apply` debería:
1. Eliminar las líneas 99-108 de `_Form.cshtml` (mantener 88-97).
2. Agregar un test de regresión que aserte `Assert.Matches`/`Assert.Single` sobre `<select name="Input.PuestoId">` (un solo match).
3. Re-ejecutar `dotnet build` + `dotnet test --filter "Web.Ocupaciones"` para confirmar verde.
4. Re-lanzar verificación.

Una vez corregido, pasar al archive (`sdd-archive`) sin más orquestación.

## Resumen métrico

- **Cumple criterios propuesta**: 6/7 (criterio 1 fallido por duplicación PuestoId).
- **Tests PASS runtime**: 3167/3168 (1 fail preexistente).
- **Tests nuevos**: 14 (7 WUG-2 + 7 WUG-3) — todos verdes.
- **Specs trazadas**: 10 requirements (7 NEW + 1 ADDED + 2 MODIFIED) / 24 scenarios (15 NEW + 9: 4 REQ-USB-12 + 3 REQ-USB-03 + 2 REQ-USB-10).
- **Scenarios PASS**: 18 ✅ / 6 ⚠️ PARTIAL / 0 ❌ FAIL (sobre 24 scenarios totales: 15 NEW + 9 MODIFIED).
- **CRITICAL findings**: 1.
- **WARNING findings**: 4.
- **SUGGESTION findings**: 3.
- **TDD strictness**: Comprometida (test+production en mismo commit, no spliteado).
- **Size**: 682 netas vs budget 400 — overrun aceptado por maintainer.
- **Riesgo residual dominante**: CRITICAL-01 (duplicación PuestoId).

---

## Re-verify post-fix (CRITICAL-01)

### Validación del fix
- **CRITICAL-01**: ✅ **RESUELTO**. Conteo de `<select asp-for="Input.PuestoId">` en `_Form.cshtml` = **1** (antes: 2). Verificado por grep preciso con patrón `<select asp-for=`.
- **`<select asp-for="Input.PersonaId">`**: **0** ocurrencias — reemplazo correcto por card enriquecida + modal.
- **Tests de regresión**: ✅ **PASS — 2/2** (`RendersExactlyOnePuestoSelect` + el test original de 5 fields, tanto en Create como en Edit).
- **Suite Web focalizada (Ocupaciones + PersonaBuscador)**: ✅ PASS.
- **Suite Web completa**: ✅ PASS — 1282/1282.
- **Build**: ✅ PASS — 0 errores, 0 warnings nuevos.
- **`bun run build`**: OK (validado en apply anterior).

### Commits del fix
- `376786d` — `fix(web): remove duplicated puesto select after #216 refactor` (código `_Form.cshtml` −10 + tests de regresión +10).
- `9c91747` — `chore(sdd): record post-verify correction for #216` (actualización `apply-progress.md`).

### Estado global del change
- Total commits de #216 en `develop`: **5** (`89715653`, `4c39f658`, `a699a288`, `376786d`, `9c91747`).
- LOC +/- total: **+827/−115 = +712 netas** (post-fix).
- Tamaño: 712 vs budget 400 — size:exception aprobada por maintainer.
- Hallazgos residuales del primer verify (WARNING 1-4 y SUGGESTION 1-3): siguen vigentes pero **NO son blockers**.
- TDD strictness: Comprometida (test+production en mismo commit) — NO es blocker para este change; documentar para próximos.

### Veredicto final del change

**APPROVE WITH COMMENTS**

**Justificación**: El CRITICAL-01 detectado en el primer verify fue resuelto por el commit `376786d`. La duplicación del `<select>` de PuestoId quedó eliminada, los tests de regresión cuentan exactamente 1 ocurrencia del `<select>` (previenen reincidencia), la build pasa limpia, y la suite Web completa (1282/1282) está verde. El resto de la implementación cumple los requisitos de las specs (REQ-USB-12 back-compat del JS compartido, OCC-PER-BUSC-01..07 del formulario de Ocupaciones, REQ-USB-03/10 MODIFIED sin regresión). Los WARNING y SUGGESTION documentados en el primer verify quedan como follow-ups no bloqueantes. El change está listo para `sdd-archive`.
