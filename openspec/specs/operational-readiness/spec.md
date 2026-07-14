# Operational Readiness Specification

## Purpose

Define the operational readiness contract for SGV API and Web: health probes, startup validation, response shape, and runtime MySQL documentation. This spec ensures the system is observable and fail-safe at the infrastructure level without exposing business data.

## Requirements

### Requirement: API Liveness Probe

`SGV.Api` MUST expose `GET /health/live` returning `200 OK` for any caller, including anonymous requests. The liveness probe MUST NOT depend on MySQL or any external dependency. Its purpose is to confirm the process is running and accepting requests.

#### Scenario: API liveness returns 200 anonymously

- **GIVEN** `SGV.Api` is running
- **WHEN** a client sends `GET /health/live` without credentials
- **THEN** the API MUST respond `200 OK`
- **AND** the response MUST NOT require authentication.

### Requirement: API Readiness Probe

`SGV.Api` MUST expose `GET /health/ready` that checks MySQL connectivity via `AddDbContextCheck<SgvDbContext>`. The probe MUST return `200 OK` when MySQL is healthy and `503 Service Unavailable` when MySQL is unreachable or reports `Unhealthy`.

#### Scenario: API readiness returns 200 when MySQL is healthy

- **GIVEN** `SGV.Api` is running and MySQL is reachable
- **WHEN** a client sends `GET /health/ready`
- **THEN** the API MUST respond `200 OK`
- **AND** the response body MUST indicate `status: "Healthy"`.

#### Scenario: API readiness returns 503 when MySQL is unhealthy

- **GIVEN** `SGV.Api` is running and MySQL is unreachable or reports `Unhealthy`
- **WHEN** a client sends `GET /health/ready`
- **THEN** the API MUST respond `503 Service Unavailable`
- **AND** the response body MUST indicate `status: "Unhealthy"` or `status: "Degraded"`.

### Requirement: Web Liveness Probe

`SGV.Web` MUST expose `GET /health/live` returning `200 OK` for any caller, including anonymous requests without authentication cookies. The probe MUST NOT redirect to `/auth/sign-in`. Its purpose is to confirm the web process is running.

#### Scenario: Web liveness returns 200 anonymously without redirect

- **GIVEN** `SGV.Web` is running with cookie authentication configured
- **WHEN** a client sends `GET /health/live` without an authentication cookie
- **THEN** Web MUST respond `200 OK`
- **AND** MUST NOT redirect to `/auth/sign-in`
- **AND** MUST NOT respond `302 Found`.

### Requirement: Web Readiness Probe

`SGV.Web` MUST expose `GET /health/ready` that delegates to an
upstream health check (`SgvApiUpstreamHealthCheck`) hitting
`<SgvApi:BaseUrl>/health/live` with a 3-second timeout budget. The
probe MUST return `200 OK` when the upstream API is reachable and
`503 Service Unavailable` when the upstream is unreachable or the
budget is exceeded.

#### Scenario: Web readiness returns 200 when upstream API is healthy

- **GIVEN** `SGV.Web` is running and `SGV.Api` is reachable at `<SgvApi:BaseUrl>/health/live`
- **WHEN** a client sends `GET /health/ready`
- **THEN** Web MUST respond `200 OK`
- **AND** the response body MUST indicate `status: "Healthy"`.

### Requirement: MySQL Startup Validation

`SGV.Api` MUST fail loud on startup if `ConnectionStrings:SgvDatabase` is missing, empty, or invalid. The validation MUST occur during host build (via `IValidateOptions<SgvDbContextOptions>` with `ValidateOnStart = true`) and MUST throw `OptionsValidationException` before the application starts accepting requests.

#### Scenario: Missing connection string causes startup failure

- **GIVEN** `ConnectionStrings:SgvDatabase` is not configured or is empty
- **WHEN** `SGV.Api` builds the host
- **THEN** the application MUST throw `OptionsValidationException`
- **AND** MUST NOT start listening for requests.

