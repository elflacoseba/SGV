# Delta para `sgv-readonly-api`

Delta del change `2026-07-14-fix-126-operational-tech-debt` (issue
#126): exceptúa los probes operacionales `/health/live` y
`/health/ready` (tanto en `SGV.Api` como en `SGV.Web`) de la postura
de default-deny declarada en `openspec/specs/sgv-readonly-api/spec.md:174-191`,
sin relajar la `FallbackPolicy = RequireAuthenticatedUser()` ni
extender la excepción a ninguna otra ruta. Cross-referencia
`operational-readiness/spec.md:77-96` (REQ probes anónimos) para
mantener el contrato unificado.

Trazabilidad: AC-4, AC-5, AC-6, AC-7 del proposal; cross-ref
`operational-readiness/spec.md`.

## ADDED Requirements

### Requirement: Excepción de anonimato para probes operacionales

Los endpoints `GET /health/live` y `GET /health/ready` en `SGV.Api` y
`SGV.Web` MUST ser accesibles sin autenticación, como excepción
puntual del default-deny declarado por el requirement "No
Authentication Requirement" vigente. La `FallbackPolicy =
RequireAuthenticatedUser()` MUST permanecer intacta para el resto de
la API. La excepción MUST limitarse exclusivamente a las dos rutas
listadas y MUST NOT extenderse a ningún otro endpoint. La mecánica
MUST ser `.AllowAnonymous()` explícito en cada `MapHealthChecks(...)`
de `SGV.Api/Program.cs` y `SGV.Web/Program.cs`.

#### Scenario: Probe API `/health/live` responde 200 anónimo

- **GIVEN** `SGV.Api` arrancada con la fallback policy global activa
- **AND** sin credenciales (sin bearer token ni cookie)
- **WHEN** un cliente solicita `GET /health/live`
- **THEN** la API MUST responder `200 OK`
- **AND** MUST NOT responder `401 Unauthorized`.

#### Scenario: Probe API `/health/ready` responde 503 anónimo cuando MySQL está caído

- **GIVEN** `SGV.Api` arrancada con la fallback policy global activa
- **AND** MySQL inaccesible o el readiness check reporta `Unhealthy`
- **AND** sin credenciales
- **WHEN** un cliente solicita `GET /health/ready`
- **THEN** la API MUST responder `503 Service Unavailable` con cuerpo JSON describiendo el estado
- **AND** MUST NOT responder `401 Unauthorized`.

#### Scenario: Probe Web `/health/live` y `/health/ready` responden 200/503 anónimos sin redirigir

- **GIVEN** `SGV.Web` arrancada con cookie auth configurada
- **AND** sin cookie de autenticación presente en el request
- **WHEN** un cliente (orquestador) solicita `GET /health/live` o `GET /health/ready`
- **THEN** Web MUST responder `200` o `503` según el estado
- **AND** MUST NOT redirigir a `/auth/sign-in`
- **AND** MUST NOT responder `302 Found`.

#### Scenario: Default-deny sigue vigente para el resto de la API

- **GIVEN** la excepción de anonimato aplica solo a `/health/live` y `/health/ready`
- **WHEN** un cliente sin credenciales solicita cualquier otro endpoint (incluidos `/api/v1/personas`, `/api/v1/unidades-organizativas`, `/api/v1/niveles-cargo`, `/api/v1/tipos-unidad-organizativa`, lecturas o mutaciones de Cargos u otros recursos)
- **THEN** la API MUST responder `401 Unauthorized`
- **AND** la fallback policy `RequireAuthenticatedUser` MUST NO estar relajada.

### Requirement: Probes operacionales no exponen datos de negocio

Los probes `GET /health/live` y `GET /health/ready` MUST responder
únicamente con el estado del check (Healthy / Unhealthy / Degraded) y
detalles operativos del check (nombre, descripción acotada, duración).
MUST NOT exponer datos persistidos, entidades de dominio, registros de
auditoría ni mensajes de excepción con stack traces. El cuerpo de la
respuesta MUST ser JSON con la shape `{ status, totalDurationMs,
entries: [{ name, status, description, durationMs }] }` definida por
el writer compartido `HealthCheckResponseWriter` (ver
`design.md:§4.C`).

#### Scenario: Cuerpo JSON del probe no contiene stack trace ni exception

- **GIVEN** un probe `/health/ready` que reporta `Unhealthy` por fallo de transporte o de DB
- **WHEN** un cliente inspecciona el cuerpo de la respuesta
- **THEN** el JSON MUST contener `status: "Unhealthy"`
- **AND** MUST NO contener campos `stackTrace`, `exception`, ni mensajes con detalle interno del servidor.

## Source

- `openspec/specs/sgv-readonly-api/spec.md:174-191` (default-deny vigente que este delta exceptúa parcialmente)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/specs/operational-readiness/spec.md:77-96` (REQ probes anónimos; cross-ref unificado)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.G (rationale y mecánica de `.AllowAnonymous()` por endpoint)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.C (shape JSON del writer compartido)

## Verification

- API: `ApiHealthTests.Live_NoAuth_Returns200`, `ApiHealthTests.Ready_NoAuth_Returns503_UnhealthyJson`, `ApiHealthTests.Ready_ResponseHasNoStackTrace`
- Web: `WebHealthTests.Live_AnonymousReturns200`, `WebHealthTests.Ready_NoCookie_NoRedirect`, `WebHealthTests.Ready_ResponseHasNoStackTrace`