# Spec Delta: vacante-management

## MODIFIED Requirements

### Requisito: Crear Vacante

El sistema DEBE permitir abrir una vacante indicando `PuestoId`, `EstadoVacanteId` (inicial), `FechaApertura`, `Motivo` y opcionalmente `Observaciones`. Solo el rol `Administrador` o `GestorVacantes` DEBE poder invocar la mutación. El `EstadoVacanteId` inicial NO DEBE referenciar un estado terminal (`EsTerminal = true`); si lo hace, la operación DEBE ser rechazada con `400 Bad Request`.

(Previously: el requisito no imponía restricción sobre la terminalidad del estado inicial; la creación aceptaba cualquier `EstadoVacanteId` existente, incluso terminales.)

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