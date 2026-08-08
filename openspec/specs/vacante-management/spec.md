# Especificación de Gestión de Vacantes

## Propósito

Gestión del ciclo de vida de vacantes de puestos vía API REST: abrir, consultar, cambiar estado con registro en `HistorialEstadoVacante`, cerrar y consultar el catálogo de estados. El dominio (`Vacante`, `EstadoVacante`, `HistorialEstadoVacante`) y la persistencia ya existen; esta spec cubre la capa de aplicación, contratos wire, repositorio y controller.

## Decisiones de negocio asumidas (PB-1 a PB-5)

| PB | Decisión asumida | Sujeta a confirmación |
|----|-------------------|------------------------|
| PB-1 | Mutaciones (crear, cambiar estado, cerrar) requieren rol `Administrador` **o** `GestorVacantes`. GET/catálogo requieren solo autenticación. | Sí — confirmar antes de design |
| PB-2 | La creación de vacantes se realiza desde el módulo de Vacantes (no desde el detalle de puesto). | Sí — este change no implementa botón en detalle de puesto |
| PB-3 | `Motivo` al cerrar NO es obligatorio (el dominio no lo impone). Se acepta nulo/vacío. | Sí — si negocio exige motivo, agregar validador |
| PB-5 | El segmento por defecto del listado es `abiertas` (análogo a `activas` en otros módulos). | Sí |

## Requisitos

### Requisito: Crear Vacante

El sistema DEBE permitir abrir una vacante indicando `PuestoId`, `EstadoVacanteId` (inicial), `FechaApertura`, `Motivo` y opcionalmente `Observaciones`. Solo el rol `Administrador` o `GestorVacantes` DEBE poder invocar la mutación. El `EstadoVacanteId` inicial NO DEBE referenciar un estado terminal (`EsTerminal = true`); si lo hace, la operación DEBE ser rechazada con `400 Bad Request`.

**Regla N1 — Bloqueo por Ocupacion activa**: antes de evaluar la constraint de BD, el sistema DEBE verificar que no exista una `Ocupacion` activa (`EsVigente = true`, `IsDeleted = 0`) para el `PuestoId`. Si existe, DEBE rechazar con `409 Conflict` y código `PuestoOcupado`. La unicidad "una sola vacante abierta por puesto" DEBE reforzarse con unique constraint parcial en BD sobre `PuestoId` filtrado por `FechaCierre IS NULL` y `IsDeleted = 0`; ante carrera concurrente la BD es fuente de verdad, y la aplicación DEBE mapear la `DbUpdateException` de constraint violation al código `VacanteErrorCodigo.PuestoConVacanteAbierta` respondiendo `409 Conflict`.

#### Escenario: Puesto con Ocupacion activa (N1)

- **DADO** que existe una `Ocupacion` con `EsVigente = true` para el `PuestoId` indicado
- **Y** no existe vacante abierta para ese `PuestoId`
- **CUANDO** un usuario con rol `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE responder `409 Conflict` con código `PuestoOcupado`
- **Y** la vacante NO DEBE persistirse.

#### Escenario: Creación exitosa

- **DADO** que no existe vacante abierta para el `PuestoId` indicado
- **Y** no existe `Ocupacion` activa para ese `PuestoId`
- **Y** `EstadoVacanteId` referencia un estado de vacante existente no terminal
- **CUANDO** un usuario con rol `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE persistir la vacante
- **Y** DEBE responder `201 Created` con el `VacanteDto` creado.

#### Escenario: PuestoId inexistente

- **DADO** que no existe un Puesto con el `PuestoId` proporcionado
- **CUANDO** se solicita crear la vacante
- **ENTONCES** el sistema DEBE rechazar con `400 Bad Request` y `ErrorCategoria.ValidationError`.

#### Escenario: EstadoVacanteId inválido

- **DADO** que `EstadoVacanteId` no referencia un estado sembrado
- **CUANDO** se solicita crear la vacante
- **ENTONCES** el sistema DEBE rechazar con error de validación.

#### Escenario: Mutación sin permiso

- **DADO** un usuario autenticado sin rol `Administrador` ni `GestorVacantes`
- **CUANDO** solicita `POST /api/v1/vacantes`
- **ENTONCES** la API DEBE responder `403 Forbidden`.

#### Escenario: Estado inicial terminal rechazado

