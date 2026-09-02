# R-03-05 — Configuración: opciones tipadas y secretos

Referencia de cada sección de configuración tipada consumida por `SGV.Api` y `SGV.Web`. Las validaciones corren con `Validate(...).ValidateOnStart()`, lo que garantiza fail-loud en el arranque si falta un campo obligatorio.

## Resumen de secciones

| Sección | Tipo | Lectores | Default | Notas |
| --- | --- | --- | --- | --- |
| `Jwt` | `JwtOptions` | API, Web | `Issuer=Audience="SGV"`, `TokenLifetimeMinutes=60` | `SigningKey` obligatorio, ≥32 bytes UTF-8 |
| `RefreshToken` | `RefreshTokenOptions` | API | `LifetimeDays=14`, `PermitLimit=20`, `WindowMinutes=15` | Cuota del rate limit `/refresh` |
| `Smtp` | `SmtpOptions` | API | `Mode=Logger` en Development | `Mode=Smtp` requiere host/user/pass |
| `SgvApi` | `SgvApiOptions` | Web | — | Base URL absoluta |
| `ConnectionStrings:SgvDatabase` | `string` | API | — | Incluye `Server=` y `Database=` |
| `MySql:ServerVersion` | `string` (Version) | API | `8.0.36` | Evita `ServerVersion.AutoDetect` |
| `AllowedOrigins` | `string[]` | API | (en Development: any origin) | CORS allow-list fuera de Development |
| `Logging:*` | estándar Microsoft | API, Web | — | — |
| `xunit.runner.json` | JSON | Tests | — | Sólo en `SGV.Tests` |

## `Jwt` — `JwtOptions`

`SectionName = "Jwt"`. Bind en `Program.cs` de API y Web con la misma validación.

| Propiedad | Default | Tipo | Validación |
| --- | --- | --- | --- |
| `Issuer` | `"SGV"` | `string` | Sin validación |
| `Audience` | `"SGV"` | `string` | Sin validación |
| `SigningKey` | `string.Empty` | `string` | `!= null/whitespace` Y `UTF8.GetByteCount ≥ 32`; falla con `ValidateOnStart` |
| `TokenLifetimeMinutes` | `60` | `int` | Sin validación |

Equivalentes en variables de entorno: `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`, `Jwt__TokenLifetimeMinutes`.

> La `SigningKey` es el único secreto obligatorio. Si falta o es corta, el host no arranca. Ver how-to `03-rotar-jwt-signing-key.md`.

## `RefreshToken` — `RefreshTokenOptions`

`SectionName = "RefreshToken"`. Sólo API.

| Propiedad | Default | Validación |
| --- | --- | --- |
| `RefreshTokenLifetimeDays` | `14` | `> 0` |
| `RateLimitPermitLimit` | `20` | `> 0` |
| `RateLimitWindowMinutes` | `15` | `> 0` |

Equivalentes en variables de entorno: `RefreshToken__RefreshTokenLifetimeDays`, `RefreshToken__RateLimitPermitLimit`, `RefreshToken__RateLimitWindowMinutes`.

## `Smtp` — `SmtpOptions`

`SectionName = "Smtp"`. Sólo API. Implementa `IValidatableObject` para validar reglas cross-field.

| Propiedad | Default | Validación `ValidateDataAnnotations` | Validación `Validate` |
| --- | --- | --- | --- |
| `Host` | `string.Empty` | — | Si `Mode=Smtp` ⇒ `!= whitespace` |
| `Port` | `25` | — | Si `Mode=Smtp` ⇒ `1..65535` |
| `EnableSsl` | `false` | — | — |
| `UserName` | `string.Empty` | — | Si `Mode=Smtp` y host ≠ localhost ⇒ `!= whitespace` |
| `Password` | `string.Empty` | — | Si `Mode=Smtp` y host ≠ localhost ⇒ `!= whitespace` |
| `FromAddress` | `string.Empty` | `[Required]`, `[EmailAddress]` | — |
| `FromName` | `string.Empty` | `[Required]` | — |
| `WebBaseUrl` | `string.Empty` | `[Required]`, `[Url]` | — |
| `Mode` | `Logger` (`SmtpDeliveryMode`) | — | — |

`Mode` acepta el enum `SmtpDeliveryMode` (`Smtp`, `Logger` u otros). En Development el host tolera la sección ausente; cualquier otra env requiere los campos obligatorios.

Equivalentes en variables de entorno: `Smtp__Host`, `Smtp__Port`, etc.

> Ver how-to `12-configurar-smtp-real.md` para producción.

## `SgvApi` — `SgvApiOptions`

Sólo Web. `BindConfiguration(SgvApiOptions.SectionName)` con validación:

| Propiedad | Default | Validación |
| --- | --- | --- |
| `BaseUrl` | — | `Uri.IsWellFormedUriString(..., UriKind.Absolute)` |

Sin la `BaseUrl` absoluta, `WebApplication.Build()` falla con `ValidateOnStart`.

## `ConnectionStrings:SgvDatabase`

