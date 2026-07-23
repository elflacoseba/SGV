schema: gentle-ai.verify-result/v1
evidence_revision: sha256:e42103151db7a4bd4765bbbff0d5c23b9711147cb794503cc648acb029ed21bc
verdict: fail
blockers: 1
critical_findings: 1
requirements: 3/3
scenarios: 7/7
test_command: dotnet test SGV.slnx
test_exit_code: 0
test_output_hash: sha256:6e7bc2fda0e4c28bbefd2e2e0c9bd00f490f7dbb92bb90b6a4e24bffc0ed5677
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:86c9c9425f6c963dcb3ca1cbc2d2f2e40fb42bafa6198df87c6a99a7d7a6a048

## Verification Report — Slice 2

**Cambio**: `implementa-persona-habilidades`
**Slice**: 2/4 — PR 2
**Branch**: `feat/implementa-persona-habilidades-pr2`
**Modo**: Strict TDD activo, persistencia híbrida, verificación interactiva
**Fecha**: 2026-07-21

### Resumen ejecutivo

La implementación de Slice 2 compila y todos los comandos de pruebas ejecutados terminan con código 0; la suite completa registra 2.750 aprobados, 0 fallidos y 0 omitidos en la corrida solicitada, además de tres corridas consecutivas `--no-build` con los mismos conteos. Las cinco tareas 2.1–2.5 están marcadas semánticamente como completadas mediante `Estado: ✅` y tienen evidencia TDD en `apply-progress.md`, aunque no usan checkboxes Markdown `[x]`. La interfaz, el cliente HTTP, el fake y la propagación de `ErrorCategoria` cubren el contrato de cliente de Slice 2, y la extensión de `PersonaSkillCommandResult` conserva los call sites existentes. El bridge persona-skill end-to-end no fue introducido y queda correctamente diferido a Slice 3b; los tests nuevos son unit/seam con `RecordingHandler` y guards de DI. El gate de scope exacto falla porque `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` fue modificado aunque el límite declarado permite únicamente `tests/SGV.Tests/Web/Persona/**` fuera de los artefactos SDD; por eso el veredicto global es `FAIL`/`needs-fix` pese a la evidencia funcional verde.

### Contexto de artefactos y alcance de la verificación

Se leyeron íntegramente:

- `openspec/changes/implementa-persona-habilidades/tasks.md` — presente en el branch.
- `openspec/changes/implementa-persona-habilidades/apply-progress.md` — presente en el branch, con secciones de Slice 1 y Slice 2.
- Proposal, design y specs de Slice 2 — los archivos no están presentes en el árbol actual de PR 2; se recuperaron desde el commit de artefactos `bf7651dc` y se contrastaron con Engram:
  - proposal: Engram `#1283`, `sdd/implementa-persona-habilidades/proposal`.
  - specs: Engram `#1285`, `sdd/implementa-persona-habilidades/spec`.
  - design: Engram `#1286`, `sdd/implementa-persona-habilidades/design`.
- Apply progress: Engram `#1290`, `sdd/implementa-persona-habilidades/apply-progress`.
- Decisiones Slice 2: Engram `#1295`, `sdd/implementa-persona-habilidades/slice2-decisions`.
- `AGENTS.md`, `openspec/config.yaml` y `docs/decisiones-implementacion.md`.

El CLI nativo `gentle-ai sdd-status` observa `artifactStore: openspec` y reporta proposal/specs/design ausentes porque PR 2 no los contiene en su árbol; esa limitación queda registrada como WARNING, no como una omisión de lectura: los artefactos fueron recuperados desde Engram y el objeto Git de planificación para comparar specs y diseño.

Los tres artefactos fuente recuperados contienen 10 requirements y 16 escenarios para el change completo. Esta verificación cuenta únicamente el límite de Slice 2: tres grupos de requirements de cliente/fake/taxonomía y siete escenarios observables del cliente, todos con tests pasados. Los escenarios de autorización Razor, PageModel GET/POST, PRG, persona inactiva en la UI y navegación desde Details quedan explícitamente fuera de Slice 2 y se difieren a Slice 3a/3b.