- **DADO** que el `EstadoVacanteId` referenciado en el request corresponde a un estado con `EsTerminal = true` (Cubierta o Cancelada)
- **CUANDO** un usuario con rol `Administrador` o `GestorVacantes` solicita `POST /api/v1/vacantes`
- **ENTONCES** el sistema DEBE responder `400 Bad Request`
- **Y** DEBE incluir `ErrorCategoria.Validation` y código `VacanteErrorCodigo.EstadoTerminalInmutable`
- **Y** DEBE poblar `FieldErrors["estadoVacanteId"]` con el mensaje `"El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."`
- **Y** la vacante NO DEBE persistirse.

#### Escenario: Puesto con vacante abierta

- **DADO** que ya existe una vacante abierta (`FechaCierre IS NULL`, `IsDeleted = 0`) para el `PuestoId`
- **CUANDO** se solicita `POST /api/v1/vacantes` para el mismo `PuestoId`
- **ENTONCES** el sistema DEBE responder `409 Conflict` con código `VacanteErrorCodigo.PuestoConVacanteAbierta`
- **Y** la nueva vacante NO DEBE persistirse.

#### Escenario: Carrera concurrente para el mismo PuestoId

- **DADO** que no existe vacante abierta para un `PuestoId`
- **CUANDO** dos solicitudes `POST /api/v1/vacantes` concurren simultáneamente para ese `PuestoId`
- **ENTONCES** exactamente una DEBE recibir `201 Created`
- **Y** la otra DEBE recibir `409 Conflict` con código `VacanteErrorCodigo.PuestoConVacanteAbierta`
- **Y** NO DEBE persistirse más de una vacante abierta para ese `PuestoId`.

### Requisito: Consultar Vacantes (query segmentada)

El sistema DEBE exponer `GET /api/v1/vacantes?status={abiertas|cerradas|todas}&p=&pageSize=&search=&sort=` con segmentación que no mezcla conjuntos. El valor por defecto DEBE ser `abiertas`.

#### Escenario: Listado por defecto retorna abiertas

- **DADO** vacantes abiertas y cerradas persistidas
- **CUANDO** un cliente autenticado consulta `GET /api/v1/vacantes` sin `status`
- **ENTONCES** la API DEBE normalizar a `abiertas`
- **Y** DEBE devolver solo vacantes con estado no terminal.

#### Escenario: Segmento cerradas no mezcla abiertas

- **DADO** vacantes abiertas y cerradas
- **CUANDO** se consulta `?status=cerradas`
- **ENTONCES** la respuesta DEBE contener solo vacantes en estado terminal (`Cubierta` o `Cancelada`)
- **Y** NO DEBE incluir vacantes abiertas.

#### Escenario: Status inválido cae a abiertas

- **DADO** vacantes en distintos estados
- **CUANDO** se consulta `?status=invalido`
- **ENTONCES** la API DEBE normalizar el valor a `abiertas`.

### Requisito: Obtener Vacante por identificador

El sistema DEBE exponer `GET /api/v1/vacantes/{id}` devolviendo `VacanteDetailDto` con el estado actual y el histórico de cambios.

#### Escenario: Detalle exitoso

- **DADO** una vacante existente
- **CUANDO** un usuario autenticado solicita `GET /api/v1/vacantes/{id}`
- **ENTONCES** la API DEBE responder `200 OK` con `VacanteDetailDto`.

#### Escenario: Vacante inexistente

- **DADO** un identificador sin vacante asociada
- **CUANDO** se solicita el detalle
- **ENTONCES** la API DEBE responder `404 Not Found`.

### Requisito: Cambiar estado de Vacante con historial

El sistema DEBE permitir transicionar el `EstadoVacanteId` persistiendo simultáneamente un registro en `HistorialEstadoVacante`. La transición a estado terminal (`Cubierta`, `Cancelada`) DEBE setear `FechaCierre` automáticamente. `Motivo` es OPCIONAL (PB-3).

**Regla N2 — Cubrir crea Ocupacion automáticamente**: la transición a `Cubierta` DEBE recibir `PersonaId` (identificador de la Postulación ganadora) y DEBE crear automáticamente una `Ocupacion` vinculada con `VacanteId` seteado y `EsVigente = true`, en la misma transacción que el cambio de estado y el histórico. Si `PersonaId` no se provee, la transición DEBE rechazarse con `400 Validation` y `FieldErrors["personaId"]`.

#### Escenario: Transición exitosa a estado no terminal

