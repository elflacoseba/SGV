# OpenSpec/SDD en este repositorio

## Por qué OpenSpec existe

SGV es un sistema con reglas de negocio no triviales (máquina de
estados de Vacantes, defensa anti-ciclos, auditoría transversal,
columnas generadas para unicidad activa) y un grafo de capas
deliberado. Cualquier modificación mediana o grande tiene muchas
maneras de salir mal: el equipo necesita un proceso que obligue a
pensar el cambio antes de tocar código, que registre las decisiones
para que un futuro developer pueda reconstruir el razonamiento, y
que separe "lo que el sistema promete" (specs) de "cómo lo
implementamos" (design).

OpenSpec (Spec-Driven Development, SDD) cumple ese rol. No es una
herramienta de generación de código: es una disciplina de
documentación que precede al código. La configuración del repo vive
en `openspec/config.yaml` con `schema: spec-driven` y `strict_tdd:
true`, y la regla de idiomas para los artefactos está dada por
`AGENTS.md §"OpenSpec / SDD"`: español.

## El ciclo: proposal → specs → design → tasks → apply → verify → archive

El cambio mediano o grande recorre siete fases, cada una con un
artefacto específico.

**Exploration (opcional).** `exploration.md` se usa cuando el alcance
no está claro o el problema requiere entender varios módulos. No
todos los cambios la necesitan.

**Proposal.** `proposal.md` define el "qué" y el "por qué": intención,
alcance, non-goals. Una buena proposal cabe en pocas páginas y se
lee sin abrir el repo. Si la proposal no cabe en una página, el
cambio probablemente debe partirse.

**Specs.** `specs/<capability>/spec.md` declara los requisitos en
formato Given/When/Then. Cada escenario debe ser independientemente
testeable — la idea es que el verify final pueda mapear 1-a-1
escenarios a tests. Los escenarios son append-only: nunca se borra
un escenario aprobado, sólo se archiva en `archive/` cuando la
capacidad cambia.

**Design.** `design.md` describe el "cómo": arquitectura,
componentes nuevos, archivos afectados, trade-offs. El equipo exige
que el design siga principios de Clean Architecture y referencie
proyectos específicos de la solución (regla del config.yaml). Si el
design dice "vamos a agregar X a SGV.Aplicacion" sin explicar por
qué no va en otra capa, es una señal para re-pensar.

**Tasks.** `tasks.md` divide el trabajo en chunks de máximo 2 horas
cada uno, cada uno independientemente testeable. La granularidad
importa porque commits chicos se revisan mejor que commits
monolíticos.

**Apply.** El equipo implementa, corriendo `dotnet build` antes de
crear commits y `dotnet test` antes de marcar apply completo. El
artefacto `apply-progress.md` se actualiza en tiempo real durante
la implementación para que cualquier reviewer pueda ver el
progreso.

**Verify.** `verify-report.md` documenta el resultado de ejecutar
los tests contra los specs. La regla del config.yaml es dura:
"All tests must pass before archiving" y "Verify behavior matches
spec scenarios". Si un escenario no se puede probar automáticamente,
eso es una deficiencia que se documenta pero también se trabaja
para cerrar.

**Archive.** `archive-report.md` cierra el change, moviendo los
specs aprobados a `openspec/specs/` para que queden como
referencia vigente. Los specs archivados siguen siendo append-only
— un escenario archivado nunca se borra aunque la capacidad que
describe deje de existir.

## Cuándo SÍ y cuándo NO

El `AGENTS.md §"OpenSpec / SDD"` distingue claramente el caso
grande del mediano y del trivial. La regla operativa, traducida:

- **GRANDE (obligatorio SDD):** introduce un módulo, modifica
  arquitectura, modifica decisiones técnicas importantes, modifica
  persistencia, modifica seguridad, modifica contratos públicos de
  manera importante, requiere coordinación de varios desarrolladores
  o agentes. Ejemplos: implementar un sistema de permisos,
  introducir una nueva estrategia de persistencia, cambiar la
  arquitectura de comunicación Web → API.
