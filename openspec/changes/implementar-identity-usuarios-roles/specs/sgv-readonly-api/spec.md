# Delta for SGV Read-only API

## MODIFIED Requirements

### Requirement: No Authentication Requirement

The system MUST allow access to existing read-only endpoints without requiring authentication or authorization in this version. Identity management endpoints and role-assignment operations MAY require authentication/authorization. Enabling authentication MUST NOT apply a global authorization requirement that breaks existing anonymous read access.
(Previously: the requirement allowed all read-only endpoints anonymously but did not distinguish new protected Identity operations.)

#### Scenario: Anonymous client reads supported data

- **GIVEN** the API is running and persisted data exists
- **WHEN** an unauthenticated client requests a supported resource collection
- **THEN** the API MUST process the request without requiring credentials
- **AND** the response MUST follow the same contract as an authenticated request would.

#### Scenario: Identity operation can require credentials

- **DADO** que un cliente no autenticado solicita administrar usuarios o asignar roles
- **CUANDO** la operación está definida como protegida
- **ENTONCES** el sistema MAY rechazarla por falta de credenciales o permisos.

#### Scenario: Authentication does not change public read contracts

- **DADO** que la autenticación está habilitada en SGV
- **CUANDO** un cliente anónimo consume endpoints públicos existentes de lectura
- **ENTONCES** el sistema MUST preservar acceso anónimo y contratos de respuesta existentes.