- **DADO** una vacante en estado `Abierta`
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `PATCH /api/v1/vacantes/{id}/estado` con `EstadoVacanteId=EnSeleccion`
- **ENTONCES** el sistema DEBE persistir el nuevo estado
- **Y** DEBE insertar un registro en `HistorialEstadoVacante`
- **Y** `FechaCierre` DEBE permanecer nula.

#### Escenario: Transición a Cubierta crea Ocupacion (N2)

- **DADO** una Vacante `Abierta` con una Postulación ganadora cuyo `PersonaId` está disponible
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `PATCH /api/v1/vacantes/{id}/estado` con destino `Cubierta` y `PersonaId`
- **ENTONCES** el sistema DEBE setear `FechaCierre` y registrar `HistorialEstadoVacante`
- **Y** DEBE crear una `Ocupacion` con `VacanteId` igual al id de la Vacante, `PuestoId` igual al de la Vacante, `PersonaId` recibido y `EsVigente = true`
- **Y** la creación de la `Ocupacion` DEBE ser atómica respecto del cambio de estado.

#### Escenario: Cubrir sin PersonaId es rechazado (N2)

- **DADO** una Vacante `Abierta`
- **CUANDO** se solicita transición a `Cubierta` sin `PersonaId`
- **ENTONCES** el sistema DEBE responder `400 Bad Request` con `ErrorCategoria.Validation` y `FieldErrors["personaId"]`
- **Y** NO DEBE mutar la Vacante ni crear `Ocupacion`.

#### Escenario: Atomicidad extendida a Ocupacion (N2)

- **DADO** una transición a `Cubierta` válida
- **CUANDO** la persistencia de la `Ocupacion` derivada falla
- **ENTONCES** el cambio de estado de la Vacante y el histórico DEBEN revertirse (misma transacción).

#### Escenario: Transición a estado terminal setea FechaCierre

- **DADO** una vacante abierta
- **CUANDO** se solicita cambiar a `Cubierta` sin `Motivo` (PB-3 asumido opcional)
- **ENTONCES** el sistema DEBE setear `FechaCierre`
- **Y** DEBE registrar el histórico.

#### Escenario: Estado terminal inmutable

- **DADO** una vacante en estado `Cubierta`
- **CUANDO** se solicita cambiar su estado
- **ENTONCES** el sistema DEBE rechazar la operación con error de conflicto.

### Requisito: Catálogo de estados de vacante (solo lectura)

El sistema DEBE exponer `GET /api/v1/estados-vacante` autenticado que devuelva los estados sembrados (bloque GUID `20000000-…`) sin permitir mutaciones.

#### Escenario: Listado de estados

- **DADO** los 4 estados sembrados
- **CUANDO** un usuario autenticado solicita el catálogo
- **ENTONCES** la API DEBE responder `200 OK` con los 4 `EstadoVacanteDto` ordenados por `Orden`.

#### Escenario: Catálogo sin autenticación

- **DADO** un cliente sin credenciales
- **CUANDO** solicita `GET /api/v1/estados-vacante`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

### Requisito: Contrato de respuesta Vacante consumer-safe

Las respuestas de vacantes DEBEN exponer `id`, `puestoId`, `puestoNombre` (denormalizado), `estadoVacanteId`, `estadoVacanteNombre` (denormalizado), `fechaApertura`, `fechaCierre`, `motivo`, `observaciones`. NO DEBEN incluir campos internos de auditoría ni tracking de persistencia.

#### Escenario: Respuesta sin campos internos

- **DADO** una vacante persistida con datos completos
- **CUANDO** se consulta por la API
- **ENTONCES** la respuesta NO DEBE incluir `createdAt`, `isDeleted` ni `isActive`.

### Requisito: Autorización de endpoints de vacantes

`VacantesController` DEBE requerir autenticación en todos los endpoints. Lecturas (`GET`) DEBEN permitir cualquier usuario autenticado. `POST`, `PATCH` y mutaciones DEBEN requerir rol `Administrador` o `GestorVacantes` (PB-1).

#### Escenario: Lectura autenticada exitosa

- **DADO** un usuario autenticado con cualquier rol
- **CUANDO** solicita `GET /api/v1/vacantes` o `GET /api/v1/vacantes/{id}`
- **ENTONCES** la API DEBE responder `2xx`.

#### Escenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita cualquier endpoint de vacantes
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Escenario: Mutación protegida por rol