### Completitud de tareas

| Tarea | Estado semántico | Evidencia | Resultado |
|---|---|---|---|
| 2.1 | Completada | `PersonaSkillClientContractTests.cs`, commit `b9f0da2f`, 4 casos pasados | COMPLIANT |
| 2.2 | Completada | `PersonaApiClientSkillErrorsTests.cs`, commit `b9f0da2f`, 14 casos pasados | COMPLIANT |
| 2.3 | Completada | `IPersonaApiClient.cs`, commit `3664b1a9`, 3 métodos con XML docs | COMPLIANT |
| 2.4 | Completada | `PersonaApiClient.cs`, commit `3664b1a9`, 25 casos HTTP pasados | COMPLIANT |
| 2.5 | Completada | `FakePersonaApiClient.cs`, seed/llamadas/excepciones y 14 casos pasados | COMPLIANT |

**Resumen**: 5/5 tareas completadas según `tasks.md` y `apply-progress.md`. Formalmente, 0/5 usan la sintaxis `[x]`; el archivo usa `Estado: ✅`. Esto no se modificó durante la verificación y queda como WARNING según el pedido de esta ejecución.

### Estado Git y dimensión del diff

`git log develop..HEAD --oneline` devuelve exactamente tres commits de Slice 2:

1. `b9f0da2f test(slice2): add PersonaSkill client contract and error mapping tests (RED, 18 tests en 2 archivos)`
2. `3664b1a9 feat(slice2): extend IPersonaApiClient and PersonaApiClient with PersonaSkill methods (GREEN)`
3. `8f90009f docs(slice2): register Slice 2 apply-progress and mark tasks complete`

`git diff --stat develop..HEAD`:

```text
12 files changed, 1472 insertions(+), 9 deletions(-)
```

El diff de implementación (excluyendo los dos artefactos SDD modificados) suma 1.334 líneas netas, sobre el presupuesto de 400. La decisión `size:exception` fue aprobada explícitamente en Engram `#1295`; no se reabre ni se vuelve a presupuestar.

### Build y tests

#### Build

- Comando: `dotnet build SGV.slnx`
- SDK: `10.0.300` (`dotnet --version`), compatible con el requisito .NET 10.x.
- Resultado: PASS, exit code 0, 0 errors, 84 warnings, 2,54 s.
- `build_output_hash`: `sha256:86c9c9425f6c963dcb3ca1cbc2d2f2e40fb42bafa6198df87c6a99a7d7a6a048`.
- Los warnings son preexistentes/endémicos del repositorio; el warning CS8524 que aparece en `PersonaApiClient.cs` corresponde al switch legacy que ya existía, no al helper nuevo de PersonaSkill.

#### Tests solicitados

| Filtro/comando | Pass | Fail | Skipped | Tiempo | Resultado |
|---|---:|---:|---:|---:|---|
| `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaSkill"` | 77 | 0 | 0 | 3 s | PASS, pero por debajo del umbral esperado de 90+ |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaApiClient"` | 75 | 0 | 0 | 3 s | PASS |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~ApiBearerToken"` | 8 | 0 | 0 | 3 s | PASS |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Web"` | 1017 | 0 | 0 | 67 s | PASS |
| `dotnet test SGV.slnx` | 2750 | 0 | 0 | 80 s | PASS |

Hashes de los cuatro filtros y de la corrida full exacta:

