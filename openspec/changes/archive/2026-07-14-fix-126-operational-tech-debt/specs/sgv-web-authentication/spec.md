# Delta para `sgv-web-authentication`

Delta del change `2026-07-14-fix-126-operational-tech-debt` (issue
#126): refuerza la consistencia lingüística del copy de error en la
UX de login para excepciones de transporte y timeout.

Trazabilidad: AC-2, AC-3.

## Requisitos AÑADIDOS

### Requisito: Consistencia lingüística del copy de error en login

El PageModel de login (`SignIn.cshtml.cs`) DEBE mostrar mensajes de
error de transporte (`HttpRequestException`, `TaskCanceledException`)
en español neutro/profesional, consistente con el tono del resto de la
UI de SGV. Los mensajes DEBEN ser accibles (texto plano, no
dependientes de color ni ícono exclusivamente).

#### Escenario: Mensaje de error de transporte es legible y en español

- **DADO** que `SignInModel.OnPostAsync` intenta llamar a `AuthApiClient.LoginAsync`
- **Y** la API no es accesible (fallo de red, resolución DNS, conexión rechazada)
- **CUANDO** `AuthApiClient.LoginAsync` lanza `HttpRequestException`
- **ENTONCES** la página DEBE renderizarse con un mensaje en español neutro/profesional
- **Y** el mensaje DEBE ser texto plano visible (no oculto detrás de un ícono o color)
- **Y** el usuario DEBE permanecer en la página de login (sin redirección a `/Error`).

#### Escenario: Mensaje de timeout es legible y en español

- **DADO** que `SignInModel.OnPostAsync` intenta llamar a `AuthApiClient.LoginAsync`
- **Y** la API no responde dentro del timeout del cliente (10s)
- **Y** el `CancellationToken` NO fue cancelado por el caller
- **CUANDO** `AuthApiClient.LoginAsync` lanza `TaskCanceledException`
- **ENTONCES** la página DEBE renderizarse con un mensaje de timeout en español neutro/profesional
- **Y** el mensaje DEBE ser texto plano visible (no oculto detrás de un ícono o color)
- **Y** el usuario DEBE permanecer en la página de login (sin redirección a `/Error`).

## Source

- `openspec/specs/sgv-web-authentication/spec.md` (main spec vigente)
- `openspec/changes/2026-07-14-fix-126-operational-tech-debt/design.md` §4.B (SignIn UX boundary)

## Verification

- `SignInTransportTests`: 4 tests covering HttpRequestException and TaskCanceledException UX
- `WebAuthenticationTests`: existing tests confirm no regression in login flow
