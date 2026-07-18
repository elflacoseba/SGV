# Verify Report — 2026-07-17-detalles-usuario-persona-enriched-card

## Verdict

**PASS** 🎯

## Resumen ejecutivo

El PR #169 satisface los 6 escenarios del delta spec (`REQ-ULD-04` MODIFIED) con cobertura completa. Build limpio (0 errores, 23 warnings **pre-existentes** en archivos no tocados), 2464/2464 tests verdes — incluyendo los 5 tests nuevos y los 14 `MySqlFact` (la nota de regresión MySQL del PR body ya no aplica: la corrida limpia pasa sin warnings). Governance limpia (no hay review lineage activo para este change — `gentle-ai review validate --gate pre-commit` reporta `facade review receipt is not available`, equivalente a estado **missing**, NO bloqueante per protocolo del orquestador). Non-goals respetados.

## Trazabilidad de escenarios (delta spec)

| # | Escenario | Cobertura | Evidencia |
|---|-----------|-----------|-----------|
| 1 | Detalle existente muestra campos legibles y retorno preservado | ✅ COMPLIANT | `Details.cshtml` líneas 65-77 (campos legibles `Usuario`, `Email`, `Nombres`, `Apellidos` con fallback `—`) + línea 228-230 (link "Volver al listado"). Helper `BuildIndexUrl()` (línea 216) y `BuildContextUrl()` (línea 226) intactos: `git diff` confirma sólo cambios en comentarios XML-doc, ningún cambio de firma. Tests pre-existentes `Get_Details_WhenAuthenticatedAsRegularUser_RendersReadonlyUserData` (líneas 25-49) y `Get_Details_WhenListingContextProvided_PreservesItInBackLink` (líneas 154-180) verdes en runtime. |
| 2 | Identificador no consultable produce estado recuperable | ✅ COMPLIANT | `Details.cshtml` líneas 21-42: bloque `IsNotFound` con "El usuario solicitado no está disponible" + "Volver al listado". `Details.cshtml.cs` líneas 146-150 mantiene `IsNotFound = true` + `LogWarning` estructurado. Test pre-existente `Get_Details_WhenUserIsNotFound_ShowsRecoverableState` (líneas 115-131) verde. |
| 3 | Persona enriquecida visible cuando el API devuelve DTO | ✅ COMPLIANT | Test `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedCard` (líneas 349-391): asserta `data-usuario-persona-card`, `L-7777`, `DNI 30123456`, email, teléfono, badge `Activa`, `href="/personas/detalle/{personaId:D}"` y ausencia de Guid crudo. **PASÓ**. Cobertura de los 7 campos del spec: `Apellidos+Nombres` (h6 título), `Legajo`, `Documento` (`TipoDocumento NumeroDocumento` vía `FormatDocumento`), `Email`, `Teléfono`, `Estado` (badge), `PersonaId` (href). |
| 4 | Fallback plano cuando el API devuelve 404 | ✅ COMPLIANT | Test `Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` (líneas 452-489): `FakePersonaApiClient` vacío (sin DTOs) → asserta `data-usuario-details-persona`, `García, Ana`, ausencia de `data-usuario-persona-card`, href preservado, ausencia de Guid crudo, "Detalle de usuario" presente (NO "no está disponible"). **PASÓ**. `Details.cshtml` líneas 141-143 implementan el fallback. |
| 5 | Fallback plano sin IsNotFound ante error de transporte | ✅ COMPLIANT | Test `Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` (líneas 491-521): `FakePersonaApiClient.GetByIdException = new HttpRequestException(...)` → asserta fallback plano + `DoesNotContain("no está disponible")` + "Detalle de usuario" presente. **PASÓ**. `Details.cshtml.cs` líneas 198-205 implementan el catch `TransportFailureClassifier.IsTransportFailure` + `LogWarning` estructurado + `PersonaVinculada = null` (sin tocar `IsNotFound`). |
| 6 | Detalle sin controles de selección de persona | ✅ COMPLIANT | Dos tests triangulan ambas ramas: `Get_Details_WhenPersonaApiReturnsDto_NoControlesSeleccionPersona` (líneas 397-422) y `Get_Details_WhenPersonaApiMissing_NoControlesSeleccionPersona` (líneas 424-444). Cada uno asserta `DoesNotContain` sobre `data-usuario-persona-quitar`, `data-usuario-persona-buscar` y `usuario-persona-buscador-modal`. **AMBOS PASARON**. Verificación adicional por grep en `Details.cshtml` y `DetailsPageTests.cs` no encuentra ninguno de los 3 selectores prohibidos. |

