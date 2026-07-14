# Tasks: Alineación doc/modelo de unicidad de Ocupaciones (issue #127)

> **Change**: `2026-07-13-fix-127-doc-ocupaciones-unicidad-persona`
> **Modo**: hybrid (Engram + filesystem)
> **Strict TDD**: ACTIVO. Cada tarea de código sigue RED → GREEN → REFACTOR.
> **Aceptación**: la prosa de `docs/decisiones-implementacion.md` declara los DOS invariantes vigentes (`ActivePuestoIdUnique`, `ActivePersonaPuestoUnique`), elimina la nota sobre cargos concurrentes, y queda blindada por un test de coherencia prosa↔modelo.

## Tareas

### 1. RED — Crear test de coherencia prosa↔modelo
- **Descripción**: Crear `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs` con una clase `[Fact]` que parsee `docs/decisiones-implementacion.md` y assertar:
  - La sección "Ocupaciones Activas" contiene las sub-cadenas `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique` (case-insensitive).
  - No contiene la frase "una única ocupación vigente por persona" como invariante sin matizar.
  - No contiene la frase "Si el negocio requiere cargos concurrentes…".
  - El modelo EF Core expuesto por el `SgvDbContext` activo tiene `ActivePuestoIdUnique` (índice único) y `ActivePersonaPuestoUnique` (índice único), y NO tiene `ActivePersonaIdUnique`.
- **Antes de este task**: ninguna línea del test existe. Crear el directorio `tests/SGV.Tests/Docs/` si no existe.
- **Validación**: `dotnet test SGV.slnx --filter "FullyQualifiedName~CoherenciaDecisionesImplementacion"` debe FALLAR con el doc actual (espera sub-cadenas que aún no están). Esperar RED.
- **Notas**: resolver la ruta del markdown desde `AppContext.BaseDirectory` o un path relativo a la solución para que sea robusto ante cambios de cwd. Parser con `Regex` nativo (sin paquetes nuevos). Case-insensitive. Reusar el patrón de `Modelo_Ocupacion_ReemplazaUnicidadPersonaPorPersonaPuesto` (`tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs:151-174`) para la aserción sobre shadow properties. Considerar `[Collection("DocsCoherencia")]` si se agregan más pruebas de coherencia en el futuro; para una sola `[Fact]` el default sequential basta.

### 2. GREEN — Reescribir la sección "Ocupaciones Activas"
- **Descripción**: Editar `docs/decisiones-implementacion.md` L19-21. Reemplazar el bloque actual por una versión que declare los DOS invariantes vigentes (per-Puesto y per-Persona+Puesto) usando los nombres de shadow property explícitos.
- **Texto propuesto**:
  ```
  ## Ocupaciones Activas

  La versión inicial aplica una única ocupación vigente por Puesto (`ActivePuestoIdUnique`) y una única ocupación vigente por la combinación Persona + Puesto (`ActivePersonaPuestoUnique`), mediante columnas generadas con índices únicos. Una Persona puede mantener varias ocupaciones activas simultáneas siempre que correspondan a Puestos distintos. La regla vigente de unicidad per-persona simple no se enforce; una futura restricción de ese tipo requeriría reintroducir la columna `ActivePersonaIdUnique` con su índice único y la verificación correspondiente en la capa de aplicación.
  ```
- **Antes de este task**: la prosa actual afirma "una única ocupación vigente por persona" y conserva la nota "Si el negocio requiere cargos concurrentes…".
- **Validación**: el test de la tarea 1 debe pasar verde. Si pasa, RED → GREEN completo. Comando: `dotnet test SGV.slnx --filter "FullyQualifiedName~CoherenciaDecisionesImplementacion"`.
- **Criticidad del orden**: NO invertir tareas 1 y 2. La prosa debe quedar blindada por el test **primero**; el modelo ya está fijo y la doc tiene que alinearse — el failure en el paso 1 confirma el drift antes de tocar texto.

