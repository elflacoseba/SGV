# Tasks: Cambiar contraseña de usuario logueado

> Change: `2026-07-27-cambiar-contrasena-usuario-logueado` · Issue: #204
> Strict TDD: `true` · Idioma: español

---

## Resumen

11 tareas que implementan el cambio de contraseña para usuario autenticado desde la capa de Contracts hasta la UI Web. Sigue el orden Clean Architecture: contratos → aplicación → infraestructura → API → cliente HTTP → Razor Page → UI dropdown/banner → docs. Cada tarea ≤ 2h.

---

## Asunciones

1. `SecurityStamp` ya existe en `AspNetUsers`; **no** se requiere migración EF.
2. La política `IdentityOptions.Password` (≥6, lower, upper, digit, symbol) está vigente y no se modifica.
3. `ApiBearerTokenHandler` ya inyecta el JWT en requests autenticados.
4. La suite existente debe seguir verde tras cada tarea.
5. Tareas TDD: el ciclo rojo→verde se completa dentro de la misma tarea.

---

## Dependencias entre tareas (orden)

```
T-1 (Contracts)
  ├── T-2 (Validador + Interfaz App)
  │     ├── T-3 (Infraestructura)
  │     │     └── T-5 (Endpoint API)
  │     └── T-4 (Rate Limiter ─ puede ir paralelo a T-3)
  │                     └── T-5
  ├── T-6 (Web Client ─ puede ir paralelo a T-3..T-5)
  │     └── T-7 (Razor Page)
  │           ├── T-8 (Topbar)
  │           └── T-9 (SignIn Banner)
  └── T-10 (Smoke ─ después de T-8)

T-11 (Docs ─ al final)
```

---

## Tareas

### Tarea T-1: Agregar `ChangePasswordRequest`, `ChangePasswordOutcome` y constantes en `AuthApiRoutes` ✅

- **Estimación**: 30 min
- **Complejidad**: baja
- **Work unit**: WU-1
- **Strict TDD**: no (tipos puros, sin lógica)
- **Archivos a tocar**:
  - `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` — agregar `ChangePasswordRequest` record + `ChangePasswordOutcome` enum
  - `src/SGV.Contracts/Auth/AuthApiRoutes.cs` — agregar `ChangePasswordRelative`, `ChangePassword`, `ChangePasswordPolicyName`
- **Tests a escribir**: ninguno (compila + build basta)
- **Specs cubiertos**:
  - `password-change/spec.md`: "ChangePasswordRequest expone los tres campos", "AuthApiRoutes expone las constantes requeridas"
- **Depende de**: nada
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] Tipos visibles desde `SGV.Aplicacion` y `SGV.Web` (compila)
  - [x] Sin warnings nuevos
- **Notas**: `ChangePasswordOutcome` enum con `Success=0, InvalidCurrentPassword=1, ValidationError=2, RateLimited=3`.

---

### Tarea T-2: Crear `IChangePasswordService` y `ChangePasswordRequestValidator` (TDD) ✅

- **Estimación**: 1h
- **Complejidad**: baja
- **Work unit**: WU-2
- **Strict TDD**: sí (rojo → verde → refactor)
  - **Rojo**: escribir `ChangePasswordRequestValidatorTests` con `Theory`+`InlineData` que cubra validación (NotEmpty, política password, coincidencia NewPassword==ConfirmPassword). Espera que falle.
  - **Verde**: implementar `ChangePasswordRequestValidator` (mirar `ResetPasswordRequestValidator`) e `IChangePasswordService` interface. Test pasa.
  - **Refactor**: verificar mensajes de error en español, sin duplicación con `ResetPasswordRequestValidator`.
- **Archivos a tocar**:
  - `src/SGV.Aplicacion/Seguridad/PasswordChange/IChangePasswordService.cs` (nuevo)
  - `src/SGV.Aplicacion/Seguridad/PasswordChange/ChangePasswordRequestValidator.cs` (nuevo)