**Compliance summary**: 6/6 escenarios compliant con cobertura de runtime ✅

## Tests

### Filtro DetailsPageTests

```text
Passed!  - Failed:     0, Passed:    35, Skipped:     0, Total:    35, Duration: 6 s - SGV.Tests.dll (net10.0)
```

35/35 verdes (30 pre-existentes + 5 nuevos: `WhenPersonaApiReturnsDto_RendersEnrichedCard`, `WhenPersonaApiReturnsDto_NoControlesSeleccionPersona`, `WhenPersonaApiMissing_NoControlesSeleccionPersona`, `WhenPersonaApiReturns404_FallsBackToPlainDisplay`, `WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound`).

### Filtro Usuarios

```text
Passed!  - Failed:     0, Passed:    82, Skipped:     0, Total:    82, Duration: 1 s - SGV.Tests.dll (net10.0)
```

82/82 verdes. Sin regresiones.

### Filtro MySqlFact

```text
Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 1 s - SGV.Tests.dll (net10.0)
```

14/14 verdes. La nota del PR body sobre `UsuariosEndToEndMySqlFactTests.Bloquear_AnotherUser_Returns200WithBloqueadoTrue` por colisión de username residual (`target-bloq-200-678`) **ya no aplica** en esta corrida: el dato residual se limpió o nunca estuvo presente. MySQL local en `localhost:3306` alcanzable.

### Suite completa

```text
Passed!  - Failed:     0, Passed:  2464, Skipped:     0, Total:  2464, Duration: 1 m 6 s - SGV.Tests.dll (net10.0)
```

2464/2464 verdes, 0 fallados, 0 skipped. **Sin regresiones**.

## Build

```text
Build succeeded.
    23 Warning(s)
    0 Error(s)
```

0 errores. Los 23 warnings son **pre-existentes** (no introducidos por este change):
- 6× `CS8524` en `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs` (enum switch no exhaustivo, pre-existente).
- 1× `CS8524` en `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs`.
- 1× `CS8524` en `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`.
- 1× `CS8524` en `src/SGV.Web/Integration/Personas/PersonaApiClient.cs`.
- 1× `CS8524` en `src/SGV.Web/Integration/Usuarios/UsuarioApiClient.cs`.
- 1× `CS8524` en `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs`.
- 1× `CS8524` en `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs`.
- 3× `CS8602`/`CS8604` en `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/{Details,Index,Edit}.cshtml.cs` y `Seguridad/Usuarios/Index.cshtml.cs`.
- 1× `CS8625` en `tests/SGV.Tests/Aplicacion/Seguridad/UsuarioContractsTests.cs`.
- 2× `EF1002` en `tests/SGV.Tests/Persistencia/BloquearDesbloquearEliminarGatewayTests.cs`.
- 1× `xUnit1026` en `tests/SGV.Tests/Web/Common/CommandResultMapperTests.cs`.
- 2× `xUnit2029` en `tests/SGV.Tests/Persistencia/SgvIdentityUserConfiguracionTests.cs`.

**Cero warnings nuevos en archivos tocados por el change** (`Details.cshtml`, `Details.cshtml.cs`, `FakePersonaApiClient.cs`, `DetailsPageTests.cs`).

## Validación operativa

### PR #169 contra `develop`