- PersonaSkill: `sha256:263f922be5ec1e743ddec74c1d02c180b1115ad35d2c32e9aa0aeebde2b89c7b`.
- PersonaApiClient: `sha256:bd529e7ef2d1a3d77ba510b6faea58486f90902c29d0bfc5575bfed27e815c80`.
- ApiBearerToken: `sha256:a9d645b1f42ebf7feb5b1de978c5371654d6e4b172f8515d82a17db75283f5ba`.
- Web: `sha256:60dac34a4fd672f6f2acf15cdf8e974d44217c1d1a4c2a562282faf890391552`.
- Full: `sha256:6e7bc2fda0e4c28bbefd2e2e0c9bd00f490f7dbb92bb90b6a4e24bffc0ed5677`.

El filtro literal `PersonaSkill` devuelve 77, no 90+. Esto no representa un fallo de ejecución: los focos directos de Slice 2 se ejecutaron adicionalmente y pasaron con 4, 14 y 25 casos; `PersonaWebSeamTests` pasó 13 casos e `IPersonaApiClientContractTests` pasó 8. La diferencia del conteo se reporta porque el criterio de aceptación especificó 90+ para ese filtro, pero el resultado observable actual no lo alcanza.

#### Gate de determinismo

Se ejecutaron tres corridas consecutivas `dotnet test SGV.slnx --no-build`:

- Corrida 1: 2750/0/0, 78 s, hash `sha256:c9851a64f1305a46d18770b94f770d39cb6f26a5a261b433de614d4d4dbaf49c`.
- Corrida 2: 2750/0/0, 78 s, hash `sha256:c9851a64f1305a46d18770b94f770d39cb6f26a5a261b433de614d4d4dbaf49c`.
- Corrida 3: 2750/0/0, 80 s, hash `sha256:6af65ded54628ccae5fe9dbf5981b63e600da4fb994488af59b6138c87b3b1b1`.

Los conteos son idénticos en las tres corridas; no apareció `MSB4166`; cada corrida quedó por debajo de 15 minutos.

#### Coverage

- Comando: `dotnet test SGV.slnx --collect:"XPlat Code Coverage"`.
- Resultado: 2750/0/0, exit code 0, 85 s.
- Hash de salida: `sha256:1104af5eb4efb0a512cb577d014328a48aa60607a8a62913fe02734c18b91958`.
- Reporte: `tests/SGV.Tests/TestResults/140647cb-3a65-4e5e-a674-664d3e4f6383/coverage.cobertura.xml`.

Cobertura de archivos de producción modificados, con líneas ejecutables distintas:

| Archivo | Cobertura de línea | Líneas sin cubrir | Evaluación |
|---|---:|---|---|
| `src/SGV.Contracts/Personas/Comandos/PersonaSkillCommandResult.cs` | 100,0% | — | Excelente |
| `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs` | N/A para interfaz; la única línea instrumentada no cubierta es el alias preexistente `DeleteAsync` | Línea no ejecutable nueva | Informativa |
| `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` | 97,1% | 104, 358, 364, 386, 387, 391; las líneas 358/364 son legacy y 386/387/391 corresponden al fallback de categorías no soportadas | Aceptable |

No hay linter ni type checker configurados en `openspec/config.yaml`; el build del compilador fue la validación estática disponible.

### Matriz de cumplimiento de Slice 2

