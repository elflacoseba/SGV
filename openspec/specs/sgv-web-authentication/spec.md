# Especificación de autenticación web SGV

## Purpose

Definir el primer flujo real de autenticación en `SGV.Web` consumiendo `SGV.Api`, con login, logout, dashboard inicial vacío y definición centralizada de endpoints de autenticación.

## Requirements

### Requirement: Pantalla de inicio de sesión web

El sistema MUST exponer `/auth/sign-in` como página pública de autenticación con layout separado del shell principal, y MUST limitar esta pantalla a inicio de sesión; registro y recuperación de credenciales MUST NOT formar parte de este corte.

#### Scenario: Usuario anónimo abre login

- GIVEN un usuario no autenticado
- WHEN navega a `/auth/sign-in`
- THEN la aplicación MUST responder con la pantalla de login
- AND la página MUST renderizarse sin sidebar ni topbar del shell principal

#### Scenario: Flujos fuera de alcance no aparecen

- GIVEN la pantalla de login renderizada
- WHEN el usuario revisa las acciones visibles
- THEN la página MUST NO requerir acciones de registro ni recuperación de contraseña

### Requirement: Inicio de sesión contra SGV.Api

El sistema MUST validar credenciales usando el contrato existente de `POST /api/v1/auth/login`, y MUST crear una sesión web local solo cuando el API autentica correctamente.

#### Scenario: Login exitoso

- GIVEN un usuario con credenciales válidas
- WHEN envía el formulario de login
- THEN `SGV.Web` MUST autenticar contra `SGV.Api`
- AND la respuesta exitosa MUST redirigir al dashboard inicial vacío

#### Scenario: Login inválido

- GIVEN un usuario con credenciales inválidas
- WHEN envía el formulario de login
- THEN la sesión web MUST NO crearse
- AND la pantalla MUST permanecer en login mostrando un error de autenticación

### Requirement: Logout y protección del dashboard

El sistema MUST requerir sesión autenticada para acceder al dashboard inicial y MUST ofrecer logout explícito que invalide la sesión web.

#### Scenario: Acceso anónimo a dashboard

- GIVEN un usuario no autenticado
- WHEN solicita el dashboard inicial
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`

#### Scenario: Logout exitoso

- GIVEN un usuario autenticado en el dashboard inicial
- WHEN ejecuta la acción de logout
- THEN la sesión web MUST invalidarse
- AND un acceso posterior al dashboard MUST redirigir a `/auth/sign-in`

### Requirement: Endpoints de autenticación centralizados

El sistema MUST consumir las rutas de autenticación desde una definición centralizada y reutilizable compartida con `SGV.Api`; los PageModels de `SGV.Web` MUST NOT duplicar literales de rutas del API.

#### Scenario: Consumo web de endpoints autenticación

- GIVEN una interacción de login o logout en `SGV.Web`
- WHEN la página necesita resolver la ruta del API correspondiente
- THEN la ruta MUST obtenerse desde la definición centralizada compartida

## Requisitos AÑADIDOS

> Delta introducida por el change `2026-07-10-endurecer-cookie-cors-deploy` (issue #101, PR #106). Verificado en `openspec/changes/archive/2026-07-10-endurecer-cookie-cors-deploy/verify-report.md`.

### Requisito: Atributos de la cookie de autenticación por ambiente

La cookie que carga el ticket de autenticación de `SGV.Web` DEBE aplicar atributos de seguridad acordes al ambiente de ejecución para evitar la filtración del JWT que `ApiBearerTokenHandler` reenvía a `SGV.Api` como `Authorization: Bearer`. El resto de los requisitos de esta especificación (login, logout, centralización de endpoints) NO se modifican.

| Atributo        | Development        | Distinto de Development |
|-----------------|--------------------|--------------------------|
| `HttpOnly`      | `true`             | `true`                   |
| `SameSite`      | `Lax`              | `Lax`                    |
| `SecurePolicy`  | `SameAsRequest`    | `Always`                 |

#### Escenario: Atributos en ambiente distinto de Development

- **DADO** que `ASPNETCORE_ENVIRONMENT` es distinto de `Development` (por ejemplo `Production` o `Staging`)
- **CUANDO** la aplicación registra la autenticación por cookies en `src/SGV.Web/Program.cs`
- **ENTONCES** la `CookieOptions` resultante DEBE tener `HttpOnly == true`
- **Y** `SameSite == SameSiteMode.Lax`
- **Y** `SecurePolicy == CookieSecurePolicy.Always`.

#### Escenario: Atributos en Development

- **DADO** que `ASPNETCORE_ENVIRONMENT == "Development"`
- **CUANDO** la aplicación registra la autenticación por cookies
- **ENTONCES** la `CookieOptions` resultante DEBE tener `HttpOnly == true`
- **Y** `SameSite == SameSiteMode.Lax`
- **Y** `SecurePolicy == CookieSecurePolicy.SameAsRequest`.

#### Escenario: Atributos verificables desde el contenedor de DI

- **DADO** que la cookie de autenticación está registrada
- **CUANDO** se inspeccionan las opciones del esquema cookie expuestas por el contenedor de DI
- **ENTONCES** los valores de `HttpOnly`, `SameSite` y `SecurePolicy` DEBEN coincidir con la tabla de atributos por ambiente
- **Y** ningún path DEBE sobrescribir esos atributos fuera de la rama de registro de `AddCookie(...)`.