- **Estado**: `OPEN`, `mergeable: "MERGEABLE"`, base `develop`, head `feat/detalles-usuario-persona-enriched-card`.
- **Body**: ✅ referencia PR #168 como antecedente ("Antecedente: PR #168 (`feat(web): block self-role edit + enrich persona card on edit form`)..."), ✅ lista los 4 escenarios cubiertos ("1. Card enriquecida con DTO completo...", "2. Fallback 404...", "3. Fallback de transporte...", "4. Sin controles de selección..."), ✅ incluye rollback trivial.
- **Archivos**: 8 (4 productivos + 4 SDD), +689/-1 líneas.
  - Productivos: `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml`, `Details.cshtml.cs`, `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs`, `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs`.
  - SDD: `openspec/changes/2026-07-17-detalles-usuario-persona-enriched-card/{proposal,design,tasks}.md` + `specs/usuario-web-listado-detalle-baja/spec.md`.
- ✅ Sin archivos no esperados (3 commits separados: `feat`, `chore` SDD artifacts, `chore` mark tasks complete).

### Governance / Receipt state

```text
$ gentle-ai review validate --gate pre-commit --cwd /Users/elflacoseba/Source/SGV
Error: multiple compact facade review lineages found; specify --lineage

$ gentle-ai review status --cwd /Users/elflacoseba/Source/SGV
[...14 lineages en estado mixto: approved/superseded/active/correction_required/invalid...]

$ gentle-ai review validate --gate pre-commit --cwd /Users/elflacoseba/Source/SGV --lineage <id>
Error: facade review receipt is not available
```

**Estado del receipt**: **MISSING** (no bloqueante per protocolo del orquestador).

- El binario `gentle-ai` está disponible en `/opt/homebrew/bin/gentle-ai`.
- `review status` muestra 14 lineages históricos. **Ninguno corresponde al change `2026-07-17-detalles-usuario-persona-enriched-card`** — los 3 lineages activos (`review-07f93f1366a3d7c6`, `review-b493c23be0f7a0a6`, `review-e636057aa563610c`) cubren otros cambios:
  - `07f93f1366a3d7c6`: `current-changes` (working tree limpio, paths vacío).
  - `b493c23be0f7a0a6` y `e636057aa563610c`: `2026-07-17-buscador-personas-modal` (otro PR).
- Validar con cualquier lineage activo devuelve `facade review receipt is not available` (no hay receipt terminal para validar el pre-commit del branch actual).
- Per protocolo del orquestador ("NO crees un review nuevo"), no se invoca `gentle-ai review start`. El orquestador decide si arranca el governance como prerrequisito del merge.

## Non-goals respetados

| Non-goal | Estado | Evidencia |
|----------|--------|-----------|
| `_Form.cshtml` no modificado | ✅ | `git diff develop..HEAD -- src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` → vacío. |
| `Index.cshtml` no modificado | ✅ | `git diff develop..HEAD -- src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` → vacío. |
| `SGV.Api` no modificado | ✅ | `git diff develop..HEAD -- src/SGV.Api/` → vacío. |
| Spec `usuario-web-crear-editar` no modificado | ✅ | `git diff develop..HEAD -- openspec/specs/usuario-web-crear-editar/` → vacío. |
| Spec `usuario-web-selector-persona-buscador` no modificado | ✅ | `git diff develop..HEAD -- openspec/specs/usuario-web-selector-persona-buscador/` → vacío. |
| Sin `data-usuario-persona-quitar` en `Details.cshtml` | ✅ | grep en `Details.cshtml` no encuentra el atributo. |
| Sin `data-usuario-persona-buscar` en `Details.cshtml` | ✅ | grep en `Details.cshtml` no encuentra el atributo. |
| Sin `#usuario-persona-buscador-modal` en `Details.cshtml` | ✅ | grep en `Details.cshtml` no encuentra el id. |

## Validación contra código real

### `Details.cshtml.cs`

- ✅ `IPersonaApiClient personaApiClient` inyectado en primary constructor (línea 44).
- ✅ `public PersonaDto? PersonaVinculada { get; private set; }` (línea 67).
- ✅ `public string? PersonaDisplay { get; private set; }` (línea 74).
- ✅ `TryLoadPersonaVinculadaAsync(Guid, CancellationToken)` (líneas 183-207):
  - ✅ Guarda `Guid.Empty` (línea 187-190): early-return sin tocar el API.
  - ✅ `try/catch` sobre `TransportFailureClassifier.IsTransportFailure` (línea 198).
  - ✅ `logger.LogWarning(ex, "...{PersonaId} for detail page; falling back to PersonaDisplay.", personaId)` estructurado (líneas 201-204).
