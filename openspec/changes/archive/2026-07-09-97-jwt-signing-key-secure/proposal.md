# Proposal: #97 — JWT signing key seguro (sin default hardcodeado, con validación al arranque)

## Resumen

`JwtOptions.SigningKey` está hardcodeado como default en `src/SGV.Infraestructura/Seguridad/JwtOptions.cs:11` y `Program.cs` lo materializa con `?? new JwtOptions()` cuando la sección `Jwt` falta en config. Cualquier deploy sin sección `Jwt` queda firmado con una clave pública conocida. La propuesta elimina el default, exige presencia de la clave (≥32 bytes UTF-8) al arranque con `AddOptions().Bind().Validate().ValidateOnStart()`, y agrega un placeholder dev en `appsettings.Development.json` siguiendo el patrón ya vigente de `SgvApiOptions` en `SGV.Web/Program.cs:13-18`.

## Motivación

El issue #97 (auditoría arquitectónica julio 2026) marca este caso como **blocker de seguridad**: con el código actual, un atacante con acceso de lectura al repo puede falsificar tokens de administrador en cualquier entorno que arranque sin la sección `Jwt` configurada (default confirmado en dev porque `appsettings.Development.json` no incluye `Jwt`). El blast radius es total — un único `JWT` firmado con la clave hardcodeada sortea `[Authorize]`, el guard de rol `Administrador` y el cookie→bearer bridge. La política del repo (`SgvDbContextFactory` en `docs/decisiones-implementacion.md:40-50`) ya define el patrón "fail-loud + user-secrets"; este fix lo extiende a JWT.

## Contexto actual (evidencia del explore)

| Archivo:Línea | Síntoma |
|---|---|
| `src/SGV.Infraestructura/Seguridad/JwtOptions.cs:11` | Default `SigningKey = "SGV-development-signing-key-change-before-production-2026"` (60 chars, público en el repo) |
| `src/SGV.Api/Program.cs:71` | Fallback silencioso `?? new JwtOptions()` si sección ausente |
| `src/SGV.Api/Program.cs:98` | `IssuerSigningKey` capturado por cierre con la clave resuelta arriba |
| `src/SGV.Api/Program.cs:72` | `Configure<JwtOptions>` separado del `Get<JwtOptions>()` previo |
| `src/SGV.Api/appsettings.Development.json` | Sin sección `Jwt` → en dev también se usa el default |
| `src/SGV.Infraestructura/Seguridad/AuthServicio.cs:33` | Único emisor real (`LoginAsync`) firma con la misma clave |

Único emisor confirmado: `grep "JwtSecurityToken\|WriteToken"` sobre `src/` solo matchea `AuthServicio.cs`. No hay refresh token, password reset, email confirmation, ni tokens persistidos en DB.

## Alcance

### In Scope
- Quitar default de `JwtOptions.SigningKey` (deja `string` vacío).
- En `src/SGV.Api/Program.cs`: reemplazar `?? new JwtOptions()` + `Configure<>` por `AddOptions<JwtOptions>().BindConfiguration("Jwt").Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes").ValidateOnStart();`.
- Diferir la construcción de `IssuerSigningKey` en `AddJwtBearer` para que lea de `IOptions<JwtOptions>` resuelto en runtime (capturar el `IServiceProvider` del builder y resolver al armar `TokenValidationParameters`).
- Agregar sección `Jwt` con placeholder dev (≥32 bytes, sufijo `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD`) a `src/SGV.Api/appsettings.Development.json`.
- Tests nuevos en `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs` que prueben fail-loud: (a) `Jwt:SigningKey` ausente, (b) clave <32 bytes. Assert `OptionsValidationException` al `WebApplicationFactory.Build()`.
- `docs/decisiones-implementacion.md`: nueva sección "Gestión de secretos JWT" con comandos `dotnet user-secrets` y contrato fail-loud.
- `AGENTS.md`: paso `dotnet user-secrets set "Jwt:SigningKey" ...` previo al primer arranque de la API.

### Out of Scope (no-goals)
- Rotación de claves o múltiples `SigningKey` activas.
- Refresh tokens, revocación, lista negra de tokens.
- Cambiar de HS256 a RSA/ECDSA.
- Endurecer `Issuer`/`Audience` (mantienen sus defaults `"SGV"`; no son secretos).
- Migración de DB o cambios al esquema `AspNet*` / Identity.

## Capacidades (contrato con sdd-spec)

