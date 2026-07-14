# Delta para `sgv-web-authentication`

Este delta agrega un requisito nuevo a la spec vigente
`sgv-web-authentication` para reflejar la frontera de UX del change
`2026-07-14-fix-126-operational-tech-debt` (issue #126). Refuerza que
el inicio de sesión debe responder con copy español accionable ante
fallos de transporte de la API upstream, manteniendo la consistencia
lingüística del resto del formulario. No se modifican los requisitos
existentes (login, logout, centralización de endpoints, atributos de
cookie por ambiente, validación JWT).

Trazabilidad: AC-2, AC-3 de
`openspec/changes/2026-07-14-fix-126-operational-tech-debt/proposal.md`.

## Requisitos AÑADIDOS

### Requisito: UX del login con errores de transporte en español

El handler `SignInModel.OnPostAsync` (`src/SGV.Web/Pages/Auth/SignIn.cshtml.cs:26`)
DEBE traducir los fallos de transporte del cliente `AuthApiClient` a
mensajes en español visibles para el usuario en el `validation-summary
ModelOnly` (`src/SGV.Web/Pages/Auth/SignIn.cshtml:20-22`), y DEBE
mantener la página `/auth/sign-in` sin redirigir. La terminología
empleada DEBE ser consistente con el resto del copy del formulario
("Credenciales inválidas.", "No se pudo validar la sesión de
autenticación."). La excepción solo se captura cuando el
`CancellationToken` del request NO está cancelado; cuando el token está
cancelado por el cliente, la excepción DEBE propagarse.

#### Escenario: Transporte caído durante el login

- **DADO** que `AuthApiClient.LoginAsync` lanza `HttpRequestException` (upstream caído, DNS, refused connection)
- **CUANDO** el usuario envía el formulario de login
- **ENTONCES** el `ModelState` DEBE contener un mensaje en español que indique que no se pudo contactar al servicio de autenticación
- **Y** la pantalla DEBE permanecer en `/auth/sign-in` mostrando el mensaje en el `validation-summary`
- **Y** la excepción NO DEBE propagarse al pipeline `UseExceptionHandler`.

#### Escenario: Timeout durante el login

- **DADO** que la API upstream demora más de 10 s
- **Y** `AuthApiClient.LoginAsync` lanza `TaskCanceledException`
- **Y** el `CancellationToken` del request NO está cancelado
- **CUANDO** el usuario envía el formulario de login
- **ENTONCES** el `ModelState` DEBE contener un mensaje en español indicando que la autenticación tardó demasiado
- **Y** la pantalla DEBE permanecer en `/auth/sign-in` mostrando el mensaje en el `validation-summary`
- **Y** la excepción NO DEBE propagarse al pipeline `UseExceptionHandler`.

#### Escenario: Cancelación cooperativa del request se respeta

- **DADO** que el `CancellationToken` del request fue cancelado por el cliente
- **CUANDO** `AuthApiClient.LoginAsync` lanza `TaskCanceledException` por la cancelación del token
- **ENTONCES** la excepción DEBE propagarse al pipeline
- **Y** NO DEBE agregarse ningún mensaje a `ModelState`
- **Y** NO DEBE capturarse como si fuera un timeout.