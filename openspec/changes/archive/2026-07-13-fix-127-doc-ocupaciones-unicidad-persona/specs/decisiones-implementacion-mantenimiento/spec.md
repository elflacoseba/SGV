# Capability: decisiones-implementacion-mantenimiento

## Purpose
Este delta blinda la prosa de `docs/decisiones-implementacion.md` contra drift respecto del modelo EF Core. La sección "Ocupaciones Activas" describe el contrato de unicidad que el modelo efectivamente enforza y debe permanecer alineada con los shadow properties `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique` ya garantizados por `OcupacionConfiguracion`, `SgvDbContextModelSnapshot` y la spec canónica `openspec/specs/sgv-database/spec.md`. Este capability no redefine invariantes del modelo (esos viven en `sgv-database`); solo formaliza la regla de mantenimiento: cuando el modelo cambie, la prosa debe moverse en la misma dirección.

## Requirements

### Requirement: Coherencia prosa-modelo en `decisiones-implementacion.md`

`docs/decisiones-implementacion.md` MUST declarar, en su sección "Ocupaciones Activas", los invariantes que el modelo EF Core efectivamente enforza: una sola Ocupación activa por Puesto (shadow property `ActivePuestoIdUnique`) y una sola Ocupación activa por la combinación Persona + Puesto (shadow property `ActivePersonaPuestoUnique`).

La sección MUST NOT afirmar restricciones que el modelo no enforza (por ejemplo, "una única ocupación vigente por persona" como invariante estricto) sin matizar explícitamente que esa restricción requeriría reintroducir `ActivePersonaIdUnique`.

La prosa MAY ser editada para reflejar el estado vigente del modelo sin reintroducir `ActivePersonaIdUnique` ni reinstalar la nota sobre cargos concurrentes redirigidos a un futuro.

#### Scenario: la sección declara los DOS invariantes vigentes
- **GIVEN** el archivo `docs/decisiones-implementacion.md` es leído del working tree
- **WHEN** la sección "Ocupaciones Activas" se analiza textualmente
- **THEN** debe declarar los dos invariantes: "una sola Ocupación activa por Puesto" y "una sola Ocupación activa por la combinación Persona + Puesto"
- **AND** debe mencionar explícitamente los nombres `ActivePuestoIdUnique` y `ActivePersonaPuestoUnique`
- **AND** no debe afirmar "una única ocupación vigente por persona" sin matizar que esa restricción requiere reintroducir `ActivePersonaIdUnique`.

#### Scenario: el modelo expone las shadow properties esperadas
- **GIVEN** el modelo EF Core se construye a partir de `SgvDbContext` con `OcupacionEntity`
- **WHEN** se consultan las shadow properties con índice único de la entidad `Ocupaciones`
- **THEN** debe existir `ActivePuestoIdUnique` con índice único y `ActivePersonaPuestoUnique` con índice único
- **AND** no debe existir `ActivePersonaIdUnique` con índice único.

#### Scenario: el test de coherencia pasa verde en CI
- **GIVEN** el comando `dotnet test SGV.slnx` corre en CI con MySQL 8 disponible
- **WHEN** se ejecuta el test `CoherenciaDecisionesImplementacionTests` (ruta esperada: `tests/SGV.Tests/Docs/CoherenciaDecisionesImplementacionTests.cs`)
- **THEN** el test pasa verde en menos de 5 segundos
- **AND** si la prosa vuelve a afirmar el invariante incorrecto o el modelo recupera `ActivePersonaIdUnique`, el test falla con un mensaje que cite el shadow property faltante o sobrante.

### Requirement: Nota sobre cargos concurrentes removida

La sección "Ocupaciones Activas" de `docs/decisiones-implementacion.md` MUST NOT contener la frase "Si el negocio requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de dedicación" ni reformulaciones semánticamente equivalentes que redirijan el invariante a un futuro sin nombrarlo.

La conversación sobre extensibilidad futura, si la hubiera, vive en `openspec/specs/sgv-database/spec.md` y en issues GitHub, no en la prosa del archivo de decisiones.

#### Scenario: ausencia de la nota de extensibilidad
- **GIVEN** la sección "Ocupaciones Activas" después del fix
- **WHEN** se busca una frase que sugiera que las ocupaciones concurrentes requieren "tipo de ocupación o porcentaje de dedicación"
- **THEN** esa frase está ausente
- **AND** tampoco aparece una reformulación equivalente que posponga el invariante a un futuro sin nombrarlo explícitamente.

## Fuera de alcance
- Cualquier cambio en código de modelo (`OcupacionConfiguracion.cs`, repositorio, servicio).
- Cualquier migración nueva sobre `Ocupaciones`.
- Cualquier delta al canonical spec `sgv-database` — los invariantes vigentes ya están descritos allí.
- Cambios en otras secciones de `docs/decisiones-implementacion.md`.