`string` plano. API. Validación inline (no `IValidateOptions`):

| Regla | Error |
| --- | --- |
| `null/whitespace` | `OptionsValidationException` con `"Debe configurar ConnectionStrings:SgvDatabase antes de iniciar la API."` |
| Sin substring `Server=` (case-insensitive) | `OptionsValidationException` con `"...debe incluir Server= y Database=."` |
| Sin substring `Database=` | idem |

Equivalentes en variables de entorno: `ConnectionStrings__SgvDatabase` (doble underscore por convención Microsoft). Acepta formato Pomelo estándar: `server=...;database=sgv;user=...;password=...;`.

## `MySql:ServerVersion`

`string` parseable por `Version.TryParse`. API.

| Default | Validación |
| --- | --- |
| `8.0.36` | `Version.TryParse` y `.Major > 0`; cualquier valor inválido lanza `OptionsValidationException` |

Equivalente en variable de entorno: `MySql__ServerVersion`.

## `AllowedOrigins`

`string[]` (índices numéricos). API.

| Ambiente | Comportamiento |
| --- | --- |
| `Development` | Si la sección está vacía: `SetIsOriginAllowed(_ => true)` con `AllowAnyHeader/Method`, sin credenciales |
| Otro ambiente | Si la sección está vacía: el host falla con `InvalidOperationException` en `CorsService` |
| Con sección | `WithOrigins(allowedOrigins).AllowCredentials()` |

Equivalentes en variables de entorno: `AllowedOrigins__0`, `AllowedOrigins__1`, etc. (ver how-to `06-configurar-allowed-origins-produccion.md`).

## Variables de entorno operativas adicionales

| Variable | Sección equivalente | Notas |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | — | Fija `Development`/`Staging`/`Production` |
| `ASPNETCORE_URLS` | — | Bind address (Web: default `http://localhost:5000`) |
| `DOTNET_ENVIRONMENT` | — | Mismo fin |
| `ConnectionStrings__SgvDatabase` | (ver arriba) | — |
| `Jwt__SigningKey` | (ver arriba) | El secreto más sensible |

## `appsettings.*.json`

| Archivo | Comportamiento |
| --- | --- |
| `appsettings.json` | Defaults compartidos (committeado) |
| `appsettings.Development.json` | Override en Development (committeado; `Jwt:SigningKey` placeholder sólo para arrancar) |
| `appsettings.{Environment}.json` | Override por ambiente |
| `appsettings.Local.json` | Opcional, ignorado por `.gitignore` para secretos locales |
| Variables de entorno | Mayor precedencia que `appsettings.json` |

`SGV.Api/appsettings.Development.json` contiene un placeholder `Jwt:SigningKey` con un string corto para arrancar el host; **no** es apto para producción. Reemplazar con `dotnet user-secrets` (`dotnet user-secrets set "Jwt:SigningKey" "..." --project src/SGV.Api`).

## `xunit.runner.json` (tests)

Sólo en `tests/SGV.Tests/xunit.runner.json`. Configuración típica de xUnit v2 runner (paralelismo, filtros, reporteros). No impacta runtime de API/Web.

## Secret store

| Comando | Notas |
| --- | --- |
| `dotnet user-secrets set "Jwt:SigningKey" "..." --project src/SGV.Api` | Recomendado para local |
| `dotnet user-secrets set "ConnectionStrings:SgvDatabase" "..." --project src/SGV.Api` | Alternativa al env var |
| `dotnet user-secrets set "Smtp:Host" "..." --project src/SGV.Api` | Si `Mode=Smtp` real |

En CI (`.github/workflows/ci.yml`) el secreto se inyecta como variable de entorno `JWT_SIGNING_KEY` y la connection string real contra `mysql:8.0`.

## Resumen de fail-loud en arranque

| Validación | Tipo de fallo |
| --- | --- |
| `Jwt:SigningKey` faltante/corto | `OptionsValidationException` |
| `ConnectionStrings:SgvDatabase` faltante o sin `Server=`/`Database=` | `OptionsValidationException` |
| `MySql:ServerVersion` no parseable | `OptionsValidationException` |
| `Smtp` (no-Development) sin `FromAddress`/`FromName`/`WebBaseUrl` | `OptionsValidationException` |
| `Smtp:Mode=Smtp` sin `Host`/`User`/`Pass` | `OptionsValidationException` |
| `RefreshToken:*` ≤ 0 | `OptionsValidationException` |
| `SgvApi:BaseUrl` no absoluta | `OptionsValidationException` |
| `AllowedOrigins` vacío fuera de Development | `InvalidOperationException` (en `CorsService`) |

## Referencias

- How-to: [Rotar JWT signing key](../how-to/03-rotar-jwt-signing-key.md)
- How-to: [Configurar SMTP real](../how-to/12-configurar-smtp-real.md)
- How-to: [Configurar Allowed Origins producción](../how-to/06-configurar-allowed-origins-produccion.md)
- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- R-03-09 — Health checks (usa `SgvDbContextReadinessHealthCheck` que abre una conexión MySQL cruda)
