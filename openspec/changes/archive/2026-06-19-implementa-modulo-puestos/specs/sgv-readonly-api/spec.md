# Delta for sgv-readonly-api

## MODIFIED Requirements

### Requirement: Read-only Resource Access

El sistema MUST exponer acceso HTTP a unidades organizativas, tipos de unidad organizativa, cargos, puestos y habilidades. MUST devolver datos persistidos reales para todos los recursos soportados. Unidades organizativas, cargos y puestos MAY exponer acciones documentadas de creación, actualización y baja lógica/reactivación; habilidades y tipos de unidad organizativa MUST permanecer read-only en esta versión.
(Previously: positions/puestos permanecían read-only; solo unidades organizativas y cargos podían exponer escritura.)

#### Scenario: List supported resources

- **GIVEN** existen unidades organizativas, tipos de unidad organizativa, cargos, puestos y habilidades persistidos
- **WHEN** un cliente solicita cada colección soportada
- **THEN** la API MUST devolver los registros persistidos correspondientes
- **AND** `tipos-unidad-organizativa` MUST responder un JSON array de `{ id, codigo, nombre }`
- **AND** cada respuesta MUST ser exitosa.

#### Scenario: Resource `tipos-unidad-organizativa` is listed

- **GIVEN** la API read-only está documentada
- **WHEN** se enumera la lista de recursos soportados
- **THEN** `tipos-unidad-organizativa` MUST aparecer con `GET /api/v1/tipos-unidad-organizativa` y `GET /api/v1/tipos-unidad-organizativa/{id:guid}`.

#### Scenario: Empty supported resource collection

- **GIVEN** un recurso soportado no tiene registros persistidos
- **WHEN** un cliente solicita esa colección
- **THEN** la API MUST devolver una respuesta exitosa con una colección vacía.

#### Scenario: Allow organizational unit writes

- **GIVEN** un cliente opera sobre unidades organizativas
- **WHEN** usa una acción documentada de creación, actualización, cambio de padre o baja lógica
- **THEN** la API MAY modificar datos persistidos según el contrato CRUD.

#### Scenario: Allow cargo write operations

- **GIVEN** un cliente opera sobre cargos
- **WHEN** usa una acción documentada de creación, actualización, desactivación o reactivación
- **THEN** la API MAY modificar cargos persistidos según el contrato de gestión.

#### Scenario: Allow puesto write operations

- **GIVEN** un cliente opera sobre puestos
- **WHEN** usa una acción documentada de creación, actualización, desactivación o reactivación
- **THEN** la API MAY modificar puestos persistidos según el contrato de gestión de puestos.

#### Scenario: Reject unrelated write operations

- **GIVEN** un cliente opera sobre habilidades o tipos de unidad organizativa
- **WHEN** intenta crear, actualizar o eliminar datos por API
- **THEN** la API MUST NOT modificar datos persistidos
- **AND** la operación MUST NOT exponerse como acción soportada.

### Requirement: Public API Discoverability

El sistema MUST publicar documentación de API que permita descubrir endpoints de lectura, endpoints de escritura para unidades organizativas, cargos y puestos, y contratos de respuesta.
(Previously: la documentación excluía operaciones de escritura para puestos.)

#### Scenario: Discover endpoints through API documentation

- **GIVEN** la API está ejecutándose localmente
- **WHEN** un cliente abre la documentación
- **THEN** la documentación MUST listar endpoints de lectura para unidades organizativas, tipos de unidad organizativa, cargos, puestos y habilidades.
- **AND** MUST describir el contrato de respuesta exitosa de cada endpoint.

#### Scenario: Discover organizational unit write operations

- **GIVEN** el CRUD de unidades organizativas está soportado
- **WHEN** un cliente inspecciona la documentación
- **THEN** las operaciones documentadas de creación, actualización, cambio de padre y baja lógica MUST ser discoverables.

#### Scenario: Discover cargo management operations

- **GIVEN** la gestión de cargos está soportada
- **WHEN** un cliente inspecciona la documentación
- **THEN** las operaciones de crear, actualizar, desactivar y reactivar cargos MUST ser discoverables.

#### Scenario: Discover puesto management operations

- **GIVEN** la gestión de puestos está soportada
- **WHEN** un cliente inspecciona la documentación
- **THEN** las operaciones de crear, actualizar, desactivar y reactivar puestos MUST ser discoverables.

#### Scenario: Exclude unsupported operations from documentation

- **GIVEN** habilidades y tipos de unidad organizativa permanecen read-only
- **WHEN** un cliente inspecciona la documentación
- **THEN** operaciones de escritura para esos recursos MUST NOT documentarse como disponibles.

## ADDED Requirements

### Requirement: Puesto Management Contract

El sistema MUST gestionar puestos como catálogo administrable. `codigo` y `nombre` SHALL ser obligatorios; `PuestoSuperiorId` MAY omitirse; Ocupaciones, Vacantes, permisos y roles MUST permanecer fuera de alcance.

#### Scenario: Crear puesto válido

- **GIVEN** existen una unidad organizativa y un cargo válidos
- **WHEN** se crea un puesto con `codigo` y `nombre`
- **THEN** el puesto MUST persistirse activo y quedar disponible en consultas activas.

#### Scenario: Rechazar datos mínimos faltantes

- **GIVEN** una solicitud de creación o actualización de puesto
- **WHEN** falta `codigo` o `nombre`
- **THEN** la API MUST rechazar la solicitud sin persistir cambios.

#### Scenario: Desactivar y reactivar puesto

- **GIVEN** existe un puesto activo
- **WHEN** se solicita desactivarlo y luego reactivarlo
- **THEN** el sistema MUST aplicar baja lógica y MAY restaurar visibilidad si no existe conflicto de código activo.