| Requirement/scenario aplicable | Test runtime | Resultado |
|---|---|---|
| REQ-WEB-04 — contrato del cliente; `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync` | `PersonaSkillClientContractTests` — 4 casos; `IPersonaApiClientContractTests` — guard de 12 métodos | COMPLIANT |
| REQ-WEB-04 — fake con seed, llamadas y ausencia de HTTP | `PersonaApiClientSkillErrorsTests` — seed/defaults y listas `GetSkillsCalls`, `SkillUpsertCalls`, `SkillDeleteCalls` | COMPLIANT |
| REQ-WEB-04 — rutas y verbos HTTP del subrecurso | `PersonaSkillApiClientTests` — GET/PUT/DELETE contra `RecordingHandler` | COMPLIANT |
| REQ-WEB-04 — wire de lectura anidado y request de escritura `nivelId` | `PersonaSkillJsonCompatibilityTests` de Slice 1 en regresión + `PersonaSkillApiClientTests` de payload/respuesta | COMPLIANT para el cliente; la migración fuente es responsabilidad de Slice 1 |
| REQ-WEB-05 — `ValidationProblemDetails` con `FieldErrors` | `UpsertSkillAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors` | COMPLIANT |
| REQ-WEB-05 / TAXO-02 — NotFound, Validation, Unauthorized, Forbidden y Transport | `PersonaSkillApiClientTests` + `PersonaApiClientSkillErrorsTests`; todas las clases focalizadas pasan | COMPLIANT para la traducción del cliente |
| TAXO-02/TAXO-03 — `DeleteResultMapper`, `Categoria`, `StatusCode` y mapper común | Tests de DELETE 204/400/404/401/403/5xx y `ToSkillCommandResultAsync` | COMPLIANT |

**Resumen de alcance**: 3 requirements de cliente/fake/taxonomía y 7 escenarios observables del cliente cubiertos en runtime. Los escenarios completos de UI para autorización, carga del PageModel, PRG, persona inactiva, feedback `TempData` y navegación desde Details no se reclaman en Slice 2 y quedan SKIPPED/DEFERRED a Slice 3a/3b.

### Correctness (evidencia estática)

| Área | Estado | Evidencia |
|---|---|---|
| Contrato de interfaz | Implementado | Los tres métodos públicos tienen tipos, parámetros y `CancellationToken` esperados; guard de 12 métodos pasa. |
| Cliente HTTP GET | Implementado | Ruta `/api/v1/personas/{personaId}/skills`; 404 devuelve lista vacía; otros errores se propagan. |
| Cliente HTTP PUT | Implementado | Ruta `/api/v1/personas/{personaId}/skills/{skillId}`; serializa `AsignarPersonaSkillRequest`; 2xx con DTO devuelve Success; body vacío devuelve Failure `Validation/EmptyBody`. |
| Cliente HTTP DELETE | Implementado | Usa `DeleteResultMapper`, conserva `StatusCode`, `Code`, `Message` y `Categoria`. |
| Fake | Implementado | Seed configurable, registro de identificadores/tuplas y excepciones nativas propagadas sin HTTP. |
| FieldErrors | Implementado | `PersonaSkillCommandResult` acepta diccionario opcional y conserva la sobrecarga `Failure(error)`. |
| Fuera de Slice 2 | Diferido | No se implementaron PageModel, vistas, handlers POST, PRG ni Details. |

### Coherencia con el diseño

| Decisión de diseño | Seguida | Evidencia |
|---|---|---|
| Cliente tipado en `SGV.Web.Integration.Personas` | Sí | Solo `IPersonaApiClient`/`PersonaApiClient` reciben el subrecurso. |
| Delegar errores en `CommandResultMapper`/`DeleteResultMapper` | Sí | `ToSkillCommandResultAsync` y `DeleteSkillAsync` delegan en mappers comunes; no hay matriz HTTP paralela nueva. |
| Preservar wire JSON | Sí | Tests de DTO/request de regresión y seam HTTP pasan; no se agregan campos de Cargo (`Ponderacion`, `EsObligatoria`, `NivelRequeridoId`). |
| Bridge cookie → JWT | Sí, limitado | La producción conserva `ApiBearerTokenHandler`; el guard de registro pasa. La comunicación end-to-end persona-skill está diferida. |
| No tocar API, Pages, Dominio ni Infraestructura | Sí | El diff solicitado sobre esas raíces está vacío. |
| Presupuesto de review | Excepción aprobada | Engram `#1295` aprueba `size:exception` para 1.334 líneas netas de implementación frente a 400. |

### Strict TDD