- **Tests a escribir**:
  - `tests/SGV.Tests/Aplicacion/Seguridad/ChangePasswordRequestValidatorTests.cs` — 1 `[Theory]` con `InlineData` cubriendo: happy path, `CurrentPassword` vacío, `NewPassword` <6 chars, `NewPassword` sin mayúscula, `ConfirmPassword != NewPassword`
- **Specs cubiertos**:
  - `password-change/spec.md`: "POST con CurrentPassword incorrecta", "POST con NewPassword que no cumple la política", "POST con ConfirmPassword distinta de NewPassword"
- **Depende de**: T-1
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] Tests del scope pasan: `dotnet test --filter "FullyQualifiedName~ChangePasswordRequestValidator"`
  - [x] Ciclo TDD rojo→verde documentado en notas del commit
  - [x] Sin romper tests existentes
  - [x] Sin warnings nuevos
- **Notas**: `IChangePasswordService.ChangePasswordAsync(userId, request, ct)` mantiene el servicio HTTP-agnóstico (recibe `userId` string, no `IPrincipal`).

---

### Tarea T-3: Implementar `ChangePasswordService` en Infraestructura y registrar DI ✅

- **Estimación**: 1.5h
- **Complejidad**: media
- **Work unit**: WU-3
- **Strict TDD**: no (se prueba por integración en T-5)
- **Archivos a tocar**:
  - `src/SGV.Infraestructura/Seguridad/PasswordChange/ChangePasswordService.cs` (nuevo) — orquesta `UserManager.ChangePasswordAsync` + `UpdateSecurityStampAsync`
  - `src/SGV.Infraestructura/DependencyInjection.cs` — agregar `services.AddScoped<IChangePasswordService, ChangePasswordService>()` junto a `IPasswordResetService`
- **Tests a escribir**: ninguno (cubierto por T-5 con `MySqlFact`)
- **Specs cubiertos**: (cobertura indirecta vía T-5)
- **Depende de**: T-2
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] `ChangePasswordService` implementa: null check → `FindByIdAsync` → `ChangePasswordAsync` → mapeo `PasswordMismatch` → `UpdateSecurityStampAsync` (best-effort) → log
  - [x] Sin warnings nuevos
- **Notas**: `UpdateSecurityStampAsync` es best-effort. Falla → log `LogWarning`, no bloquea. `PasswordMismatch` → `InvalidCurrentPassword`. Otros errores de Identity → `ValidationError`.

---

### Tarea T-4: Agregar política rate limit `ChangePassword` en `Program.cs` ✅

- **Estimación**: 15 min
- **Complejidad**: baja
- **Work unit**: WU-4
- **Strict TDD**: no (configuración, sin lógica)
- **Archivos a tocar**:
  - `src/SGV.Api/Program.cs` — agregar `AddFixedWindowLimiter(ChangePasswordPolicyName, ...)` después de `SetupApiRoutes.SetupPolicyName`
- **Tests a escribir**: ninguno (cubierto por T-5 con test 429)
- **Specs cubiertos**: (cobertura indirecta vía T-5)
- **Depende de**: T-1 (constante `ChangePasswordPolicyName`)
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] `PermitLimit=5`, `Window=15 min`, `QueueLimit=0`
  - [x] Sin warnings nuevos
- **Notas**: `OnRejected` global existente ya maneja `Retry-After`.

---

### Tarea T-5: Agregar endpoint `ChangePassword` en `AuthController` (TDD)

- **Estimación**: 1.5h
- **Complejidad**: media
- **Work unit**: WU-5
- **Strict TDD**: sí (rojo → verde → refactor)
  - **Rojo**: escribir `FakeChangePasswordService` + `AuthControllerChangePasswordTests` (5 tests: 401 sin auth, 200+stamp rotado con `MySqlFact`, 400 con current inválida, 400 con política débil, 429 al sexto request). Falla porque no existe endpoint.
  - **Verde**: implementar endpoint `[HttpPost][Authorize][EnableRateLimiting]` que resuelve `userId` del `ClaimTypes.NameIdentifier`, valida con `IValidator`, delega en `IChangePasswordService`, mapea outcome a HTTP status. Tests pasan.
  - **Refactor**: verificar mensajes en español, código duplicado con otros endpoints del controller.
