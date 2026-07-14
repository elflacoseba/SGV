# Delta para `web-apiclient-transport-contract`

Delta del change `2026-07-14-fix-126-operational-tech-debt` (issue
#126): agrega timeouts explícitos de 10s en `AuthApiClient` y
`UnidadOrganizativaApiClient`, y define la UX de mensajes de error
de transporte en `SignInModel.OnPostAsync` para `HttpRequestException`
y `TaskCanceledException`.

Trazabilidad: AC-1, AC-2, AC-3.

## ADDED Requirements

### Requirement: AuthApiClient timeout 10s

`AuthApiClient` MUST set `Timeout = TimeSpan.FromSeconds(10)` on its
inner `HttpClient`. This timeout governs the entire request lifecycle
including connection, sending, and receiving.

#### Scenario: AuthApiClient timeout raises TaskCanceledException

- **GIVEN** `AuthApiClient` is configured with `Timeout = 10s`
- **AND** the upstream API does not respond within 10 seconds
- **WHEN** `SignInModel.OnPostAsync` invokes `AuthApiClient.LoginAsync`
- **THEN** a `TaskCanceledException` MUST be thrown
- **AND** the exception MUST NOT be caught and swallowed by the client.

### Requirement: UnidadOrganizativaApiClient timeout 10s

`UnidadOrganizativaApiClient` MUST set `Timeout =
TimeSpan.FromSeconds(10)` on its inner `HttpClient`. This timeout
governs the entire request lifecycle including connection, sending,
and receiving.

#### Scenario: UnidadOrganizativaApiClient timeout raises TaskCanceledException

- **GIVEN** `UnidadOrganizativaApiClient` is configured with `Timeout = 10s`
- **AND** the upstream API does not respond within 10 seconds
- **WHEN** a consumer invokes any operation on `UnidadOrganizativaApiClient`
- **THEN** a `TaskCanceledException` MUST be thrown
- **AND** the exception MUST NOT be caught and swallowed by the client.

### Requirement: SignIn UX for transport exceptions

`SignInModel.OnPostAsync` MUST catch `HttpRequestException` and
display a Spanish error message: "No se pudo conectar con el
servidor. Verificá tu conexión y volvé a intentar." MUST catch
`TaskCanceledException` when the `CancellationToken` was NOT canceled
by the caller and display: "El servidor tardó demasiado en responder.
Volvé a intentar en unos segundos." When the `CancellationToken` IS
cancelled by the caller, `TaskCanceledException` MUST propagate
without being caught by this handler.

#### Scenario: HttpRequestException shows Spanish error message

- **GIVEN** `SignInModel.OnPostAsync` attempts to call `AuthApiClient.LoginAsync`
- **AND** the API is unreachable (network failure, DNS resolution failure, connection refused)
- **WHEN** `AuthApiClient.LoginAsync` throws `HttpRequestException`
- **THEN** the page MUST render with a Spanish error message: "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar."
- **AND** the user MUST remain on the sign-in page (no redirect to `/Error`).

#### Scenario: TaskCanceledException (non-CT) shows timeout message

- **GIVEN** `SignInModel.OnPostAsync` attempts to call `AuthApiClient.LoginAsync`
- **AND** the API does not respond within the client timeout (10s)
- **AND** the `CancellationToken` was NOT cancelled by the caller
- **WHEN** `AuthApiClient.LoginAsync` throws `TaskCanceledException`
- **THEN** the page MUST render with a Spanish error message: "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos."
- **AND** the user MUST remain on the sign-in page (no redirect to `/Error`).

## Source

- `openspec/specs/web-apiclient-transport-contract/spec.md:9-35` (existing propagation contract for typed clients)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.A (timeout strategy)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.B (SignIn UX boundary)

## Verification

- `AuthApiClientTimeoutTests`: 3 tests covering 10s timeout propagation
- `SignInTransportTests`: 4 tests covering HttpRequestException and TaskCanceledException UX