- **MEDIANA (SDD opcional que aporta valor):** afecta varias capas,
  modifica contratos, modifica una integración existente, modifica
  persistencia pero sin constituir un cambio arquitectónico. SDD
  puede usarse si reduce errores; no es obligatorio si no aporta.
- **PEQUEÑA / TRIVIAL (NO SDD):** cambios visuales, ordenamientos
  simples, correcciones localizadas, ajustes de UX. Una buena
  propuesta en estos casos sería abrir un PR directo sin artefactos
  intermedios.

La elección se sostiene en un principio: el proceso debe ser
proporcional al riesgo. Una tarea trivial debe resolverse
trivialmente. Una tarea grande necesita todo el proceso. Una
mediana necesita lo que ayude y nada más.

## El idioma y el lugar de los artefactos

Todos los artefactos nuevos viven en `openspec/changes/<nombre>/`
durante la implementación. Una vez archiveado, el change completo
se mueve a `openspec/changes/archive/<nombre>/` y los specs
promovidos pasan a `openspec/specs/<capacidad>/spec.md`. El
historial de cambios del repo cuenta con 89 entradas archivadas
(según el listado de `ls openspec/changes/archive/`), lo que da una
idea del ritmo de iteración del equipo: cada cambio mediano o
grande deja un rastro completo.

El idioma de los artefactos es español — la regla está explícita en
`AGENTS.md §"OpenSpec / SDD"` y se sostiene en el repo. El código,
los identificadores y los comentarios van en inglés por convención
de la industria. Esa separación es deliberada: los artefactos SDD
son comunicación entre humanos del equipo, no entrada para un
generador de código.

## Relación con el código y los tests

`strict_tdd: true` significa que los escenarios de los specs son la
fuente de verdad del comportamiento testeado. La suite `SGV.Tests`
cubre los módulos por capas (unit, integración, [MySqlFact]) pero
los specs Given/When/Then son la especificación legible. Un
escenario sin test es un agujero en el coverage del contrato.

El `coverage_command: dotnet test SGV.slnx --collect:"XPlat Code Coverage"`
de `openspec/config.yaml` no significa "persigue 100% de cobertura"
— significa "tenemos una métrica observable de cuánto del código
está ejercitado por la suite". La filosofía del repo (documentada
en `AGENTS.md §"Filosofía de Testing"`) es preferir pocos tests
significativos sobre muchos triviales. Un test que verifica un
getter no agrega valor; un test que verifica una transición de
estado sí.

## Consecuencias operativas

OpenSpec introduce fricción al inicio de cada cambio mediano o
grande. Esa fricción es el producto. Un equipo que pueda empezar a
codear sin proposal tiende a saltar a la solución obvia, que suele
ser localmente correcta pero arquitectónicamente inconsistente con
el resto del sistema. El costo de OpenSpec se paga al inicio; el
beneficio se cobra cada vez que un developer nuevo necesita
entender por qué una decisión se tomó hace dos años.

El catálogo de specs vigentes en `openspec/specs/` (más de 50
capacidades) es la fuente de verdad del comportamiento observable
del sistema. Cuando un developer nuevo necesita entender cómo
funciona el módulo Vacantes, abre `openspec/specs/vacante-management/spec.md`
y lee los escenarios. No necesita abrir el código de los servicios
de comandos hasta que los escenarios no alcancen para responder su
pregunta.

## Referencias

- `../tutorials/04-primer-cambio-clean-architecture.md` — un primer cambio que ejercita el ciclo completo en pequeño.
- `openspec/config.yaml` — la configuración vigente del repo.
- `openspec/changes/archive/` — el historial completo de cambios archivados.
- `openspec/specs/` — las capacidades vigentes con sus escenarios Given/When/Then.
- `AGENTS.md §"OpenSpec / SDD"` — la regla de cuándo usar OpenSpec y cuándo no.