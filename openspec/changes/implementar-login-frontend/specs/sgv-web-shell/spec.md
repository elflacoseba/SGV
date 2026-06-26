# Delta para sgv-web-shell

## MODIFIED Requirements

### Requirement: No authentication dependency

El sistema MUST mantener `/auth/sign-in` accesible para usuarios no autenticados, MUST proteger el dashboard inicial y el shell autenticado detrás de sesión web, y MUST separar el layout de autenticación del layout principal. El sistema MUST NOT incorporar UI de registro, recuperación de contraseña ni navegación de account-management en esta entrega.

(Previously: el shell era completamente público y no mostraba login, logout ni navegación de cuenta.)

#### Scenario: Acceso anónimo al shell protegido

- GIVEN un usuario no autenticado
- WHEN abre el punto de entrada protegido del shell SGV
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`

#### Scenario: Acceso público a login

- GIVEN un usuario no autenticado
- WHEN abre `/auth/sign-in`
- THEN la pantalla MUST renderizarse sin redirigir al shell autenticado
- AND la vista MUST usar un layout distinto de `_VerticalLayout`

#### Scenario: UI de cuenta acotada

- GIVEN el shell autenticado renderizado
- WHEN el usuario revisa las acciones visibles de cuenta
- THEN la interfaz MUST ofrecer logout
- AND la interfaz MUST NOT mostrar registro, forgot password ni account-management