### Requirement: Health Check Response Shape

All health endpoints MUST return a JSON response conforming to the shape: `{ status, totalDurationMs, entries: [{ name, status, description, durationMs }] }`. The response MUST be produced by the shared `HealthCheckResponseWriter` to ensure consistency across API and Web.

#### Scenario: Health response matches expected JSON shape

- **GIVEN** a health endpoint (`/health/live` or `/health/ready`) in API or Web
- **WHEN** a client sends a request and receives a `200` or `503` response
- **THEN** the response body MUST be valid JSON
- **AND** MUST contain top-level fields `status` and `totalDurationMs`
- **AND** MUST contain an `entries` array where each entry has `name`, `status`, `description`, and `durationMs`.

### Requirement: Probes Do Not Expose Business Data

Health endpoints MUST NOT expose persisted entities, domain objects, audit records, stack traces, or exception details. The response MUST contain only operational data: status, check names, descriptions, and durations.

#### Scenario: Health response does not contain business data or stack traces

- **GIVEN** a health endpoint reports `Unhealthy` due to a MySQL connectivity failure
- **WHEN** a client inspects the response body
- **THEN** the JSON MUST NOT contain fields like `stackTrace`, `exception`, or any entity data
- **AND** the response MUST contain only operational check information.

### Requirement: Health Endpoints Are Anonymous

Health endpoints (`/health/live` and `/health/ready` in both API and Web) MUST be accessible without authentication as exceptions to the default-deny `FallbackPolicy`. The `FallbackPolicy = RequireAuthenticatedUser()` MUST remain intact for all other endpoints. The exception MUST be implemented via explicit `.AllowAnonymous()` on each `MapHealthChecks(...)` call.

#### Scenario: Health endpoints bypass fallback policy

- **GIVEN** `SGV.Api` or `SGV.Web` is running with `FallbackPolicy = RequireAuthenticatedUser()`
- **WHEN** a client sends `GET /health/live` or `GET /health/ready` without credentials
- **THEN** the endpoint MUST respond with `200` or `503` (not `401 Unauthorized`)
- **AND** the fallback policy MUST remain enforced for all other endpoints.

### Requirement: MySQL Runtime Contract Documentation

`docs/decisiones-implementacion.md` MUST contain a dedicated subsection documenting the runtime MySQL contract. The documentation MUST cover: liveness probes, readiness probes, anonymous access for health endpoints, recommended connection timeout, `ServerVersion.AutoDetect` behavior, design-time vs runtime separation, secrets management by environment, migration strategy, and startup validation. The documentation MUST explicitly mark the JWT dev placeholder as not suitable for production.

#### Scenario: Documentation section exists with required topics

- **GIVEN** the repository contains `docs/decisiones-implementacion.md`
- **WHEN** a developer searches for the MySQL runtime contract
- **THEN** a dedicated subsection MUST exist covering liveness, readiness, anonimato, timeout, AutoDetect, design-time/runtime, secrets, migraciones, and validación startup
- **AND** the JWT dev placeholder MUST be explicitly marked as not production-ready.

## Source

- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.C (health check architecture)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.D (startup validation)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.E (response writer)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.F (AutoDetect trade-off)

## Verification

- API: `ApiHealthTests.Live_NoAuth_Returns200`, `ApiHealthTests.Ready_Returns200_HealthyJson`, `ApiHealthTests.Ready_Returns503_UnhealthyJson`, `ApiHealthTests.Ready_ResponseHasNoStackTrace`, `StartupValidationTests.MissingConnectionString_ThrowsOptionsValidationException`
- Web: `WebHealthTests.Live_AnonymousReturns200`, `WebHealthTests.Ready_Returns200_HealthyJson`, `WebHealthTests.Ready_NoCookie_NoRedirect`, `WebHealthTests.Ready_ResponseHasNoStackTrace`