- **Archivos a tocar**:
  - `src/SGV.Api/Controllers/AuthController.cs` — agregar `ChangePassword`
- **Tests a escribir**:
  - `tests/SGV.Tests/Api/AuthControllerChangePasswordTests.cs` (~120 LoC, 5 tests)
  - `tests/SGV.Tests/Api/Fakes/FakeChangePasswordService.cs` (mirar `FakePasswordResetService`)
- **Specs cubiertos**:
  - `password-change/spec.md`: todos los escenarios (POST exitoso, 401, 400 current, 400 policy, 429, SecurityStamp rotado, mensajes en español)
- **Depende de**: T-3, T-4
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] Tests de integración pasan: `dotnet test --filter "FullyQualifiedName~AuthControllerChangePassword"`
  - [x] `MySqlFact` de stamp rotado pasa contra MySQL local
  - [x] Ciclo TDD rojo→verde documentado
  - [x] Sin romper tests existentes
  - [x] Sin warnings nuevos
- **Notas**: `[ProducesResponseType]` documenta 200, 400, 429. Mensajes HTTP en español. `userId` resuelto con `FindFirstValue(ClaimTypes.NameIdentifier)`.

---

### Tarea T-6: Agregar `IAuthApiClient.ChangePasswordAsync` y su implementación (TDD)

- **Estimación**: 1h
- **Complejidad**: baja
- **Work unit**: WU-6
- **Strict TDD**: sí (rojo → verde → refactor)
  - **Rojo**: escribir `AuthApiClientChangePasswordTests` (POST con Bearer assert, 400→InvalidCurrentPassword, 429→RateLimited, cancelación). Falla sin implementación.
  - **Verde**: implementar método en `AuthApiClient` usando `httpClient` autenticado (no `anonymousHttpClient`). Mapeo: 2xx→Success, 400→InvalidCurrentPassword, 429→RateLimited, otros→`HttpRequestException`.
  - **Refactor**: verificar que no usa `CommandResultMapper.Map` (exceptuado).
- **Archivos a tocar**:
  - `src/SGV.Web/Integration/Auth/IAuthApiClient.cs` — agregar firma
  - `src/SGV.Web/Integration/Auth/AuthApiClient.cs` — implementación
- **Tests a escribir**:
  - `tests/SGV.Tests/Web/AuthApiClientChangePasswordTests.cs` (~80 LoC, 4+ tests)
- **Specs cubiertos**:
  - `password-change-web/spec.md`: "IAuthApiClient.ChangePasswordAsync envía POST", "CambioPasswordAsync mapea 400", "CambioPasswordAsync mapea 429", "CambioPasswordAsync propaga HttpRequestException nativa"
  - `web-apiclient-transport-contract/spec.md`: todos los escenarios (Bearer, 401, 200, 400, 429, 5xx, TaskCanceled, CancellationToken pre-cancelado)
- **Depende de**: T-1
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] Tests pasan: `dotnet test --filter "FullyQualifiedName~AuthApiClientChangePassword"`
  - [x] Usa `httpClient` (autenticado), NO `anonymousHttpClient`
  - [x] Sin romper tests existentes
  - [x] Sin warnings nuevos
- **Notas**: el `httpClient` ya incluye `ApiBearerTokenHandler`. `cancellationToken.ThrowIfCancellationRequested()` al inicio.

---

### Tarea T-7: Crear Razor Page `CambiarContrasena` con PageModel (TDD)

- **Estimación**: 2h
- **Complejidad**: media
- **Work unit**: WU-7
- **Strict TDD**: sí (rojo → verde → refactor)
  - **Rojo**: escribir `CambiarContrasenaPageTests` (5 tests: GET anónimo→redirect, GET autenticado→render form, POST exitoso→SignOut+redirect+TempData, POST current inválida→error, POST 429→mensaje). Falla sin page.
  - **Verde**: crear `CambiarContrasena.cshtml` (form con `data-password="bar"`) + `CambiarContrasena.cshtml.cs` (`[Authorize]`, `OnGet`, `OnPostAsync` con manejo de outcomes, `SignOutAsync` en éxito, catch `HttpRequestException(401)`→redirect, catch transporte/timeout).
  - **Refactor**: verificar mensajes en español, sin código duplicado con `ResetPasswordModel`.