### 3. REFACTOR — Validar suite completa
- **Descripción**: correr `dotnet test SGV.slnx` para confirmar 0 regresiones. No se toca persistencia ni código de modelo, así que los `[MySqlFact]` deben seguir verdes contra `sgv_test` si MySQL 8 está disponible localmente; si no, esos tests se skipean limpio según el contrato vigente del `MySqlFactAttribute`.
- **Validación**: suite verde o skip explicable (sin fallos nuevos). El nuevo test `CoherenciaDecisionesImplementacionTests` cuenta como pase en menos de 5 s (ver spec scenario del REQ-1).
- **Notas TDD**: este paso es **opcional como tarea autónoma** — la cobertura del RED→GREEN ya garantiza protección. Si la prosa no quedó perfectamente pulida en la tarea 2, se hace un commit fix-up dentro del mismo PR; no genera un work-unit separado.

### 4. Commit + PR
- **Descripción**: un solo commit con mensaje `feat: alinear doc de Ocupaciones con modelo vigente (issue #127)`. Conventional commit, sin atribución IA. PR hacia `develop`. Cuerpo del PR: "Closes #127. Reemplaza la prosa desactualizada de `decisiones-implementacion.md` que afirmaba unicidad simple per-persona, alineando con el modelo vigente (per-Puesto + per-Persona+Puesto) y agregando test de coherencia que blinda la prosa contra el modelo EF Core."
- **Notas**: con `work-unit-commits` skill, todo cabe en una unidad: 1 archivo markdown (~10 líneas modificadas) + 1 archivo de test nuevo (~50-80 líneas). No requiere trocear. Rollback: borrar el test y restaurar L19-21 al texto previo; sin efecto sobre migraciones ni código. Verificar `git diff --stat` antes de commit para confirmar el conteo (`Estimated 70 changed lines`).

## Glosario de shadow properties usados por el test

| Shadow property | Tipo | Índice | Sentido del invariante |
|-----------------|------|--------|------------------------|
| `ActivePuestoIdUnique` | `string?`, computed, `ascii_general_ci` | UNIQUE | Una sola Ocupación activa por Puesto |
| `ActivePersonaPuestoUnique` | `string?`, computed, `varchar(100)` | UNIQUE | Una sola Ocupación activa por la combinación Persona + Puesto |
| `ActivePersonaIdUnique` | — | — | NO debe existir (dropeado en migración `20260624153353`) |

## Orden de implementación recomendado

1. Tarea 1 — escribe el test **sin** tocar el markdown. Ejecuta la filtro, confirma RED con dos tipos de fallo: (a) sub-cadenas ausentes, (b) ausencia del shadow property (improbable porque ya está, pero el test asserta el contrato).
2. Tarea 2 — edita el markdown. Re-ejecuta el filtro. Confirma GREEN.
3. Tarea 3 — corre suite completa sin filtro.
4. Tarea 4 — commit + PR hacia `develop`.

## Riesgos de implementación

| Riesgo | Mitigación |
|--------|------------|
| El regex del parser es tan laxo que cualquier doc pasa | Asertar explícitamente presencia de los dos nombres de shadow property, no solo "Puesto" / "Persona + Puesto". |
| Path resolution del markdown falla en CI por cwd distinto | Usar `AppContext.BaseDirectory` + búsqueda ascendente hasta encontrar `docs/decisiones-implementacion.md`. Fallar con mensaje claro si no se encuentra. |
| El test RED se vuelve flaky ante reformateos de prosa | El test debe ser estable frente a whitespace, saltos de línea y case; nunca debe asertar el texto literal del párrafo. |

## Próximo paso SDD
`sdd-apply` con las 4 tareas listadas, secuencialmente en este orden.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 70 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Prosa alineada + test de coherencia verde | PR 1 | Base: develop. Incluye docs y tests del nuevo test RED→GREEN. |

## Próximo paso SDD
`sdd-apply` con las 4 tareas listadas, secuencialmente en este orden.