- ✅ Llamada al helper en `OnGetAsync` tras `GetByIdAsync` exitoso (líneas 154-155): `PersonaDisplay = FormatPersonaDisplay(...); await TryLoadPersonaVinculadaAsync(...)`.
- ✅ `FormatPersonaDisplay(string?, string?)` espejo de Edit (líneas 209-214).
- ✅ `IsNotFound` intacto: la firma y semántica NO cambiaron (sólo un comentario XML-doc actualizado que referencia el helper nuevo, sin cambio de comportamiento).

### `Details.cshtml`

- ✅ Bloque enriquecido con `data-usuario-persona-card` (línea 92), árbol DOM `card border mb-0` > `card-body` > `dl.row.mb-0` + `dt.col-sm-3`/`dd.col-sm-9` (líneas 92-133).
- ✅ Bloque fallback con `data-usuario-details-persona` (línea 141).
- ✅ `<a href="/personas/detalle/@Model.Usuario.PersonaId">` en ambas ramas (líneas 95 y 142).
- ✅ Badge `Activa`/`Inactiva` con `badge-soft-success`/`badge-soft-secondary` (líneas 122-129).
- ✅ `@functions { FormatDocumento(PersonaDto?) }` local espejo de `_Form.cshtml` (líneas 256-284).
- ✅ NO contiene `data-usuario-persona-quitar`, `data-usuario-persona-buscar`, ni `#usuario-persona-buscador-modal`.

### `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs`

- ✅ Test 1 (happy path): `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedCard` (líneas 349-391) — asserta los 7 campos (`L-7777`, `DNI 30123456`, `ana.garcia@example.com`, `+54 11 5555-0000`, `Activa`, `Apellidos, Nombres` título, `<a href="/personas/detalle/{pid}">`) + `data-usuario-persona-card`.
- ✅ Test 2 (fallback 404): `Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` (líneas 452-489) — asserta `data-usuario-details-persona`, `García, Ana`, ausencia de `data-usuario-persona-card`, href preservado, NO "no está disponible".
- ✅ Test 3 (transporte): `Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` (líneas 491-521) — asserta fallback plano + `DoesNotContain("no está disponible")`.
- ✅ Test 4 (NoControles enriched): `Get_Details_WhenPersonaApiReturnsDto_NoControlesSeleccionPersona` (líneas 397-422).
- ✅ Test 5 (NoControles fallback): `Get_Details_WhenPersonaApiMissing_NoControlesSeleccionPersona` (líneas 424-444).
- ✅ Overload `BuildUsuario(string id, Guid personaId, bool bloqueado = false)` agregado (líneas 334-342) como espejo de `EditPageTests.cs`.

### `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs`

- ✅ `GetByIdException` property agregada (líneas 105-112).
- ✅ `GetByIdAsync` lanza la excepción ANTES de la lógica de lookup (líneas 176-179).

## TDD Compliance (Strict TDD)

Strict TDD Mode está activo. `apply-progress` no existe como artefacto separado (la pista está embebida en `tasks.md`), pero las 9 tasks están marcadas `[x]` y los tasks RED/GREEN son explícitos:

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reportado | ✅ | `tasks.md` declara RED/GREEN explícitamente por task (T-01 RED, T-02 GREEN-IMPL, T-03 RUN, T-04 RED, T-05 GREEN-IMPL, T-06 RUN, T-07 COMMIT, T-08 CHORE-SDD, T-09 PR). |
| Todos los tasks con tests | ✅ | 9/9 tasks marcados `[x]`. |
| RED confirmado (tests existen) | ✅ | Los 5 tests están físicamente en `DetailsPageTests.cs` y son localizables. |
| GREEN confirmado (tests pasan) | ✅ | 5/5 tests pasaron en la corrida filtrada `FullyQualifiedName~DetailsPageTests`. |
| Triangulación adecuada | ✅ | 5 tests cubren 6 escenarios: 1 happy path (esc. 3), 2 fallbacks (esc. 4 + 5), 2 variantes NoControles (esc. 6 triangulada en ambas ramas). Los escenarios 1 y 2 están cubiertos por tests pre-existentes. |
| Safety Net para archivos modificados | ✅ | 4 archivos modificados (2 productivos + 2 tests). Los archivos productivos tienen suite de tests pre-existente que pasó sin regresión. |