- **Archivos a tocar**:
  - `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml` (nuevo, mirror de `ResetPassword.cshtml`)
  - `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml.cs` (nuevo, `CambiarContrasenaModel`)
- **Tests a escribir**:
  - `tests/SGV.Tests/Web/CambiarContrasenaPageTests.cs` (~150 LoC, 5 tests)
- **Specs cubiertos**:
  - `password-change-web/spec.md`: "GET autenticado renderiza formulario", "GET sin autenticación redirige a login", "POST exitoso cierra sesión y redirige", "POST con CurrentPassword inválida muestra error", "POST con RateLimited muestra mensaje", "POST con API caída muestra error transporte", "POST con cookie vencida redirige a sign-in"
- **Depende de**: T-6
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] `bun run build` (frontend assets, verifica `auth-password.js`)
  - [x] Tests pasan: `dotnet test --filter "FullyQualifiedName~CambiarContrasenaPage"`
  - [x] Ciclo TDD rojo→verde documentado
  - [x] Sin romper tests existentes
  - [x] Sin warnings nuevos
- **Notas**: `InputModel` con `[Required]` + `[DataType(DataType.Password)]` en cada campo. `MeetsPasswordPolicy` como primera barrera cliente. `SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)` antes del `LocalRedirect`.

---

### Tarea T-8: Agregar ítem "Cambiar Contraseña" en dropdown del topbar + smoke test

- **Estimación**: 30 min
- **Complejidad**: baja
- **Work unit**: WU-8
- **Strict TDD**: no (cambio markup + smoke test)
- **Archivos a tocar**:
  - `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` — insertar `<a href="/auth/cambiar-contrasena">` con `ti ti-key` antes del form de logout (línea 67-68)
- **Tests a escribir**:
  - Extender `tests/SGV.Tests/Web/WebShellSmokeTests.cs` con 1 test: assert que el dropdown autenticado contiene "Cambiar Contraseña" + link `/auth/cambiar-contrasena`
- **Specs cubiertos**:
  - `password-change-web/spec.md`: "Dropdown autenticado expone ítem antes de 'Cerrar Sesión'", "Dropdown no expone ítem para anónimos"
- **Depende de**: T-7 (la página debe existir, aunque el ítem apunte a 404 si no; semánticamente la page debe estar operativa)
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] Smoke test pasa: `dotnet test --filter "FullyQualifiedName~WebShellSmokeTests"`
  - [x] Sin romper tests existentes
  - [x] Sin warnings nuevos
- **Notas**: Ícono `ti ti-key`. Fallback `ti ti-lock` si el bundle no lo incluye. El ítem va **antes** del form de logout.

---

### Tarea T-9: Agregar banner `PasswordChangeMessage` en `SignIn.cshtml`

- **Estimación**: 15 min
- **Complejidad**: baja
- **Work unit**: WU-9
- **Strict TDD**: no (solo markup Razor, sin lógica)
- **Archivos a tocar**:
  - `src/SGV.Web/Pages/Auth/SignIn.cshtml` — agregar bloque `@if (TempData["PasswordChangeMessage"] is string msg)` después del bloque existente `TempData["PasswordResetMessage"]`
- **Tests a escribir**: ninguno (cubierto por el test POST exitoso de T-7 que verifica `TempData`)
- **Specs cubiertos**:
  - `password-change-web/spec.md`: "SignIn muestra banner tras cambio exitoso"
- **Depende de**: T-7 (la page existe y setea `TempData`)
- **Definición de Hecho**:
  - [x] `dotnet build SGV.slnx` limpio
  - [x] Banner coexiste con `PasswordResetMessage` sin interferencia
  - [x] Sin warnings nuevos
- **Notas**: El bloque va después de `PasswordResetMessage`. Comentario XML aclara que es segundo banner independiente.

---

