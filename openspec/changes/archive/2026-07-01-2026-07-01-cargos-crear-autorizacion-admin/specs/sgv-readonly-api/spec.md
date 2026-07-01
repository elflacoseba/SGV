# Delta for sgv-readonly-api

## MODIFIED Requirements

### Requirement: No Authentication Requirement

El sistema MUST preservar acceso anónimo para los endpoints públicos de lectura existentes, excepto las lecturas de Cargos. `GET /api/v1/cargos`, `GET /api/v1/cargos/{id}` y `GET /api/v1/cargos/{cargoId}/skills` MUST requerir autenticación. Las mutaciones de Cargos y del subrecurso de skills MUST requerir rol `Administrador`. Habilitar autenticación MUST NOT romper el acceso anónimo del resto de contratos públicos de lectura.
(Previously: todos los endpoints read-only existentes podían consumirse anónimamente.)

#### Scenario: Lectura pública anónima permitida
- GIVEN la API está disponible y existe un recurso público que no es Cargo
- WHEN un cliente sin credenciales solicita ese endpoint de lectura
- THEN la API MUST responder exitosamente sin autenticación.

#### Scenario: Lectura anónima de Cargos rechazada
- GIVEN la API está disponible y existen datos de Cargos
- WHEN un cliente sin credenciales solicita una lectura de Cargos o `GET /api/v1/cargos/{cargoId}/skills`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Lectura autenticada de Cargos exitosa
- GIVEN un cliente autenticado
- WHEN solicita un endpoint de lectura de Cargos
- THEN la API MUST responder `2xx` con el contrato documentado de Cargos.

#### Scenario: Mutación de Cargos protegida por rol administrador
- GIVEN una solicitud válida de mutación sobre Cargos o su subrecurso de skills
- WHEN la ejecuta un cliente autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND, si la ejecuta un `Administrador`, MUST responder `2xx`.