| Check | Resultado | Evidencia |
|---|---|---|
| Evidencia TDD reportada | PASS | `apply-progress.md` contiene tabla `TDD Cycle Evidence` para 2.1–2.5. |
| Archivos de test RED presentes | PASS | Existen `PersonaSkillClientContractTests.cs`, `PersonaApiClientSkillErrorsTests.cs` y `PersonaSkillApiClientTests.cs`. |
| GREEN confirmado en runtime | PASS | 4/14/25 casos focalizados pasan; suite full pasa. |
| Triangulación | PASS | 2.1: 4 guards; 2.2: 14 casos; 2.4/2.5: 25 casos HTTP; las 3 filas de transporte se parametrizan con `MemberData`. |
| Safety net | PASS | `apply-progress` reporta 18/18 para los tests modificados; los archivos nuevos se declaran N/A (new). |
| Assertion quality | PASS | No hay tautologías, ghost loops, mocks huérfanos ni smoke tests. Los `Assert.Empty` de Slice 2 tienen tests compañeros con resultados no vacíos. |

**TDD Compliance**: PASS para las cinco tareas. Test distribution: 43 unit tests nuevos en tres archivos de comportamiento/contrato y 2 guards de integración en `PersonaWebSeamTests`; no hay tests E2E nuevos.

### Decisiones Slice 2

1. **`size:exception`** — Aprobada en Engram `#1295`: 1.334 líneas netas de implementación frente al presupuesto 400, justificadas por paridad con `CargoSkillApiClientTests`. No se reabre.
2. **`PersonaSkillCommandResult.FieldErrors` + overload** — Aprobada y verificada como source-compatible dentro del repositorio: el baseline en `develop` conserva constructor posicional de tres argumentos y `Failure(error)`; HEAD agrega `FieldErrors = null` y mantiene la overload simple, además de agregar `Failure(error, fieldErrors)`. Los tests preexistentes que consumen estos records y los tests API/Aplicación de Slice 1 siguen compilando y la suite completa pasa 2750/0/0 sin cambios en esos archivos. No se observa breaking change en los call sites del repositorio.
3. **Bridge end-to-end diferido a Slice 3b** — Correctamente respetada en este slice. `SgvWebApplicationFactory` queda preparado con `personaApiHandler`, pero no existe un test HTTP end-to-end persona-skill; `PersonaSkillApiClientTests` usa `RecordingHandler` y `PersonaWebSeamTests` solo verifica DI/reflection. La cobertura real cookie→JWT para este subrecurso queda para Slice 3b.

### Scope sanity

#### Gate de capas prohibidas

El comando solicitado devuelve salida vacía:

```text
git diff --stat develop..HEAD -- src/SGV.Api/ tests/SGV.Tests/Api/ src/SGV.Web/Pages/ src/SGV.Dominio/ src/SGV.Infraestructura/
```

Por lo tanto no se tocaron API, tests de API, Pages, Dominio ni Infraestructura. Este checklist de capas es PASS.

#### Gate de archivos permitidos

El diff de implementación incluye:

- Permitidos: `src/SGV.Contracts/Personas/Comandos/PersonaSkillCommandResult.cs`, `src/SGV.Web/Integration/Personas/**`, `tests/SGV.Tests/Web/Persona/**`.
- Fuera del límite literal declarado: `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` (+25 líneas), que no está bajo `tests/SGV.Tests/Web/Persona/**`.
- Artefactos SDD esperados: `openspec/.../tasks.md` y `openspec/.../apply-progress.md`.

La extensión del factory es de test infrastructure, no producción, y está mencionada en el apply progress; aun así, el límite de scope entregado para esta verificación es literal. No se puede marcar el scope exacto como respetado sin una excepción explícita o sin mover esa preparación a Slice 3b.

### Revisión de calidad