- **DADO** una solicitud válida de mutación sobre vacantes
- **CUANDO** la ejecuta un usuario sin rol `Administrador` ni `GestorVacantes`
- **ENTONCES** la API DEBE responder `403 Forbidden`
- **Y** si la ejecuta un rol permitido, DEBE responder `2xx`.

### Requisito: Unicidad de vacante abierta por puesto (defense-in-depth en BD)

El sistema DEBE garantizar, mediante unique constraint parcial en BD sobre `PuestoId` filtrado por `FechaCierre IS NULL` y `IsDeleted = 0`, que nunca coexistan dos vacantes abiertas para el mismo puesto. La columna calculada que soporta la constraint DEBE evaluar a `NULL` para vacantes cerradas o soft-deleted, de modo que MySQL las ignore del unique index.

**Regla N4 — La posición del Puesto se libera al finalizar la Ocupacion derivada**: la disponibilidad de negocio del Puesto para una nueva Vacante depende también de la `Ocupacion` derivada. La posiciónsólo se libera cuando la `Ocupacion` derivada deja de ser activa (`Finalizada` o eliminada lógicamente), no cuando la Vacante transiciona a `Cubierta`. El check N1 (`PuestoOcupado`) gobierna la creación de nuevas Vacantes mientras exista `Ocupacion` activa.

#### Escenario: Cubrir la Vacante no libera la posición (N4)

- **DADO** una Vacante `Cubierta` con `Ocupacion` derivada `EsVigente = true`
- **CUANDO** se solicita `POST /api/v1/vacantes` para el mismo `PuestoId`
- **ENTONCES** el sistema DEBE responder `409 Conflict` con código `PuestoOcupado` (N1)
- **Y** la constraint de BD de vacante abierta NO entra en juego porque `FechaCierre` ya está seteada.

#### Escenario: Finalizar la Ocupacion derivada libera la posición (N4)

- **DADO** una Vacante `Cubierta` cuya `Ocupacion` derivada fue finalizada (`EsVigente = false`)
- **Y** no existe otra vacante abierta para el `PuestoId`
- **CUANDO** se solicita `POST /api/v1/vacantes` para ese `PuestoId`
- **ENTONCES** el sistema DEBE responder `201 Created`.

#### Escenario: Una vacante abierta por puesto no viola la constraint

- **DADO** que no existe vacante abierta para un `PuestoId`
- **CUANDO** se persiste una nueva vacante abierta para ese puesto
- **ENTONCES** la constraint DEBE aceptar la inserción sin regresión observable.

#### Escenario: Vacante cerrada deja de violar la constraint

- **DADO** una vacante abierta con `FechaCierre IS NULL` para un `PuestoId`
- **CUANDO** se transiciona a estado terminal seteando `FechaCierre`
- **ENTONCES** la columna calculada DEBE evaluar a `NULL`
- **Y** DEBE ser posible abrir una nueva vacante para el mismo `PuestoId`.

#### Escenario: Vacante soft-deleted deja de violar la constraint

- **DADO** una vacante abierta con `IsDeleted = 0` para un `PuestoId`
- **CUANDO** se elimina lógicamente seteando `IsDeleted = 1`
- **ENTONCES** la columna calculada DEBE evaluar a `NULL`
- **Y** DEBE ser posible abrir una nueva vacante para el mismo `PuestoId`.

#### Escenario: Reabrir vacante cerrada con puesto abierto es rechazado por la BD

- **DADO** una vacante cerrada para un `PuestoId` que ya posee otra vacante abierta
- **CUANDO** por error se reabre seteando `FechaCierre = NULL`
- **ENTONCES** la BD DEBE rechazar por violación de unique constraint
- **Y** el rechazo DEBE mapearse a `VacanteErrorCodigo.PuestoConVacanteAbierta`.

### Requisito: Códigos de error de Ocupacion cruzada

El sistema DEBE exponer el código funcional `PuestoOcupado` (Ocupacion activa bloqueando creación de Vacante) en `VacanteErrorCodigo`, distinto de `PuestoConVacanteAbierta` (constraint de BD) y de `OcupacionErrorCodigo.PuestoOcupado` (conflicto de Ocupacion por Puesto ya vigente).

#### Escenario: Discriminación de códigos 409

- **DADO** un Puesto con `Ocupacion` activa y sin vacante abierta
- **CUANDO** se intenta crear una Vacante
- **ENTONCES** el código DEBE ser `PuestoOcupado` (Ocupacion activa), no `PuestoConVacanteAbierta`.