### New Capabilities
- `jwt-signing-key-validation`: contrato de arranque para `JwtOptions.SigningKey` — presencia obligatoria, longitud mínima 32 bytes UTF-8, fail-loud vía `ValidateOnStart`. Cubre el reemplazo del default hardcodeado, la validación al startup y el contrato de secreto por ambiente.

### Modified Capabilities
- Ninguna. El cambio es netamente de infraestructura/seguridad: no introduce ni modifica requisitos funcionales observables por el usuario.

## Enfoque técnico

Patrón a replicar (referencia 1:1):

```csharp
// src/SGV.Web/Program.cs:13-18 (canónico del repo)
builder.Services
    .AddOptions<SgvApiOptions>()
    .BindConfiguration(SgvApiOptions.SectionName)
    .Validate(options => Uri.IsWellFormedUriString(options.BaseUrl, UriKind.Absolute),
        $"{SgvApiOptions.SectionName}:BaseUrl must be an absolute URI")
    .ValidateOnStart();
```

Aplicado a `JwtOptions`:

```csharp
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes")
    .ValidateOnStart();
```

Para el `IssuerSigningKey` en `AddJwtBearer` (línea 98 actual), diferir la lectura con `IOptions<JwtOptions>` resuelto en el `TokenValidationParameters` factory, en lugar de capturar la clave por cierre. Forma concreta: `AddJwtBearer` se configura con un callback que reciba el `IServiceProvider`/`IOptions<JwtOptions>` y construya el `SymmetricSecurityKey` al materializarse. La clave queda estática (sin hot-reload en este issue), pero el cierre sobre el valor resuelto deja de existir, eliminando el bug de captura temprana.

`appsettings.Development.json` recibe un placeholder reconocible:

```json
"Jwt": {
  "SigningKey": "DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000"
}
```

`ApiWebApplicationFactory` no requiere override: el `appsettings.Development.json` se aplica a `WebApplicationFactory<Program>` y la validación pasa. Tests nuevos prueban el fail-loud con `ConfigureAppConfiguration` que sobreescriba la sección.

## Áreas afectadas

| Área | Impacto | Descripción |
|---|---|---|
| `src/SGV.Infraestructura/Seguridad/JwtOptions.cs` | Modified | Quitar default de `SigningKey`; `Issuer`/`Audience` sin cambios |
| `src/SGV.Api/Program.cs` | Modified | Reemplazar `Get<JwtOptions>() ?? new JwtOptions()` por `AddOptions().Bind().Validate().ValidateOnStart()`; diferir `IssuerSigningKey` a `IOptions<JwtOptions>` |
| `src/SGV.Api/appsettings.Development.json` | Modified | Agregar sección `Jwt` con placeholder dev ≥32 bytes |
| `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs` | New | Tests de fail-loud (sin clave / clave corta) |
| `docs/decisiones-implementacion.md` | Modified | Nueva sección "Gestión de secretos JWT" |
| `AGENTS.md` | Modified | Paso `dotnet user-secrets` previo al arranque |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Placeholder dev filtrado a repo público → firma de tokens admin posible en deploys que lo usen | Media | Sufijo `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-...` ≥32 bytes; documentar en `AGENTS.md` y `docs/decisiones-implementacion.md` que producción debe sobrescribir vía env var o secret manager |
| Tokens pre-fix quedan inválidos al deploy | Baja | Rechazo por `ValidateIssuerSigningKey = true` con clave nueva: JWT firmado con clave vieja falla `SignatureValidation` → `401`. `CookieAuthentication.AllowRefresh = false` es salvaguarda complementaria del lado cookie, **no el mecanismo principal**. Sin tabla de revocación; expiran a 60 min. Aceptable per issue |
| `ValidateOnStart` rompe suite API si `appsettings.Development.json` no carga | Baja | Placeholder en `appsettings.Development.json` aplica a `WebApplicationFactory<Program>`; tests nuevos cubren el fail-loud con `ConfigureAppConfiguration` explícito |
| `IssuerSigningKey` capturado por cierre si no se difiere | Baja | Diferir lectura a `IOptions<JwtOptions>` resuelto en `AddJwtBearer` callback; documentar en `design.md` que hot-reload no es objetivo de este issue |
| Bug abierto #59 (`ActivePuestoIdUnique`) se confunda con este PR | Baja | Este change no toca migraciones ni `Ocupaciones`; PR debe declararlo en descripción para no contaminar scope |

## Rollback Plan

