# Máquina de estados de Vacantes y `ActivePuestoIdUnique`

## El ciclo de vida de una vacante

Una Vacante recorre cuatro estados canónicos durante su vida útil:
**Abierta**, **En Selección**, **Cubierta** y **Cancelada**. Las dos
primeras son activas — admiten postulaciones, transiciones internas y
operaciones de edición. Las dos últimas son terminales: una vez que
la vacante llega a `Cubierta` o `Cancelada`, no hay vuelta atrás dentro
del modelo. No existe la reactivación de una vacante cerrada; lo que
sí existe es crear una vacante nueva para el mismo Puesto si la
organización decide intentarlo de nuevo.

El catálogo vive en `SGV.Infraestructura/Persistencia/Catalogos/EstadoVacanteConstantes.cs`
como bloque GUID reservado `20000000-…`. Las cuatro filas tienen
constantes de `Codigo` (`Abierta`, `EnSeleccion`, `Cubierta`,
`Cancelada`) y flags persistidos en la entidad: `EsTerminal`,
`EsCubierta` y `EsCancelada`. La separación importa porque la lógica
de dominio consume los flags booleanos mientras que los servicios de
comandos y los wire-types usan el `Codigo`.

## La tabla de transiciones

| Desde          | Hacia          | Permitido por dominio | Quién lo invoca                              |
|----------------|----------------|-----------------------|----------------------------------------------|
| Abierta        | En Selección   | Sí                    | RRHH/Servicio de comandos                    |
| En Selección   | Cubierta       | Sí (vía Ocupación)    | `OcupacionServicioComandos.CrearAsync` con `VacanteId` |
| En Selección   | Cancelada      | Sí                    | Servicio de comandos                         |
| Abierta        | Cubierta       | NO — sólo vía En Selección | (Bloqueado por el flujo invertido)         |
| Abierta        | Cancelada      | Sí                    | Servicio de comandos                         |
| Cubierta       | cualquiera     | NO (terminal)         | —                                            |
| Cancelada      | cualquiera     | NO (terminal)         | —                                            |

