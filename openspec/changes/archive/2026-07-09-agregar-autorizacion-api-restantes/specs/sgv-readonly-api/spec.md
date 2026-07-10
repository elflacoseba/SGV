# Delta for sgv-readonly-api

## MODIFIED Requirements

### Requirement: No Authentication Requirement

El sistema MUST aplicar una postura de default-deny: el único endpoint explícitamente anónimo de toda la API es `POST /api/v1/auth/login`. Cualquier otro endpoint MUST requerir autenticación; las mutaciones MUST requerir, además, el rol `Administrador`. Las lecturas autenticadas MUST conservar sus contratos `2xx` vigentes y los clientes autenticados sin el rol correcto sobre una mutación MUST recibir `403 Forbidden`. Los clientes sin credenciales sobre cualquier endpoint distinto de `POST /api/v1/auth/login` MUST recibir `401 Unauthorized`. La excepción `[AllowAnonymous]` MUST limitarse a `AuthController.Login` para que sobreviva la fallback policy global aplicada en `Program.cs`; cualquier otro caso MUST seguir la regla default-deny.
(Previously: todos los endpoints read-only existentes podían consumirse anónimamente, con excepción de las lecturas y mutaciones de Cargos y su subrecurso de skills, que ya requerían autenticación o rol `Administrador`. El resto de la API —incluidos PersonasController, UnidadesOrganizativasController, NivelesCargoController y TipoUnidadesOrganizativasController— permanecía accesible sin credenciales.)

#### Scenario: Login como única ruta anónima

- GIVEN la API está disponible con la fallback policy global activa
- WHEN un cliente sin credenciales solicita `POST /api/v1/auth/login` con payload válido
- THEN la API MUST responder `2xx` con el contrato vigente de autenticación
- AND la acción `Login` MUST ser la única ruta anonima permitida por la API.

#### Scenario: Lectura anónima rechazada en endpoint distinto a Login

- GIVEN la API está disponible con la fallback policy global activa
- WHEN un cliente sin credenciales solicita cualquier endpoint distinto a `POST /api/v1/auth/login` (incluidos `GET /api/v1/personas`, `GET /api/v1/unidades-organizativas`, `GET /api/v1/niveles-cargo`, `GET /api/v1/tipos-unidad-organizativa`, lecturas de Cargos u otros recursos)
- THEN la API MUST responder `401 Unauthorized`
- AND MUST NOT exponer datos persistidos a clientes anónimos.

#### Scenario: Lectura autenticada exitosa

- GIVEN un cliente autenticado
- WHEN solicita un endpoint de lectura de cualquier recurso cubierto por la API (Cargos, Personas, UnidadesOrganizativas, NivelesCargo, TipoUnidadesOrganizativa, Puestos o Skills)
- THEN la API MUST responder `2xx` con el contrato documentado del recurso solicitado.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de mutación sobre cualquier recurso cubierto por la API (Cargos, Personas, UnidadesOrganizativas, Puestos, Skills o Usuarios)
- WHEN la ejecuta un cliente autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx` con el contrato vigente.

#### Scenario: Catálogos read-only requieren autenticación

- GIVEN la API está disponible con la fallback policy global activa
- WHEN un cliente sin credenciales solicita `GET /api/v1/niveles-cargo` o `GET /api/v1/tipos-unidad-organizativa`
- THEN la API MUST responder `401 Unauthorized`
- AND un cliente autenticado MUST recibir `2xx` con el contrato de catálogo vigente.