Revertir el PR (`git revert`) restaura defaults y patrón previo. Riesgo de rollback bajo: ningún cambio en esquema de DB, ningún contrato HTTP alterado, ningún consumidor externo del token depende del valor de la clave (solo cambia de dónde se lee). Si tras rollback quedan sesiones activas firmadas con la clave vieja, expiran naturalmente al `TokenLifetimeMinutes` (60 min). Comandos:
1. `git revert <merge-commit>`
2. `dotnet build SGV.slnx` — debe compilar sin cambios
3. `dotnet test SGV.slnx` — la suite vuelve a verde con el default original

## Criterios de éxito

Cumplidos los acceptance criteria del issue #97:
- [ ] El default de `JwtOptions.SigningKey` eliminado; cualquier arranque sin sección `Jwt` configurada lanza `OptionsValidationException` con mensaje claro.
- [ ] `ValidateOnStart` activo: el host falla en `Build()` si la clave está vacía o tiene <32 bytes UTF-8.
- [ ] `appsettings.Development.json` contiene un placeholder reconocible ≥32 bytes que permite `dotnet run` de la API local sin setup adicional.
- [ ] Test `JwtOptionsTests.HostBuild_SinSigningKey_LanzaOptionsValidationException` pasa.
- [ ] Test `JwtOptionsTests.HostBuild_SigningKeyCorto_LanzaOptionsValidationException` pasa.
- [ ] `docs/decisiones-implementacion.md` documenta el contrato de gestión de secretos JWT.
- [ ] `AGENTS.md` menciona el comando `dotnet user-secrets set "Jwt:SigningKey" ... --project src/SGV.Api` como paso de setup.
- [ ] Suite completa `dotnet test SGV.slnx` sigue verde (incluido `Login_WithValidCredentials_ReturnsAccessToken` y los 12 tests de `OcupacionRepositoryTests` que siguen bloqueados por #59, sin cambios).
- [ ] `dotnet build SGV.slnx` sin warnings nuevos.

## Migración / impacto en deploy

- **Tokens pre-fix**: quedan inválidos al deploy. El mecanismo principal de rechazo es `ValidateIssuerSigningKey = true` con la clave nueva (`design.md` §4): cualquier JWT firmado con la clave vieja falla `SignatureValidation` y devuelve `401`. `CookieAuthentication.AllowRefresh = false` es una salvaguarda complementaria del lado cookie (evita rehidratar `ClaimsPrincipal` con un `exp` viejo), **no el mecanismo principal**. No hay tabla de revocación que limpiar; expiran a 60 min. Aceptable per issue.
- **Producción**: debe setear `Jwt:SigningKey` antes del primer arranque (env var `Jwt__SigningKey` o secret manager del proveedor). Sin esto, la API **no levanta** — comportamiento deseado.
- **Dev local**: `dotnet run` funciona con el placeholder del `appsettings.Development.json`; para desarrollo con tokens propios, `dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes>"`.
- **CI**: `.github/workflows/ci.yml` **debe modificarse** para exportar `Jwt__SigningKey` desde un secreto de GitHub Actions hacia el job de `dotnet test`. **Racional (defense-in-depth / resiliencia operativa)**: `WebApplicationFactory<TEntryPoint>` carga `appsettings.Development.json` por defecto, por lo que hoy el placeholder ya cubre los tests sin este export. Modificamos CI para que **no dependa del placeholder dev** — si alguien borra o recorta `appsettings.Development.json` a futuro, la suite sigue verde porque CI inyecta su propia clave vía env var. Es una decisión operativa, no una corrección de bug. El valor es un secreto dedicado (NO el placeholder dev) y se rota manualmente; instrucción documentada en `docs/decisiones-implementacion.md`.

## Referencias

- Issue: [#97 — Eliminar JWT signing key default hardcodeado y validar al arranque](https://github.com/elflacoseba/SGV/issues/97)
- Explore: `openspec/changes/97-jwt-signing-key-secure/exploration.md`
- Patrón de validación: `src/SGV.Web/Program.cs:13-18` (`SgvApiOptions.AddOptions().Bind().Validate().ValidateOnStart()`)
- Precedente fail-loud: `docs/decisiones-implementacion.md:40-50` (`SgvDbContextFactory`)
- Issuer único: `src/SGV.Infraestructura/Seguridad/AuthServicio.cs:13-58`
- Tests API inmunes al JWT real: `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs:29-37, 894-896`
- Skill: `dotnet-csharp` (options pattern + `IOptions<T>` + `ValidateOnStart` semántica)
- Microsoft Docs: [Options pattern en ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options) · [ValidateOnStart](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.optionsbuilderextensions.validateonstart)