La transición `En Selección → Cubierta` no se ejecuta nunca desde
`VacanteServicioComandos`. El flujo se invirtió en el change
`invertir-flujo-cubrir` (issue #276 / `vacante-ocupacion-flow-alignment`).
La razón: cuando se creaba la Ocupación dentro de la transición de
estado, el frontend perdía el binding natural con la Persona. La
solución es que "Cubrir una Vacante" es una operación de la página de
detalle de Vacantes que abre el formulario de creación de Ocupación
con `?vacanteId={id}` prellenado. La creación de la Ocupación
(`OcupacionServicioComandos.CrearAsync` con `request.VacanteId`)
materializa la transición dentro de la misma transacción.

## Por qué `EsTerminal` se persiste como flag

Una alternativa era derivar `EsTerminal` cada vez que se necesita,
expresándolo como `Codigo IN ("Cubierta", "Cancelada")`. Se descartó
por tres razones:

- **Rendimiento.** El dominio necesita preguntar `EsTerminal` en
  validaciones que se ejecutan en cada `SaveChanges`. Un flag bool
  cuesta una lectura directa; una comparación de strings cuesta un
  scan y una normalización.
- **Estabilidad evolutiva.** Si en el futuro el catálogo suma una
  nueva fila "CubiertaReactivada" o "CanceladaAdministrativamente",
  la pregunta "qué estados cuentan como terminales" puede cambiar
  sin reescribir todas las validaciones — basta con que cada fila
  declare su flag.
- **Debugging claro.** Cuando se audita una transición, la columna
  persistida `EsTerminal` permite reconstruir sin ambigüedad el
  momento en que la fila pasó a ser terminal, sin tener que
  reinterpretar reglas derivadas.

El catálogo se persiste con el flag, y el equipo confía en que el
seed inicial de `DatosSemilla.HasData` lo setea correctamente. Los
tests de paridad assertan que `EstadoVacanteConstantes` y el snapshot
del modelo no se desincronicen.

## El historial inmutable

Cada transición de estado produce una fila en
`HistorialEstadoVacante`. La entidad es un `record class` con los
siguientes campos:

- `VacanteId` — la vacante que cambió.
- `EstadoAnteriorId` / `EstadoNuevoId` — los GUIDs de los estados.
- `ChangedAt` — timestamp UTC del cambio.
- `ChangedByUserId` — el usuario que gatilló la transición.
- `Motivo` — texto libre que justifica el cambio.

El dominio expone `Vacante.CambiarEstado(estadoNuevoId, usuarioId,
motivo)`, que devuelve la fila del historial armada y la agrega a la
lista interna `_historialEstados`. La colección se expone como
`IReadOnlyCollection` — el dominio garantiza que el historial no se
edita post-construcción. Esto es importante porque la auditoría de la
transición (alta/baja) ya queda cubierta por el interceptor de
`SaveChanges` para `VacanteEntity` y `HistorialEstadoVacanteEntity`;
pero el historial de dominio aporta la narrativa semántica
("Cubierta porque el postulante X fue seleccionado") que la tabla
`Auditorias` no captura.

## La interacción con `ActivePuestoIdUnique`

La columna generada `ActivePuestoIdUnique` ya se describió en el
documento de unicidad activa. Lo que agrega el contexto de la máquina
de estados es entender por qué la condición incluye `FechaCierre IS
NULL`: el soft-delete por `IsDeleted = 1` marca la fila como
eliminada por completo, pero una Vacante "Cubierta" sigue ocupando
espacio en la tabla — sólo perdió la posibilidad de transicionar más.

Cuando una Vacante pasa a `Cubierta`, `Vacante.CambiarEstado` setea
`FechaCierre = cambio.ChangedAt` (cuando el caller pasa `cerrar: true`).
A partir de ese momento, `ActivePuestoIdUnique` evalúa a `NULL` y la
constraint deja de rechazar nuevas vacantes para el mismo Puesto.
Esto es exactamente lo que el negocio necesita: una vacante cubierta
para un puesto no impide que el mismo puesto vuelva a estar vacante
en el futuro.

## Cubrir vía Ocupación: el flujo concreto

El comando `OcupacionServicioComandos.CrearAsync` con `VacanteId`
poblado ejecuta esta secuencia:

1. Carga la Vacante vía `IVacanteRepository.GetByIdForUpdateAsync`. Si
   no existe → `404 VacanteNoEncontrada`.
2. Si `EstadoVacante.EsTerminal` → `400 VacanteNoAbierta` (cubre
   `Cubierta` y `Cancelada`).
3. Si `IOcupacionRepository.ExistsActiveByVacanteAsync` →
   `409 VacanteYaCubierta`. Esta defensa evita el caso donde dos
   Ocupaciones diferentes reclaman la misma Vacante.
4. Si `request.PuestoId` viene vacío, se resuelve desde
   `vacante.PuestoId`; si viene poblado y no coincide →
   `400 PuestoIdNoCoincideConVacante`.
5. Crea la `Ocupacion` con `VacanteId` y persiste vía el mismo
   `IUnitOfWork.SaveChangesAsync` que invoca
   `vacante.CambiarEstado(Cubierta, ..., cerrar: true)`. EF agrupa
   ambas escrituras en una sola transacción; el catch de
   `DbUpdateException` cubre el rollback si algo falla.
6. La constraint `IX_Ocupaciones_VacanteIdUnique` en la BD es la red
   de seguridad final. Si por alguna carrera la lógica de servicio
   pasó ambas defensas pero dos requests paralelos intentan cubrir la
   misma Vacante, la BD rechaza con `Duplicate entry`, y
   `MySqlConstraintViolationDetector` lo mapea a
   `OcupacionErrorCodigo.VacanteYaCubierta`.

`VacanteServicioComandos.CambiarEstadoAsync` rechaza cualquier destino
`EsCubierta` con `400 CubrirVacanteRequiereCrearOcupacion` y un
mensaje que apunta al botón "Cubrir Vacante" en el detalle. El campo
legacy `PersonaId` del request se ignora silenciosamente — quedó
marcado `[Obsolete]` para clientes cacheados pero nunca vuelve a
materializarse en runtime.

## Consecuencias operativas

La consecuencia más visible del modelo es la regla operativa para
el frontend: ningún flujo puede llegar a "Cubierta" sin pasar por
una Ocupación. Esto se refleja en los dropdowns de edición de Vacantes
(`vacante-edit-estado-cambio`), que omiten la opción `Cubierta` y
redirigen al operador al detalle de Vacante con el botón
correspondiente.

Una segunda consecuencia, más sutil, es la dependencia del catálogo
de `EstadosVacante`. Cualquier cambio en la tabla (renombrar "En
SelecciÃ³n", agregar un nuevo estado, mover el orden) impacta la
constante `EstadoVacanteCodigos` en `SGV.Contracts`, el método
`IEstadoVacanteRepository.GetByCodigoAsync` (issue #273.8) y los
tests de paridad del catálogo. La trazabilidad se sostiene por el
test `MigracionEstadoVacanteEncodingTests` que verifica que la
migración `20260813120000_FixEstadoVacanteEnSeleccionEncoding` sea
idempotente y forward-only.

La tercera consecuencia es la defensa contra soft-delete simultáneo
con cambio de estado: si una Vacante es soft-deleted mientras está
en `Abierta` o `En Selección`, ambas transiciones están bloqueadas —
`EsTerminal` se evalúa en cada operación de `CambiarEstado` y un
soft-delete es una operación distinta que no pasa por esa ruta. El
soft-delete sólo afecta la visibilidad del listado, no la máquina de
estados.

## Referencias

- `../how-to/08-auditar-quien-modifico-entidad.md` — cómo un operador rastrea el historial de una vacante en particular.
- `../reference/04-roles-matriz-autorizacion.md` — qué roles pueden gatillar cada transición.
- `openspec/changes/archive/2026-07-30-feature-implementar-modulo-vacantes/` — artefactos SDD completos del módulo.
- `openspec/specs/vacante-management/` — los specs Given/When/Then vigentes del módulo.
- `docs/decisiones-implementacion.md` — secciones "Issue #273", "Inversión del flujo Cubrir" y "Vacantes Hardening".