- Indentación: 4 espacios en el código nuevo.
- Miembros públicos: PascalCase.
- Métodos asíncronos: sufijo `Async`.
- No se introducen DTO tests triviales, getters/setters tests ni mappers vacíos en los tests nuevos; los tests nuevos ejercitan contratos, rutas, payloads, resultados y fallas observables.
- `git diff --check`: sin errores de whitespace.
- Commits: convencionales, exactamente 3 commits Slice 2.
- `Co-Authored-By`: no aparece en los mensajes/cuerpos de los 3 commits.
- Secret scan local sobre el diff de implementación: no encontró patrones de private key, AWS key, GitHub token, JWT ni connection-string password.

### Hallazgos

#### CRITICAL

1. **Scope fuera del límite literal — `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs`**
   - **scenario_or_task**: Sanidad de scope / Slice 2.
   - **evidence**: `git diff --name-only develop..HEAD` muestra el archivo y el diff agrega el parámetro/campo `personaApiHandler` y la registración typed-client. El límite declarado permite `tests/SGV.Tests/Web/Persona/**`, no la raíz `tests/SGV.Tests/Web/`.
   - **suggested_fix**: Antes del PR, decidir una excepción de scope explícita o retirar/mover esta preparación al Slice 3b, donde se materializará el bridge end-to-end. El agente de verificación no modificó el archivo.

#### WARNING

1. **Tasks sin checkboxes Markdown**
   - **scenario_or_task**: Completitud 2.1–2.5.
   - **evidence**: `tasks.md` usa `Estado: ✅` en cada tarea, pero no contiene `[x]`; el `gentle-ai sdd-status` nativo también informa que `tasks.md` no tiene markdown task checkboxes.
   - **suggested_fix**: Normalizar la representación de completion a `[x]` en el próximo paso SDD, sin cambiar el estado semántico ya documentado.

2. **Filtro `PersonaSkill` debajo del conteo esperado**
   - **scenario_or_task**: Test runner requerido.
   - **evidence**: `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaSkill"` pasó 77/0/0, mientras el criterio operativo esperaba 90+; los focos directos pasan 4/14/25 y la suite completa pasa 2750/0/0.
   - **suggested_fix**: Confirmar si 90+ es un conteo conceptual de todos los tests PersonaSkill o un umbral literal del filtro FQN; si es literal, ajustar el filtro/criterio o agregar cobertura solo si falta comportamiento real. No se infiere un fallo funcional a partir del conteo.

3. **Artefactos de planificación no están en el árbol de PR 2**
   - **scenario_or_task**: Persistencia híbrida / status SDD.
   - **evidence**: `proposal.md`, `design.md` y `specs/persona-skill-web-management/spec.md` no existen en el árbol actual; se recuperaron desde Engram `#1283/#1285/#1286` y el commit `bf7651dc`. `gentle-ai sdd-status` queda bloqueado en modo openspec.
   - **suggested_fix**: Confirmar que el workflow stacked/hybrid acepta que PR 2 transporte solo `tasks.md`/`apply-progress.md` y que la fuente Engram sea la autoridad compartida; de lo contrario, incorporar los artefactos de planificación en la base del PR sin duplicarlos.

#### SUGGESTION

1. `ProductionRegistration_PersonaApiClient_SubresourceSkillMethodsResolve` verifica tres métodos por reflection pero no llama a `GetRequiredService<IPersonaApiClient>`; el test previo `ProductionRegistration_ResolvesPersonaApiClient` sí valida la resolución. Renombrar el guard o resolver explícitamente el servicio mejoraría la correspondencia entre nombre y aserción.
2. La coverage del archivo `PersonaApiClient.cs` es 97,1%; las líneas no cubiertas nuevas son solo el fallback de categorías no soportadas (`MapCategoriaToLegacySkillType`). No es un bloqueo y el repo no exige umbral adicional.

### Compliance checklist

