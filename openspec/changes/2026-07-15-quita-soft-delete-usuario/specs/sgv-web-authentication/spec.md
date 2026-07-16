# Delta para `sgv-web-authentication`

> Modifica la especificación base `openspec/specs/sgv-web-authentication/spec.md` para incorporar el rechazo de la sesión cookie cuando la cuenta autenticada queda bloqueada o eliminada físicamente, sin esperar al logout explícito ni al vencimiento natural.

## MODIFIED Requirements

### Requirement: Logout y protección del dashboard

El sistema MUST requerir sesión autenticada para acceder al dashboard inicial y MUST ofrecer logout explícito que invalide la sesión web. Adicionalmente, MUST redirigir a `/auth/sign-in` cuando la cuenta autenticada quede bloqueada con `LockoutEnd` futuro o eliminada físicamente, sin esperar logout ni expiración de cookie. El acceso MUST restaurarse solo mediante un login fresco posterior.
(Previously: solo trataba sesión autenticada y logout explícito; no cubría el rechazo por cuenta bloqueada o eliminada mid-session.)

#### Scenario: Acceso anónimo a dashboard

- GIVEN un usuario no autenticado
- WHEN solicita el dashboard inicial
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`

#### Scenario: Logout exitoso

- GIVEN un usuario autenticado en el dashboard inicial
- WHEN ejecuta la acción de logout
- THEN la sesión web MUST invalidarse
- AND un acceso posterior al dashboard MUST redirigir a `/auth/sign-in`

#### Scenario: Cuenta bloqueada mid-session redirige a login

- GIVEN un usuario autenticado en el dashboard con cookie vigente
- WHEN un `Administrador` bloquea esa cuenta vía API
- THEN la siguiente navegación MUST redirigir a `/auth/sign-in` sin esperar logout.

#### Scenario: Cuenta eliminada mid-session redirige a login

- GIVEN un usuario autenticado en el dashboard con cookie vigente
- WHEN un `Administrador` elimina físicamente esa cuenta
- THEN la siguiente navegación MUST redirigir a `/auth/sign-in` aunque la cookie siga presente.

## ADDED Requirements

### Requirement: Rechazo de cookie cuando la cuenta está bloqueada o eliminada

La cookie de autenticación web MUST NO considerarse válida mientras la cuenta del titular esté bloqueada con `LockoutEnd` futuro o haya sido eliminada físicamente. La siguiente petición web protegida para esa cuenta MUST redirigir al navegador a `/auth/sign-in`, sin esperar logout explícito ni expiración natural de la cookie. La API consumida vía `ApiBearerTokenHandler` también MUST rechazar el JWT asociado con `401` (observable cubierto en `identity-user-role-management`).

#### Scenario: Cookie activa rechazada tras bloqueo

- **DADO** usuario con cookie web vigente en `SGV.Web`
- **CUANDO** `Administrador` ejecuta `POST /api/v1/usuarios/{id}/bloquear`
- **ENTONCES** la siguiente navegación a una página protegida MUST redirigir a `/auth/sign-in`.

#### Scenario: Cookie activa rechazada tras eliminación física

- **DADO** usuario con cookie web vigente
- **CUANDO** `Administrador` ejecuta `DELETE /api/v1/usuarios/{id}`
- **ENTONCES** la siguiente navegación MUST redirigir a `/auth/sign-in` aunque la cookie siga presente.

#### Scenario: Desbloqueo requiere login fresco

- **DADO** sesión cookie previamente rechazada por bloqueo
- **CUANDO** `Administrador` ejecuta `POST /desbloquear` y el usuario navega con la cookie previa
- **ENTONCES** MUST seguir redirigiendo a `/auth/sign-in`; el acceso MUST restaurarse solo tras un login fresco.