# Delta para API de Solo Lectura SGV

## MODIFIED Requirements

### Requirement: Read-only Resource Access

El sistema MUST exponer acceso HTTP a unidades organizativas, tipos de unidad organizativa, cargos, puestos y habilidades. MUST devolver datos persistidos reales para todos los recursos soportados. Unidades organizativas, cargos, puestos y habilidades MAY exponer acciones documentadas de creación, actualización y baja/reactivación lógica; los tipos de unidad organizativa MUST permanecer solo lectura en esta versión. `/api/v1/skills` SHALL permanecer como ruta canónica del catálogo de habilidades.
(Previously: skills y tipos de unidad organizativa permanecían solo lectura; skills no exponía operaciones de escritura.)

#### Scenario: Listar recursos soportados

- **DADO** que existen unidades organizativas, tipos de unidad, cargos, puestos y habilidades persistidas
- **CUANDO** un cliente solicita cada colección soportada
- **ENTONCES** la API MUST devolver los registros persistidos correspondientes
- **Y** cada respuesta MUST ser exitosa.

#### Scenario: Recurso `tipos-unidad-organizativa` listado

- **DADO** que la API de solo lectura está documentada
- **CUANDO** se enumera la lista de recursos soportados
- **ENTONCES** `tipos-unidad-organizativa` MUST aparecer con `GET /api/v1/tipos-unidad-organizativa` y `GET /api/v1/tipos-unidad-organizativa/{id:guid}`.

#### Scenario: Colección soportada vacía

- **DADO** que un recurso soportado no tiene registros persistidos
- **CUANDO** un cliente solicita su colección
- **ENTONCES** la API MUST devolver una respuesta exitosa con una colección vacía.

#### Scenario: Permitir escrituras de unidades organizativas

- **DADO** que un cliente apunta a unidades organizativas
- **CUANDO** usa una acción documentada de creación, actualización, cambio de padre o baja lógica
- **ENTONCES** la API MAY modificar datos persistidos según el contrato CRUD.

#### Scenario: Permitir operaciones de escritura de cargos

- **DADO** que un cliente apunta a cargos
- **CUANDO** usa una acción documentada de creación, actualización o baja/reactivación lógica
- **ENTONCES** la API MAY modificar datos persistidos según el contrato de gestión de cargos.

#### Scenario: Permitir operaciones de escritura de puestos

- **DADO** que un cliente apunta a puestos
- **CUANDO** usa una acción documentada de creación, actualización, desactivación o reactivación
- **ENTONCES** la API MAY modificar datos persistidos según el contrato de gestión de puestos.

#### Scenario: Permitir operaciones de escritura de habilidades

- **DADO** que un cliente apunta a habilidades mediante `/api/v1/skills`
- **CUANDO** usa acciones documentadas de creación, actualización, desactivación o reactivación
- **ENTONCES** la API MAY modificar datos persistidos del catálogo según el contrato de gestión de habilidades.

#### Scenario: Rechazar operaciones de escritura no relacionadas

- **DADO** que un cliente apunta a tipos de unidad organizativa
- **CUANDO** intenta crear, actualizar o eliminar datos mediante la API
- **ENTONCES** la API MUST NOT modificar datos persistidos
- **Y** la operación MUST NOT exponerse como acción soportada.

### Requirement: Public API Discoverability

El sistema MUST publicar documentación de API que permita descubrir endpoints de lectura, endpoints de escritura para unidades organizativas, cargos, puestos, habilidades y contratos de respuesta.
(Previously: la documentación excluía operaciones de escritura para skills.)

#### Scenario: Descubrir endpoints mediante documentación de API

- **DADO** que la API está ejecutándose localmente
- **CUANDO** un cliente abre la documentación de API
- **ENTONCES** la documentación MUST listar endpoints de lectura para unidades organizativas, tipos de unidad, cargos, puestos y habilidades
- **Y** MUST describir el contrato de respuesta exitosa de cada endpoint.

#### Scenario: Descubrir operaciones de escritura de unidades organizativas

- **DADO** que el CRUD de unidades organizativas está soportado
- **CUANDO** un cliente inspecciona la documentación
- **ENTONCES** las operaciones documentadas de creación, actualización, cambio de padre y baja lógica MUST ser descubribles.

#### Scenario: Descubrir operaciones de gestión de cargos

- **DADO** que la gestión de cargos está soportada
- **CUANDO** un cliente inspecciona la documentación
- **ENTONCES** las operaciones documentadas de crear, actualizar, desactivar y reactivar cargos MUST ser descubribles.

#### Scenario: Descubrir operaciones de gestión de puestos

- **DADO** que la gestión de puestos está soportada
- **CUANDO** un cliente inspecciona la documentación
- **ENTONCES** las operaciones documentadas de crear, actualizar, desactivar y reactivar puestos MUST ser descubribles.

#### Scenario: Descubrir operaciones de gestión de habilidades

- **DADO** que la gestión de habilidades está soportada
- **CUANDO** un cliente inspecciona la documentación
- **ENTONCES** las operaciones documentadas de crear, actualizar, desactivar y reactivar bajo `/api/v1/skills` MUST ser descubribles.

#### Scenario: Excluir operaciones no soportadas de la documentación

- **DADO** que los tipos de unidad organizativa permanecen solo lectura
- **CUANDO** un cliente inspecciona la documentación
- **ENTONCES** las operaciones de crear, actualizar y eliminar tipos de unidad organizativa MUST NOT documentarse como acciones disponibles.
