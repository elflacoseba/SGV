# Propuesta — setup-admin-inicial-issue-195

> Issue origen: #195 — Crear una pantalla para crear el usuario Administrador  
> Pista visual: `InspinaTemplate/Inspinia/Pages/Auth/SignUp.cshtml`  
> Change: `setup-admin-inicial-issue-195` (kebab-case)

## 1. Contexto y problema
La creación actual exige crear primero una Persona y luego un Usuario, pero ambos endpoints requieren `[Authorize(Roles = RolesSgv.Administrador)]`. Cuando `AspNetUsers` está vacía no existe identidad capaz de ejecutar el flujo: hay un chicken-and-egg documentado bajo «Autorización del API» en `docs/decisiones-implementacion.md`.

## 2. Decisiones de producto confirmadas
### 2.1 Ambiente
Disponible en Development, Staging y Production. `POST /api/v1/setup` tendrá rate limiting fijo, logging estructurado sin secretos y la guarda `AnyUsersAsync()` dentro de la transacción.
### 2.2 Auditoría
La creación se audita con `userId = "system"` mediante `IAuditoriaServicio.RegistrarAsync`; design debe resolver si alcanza la firma existente o requiere overload/variante.
### 2.3 Formulario
Los nueve campos son visibles: Nombres, Apellidos, Legajo, Email, UserName, Password, TipoDocumento, NumeroDocumento y Teléfono. `TipoDocumento` será dropdown del catálogo con IDs del bloque `71000000-…`.

## 3. Decisiones arquitectónicas
### 3.1 API: nuevo SetupController
`[AllowAnonymous]`; `POST /api/v1/setup` delega a `ISetupServicio` y crea Persona, Usuario y rol `Administrador` atómicamente.
### 3.2 API: GET /api/v1/setup/status
`[AllowAnonymous]`; devuelve si `AspNetUsers` requiere setup para que Web decida la redirección.
### 3.3 Aplicación: ISetupServicio
Nuevo puerto en `SGV.Aplicacion/Setup/`. No reutiliza directamente comandos que dependen de `usuarioActual.UserId`; encapsula el caso anónimo y la auditoría `system`.
### 3.4 Infraestructura: SetupServicio
Implementación con `UserManager`, repositorio de Persona y una transacción EF. La exclusión concurrente se resolverá con aislamiento/bloqueo compatible con MySQL; el índice único de Identity queda como defensa adicional. El rate limiting pertenece al endpoint/composición API.
### 3.5 Web: middleware de detección de DB vacía
Se propone el filtro más simple: `SignIn` consulta el status al renderizar y redirige a `/auth/setup` si corresponde. Evita un middleware global, round-trips en rutas irrelevantes y problemas de orden con autenticación; design debe validar cache/latencia y el caso de status no disponible.
### 3.6 Web: Razor Page /auth/setup
Reutiliza `_AuthLayout` y el patrón de `SignIn`, con `InputModel`, validación server-side, anti-forgery, typed client y manejo de `HttpRequestException`/`TaskCanceledException`. Tras éxito aplica PRG y redirige a `/auth/sign-in`.

## 4. Wire-types y rutas
Crear contratos en `SGV.Contracts/Setup/` para request/status/resultado y agregar `SetupRelative`/`Setup` a `AuthApiRoutes.cs`. Añadir typed client de Web y registros DI correspondientes.

## 5. Manejo de race conditions y errores
| Escenario | HTTP | UI |
|---|---:|---|
| Setup exitoso | 201/200 | Mensaje y sign-in |
| Ya existe usuario | 409 | Setup completado; ir a sign-in |
| Validación/Identity | 400 | Errores por campo |
| Error transaccional/DB | 500/503 | Error recuperable; no reintentar ciegamente |

## 6. Seguridad
`[AllowAnonymous]` solo en status/setup, rate limiting, HTTPS/CORS vigente, anti-forgery Web, no loggear Password y reutilizar `IdentityErrorMap`. Registrar intento, resultado y correlación sin datos sensibles.

## 7. Plan de pruebas (alto nivel)
Unitarios del servicio; integración de controller y transacción con `[MySqlFact]`; WebApplicationFactory para status, redirección, PRG y errores. E2E no disponible según `openspec/config.yaml`.

## 8. Riesgos residuales y mitigaciones
| Riesgo | Severidad | Mitigación |
|---|---|---|
| Doble admin concurrente | Alta | Guarda dentro de tx + bloqueo/aislamiento + índice único |
| Persona huérfana | Alta | Una transacción EF para Persona y Usuario |
| Endpoint público abusado | Alta | Rate limiting, logs/alertas y sin secretos en logs |

## 9. Fuera de alcance (NO objetivos)
- Selección de roles; siempre `Administrador`.
- Email de verificación.
- Cambios en `PersonasController`/`UsuariosController`.
- Seed programático.
- Re-autenticación automática; después del setup se exige sign-in explícito.

## 10. Estimación de tamaño (preliminar)
Aproximadamente 10–14 archivos nuevos/modificados entre Contracts, Aplicación, Infraestructura, API, Web y tests; cambio mediano, con impacto de integración MySQL y WebApplicationFactory. No incluye migración de esquema prevista.

## 11. Preguntas abiertas para sdd-design
- ¿Cómo implementar el bloqueo de `AspNetUsers` correctamente con Pomelo/MySQL y qué aislamiento usar?
- ¿Dónde cargar `TipoDocumento`: cliente anónimo dedicado, endpoint existente o listado embebido/cacheado?
- ¿Cómo reducir los round-trips del status sin redirecciones circulares ni ocultar fallos de API?
- ¿Qué código exacto devuelve cada conflicto de Identity y cómo se mapea a `CommandResult`?
