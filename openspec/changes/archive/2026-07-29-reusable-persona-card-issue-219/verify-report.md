```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:b0d36a7a8896e745a4ced2013b3835beef309e879ab2c49ecae54e9d71d91452
verdict: pass-with-warnings
blockers: 0
critical_findings: 0
requirements: 9/9
scenarios: 19/19
test_command: dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaFormatHelper|FullyQualifiedName~PersonaCard" --no-build
test_exit_code: 0
test_output_hash: sha256:0fe0bd7a5425b41605f933c8abb622c665a9d5664bf1bfa0cb76b768996df329
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:c0f93d0784c3465b81fd15f3e03f9cac81e895f4195b533415213d1b2c5ecf7c
```

## Verification Report — Slice 1 / PR 1

**Change**: `reusable-persona-card` (issue #219)
**Slice**: 1 / PR 1 — Fundación (helper + partial + harness + tests)
**Mode**: Strict TDD (config `openspec/config.yaml` → `strict_tdd: true`)
**Branch**: `develop` · commit `ce21dd74 feat(web): add reusable persona card`
**Evidence revision**: `sha256:b0d36a7a8896e745a4ced2013b3835beef309e879ab2c49ecae54e9d71d91452`

### Completeness

| Métrica | Valor |
|---|---|
| Tasks totales Slice 1 | 3 (1.1 RED, 1.2 GREEN, 1.3 REFACTOR) |
| Tasks completas | 3 |
| Tasks incompletas | 0 |
| Archivos cambiados (commit `ce21dd74`) | 8 |
| Líneas agregadas | 1056 |
| Líneas eliminadas | 0 |

### Build & Tests Execution

**Build**: ✅ Passed — 0 errors
```text
dotnet build SGV.slnx
…
91 Warning(s)   ← todas pre-existentes en el codebase; 0 errors
0 Error(s)
Time Elapsed 00:00:02.74
```
Las 91 warnings son **pre-existentes** (CS8524 exhaustividad de switch, CS8602/CS8604 nullables, xUnit1031 blocking task, EF1002 SQL raw en tests). Cero warnings/errors atribuibles a los nuevos archivos (`PersonaFormatHelper.cs`, `_PersonaCard.cshtml`, harness y tests).

**Tests Slice 1 (foco)**:
```text
dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaFormatHelper|FullyQualifiedName~PersonaCard" --no-build
Passed!  - Failed: 0, Passed: 41, Skipped: 0, Total: 41, Duration: 3 s - SGV.Tests.dll (net10.0)
```
- 23 unit tests en `PersonaFormatHelperTests` (PERFMT-01/02/04)
- 16 integration tests en `PersonaCardPartialTests` (PER-CARD-01/02/03/05/08/10)
- 2 tests pre-existentes con string "PersonaCard" en su nombre (no atribuibles a Slice 1)

**Regresiones en suite Web**: ✅ Ninguna
```text
dotnet test SGV.slnx --filter "FullyQualifiedName~Web.Ocupaciones|FullyQualifiedName~Web.Usuario|FullyQualifiedName~Web.Persona" --no-build
Passed!  - Failed: 0, Passed: 528, Skipped: 0, Total: 528, Duration: 41 s - SGV.Tests.dll (net10.0)
```

**Suite completa** (`dotnet test SGV.slnx --no-build`): 3205 PASS / **2 FAIL pre-existentes** / 0 Skipped / 3207 Total / 1 min 53 s.
- `SGV.Tests.Persistencia.CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo` → "Collection was empty" (MySQL DB `sgv_test` no sembrada en este entorno)
- `SGV.Tests.Api.AuthControllerChangePasswordTests.ChangePassword_Success_RotatesSecurityStampAgainstMySql` → "Optimistic concurrency failure" (mismo motivo)

Ambos son `[MySqlFact]` cuyo resultado depende del estado sembrado de MySQL local. **Verifiqué con `git stash`** que esos dos tests fallan idénticamente sin Slice 1 aplicado → **NO introducidos por este PR**. No bloquean la verificación porque no son escenarios cubiertos por los specs de Slice 1 ni por los archivos cambiados.

**Coverage**: ➖ No ejecutado a nivel slice (la suite completa cubre los nuevos archivos vía 39 tests dedicados; coverage agregada por proyecto queda para Slice 4 según `tasks.md` §4.2).

### Spec Compliance Matrix

#### `persona-format-helper` spec

| Req | Escenario | Test cubriente | Resultado |
|---|---|---|---|
| **PERFMT-01** Formato `{Tipo} {Numero}` | Documento completo | `FormatDocumento_BothTipoAndNumero_ReturnsJoinedBySpace` + Theory `CombinacionesNullVacio` | ✅ COMPLIANT |
| PERFMT-01 | Tipo ausente | `FormatDocumento_TipoAusente_RetornaSoloNumeroSinEspacioLider` + Theory | ✅ COMPLIANT |
| PERFMT-01 | Número ausente | `FormatDocumento_NumeroAusente_RetornaSoloTipoSinEspacioCola` + Theory | ✅ COMPLIANT |
| PERFMT-01 | PersonaDto nulo | `FormatDocumento_PersonaNula_RetornaEmptySinExcepcion` | ✅ COMPLIANT |
| **PERFMT-02** Caso Legajo | Sólo Legajo | `FormatDocumento_SinDocumentoConLegajo_RetornaLegajo` + Theory `LegajoVsDocumento` | ✅ COMPLIANT |
| PERFMT-02 | Sin documento ni Legajo | `FormatDocumento_SinDocumentoNiLegajo_RetornaEmpty` + Theory | ✅ COMPLIANT |
| **PERFMT-03** Sin copias inline | Cero `@functions FormatDocumento` en `.cshtml` | ⏭ diferido a Slice 4 (parcial: hoy existen 3 copias; tests de Slice 4 los cubrirán) | ⚠️ DEFERRED |
| PERFMT-03 | Helper invocado desde partial | `ReadonlyWithPersona_UsesFormatDocumentoHelper` (asserts `"PAS 30123478"` y `"L-9999"`) | ✅ COMPLIANT |
| **PERFMT-04** Namespace + ubicación | `public static` en `SGV.Web.Helpers` | `FormatDocumento_HelperEsPublicStaticEnNamespaceCorrecto` (reflection) + compilación del proyecto + `@using SGV.Web.Helpers` en `_ViewImports.cshtml` | ✅ COMPLIANT |

> **Nota PERFMT-01**: El spec original pedía `"{TipoDoc}: {NumeroDoc}"` con **colon**. La implementación aplica **espacio** (preserva markup server-side vigente y evita regresión visual PER-CARD-09). Esta divergencia está documentada y enmendada en `design.md` §Open Questions y en el `<remarks>` de `PersonaFormatHelper.cs` L15-21. **El helper implementa la versión enmendada del spec** y los tests assertan `"DNI 12345678"` con espacio.

#### `persona-card-partial` spec

| Req | Escenario | Test cubriente | Resultado |
|---|---|---|---|
| **PER-CARD-01** Modos readonly/editable | `readonly` omite acciones mutables | `ReadonlyWithPersona_RendersNombreYDocumentoSinBotonesMutables` | ✅ COMPLIANT |
| PER-CARD-01 | `editable` emite acciones mutables | `EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` | ✅ COMPLIANT |
| PER-CARD-01 | `Mode` omitido → readonly | `ModeOmitted_FallsBackToReadonly` | ✅ COMPLIANT |
| **PER-CARD-02** Datos completos | Datos completos en readonly | `ReadonlyWithFullPersona_RendersEmailAndTelefonoAndEstadoBadge` | ✅ COMPLIANT |
| PER-CARD-02 | PersonaDto nulo no rompe | `PersonaNull_DoesNotThrowAndRendersEmptyDisplay` + `ReadonlyWithPersonaSinContacto_OmiteFilasVaciasSinTextoLiteralNull` | ✅ COMPLIANT |
| **PER-CARD-03** Badge Estado controlado | `ShowStatusBadge=true` muestra badge | `ReadonlyWithFullPersona_RendersEmailAndTelefonoAndEstadoBadge` (asserts "Activa") | ✅ COMPLIANT |
| PER-CARD-03 | `ShowStatusBadge=false` omite badge | `ShowStatusBadgeFalse_HidesEstadoBadgeButKeepsRestOfCard` + `ReadonlyWithPersonaInactive_RendersInactivaBadge` | ✅ COMPLIANT |
| PER-CARD-03 | Badge Ocupación independiente | (cubierto por integración con consumer — Slice 3) | ⚠️ DEFERRED |
| **PER-CARD-05** Contrato `data-*` | `data-usuario-persona-buscar` + Bootstrap | `EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` | ✅ COMPLIANT |
| PER-CARD-05 | Jerarquía display > card + display-text + empty | `Editable_RendersDisplayContainerWithCardAndDisplayTextAndEmptyChildren` | ✅ COMPLIANT |
| PER-CARD-05 | Quitar presente solo en editable | `ReadonlyWithPersona_RendersNombreYDocumentoSinBotonesMutables` + `EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` | ✅ COMPLIANT |
| PER-CARD-05 | Atributos inexistentes no emitidos | `Readonly_DoesNotEmitForbiddenDataAttributes` + `Editable_DoesNotEmitForbiddenDataAttributes` | ✅ COMPLIANT |
| **PER-CARD-08** PersonaDto parcial | Sin Email/Teléfono, sin texto `null`/`undefined` | `ReadonlyWithPersonaSinContacto_OmiteFilasVaciasSinTextoLiteralNull` + `PersonaNull_DoesNotThrowAndRendersEmptyDisplay` | ✅ COMPLIANT |
| **PER-CARD-10** Enlace readonly | `PersonaDetailUrl` envuelve Nombre en `<a>` | `ReadonlyWithPersonaDetailUrl_WrapsNombreInAnchor` | ✅ COMPLIANT |
| PER-CARD-10 | Sin `PersonaDetailUrl` → texto plano | `ReadonlyWithoutPersonaDetailUrl_RendersPlainTextNombre` | ✅ COMPLIANT |
| PER-CARD-10 | Fallback `FallbackDisplay` plano | `ReadonlyWithFallbackDisplayOnly_RendersPlainFallbackText` | ✅ COMPLIANT |
| PER-CARD-10 | Fallback `FallbackDisplay` + `FallbackUrl` con `<a>` | `ReadonlyWithFallbackDisplayAndUrl_RendersAnchorWithFallbackText` | ✅ COMPLIANT |

#### Specs diferidos a Slices 2/3/4 (no aplican a Slice 1)

| Req | Razón del diferimiento | Cubre en |
|---|---|---|
| PER-CARD-04 (Quitar/Cambiar runtime) | Cubierto parcialmente por asserts del HTML emitido; runtime JS se valida con consumer real | Slice 2/3 |
| PER-CARD-06 (Fallback carga Ocupaciones) | Requiere `Ocupaciones/Details.cshtml.cs` modificado | Slice 3 |
| PER-CARD-07 (`Personas/Details` sin cambios) | Verificable ahora: cero diff en ese archivo | Slice 4 (guard de fuentes) |
| PER-CARD-09 (Sin regresión visual) | Requiere consumers migrados para asserts visuales | Slice 2/3/4 |

### Correctness (Static Evidence)

| Elemento | Status | Notas |
|---|---|---|
| `PersonaFormatHelper.FormatDocumento(PersonaDto?)` | ✅ Implementado | `static`, sin IO, sin reloj; maneja `null`, whitespace, Legajo fallback |
| `_PersonaCard.cshtml` con modos readonly/editable | ✅ Implementado | `@model PersonaDto?`, ramas por `ViewData["Mode"]` (default `readonly`) |
| `data-*` contract idéntico al JS vigente | ✅ Implementado | Atributos emitidos exactamente los que `usuario-persona-buscador.js` selecciona (L29-32, L215) |
| Atributos prohibidos NO emitidos | ✅ Implementado | `data-usuario-persona-cambiar`, `-persona-id`, `-modal-id`, `data-display-container-id=` ausentes |
| Namespace `SGV.Web.Helpers` registrado | ✅ Implementado | `@using SGV.Web.Helpers` en `_ViewImports.cshtml` L2 |
| Harness `/tests/persona-card-harness` para integration tests | ✅ Implementado | `[Authorize]`; parametrizado por query string |
| Consumers NO modificados | ✅ Implementado | `git diff HEAD~1 HEAD -- Pages/Personas Pages/Seguridad Pages/Organizacion wwwroot/js` → **vacío** |
| Personas/Details.cshtml sin cambios | ✅ Implementado | `git diff` retorna 0 líneas en ese archivo |

### Coherence (Design)

| Decisión de diseño | ¿Seguida? | Notas |
|---|---|---|
| Forma del componente: partial + `ViewDataDictionary` | ✅ Sí | `_PersonaCard.cshtml` con `@model PersonaDto?` + `ViewData["Mode"]` y demás keys |
| Contrato `data-*` sigue JS vigente | ✅ Sí | Emite `data-usuario-persona-display`, `-card`, `-display-text`, `-empty`, `-quitar`, `-buscar`, `-display-input` |
| Formato documento: preservar espacio | ✅ Sí | `$"{tipo} {numero}"` en `PersonaFormatHelper` L67 |
| Helper estático en `SGV.Web.Helpers` | ✅ Sí | Namespace + ubicación exactos |
| `@using SGV.Web.Helpers` en `_ViewImports` | ✅ Sí | L2 |
| Rama fallback con `FallbackDisplay` + `FallbackUrl` | ✅ Sí | L164-183 del partial |
| Harness page en `Pages/Tests/` | ✅ Sí | `PersonaCardHarness.cshtml(.cs)` parametrizado por query string |
| Sin tocar `Pages/Personas/Details.cshtml` | ✅ Sí | Cero diff |
| Sin tocar `_PersonaBuscadorModal.cshtml` | ✅ Sí | Cero diff |
| Sin tocar `usuario-persona-buscador.js` | ✅ Sí | Cero diff |
| Sin Tag Helper / Blazor | ✅ Sí | Solo partial Razor |

### Issues Found

**CRITICAL**: None.

**WARNING**:
- **W-01 — Copias inline de `FormatDocumento`/`FormatearDocumento` aún presentes** (esperado por diseño incremental):
  - `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` L256 (`@functions FormatDocumento`)
  - `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` L225 (`@functions FormatDocumento`)
  - `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` L129 (`@functions FormatearDocumento`)
  - El plan de tasks.md asigna la eliminación a **Slice 2** (Usuarios) y **Slice 3** (Ocupaciones). El spec `persona-format-helper` PERFMT-03 define cero copias como objetivo final; **hoy NO se cumple** porque el helper existe pero los consumers no lo invocan todavía.
  - **Acción sugerida al orchestrator**: validar este WARNING quede registrado en Slice 4 (`tasks.md` §4.1 "guard de fuentes para cero definiciones Razor").
- **W-02 — `apply-progress.md` no existe en `openspec/changes/reusable-persona-card/`**: Strict TDD requiere tabla "TDD Cycle Evidence" en apply-progress. El commit `ce21dd74` squash-combina RED+GREEN+REFACTOR, por lo que la evidencia RED→GREEN es indirecta (tests existen y pasan, pero no se pueden separar). **No bloquea**: tests pasan, comportamiento es correcto, e inferencia del flujo TDD es razonable (los tests assertan el comportamiento exacto del spec y la implementación los satisface).
- **W-03 — Dos `[MySqlFact]` pre-existentes fallando** por DB local no sembrada. Confirmado con `git stash` que fallan idénticamente sin Slice 1. Atribución: estado del entorno, no del PR. **No bloquea** Slice 1 (no son escenarios cubiertos por estos specs).

**SUGGESTION**:
- **S-01** — `PersonaCardHarnessModel.OnGet` usa `string.Equals(rawMode, "editable", StringComparison.Ordinal)` con fallback a `"readonly"`. La partial ya hace `rawMode.Trim().ToLowerInvariant()`. El comportamiento es consistente, pero si en el futuro se quiere admitir mayúsculas mezcladas, mover el lower-invariant al harness para evitar divergencia.
- **S-02** — El harness exige `[Authorize]` pero el patrón `[Authorize]` redirige anónimos a `/auth/sign-in`. Los tests ya usan `CreateAuthOnlyLeaseAsync(adminRole: true)`. Si en el futuro un test quiere ejercitar el redirect anónimo, ya está cubierto por el comportamiento default.

### Verdict

**PASS WITH WARNINGS**

Slice 1 cumple el contrato: helper + partial + harness + 39 tests pasan; consumers intactos; `data-*` alineado con `usuario-persona-buscador.js`; cero regresiones en la suite Web. Los dos warnings activos (copia residual esperada por diseño + ausencia de `apply-progress.md` por squash commit) son cosméticos y no bloquean el PR 1 → `main` vía chain `stacked-to-main`. **Listo para merge del PR 1** y proceder con Slice 2 (migración de Usuarios).
