# Setup inicial del Administrador

## Propósito

Permitir el bootstrap one-time del primer usuario `Administrador` cuando `AspNetUsers` está vacía, mediante un flujo anónimo seguro y una página Razor que conserve la experiencia de autenticación existente.

## Requirements

### REQ-SETUP-001 — Estado de setup

La API MUST exponer `GET /api/v1/setup/status` sin autenticación. DEBE indicar `requiresSetup=true` cuando `AspNetUsers` esté vacía y `requiresSetup=false` cuando exista al menos un usuario.

#### Scenario: Base sin usuarios requiere setup

- GIVEN `AspNetUsers` está vacía
- WHEN un cliente solicita el estado de setup
- THEN la API MUST responder exitosamente con `requiresSetup=true`

#### Scenario: Base con usuarios no requiere setup

- GIVEN `AspNetUsers` contiene al menos un usuario
- WHEN un cliente solicita el estado de setup
- THEN la API MUST responder exitosamente con `requiresSetup=false`

### REQ-SETUP-002 — Creación atómica del primer Administrador

La API MUST exponer `POST /api/v1/setup` sin autenticación. Con datos válidos, MUST crear una Persona, un Usuario vinculado y el rol `Administrador` dentro de una única transacción. El setup MUST estar habilitado en Development, Staging y Production.

#### Scenario: Creación válida

- GIVEN `AspNetUsers` está vacía y el request contiene datos válidos
- WHEN un cliente anónimo envía `POST /api/v1/setup`
- THEN la API MUST crear Persona, Usuario y rol `Administrador`
- AND MUST confirmar todas las operaciones atómicamente

#### Scenario: Setup ya completado

- GIVEN `AspNetUsers` contiene al menos un usuario
- WHEN un cliente envía `POST /api/v1/setup`
- THEN la API MUST rechazar la operación con `409 Conflict` o `404 Gone`
- AND MUST NOT crear Persona ni Usuario

#### Scenario: Validación de Identity

- GIVEN `AspNetUsers` está vacía
- WHEN el request incumple la política de password u otra validación de Identity
- THEN la API MUST responder `400`
- AND MUST devolver errores por campo en español mediante `IdentityErrorMap`

#### Scenario: Fallo transaccional

- GIVEN una operación de persistencia falla durante el setup
- WHEN se procesa el request
- THEN la API MUST responder `500` o `503`
- AND MUST dejar sin persistir Persona y Usuario parcialmente creados

### REQ-SETUP-003 — Concurrencia e idempotencia

La guarda `AnyUsersAsync()` MUST ejecutarse dentro de la transacción de creación. El sistema MUST usar el índice único de Identity sobre `UserName` como defensa adicional contra duplicados y MUST evitar dos setups exitosos concurrentes.

#### Scenario: Requests concurrentes

- GIVEN `AspNetUsers` está vacía
- WHEN dos requests válidos de setup llegan concurrentemente
- THEN como máximo uno MUST completar exitosamente
- AND el otro MUST recibir un conflicto o recurso ya consumido

### REQ-SETUP-004 — Auditoría y seguridad operacional

La creación del primer Administrador MUST registrarse en `Auditorias` con `userId="system"`. El endpoint MUST aplicar rate limiting fijo en todos los ambientes y MUST registrar creación exitosa e intentos fallidos sin passwords, tokens ni secretos.

#### Scenario: Auditoría de creación

- GIVEN un setup válido completa Persona, Usuario y rol
- WHEN la operación queda confirmada
- THEN `Auditorias` MUST contener el registro de creación
- AND su `userId` MUST ser `system`

#### Scenario: Rate limit y logging seguro

- GIVEN múltiples requests anónimos al endpoint de setup
- WHEN se supera el límite configurado
- THEN el endpoint MUST aplicar rate limiting
- AND los logs MUST identificar intento, resultado y correlación sin datos sensibles

### REQ-SETUP-005 — Formulario web de setup

`SGV.Web` MUST exponer `/auth/setup` con el mismo `_AuthLayout` que `SignIn.cshtml`, token anti-forgery y nueve campos visibles: Nombres, Apellidos, Legajo, Email, UserName, Password, TipoDocumento, NumeroDocumento y Teléfono. `TipoDocumento` MUST ser un dropdown del catálogo del bloque GUID `71000000-…`.

#### Scenario: Redirección desde SignIn

- GIVEN un cliente no autenticado y `AspNetUsers` vacía
- WHEN visita `/auth/sign-in`
- THEN la página MUST redirigir a `/auth/setup`

#### Scenario: Render del formulario

- GIVEN un cliente no autenticado y `AspNetUsers` vacía
- WHEN visita `/auth/setup`
- THEN la página MUST mostrar los nueve campos
- AND MUST usar `_AuthLayout` e incluir anti-forgery token

#### Scenario: Setup no disponible

- GIVEN `AspNetUsers` contiene al menos un usuario
- WHEN un cliente no autenticado visita `/auth/setup`
- THEN la página MUST redirigir a `/auth/sign-in` o devolver `404 Gone`

#### Scenario: Catálogo de documentos

- GIVEN el catálogo de tipos de documento está disponible
- WHEN se renderiza `/auth/setup`
- THEN el campo `TipoDocumento` MUST mostrar sus opciones catalogadas

### REQ-SETUP-006 — Resultado y errores del formulario

Un submit exitoso MUST usar PRG y redirigir a `/auth/sign-in` con mensaje de éxito, sin autenticar automáticamente. Los errores de validación MUST mostrarse por campo en español. Los fallos de transporte MUST producir un mensaje recuperable y no un reintento ciego.

#### Scenario: Submit exitoso

- GIVEN el formulario contiene datos válidos y el API confirma el setup
- WHEN el cliente envía el formulario
- THEN la página MUST redirigir vía PRG a `/auth/sign-in`
- AND MUST mostrar un mensaje de éxito

#### Scenario: Errores de validación

- GIVEN el API responde `400` con errores por campo
- WHEN se procesa el submit
- THEN la página MUST permanecer en `/auth/setup`
- AND MUST mostrar los errores en español junto a los campos correspondientes

#### Scenario: Error de transporte

- GIVEN el API no está disponible o agota el timeout
- WHEN se procesa el submit
- THEN la página MUST permanecer en `/auth/setup`
- AND MUST mostrar un mensaje de error recuperable
