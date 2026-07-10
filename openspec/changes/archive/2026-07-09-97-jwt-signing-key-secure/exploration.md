# Exploración SDD #97 — JWT signing key secure

> Issue: [#97 — [Security] Eliminar JWT signing key default hardcodeado y validar al arranque](https://github.com/elflacoseba/SGV/issues/97)
> Categoría: secreto hardcodeado en código + falta de fail-fast al arranque.

## Estado actual

### Definición insegura

`src/SGV.Infraestructura/Seguridad/JwtOptions.cs` declara la opción con un valor por defecto hardcodeado de 60 caracteres que se publica en el repo:

```csharp
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SGV";
    public string Audience { get; set; } = "SGV";

    public string SigningKey { get; set; } = "SGV-development-signing-key-change-before-production-2026";

    public int TokenLifetimeMinutes { get; set; } = 60;
}
```

`Issuer` y `Audience` también tienen defaults pero son solo metadatos públicos; el riesgo real está en `SigningKey`. No hay validación al construir ni al enlazar.

### Fallback silencioso en `Program.cs`

`src/SGV.Api/Program.cs:71` tiene un patrón `??` que materializa la opción por default si la sección `Jwt` no existe en la configuración efectiva:

```csharp
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
```

`src/SGV.Api/appsettings.Development.json` (16 líneas) **no** incluye la sección `Jwt`. En dev, entonces, `new JwtOptions()` se activa y la clave hardcodeada pasa a ser la firma real de los tokens emitidos por `AuthServicio.LoginAsync`.

En `Program.cs:98`, esa misma clave se inyecta en `IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))`. El cierre captura la instancia resuelta al arranque; si la config cambia después, el `IssuerSigningKey` ya quedó sellado para el bearer middleware.

### Único emisor real

`src/SGV.Infraestructura/Seguridad/AuthServicio.cs:13-58` es el único punto de la solución que firma tokens con la clave real. `LoginAsync` (línea 33) toma `IOptions<JwtOptions>` y produce el `JwtSecurityToken` con `HmacSha256`. No hay refresh token, password reset, email confirmation, ni cualquier otro camino que firme con `SigningKey`. Confirmado: grep en `src/` contra `new LoginResponse|JwtSecurityToken|WriteToken` solo devuelve `AuthServicio.cs` y los tests que fabrican tokens con claves dummy.

### Patrón de validación existente (la referencia a seguir)

`src/SGV.Web/Program.cs:13-18` ya implementa el patrón canónico del repo para validar opciones al arranque usando `AddOptions` + `Validate` + `ValidateOnStart`:

```csharp
builder.Services
    .AddOptions<SgvApiOptions>()
    .BindConfiguration(SgvApiOptions.SectionName)
    .Validate(options => Uri.IsWellFormedUriString(options.BaseUrl, UriKind.Absolute),
        $"{SgvApiOptions.SectionName}:BaseUrl must be an absolute URI")
    .ValidateOnStart();
```

`SgvApiOptions` (`src/SGV.Web/Integration/Auth/SgvApiOptions.cs`) está definida en su propio archivo junto al componente que la consume, y su `SectionName` es `"SgvApi"`. Es la plantilla exacta que se debe aplicar a `JwtOptions`.

### Convención fail-loud del repo

`docs/decisiones-implementacion.md` documenta que el repo sigue una política explícita "fail-loud" para secretos y configuración:

- `SgvDbContextFactory` (`src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs:28-35`) lanza `InvalidOperationException` si `ConnectionStrings:SgvDatabase` no está configurada. El comentario cita explícitamente `dotnet user-secrets` como canal dev.
- `src/SGV.Api/SGV.Api.csproj:22` declara `<UserSecretsId>42b7cb22-3f33-422b-9269-bf47677d4ff8</UserSecretsId>`.
- `appsettings.Development.json` no incluye credenciales reales.

Esto establece precedente: cualquier secreto vive en user-secrets / variables de entorno, nunca en código ni en `appsettings.*.json` commiteado.

### Patrón de tests de WebApplicationFactory

`tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` y `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` son las dos factorías del repo:

- **API tests** (`ApiWebApplicationFactory`): reemplaza `IAuthServicio` por `FakeAuthServicio` (línea 762-768) y el esquema de autenticación por `FakeAuthenticationDefaults.Scheme = "Test"` (líneas 894-896). La validación JWT bearer real **nunca corre** en tests API: cualquier `[Authorize]` se evalúa contra `FakeAuthenticationHandler`. El único test que pasa por `AuthController.Login` real es `Login_WithValidCredentials_ReturnsAccessToken` (`tests/SGV.Tests/Api/AuthControllerTests.cs:11-23`): llama a `AuthServicio.LoginAsync` real (que sí usa `IOptions<JwtOptions>.Value`), pero solo verifica que `AccessToken` no esté vacío, no que la firma sea válida.
- **Web tests** (`SgvWebApplicationFactory`): corre `SGV.Web`, que **no** referencia `JwtOptions`. Los tests inyectan `RecordingHttpMessageHandler` con `LoginResponse("token-123", ...)` (`CargoWebTestFixture.cs:96-102`, `HabilidadWebTestFixture.cs:45`, `WebShellSmokeTests.cs:60`, etc.). El cookie auth de `SGV.Web` parsea el JWT sin validar firma (`AuthSessionFactory.TryAddTokenClaims`, `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:43-63`), por eso el test de rol admin puede firmar con una clave HMAC dummy de 52 bytes (`CargoWebTestFixture.cs:141-142`). El `ApiBearerTokenHandler` (`src/SGV.Web/Integration/Auth/ApiBearerTokenHandler.cs:78`) reenvía el token al API solo cuando el handler real está configurado; en tests el endpoint está stubbeado, así que la API nunca ve el JWT.

Implicación: el fix tiene impacto en API tests, no en web tests.

## Áreas afectadas

### Producción

- `src/SGV.Infraestructura/Seguridad/JwtOptions.cs` — quitar default de `SigningKey`. `Issuer`/`Audience` pueden mantenerlo o también endurecerse; no son secretos.
- `src/SGV.Api/Program.cs:71-72` — reemplazar el patrón `?? new JwtOptions()` por `AddOptions<JwtOptions>().BindConfiguration(...).Validate(...).ValidateOnStart()`. Mantener `Configure<JwtOptions>` actual o consolidarlo en `AddOptions` (la API moderna lo incluye).
- `src/SGV.Api/Program.cs:86-100` — `AddJwtBearer` debe leer `IssuerSigningKey` desde `IOptionsMonitor<JwtOptions>` o desde el valor ya validado. Si se usa el valor validado, capturar tras la validación. Si se quiere `IOptionsMonitor`, el bloque debe diferir la construcción del `SymmetricSecurityKey` hasta que se resuelva `IOptions<JwtOptions>`.
- `src/SGV.Api/appsettings.Development.json` — agregar sección `Jwt` con un placeholder dev explícito (≥32 bytes, marcado como tal) para que `ValidateOnStart` no falle al ejecutar la API local ni los tests API. El issue lo pide en §3.
- `docs/decisiones-implementacion.md` — agregar entrada "Gestión de secretos JWT" siguiendo el estilo de `SgvDbContextFactory fail-loud` (líneas 40-50 del archivo). Documentar: user-secrets en dev, variables de entorno / secret manager en CI / producción, y nunca commitear claves reales en `appsettings.*.json`.
- `AGENTS.md` — actualizar la sección de comandos rápidos para mencionar `dotnet user-secrets set "Jwt:SigningKey" "..." --project src/SGV.Api` como paso previo al primer arranque de la API.

### Pruebas

- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` — el host arrancará con `ValidateOnStart`. Si `appsettings.Development.json` provee un placeholder, no se necesita override. Confirmar que la nueva sección no rompa ningún `[Fact]` que asume que `Jwt` está ausente.
- `tests/SGV.Tests/Api/AuthControllerTests.cs` — `Login_WithValidCredentials_ReturnsAccessToken` sigue funcionando porque `AuthServicio.LoginAsync` ahora firma con la clave del `appsettings.Development.json` (placeholder dev), y el test no valida firma. `Login_WithInvalidCredentials_ReturnsUnauthorized` no toca el flujo real.
- `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` — sin cambios (no referencia `JwtOptions`).
- `tests/SGV.Tests/Web/Cargo/CargoWebTestFixture.cs:139-159` — sin cambios; la clave dummy HMAC del test sigue siendo válida para `AuthSessionFactory.TryAddTokenClaims` porque el cookie auth no valida firma.
- Test nuevo requerido por el issue §4 — levantar `ApiWebApplicationFactory` con `Jwt:SigningKey` ausente y con clave corta (<32 bytes) y verificar que el host lanza `OptionsValidationException` al `Build()`. Esto encaja en `tests/SGV.Tests/Api/` y puede usar la factoría existente sin agregar dependencias.

### Sin cambios

- `src/SGV.Infraestructura/Seguridad/AuthServicio.cs` — la firma del emisor es correcta; solo cambia el valor de `jwt.SigningKey` que ahora viene de config validada. Compatible con `IOptions<JwtOptions>` (singleton, valor resuelto en startup).
- `src/SGV.Web/**` — no toca `JwtOptions`.
- `src/SGV.Infraestructura/Persistencia/Migraciones/**` — ninguna migración emite ni firma tokens.
- `.github/workflows/ci.yml` — actualmente no exporta `Jwt__SigningKey`; al agregar el placeholder a `appsettings.Development.json`, CI queda cubierto sin cambios de workflow. Si más adelante se quiere endurecer CI, se puede exportar `Jwt__SigningKey` como secret de GitHub, pero eso es orthogonal a este issue.
- No hay `Dockerfile`, `docker-compose.yml`, ni webhook que lea `Jwt:SigningKey`.
- No hay refresh tokens, password reset ni email confirmation; invalidar sesiones existentes al deployar la nueva clave es aceptable (issue lo confirma implícitamente: "es de seguridad y eso es aceptable").

## Enfoques comparados

| Enfoque | Descripción | Pros | Contras | Esfuerzo |
|---------|-------------|------|---------|----------|
| **A. `ValidateDataAnnotations + ValidateOnStart` (DataAnnotations puras)** | Marcar `JwtOptions.SigningKey` con `[Required, MinLength(32)]`, `[Required]` en `Issuer`/`Audience`. `ValidateDataAnnotations().ValidateOnStart()` resuelve. | Idiomático .NET; mensajes de error estándar; cero código custom de validación. | `MinLength(32)` cuenta caracteres UTF-16, no bytes UTF-8; para HMAC-SHA256 la métrica correcta es `Encoding.UTF8.GetByteCount(SigningKey) >= 32`. No rechaza claves ASCII triviales como "12345678901234567890123456789012" que es de 32 chars pero podría ser igual de débil. | Bajo |
| **B. `AddOptions().Validate(...).ValidateOnStart` (lambda inline)** | Replicar exactamente el patrón de `SgvApiOptions` con lambdas. Validar bytes UTF-8 explícitos. | Coincide 1:1 con el patrón existente (`src/SGV.Web/Program.cs:13-18`); control total sobre la métrica (bytes, no chars); mensajes custom legibles; sin agregar DataAnnotations a un POCO que no las necesita. | Más líneas de validación inline; si crece la lógica, mover a `IValidateOptions<T>`. | Bajo |
| **C. `IValidateOptions<JwtOptions>` dedicated** | Crear `JwtOptionsValidator : IValidateOptions<JwtOptions>` con reglas: `SigningKey` no vacía, ≥32 bytes UTF-8, rechazo explícito de la clave dev placeholder cuando `Environment != Development`. | Testeable unitariamente sin levantar host; lógica compleja vive en una clase testeable; permite reglas contextuales (Environment). | Más boilerplate; sobreingeniería si las reglas son solo tres. Vale si más adelante se agregan más invariantes (rotación, longitud mínima por algoritmo, etc.). | Medio |
| **D. Hard-fail sin DI validation (`if (string.IsNullOrWhiteSpace(...)) throw` en `Program.cs`)** | Top-level guard que lea la config y lance excepción antes de `builder.Build()`. | Trivial. | Anti-patrón .NET: rompe el patrón canónico de `AddOptions`/`ValidateOnStart`, no se beneficia del fail-fast unificado, y deja a `JwtOptions` sin contrato de validación reusable. | Bajo (pero desaconsejado) |

### Recomendación: **Enfoque B (lambda inline estilo `SgvApiOptions`)** con upgrade opcional a **C** si la validación crece

Razones concretas:

1. **Consistencia 1:1 con `SgvApiOptions`** — el repo ya validó el patrón y lo eligió como referencia. Reusar la forma exacta reduce fricción cognitiva y evita introducir un segundo estilo de validación.
2. **HMAC-SHA256 mide bytes UTF-8**, no caracteres UTF-16. `MinLength` de DataAnnotations no aplica al caso; las lambdas inline permiten calcular `Encoding.UTF8.GetByteCount(o.SigningKey) >= 32` exactamente como pide el issue.
3. **El cambio es chico** — un `Validate` adicional sobre `SigningKey` (no vacío + ≥32 bytes) más dos `Validate` triviales sobre `Issuer`/`Audience`. La probabilidad de que las reglas crezcan en el corto plazo es baja (no hay refresh token, no hay rotación). Si más adelante aparece `IssuerSigningKeyRotationDays`, `Algorithms`, etc., se migra a `IValidateOptions<T>` en un cambio separado.
4. **El test "fail-fast al arrancar"** que pide el issue §4 se monta trivialmente sobre el patrón: instanciar `WebApplicationFactory` con `ConfigureAppConfiguration` que sobreescriba la sección `Jwt` y assert `OptionsValidationException` en el `Build`.

## Riesgos

- **Riesgo MEDIO — `appsettings.Development.json` recibe una clave placeholder.** Si la clave dev se filtra (commit accidental a un repo público, copy-paste a logs, etc.), cualquier persona puede firmar tokens de administrador en entornos donde se use ese `appsettings.Development.json`. Mitigación: marcar la clave con un sufijo explícito (e.g. `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-...`) ≥32 bytes, y reforzar en `AGENTS.md` y `docs/decisiones-implementacion.md` que producción debe sobrescribir vía env var / secret manager. Alternativa segura: NO incluir la clave en `appsettings.Development.json` y agregar un override en `ApiWebApplicationFactory.ConfigureWebHost` que provea una clave de prueba; los dev developers usan `dotnet user-secrets` para el resto. **Decisión a tomar en proposal.**
- **Riesgo BAJO — Tokens pre-fix quedan inválidos.** Quien esté autenticado al momento del deploy tendrá que re-login. No hay tabla de tokens revocados (no hay refresh tokens), así que no hay nada que limpiar en DB. `CookieAuthentication` tiene `AllowRefresh = false` (`src/SGV.Web/Program.cs:30`), por lo que el cookie expira junto con el JWT. Acceptable per issue.
- **Riesgo BAJO — Validación rompe tests API si `appsettings.Development.json` no tiene la sección.** Mitigación: o se agrega la sección placeholder, o `ApiWebApplicationFactory` provee la sección en `ConfigureWebHost`. Cualquiera de las dos es trivial.
- **Riesgo BAJO — `IssuerSigningKey` capturado por cierre en `AddJwtBearer`.** En `Program.cs:86-100` el `IssuerSigningKey` se construye con `jwtOptions.SigningKey` capturado del `Get<JwtOptions>()` previo. Si en el futuro se quiere hot-reload de la clave, hay que diferir la construcción con `IOptionsMonitor<JwtOptions>`. Para este issue no es necesario (la clave es estática), pero documentar la limitación en `design.md` para no crear expectativa falsa.
- **Riesgo BAJO — `Issuer`/`Audience` también tienen defaults en `JwtOptions.cs`.** No son secretos, pero el issue §1 menciona "lo mismo vale para `Issuer`/`Audience` si tienen riesgo similar". Se puede endurecer también exigiendo que estén configurados, aunque su impacto de seguridad es nulo (son strings públicos). Vale la pena hacerlo por consistencia y por el principio "no defaults de producción". Decisión a confirmar en `proposal.md`.
- **Riesgo BAJO — Conflicto con issue #59 (bug `ActivePuestoIdUnique`).** El issue #59 está abierto y bloquea 12 tests de `OcupacionRepositoryTests` en CI contra MySQL real. Este cambio **no debe** tocar migraciones ni tablas vecinas. Confirmado: no se requiere nueva migración.

## Fuera de alcance (no-goals explícitos)

- Rotación automática de claves o múltiples `SigningKey` activas simultáneamente.
- Refresh tokens / tokens persistidos en DB.
- Cambio de algoritmo de firma (se mantiene `HmacSha256`).
- Migración o cualquier cambio en la tabla `AspNet*` / esquema de Identity.
- Endurecer `Issuer`/`Audience` con regex o validación contra lista permitida — solo presencia.

## Listo para propuesta

**Sí** — el alcance es claro, el patrón ya existe en el repo (`SgvApiOptions`), el blast radius está acotado y los riesgos identificados son manejables. La propuesta puede arrancar directamente con `sdd-propose` para producir `proposal.md`, que necesitará resolver explícitamente:

1. ¿Se valida también `Issuer`/`Audience` con `Validate(...).ValidateOnStart()` o se dejan con defaults `"SGV"`?
2. ¿Se agrega una sección `Jwt` placeholder a `appsettings.Development.json` (más cómodo, riesgo de leak) o se documenta `dotnet user-secrets` como único camino (más estricto, requiere setup manual en cada dev) y los tests proveen la sección en la factory?
3. ¿Se documenta el contrato en `docs/decisiones-implementacion.md` (estilo `SgvDbContextFactory fail-loud`) y en `AGENTS.md` como parte del PR?

Decisión recomendada al usuario antes de delegar a `sdd-propose`:

> ¿Querés que la propuesta valide también `Issuer`/`Audience` como no-vacíos, o solo `SigningKey`?

Las otras dos preguntas tienen respuesta clara y pueden resolverse dentro de la propuesta sin nueva consulta.