| Check | Resultado | Evidencia |
|---|---|---|
| Strict TDD | Sí | `strict_tdd: true`; runner xUnit disponible; RED/GREEN, triangulación, safety net y assertion audit verificados. |
| Scope por capas: no API/Dominio/Infraestructura/Pages | Sí | El diff stat solicitado para esas raíces está vacío. |
| Scope exacto de archivos permitidos | No | `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` queda fuera de `tests/SGV.Tests/Web/Persona/**`. |
| `size:exception` documentado | Sí | Engram `#1295`, 1.334 líneas netas de implementación frente a 400, aprobación explícita. |
| `PersonaSkillCommandResult` source-compatible | Sí | `FieldErrors` opcional, `Failure(error)` preservada, overload nueva; build y suite preexistente 2750/0/0. |
| Bridge diferido correctamente | Sí | Sin test HTTP E2E persona-skill; solo seam `RecordingHandler` y guards DI/reflection. |
| Sin `Co-Authored-By` | Sí | Scan de los tres commits: no encontrado. |
| Sin secretos detectables | Sí | Scan de patrones sobre el diff: ninguno encontrado. |

### Veredictos por bloque

| Bloque | Veredicto |
|---|---|
| Slice 2 funcional | ready-to-merge |
| Tests/runtime | ready-to-merge con WARNING de conteo del filtro literal |
| Wire/compatibilidad | ready-to-merge |
| Código/calidad | ready-to-merge |
| Commits | ready-to-merge |
| Decisiones Slice 2 | ready-to-merge |
| Scope exacto | needs-fix |
| Estado SDD de artefactos locales | needs-fix o confirmación del workflow híbrido |

### Resultado del executor

```yaml
status: failed
executive_summary: "La implementación y la evidencia runtime de Slice 2 son verdes, pero el gate literal de scope falla por una modificación fuera de tests/SGV.Tests/Web/Persona y quedan warnings de representación SDD/conteo de filtro."
artifacts:
  - openspec/changes/implementa-persona-habilidades/verify-report-slice2.md
  - engram topic sdd/implementa-persona-habilidades/verify-report-slice2 (Engram #1296)
verdict:
  slice: ready-to-merge
  tests: ready-to-merge
  wire: ready-to-merge
  codigo: ready-to-merge
  commits: ready-to-merge
  decisiones: ready-to-merge
  scope: needs-fix
next_recommended: needs-fix
findings:
  - severity: CRITICAL
    scenario_or_task: "Sanidad de scope"
    evidence: "tests/SGV.Tests/Web/SgvWebApplicationFactory.cs fuera del límite literal"
    suggested_fix: "Mover/preparar en Slice 3b o aprobar excepción explícita"
  - severity: WARNING
    scenario_or_task: "Tasks 2.1–2.5"
    evidence: "Estado ✅ sin [x]"
    suggested_fix: "Normalizar checkboxes en el flujo SDD"
  - severity: WARNING
    scenario_or_task: "Filtro PersonaSkill"
    evidence: "77 pass, no 90+"
    suggested_fix: "Confirmar criterio conceptual frente a filtro FQN literal"
compliance_checklist:
  strict_tdd: "sí"
  scope_layers: "sí"
  scope_exact: "no"
  size_exception: "sí"
  field_errors_source_compat: "sí"
  bridge_deferred: "sí"
  no_co_authored_by: "sí"
open_questions:
  - "¿Se aprueba explícitamente SgvWebApplicationFactory.cs como excepción de infraestructura de tests o se difiere a Slice 3b?"
  - "¿El conteo 90+ debe ser literal para el filtro FQN o conceptual sobre las suites de Slice 1+2?"
  - "¿La persistencia híbrida permite que PR 2 no transporte proposal/spec/design en el árbol local?"
skill_resolution: paths-injected
```

### Veredicto final

**FAIL — needs-fix**. La implementación funcional de Slice 2 está lista y verificada, pero no puede declararse lista para mergear bajo el gate literal de scope hasta resolver `SgvWebApplicationFactory.cs` fuera de la raíz permitida o aprobar una excepción explícita.