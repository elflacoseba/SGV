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

#### Escenario: Creación exitosa

- **DADO** que no existe vacante abierta para el `PuestoId` indicado
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

#### Escenario: Transición exitosa a estado no terminal

- **DADO** una vacante en estado `Abierta`
- **CUANDO** un `Administrador` o `GestorVacantes` solicita `PATCH /api/v1/vacantes/{id}/estado` con `EstadoVacanteId=EnSeleccion`
- **ENTONCES** el sistema DEBE persistir el nuevo estado
- **Y** DEBE insertar un registro en `HistorialEstadoVacante`
- **Y** `FechaCierre` DEBE permanecer nula.

#### Escenario: Transición a estado terminal setea FechaCierre

- **DADO** una vacante abierta
- **CUANDO** se solicita cambiar a `Cubierta` sin `Motivo` (PB-3 asumido opcional)
- **ENTONCES** el sistema DEBE setear `FechaCierre`
- **Y** DEBE registrar el histórico.

#### Escenario: Atomicidad de vacante e historial

- **DADO** una solicitud de cambio de estado válida
- **CUANDO** la persistencia del histórico falla
- **ENTONCES** el cambio de estado de la vacante DEBE revertirse (misma transacción).

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