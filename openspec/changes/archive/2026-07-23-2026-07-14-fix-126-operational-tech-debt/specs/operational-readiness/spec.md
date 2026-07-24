# Especificación de Operational Readiness

## Propósito

Contrato operativo de liveness, readiness y validación de la
configuración MySQL al startup para `SGV.Api` y `SGV.Web`. Introducida
por el change `2026-07-14-fix-126-operational-tech-debt` (issue #126).
Ortogonal a `web-apiclient-transport-contract`, `sgv-web-authentication`
y `api-cors-allowed-origins-validation`.

## Requirements

### Requirement: API liveness sin dependencias externas

`SGV.Api` MUST exponer `GET /health/live` que responda `200 OK` sin
MySQL ni autenticación (predicado que excluye el tag `ready`).

#### Scenario: GET `/health/live` responde 200 sin MySQL

- **GIVEN** `SGV.Api` arrancada con MySQL inaccesible
- **WHEN** `GET /health/live` anónimo
- **THEN** la respuesta MUST ser `200 OK`
- **AND** el endpoint MUST NO ejecutar `CanConnectAsync` ni resolver `SgvDbContext`.

### Requirement: API readiness con MySQL

`SGV.Api` MUST exponer `GET /health/ready` con tag `ready`. Con
`SgvDbContext.CanConnectAsync` retornando `true`, MUST responder `200
OK` con cuerpo JSON; con MySQL inaccesible o check `Unhealthy`, MUST
responder `503 Service Unavailable` con JSON `status: Unhealthy` y causa.

#### Scenario: `/health/ready` 200 con MySQL alcanzable

- **GIVEN** MySQL alcanzable y `CanConnectAsync` retorna `true`
- **WHEN** `GET /health/ready` anónimo
- **THEN** la respuesta MUST ser `200 OK` con cuerpo JSON describiendo el estado.

#### Scenario: `/health/ready` 503 con MySQL caído

- **GIVEN** MySQL inaccesible o el check reporta `Unhealthy`
- **WHEN** `GET /health/ready` anónimo
- **THEN** la respuesta MUST ser `503 Service Unavailable`
- **AND** el cuerpo MUST ser JSON con `status: Unhealthy`.

### Requirement: Web liveness sin upstream

`SGV.Web` MUST exponer `GET /health/live` que responda `200 OK` sin
contacto con la API upstream ni autenticación.

#### Scenario: GET `/health/live` responde 200 anónimo

- **GIVEN** `SGV.Web` arrancada
- **WHEN** `GET /health/live` anónimo
- **THEN** la respuesta MUST ser `200 OK`.

### Requirement: Web readiness con upstream

`SGV.Web` MUST exponer `GET /health/ready` con budget de 3 s
(`HttpClient.Timeout = TimeSpan.FromSeconds(3)`) contra
`<SgvApi:BaseUrl>/health/live`. `200 OK` si el upstream responde `200`
dentro del presupuesto; `503 Service Unavailable` con cuerpo
`Unhealthy` si no responde o responde con código no exitoso.

#### Scenario: `/health/ready` 200 con API viva

- **GIVEN** la API upstream responde `200` en `<SgvApi:BaseUrl>/health/live` dentro de 3 s
- **WHEN** `GET /health/ready` anónimo en Web
- **THEN** la respuesta MUST ser `200 OK`.

#### Scenario: `/health/ready` 503 con upstream caído

- **GIVEN** la API upstream no responde dentro de 3 s
- **WHEN** `GET /health/ready` anónimo en Web
- **THEN** la respuesta MUST ser `503 Service Unavailable`
- **AND** el cuerpo MUST describir el estado como `Unhealthy`.

### Requirement: Probes anónimos en API y Web

Los endpoints `/health/live` y `/health/ready` de `SGV.Api` y `SGV.Web`
MUST ser accesibles sin autenticación. La API MUST aplicar
`.AllowAnonymous()` explícitamente, sin relajar la fallback policy
global `RequireAuthenticatedUser`.

#### Scenario: Probe API sin credenciales

- **GIVEN** `SGV.Api` sin sesión ni bearer token
- **WHEN** `GET /health/live` o `GET /health/ready`
- **THEN** la respuesta MUST ser `200` o `503` según el estado
- **AND** MUST NO ser `401` ni redirección.

#### Scenario: Probe Web sin sesión

- **GIVEN** `SGV.Web` sin cookie de autenticación
- **WHEN** `GET /health/live` o `GET /health/ready`
- **THEN** la respuesta MUST ser `200` o `503` según el estado
- **AND** MUST NO redirigir a `/auth/sign-in`.

### Requirement: Validación fail-loud de ConnectionStrings:SgvDatabase al startup

`SGV.Api` MUST leer `ConnectionStrings:SgvDatabase` durante la
construcción del host y, si falta o es whitespace, MUST lanzar
`OptionsValidationException` con un mensaje que nombre la clave, antes
de `AddDbContext`/`UseMySql` (coherente con `ValidateOnStart` vigente
para JWT).

#### Scenario: Host falla al startup con connection string vacía

- **GIVEN** `ConnectionStrings:SgvDatabase` ausente o whitespace
- **WHEN** se construye el host de `SGV.Api`
- **THEN** `builder.Build()` MUST lanzar `OptionsValidationException`
- **AND** el mensaje MUST mencionar `ConnectionStrings:SgvDatabase`
- **AND** el proceso MUST NO continuar con `ServerVersion.AutoDetect`.

### Requirement: Documentación del contrato runtime MySQL

`docs/decisiones-implementacion.md` MUST contener una subsección que
documente: liveness vs readiness en `SGV.Api`, semántica de
`/health/ready`, `Connection Timeout` recomendado para
`ServerVersion.AutoDetect`, separación design-time factory
(`SgvDbContextFactory`) vs runtime, y ubicación de la connection string
por ambiente. El placeholder JWT dev NO DEBE aparecer como valor
productivo.

#### Scenario: Subsección presente y completa

- **GIVEN** `docs/decisiones-implementacion.md` al cierre del change
- **WHEN** se revisa la subsección de contrato MySQL
- **THEN** DEBE cubrir liveness, readiness, timeout recomendado para AutoDetect, separación design-time/runtime y ubicación de secrets por ambiente
- **AND** NO DEBE promover el placeholder JWT dev como valor productivo.