**TDD Compliance**: 6/6 checks passed.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 0 | 0 | — |
| Integration | 5 nuevos | 1 (`DetailsPageTests.cs`) | WebApplicationFactory + FakeApiClients |
| E2E | 0 | 0 | — |
| **Total nuevos** | **5** | **1** | |

Layer correcto: tests de integración web que triangular el renderizado HTML con clientes API falsos. No se requieren tests unitarios (helper trivial, mirror de Edit ya testeado).

## Assertion Quality

| Test | Línea | Assertion | Observación |
|------|-------|-----------|-------------|
| `WhenPersonaApiReturnsDto_RendersEnrichedCard` | 378-388 | 6 `Assert.Contains` + 1 `Assert.DoesNotContain` | Verifica contenido observable del HTML rendereado, no clases CSS internas. ✅ |
| `WhenPersonaApiReturnsDto_NoControlesSeleccionPersona` | 419-421 | 3 `Assert.DoesNotContain` | Triangulación de rama enriquecida para el escenario 6. ✅ |
| `WhenPersonaApiMissing_NoControlesSeleccionPersona` | 441-443 | 3 `Assert.DoesNotContain` | Triangulación de rama fallback para el escenario 6. ✅ |
| `WhenPersonaApiReturns404_FallsBackToPlainDisplay` | 471-488 | 6 assertions (Contains + DoesNotContain) | Cubre escenario 4 con valor positivo y negativo. ✅ |
| `WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` | 512-520 | 5 assertions (Contains + DoesNotContain) | Cubre escenario 5 con valor positivo y negativo. ✅ |

**Sin tautologías, sin ghost loops, sin smoke tests**. Todas las assertions validan comportamiento observable del HTML rendereado.

**Assertion quality**: ✅ All assertions verify real behavior.

## Issues encontrados

**CRITICAL**: ninguno.

**WARNING**: ninguno del change. Nota informativa: el cuerpo del PR menciona "2463 pass / 1 fail" por un `MySqlFact` con colisión residual — esa nota está desactualizada; en la corrida actual todos los 2464 tests pasan. Esto NO afecta el verdict.

**SUGGESTION**: considerar actualizar el body del PR para reflejar que la corrida actual es 2464/2464 verde (la nota del MySQL residual ya no aplica).

## Recomendación

**Proceder con `sdd-archive`** — el change satisface:
1. Los 6 escenarios del delta spec con cobertura de runtime.
2. Las decisiones del design (espejo 1-a-1 de Edit, sin refactor compartido, atributos data correctos, no marcar IsNotFound).
3. Los 5 tests nuevos + 30 tests pre-existentes del módulo `DetailsPageTests`.
4. Los non-goals: ningún archivo fuera de scope fue tocado.
5. Build limpio sin warnings nuevos.

El orquestador debe decidir si:
- (a) Arranca el governance con `gentle-ai review start` como prerrequisito del merge (estado actual `missing` no es bloqueante per protocolo).
- (b) Invoca `sdd-archive` directamente para promover el delta al main spec.
- (c) Hace un fix menor al PR body para reflejar 2464/2464 verde.

---

## Envelope estricto (sdd-verify report-format)

```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:{digest of this report}
verdict: pass
blockers: 0
critical_findings: 0
requirements: 1/1
scenarios: 6/6
test_command: dotnet test SGV.slnx --no-build --nologo
test_exit_code: 0
test_output_hash: sha256:d60aa4e9797163e8704e95e1b07cb38b0b564afc3ea997aec16b023f685aae6e
build_command: dotnet build SGV.slnx --nologo -v minimal
build_exit_code: 0
build_output_hash: sha256:318f41ca4a36978ab56341036f24f8b04c4aa1a178adda9e51981b3effb9449e
```