### Tarea T-10: Regenerar `docs/migracion-inicial-sgv.sql`

- **Estimación**: 15 min
- **Complejidad**: baja
- **Work unit**: WU-10
- **Strict TDD**: no (script de BD idempotente)
- **Archivos a tocar**:
  - `docs/migracion-inicial-sgv.sql` (regenerar, no hay cambios de esquema — el output debe ser byte-equivalente al vigente)
- **Tests a escribir**: ninguno
- **Specs cubiertos**: N/A
- **Depende de**: T-5 (todo cambio de BD ya aplicado)
- **Definición de Hecho**:
  - [x] `dotnet ef migrations script --idempotent --output docs/migracion-inicial-sgv.sql` exitoso
  - [x] El diff del sql muestra solo cambios cosméticos (si los hay) — **output byte-equivalente al vigente (md5 d5313657ec3ec42d97313e749be71f39) → no commit**
  - [x] `dotnet build SGV.slnx` limpio
- **Notas**: No hay migraciones nuevas. Solo regenerar para mantener concordancia. Si el output es idéntico, no hacer commit.

---

## Forecast — Review Workload

| Tarea | Archivos | LoC src estimadas | LoC tests estimadas | Tests | TDD |
|-------|----------|-------------------|---------------------|-------|-----|
| T-1 Contracts | 2 | 32 | 0 | 0 | No |
| T-2 Validator + Interface | 2 | 55 | 35 | 1 ([Theory]) | Sí |
| T-3 Infra Service + DI | 2 | 83 | 0 | 0 | No |
| T-4 Rate Limiter | 1 | 15 | 0 | 0 | No |
| T-5 API Endpoint | 1 + 2 test | 70 | 120 | 5 | Sí |
| T-6 Web Client | 2 + 1 test | 45 | 80 | 4 | Sí |
| T-7 Razor Page | 2 + 1 test | 175 | 150 | 5 | Sí |
| T-8 Topbar + Smoke | 1 + 1 extend | 6 | 15 | 1 | No |
| T-9 SignIn Banner | 1 | 10 | 0 | 0 | No |
| T-10 Docs | 1 | 0 | 0 | 0 | No |
| **Total** | **~16 archivos** | **~491 LoC src** | **~400 LoC tests** | **~16 tests** | |

```text
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High
```

- **Total LoC estimadas**: ~891 (491 src + 400 tests)
- **Tests estimados**: ~16 métodos de test (~5 test files + 1 extend)
- **400-line budget risk**: High
- **Decision needed before apply**: Yes — la estimación (~891 LoC) supera ampliamente el budget de 400 líneas. Con `delivery_strategy=single-pr-default`, se requiere decisión del usuario: aceptar `size:exception` como single PR o dividir en PRs encadenados.
- **Chained PRs recommended**: Yes
- **Razón**: 891 LoC > 400 budget. El cambio cruza 5 capas (Contracts, Aplicación, Infraestructura, API, Web) con tests de integración `MySqlFact` y nueva Razor Page completa. Incluso separando src de tests, src sola (491 LoC) excede el budget. Se recomienda dividir en 2-3 PRs encadenados.

---

## Plan de implementación (orden de ejecución)

1. **T-1** → **T-2** → **T-3** + **T-4** (pueden ir en paralelo) → **T-5** → **T-6** → **T-7** → **T-8** + **T-9** (pueden ir en paralelo) → **T-10**

El orden sigue la dependencia estricta de Clean Architecture. T-3 y T-4 son independientes entre sí (rate limiter no depende del service). T-8 y T-9 son independientes entre sí (topbar no depende del banner).

Si se decide usar PRs encadenados, el split recomendado:

| PR | Tareas | Alcance |
|----|--------|---------|
| PR 1 | T-1, T-2, T-3, T-4 | Backend completo (Contracts + App + Infra + Rate Limiter) |
| PR 2 | T-5 | Endpoint API + tests de integración |
| PR 3 | T-6, T-7, T-8, T-9, T-10 | Web layer (cliente + page + topbar + banner + docs) |

Cada PR es autónomo, tiene su propia verificación y rollback independiente.
