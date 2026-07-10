# Design: #97 — JWT signing key seguro (validación al arranque)

## 1. Resumen técnico

Se elimina el default hardcodeado de `JwtOptions.SigningKey` y se reemplaza por `AddOptions<JwtOptions>().BindConfiguration(...).Validate(...).ValidateOnStart()`. La clave se materializa via `IPostConfigureOptions<JwtBearerOptions>`, eliminando el bug de captura temprana. Placeholder pinned en `appsettings.Development.json`; producción/CI deben setear `Jwt__SigningKey`.

## 2. Componentes afectados

| Archivo | Rol |
|---|---|
| `src/SGV.Infraestructura/Seguridad/JwtOptions.cs` | `SigningKey` → `string.Empty`. |
| `src/SGV.Api/Program.cs` | `Get + Configure` → `AddOptions().Bind().Validate().ValidateOnStart()`. `AddJwtBearer` con cuerpo vacío. |
| `src/SGV.Api/Seguridad/ConfigureJwtBearerFromJwtOptions.cs` (nuevo) | `IPostConfigureOptions<JwtBearerOptions>` que arma `TokenValidationParameters`. |
| `src/SGV.Api/appsettings.Development.json` | Nueva sección `Jwt:SigningKey` con placeholder pinned. |
| `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs` (nuevo) | 5 tests (4 fail-loud + 1 guard estructural). |
| `tests/SGV.Tests/Seguridad/JwtRealWebApplicationFactory.cs` (nuevo) | Factoría con `AuthServicio`/`JwtBearer` reales; siembra idempotente (rol + `PersonaEntity` + admin) dentro de `InitializeAsync()`; la factoría NO implementa `IAsyncLifetime` (cada test invoca explícitamente — ver §5.3). |
| `tests/SGV.Tests/Seguridad/JwtRealAuthTests.cs` (nuevo) | 2 tests con `[MySqlFact]`. |
| `.github/workflows/ci.yml`, `docs/decisiones-implementacion.md`, `AGENTS.md` | CI export `Jwt__SigningKey`; docs de secretos y `dotnet user-secrets`. |

## 3. Configuración

```csharp
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
                   && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes")
    .ValidateOnStart();
```

| Decisión | Justificación |
|---|---|
| **Bytes UTF-8, no `string.Length`** | HMAC-SHA256 firma bytes; 32 chars multibyte caen <32 bytes. |
| **`Issuer`/`Audience` fuera de scope** | Metadatos públicos; no reducen el blast radius del #97. |
| **`ValidateOnStart`** | Patrón vigente (`SgvApiOptions`). Falla el `Build()`. |
| **Placeholder dev pinned** | `"DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000"` (51 bytes ASCII puros: 34 chars prefijo + 16 ceros). El sufijo de 16 ceros se elige para que el patrón dev-only sea inequívoco al inspeccionar configs (≥32 cumple el validador, ≥44 dejaba el sufijo ambiguo entre 10 y 16 ceros). `src/SGV.Api/` no tiene `appsettings.json`; sin override, `ValidateOnStart` lanza. |

## 4. Autenticación JWT — diferir la clave

**Problema**: `Program.cs:71-100` hace `Get<JwtOptions>() ?? new JwtOptions()` y captura el resultado en una variable local que cierra sobre `AddJwtBearer` — clave sellada al registro.

**Decisión**: `IPostConfigureOptions<JwtBearerOptions>` (clase dedicada). Canónico .NET; corre DESPUÉS de `ValidateOnStart`; resuelve `IOptions<JwtOptions>` por DI. Lambda inline descartada.

```csharp
internal sealed class ConfigureJwtBearerFromJwtOptions(IOptions<JwtOptions> options)
    : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions bearer)
    {
        // Guard defensivo contra un futuro multi-scheme (NEW-WARN-5):
        // si mañana se suma otro handler, evitamos pisar su TokenValidationParameters.
        if (name != JwtBearerDefaults.AuthenticationScheme) return;

        var jwt = options.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true,
            ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
        };
    }
}
```

Registro: `builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerFromJwtOptions>();` justo después de `AddJwtBearer(...)`.

**Hot-reload: NO soportado**. La clave se lee una vez (singleton); runtime requeriría `IOptionsMonitor` + factory — fuera de scope.

## 5. Testing

Capa: integración. Archivos nuevos en `tests/SGV.Tests/Seguridad/`. `ApiWebApplicationFactory` **no se modifica**: el placeholder cubre el `ValidateOnStart`. Los tests existentes (incluido `Login_WithValidCredentials_ReturnsAccessToken` en `AuthControllerTests.cs:11-23`) siguen pasando: `ConfigureWebHost` reemplaza `IAuthServicio` por `FakeAuthServicio` (`"fake-token"` literal, `ApiWebApplicationFactory.cs:762-768`) y registra `FakeAuthenticationHandler` (cualquier `Bearer` pasa, `:770-816`).

### 5.1 Override de configuración: `WithWebHostBuilder`

`ApiWebApplicationFactory` (línea 822) acepta `Action<IServiceCollection>?`, no `Action<IWebHostBuilder>`. La receta original (`new ApiWebApplicationFactory(b => b.ConfigureAppConfiguration(...))`) **no compila**; se elige `factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration(...))` (§5.2 lo usa).

### 5.2 Tests de fail-loud (`JwtOptionsTests.cs`)

`CreateClient()` fuerza el `WebHost.Build()` que dispara `ValidateOnStart`. Patrón: `factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration(...))` + `Assert.Throws<OptionsValidationException>(() => factory.CreateClient())`.

| Test | Cubre | Override |
|---|---|---|
| `HostBuild_SinSigningKey_LanzaOptionsValidationException` | Spec "Fallo si falta" | `["Jwt:SigningKey"] = ""` |
| `HostBuild_SigningKeyCorto_LanzaOptionsValidationException` | Spec "Fallo si <32 bytes" | `["Jwt:SigningKey"] = "short-key"` |
| `HostBuild_SigningKey31Bytes_Lanza` | Boundary 31 | Clave de 31 bytes UTF-8 |
| `HostBuild_PlaceholderDev_Arranca` | Spec "Arranque con placeholder" | Sin override |
| `appsettings_Development_Tiene_Placeholder_Valido_≥32Bytes` | Guard estructural (NEW-WARN-3) | Lee `appsettings.Development.json`, assert `GetByteCount >= 32` |

Test defensivo y barato (`File.ReadAllText` + `JsonDocument`).

### 5.3 Tests con JWT real (`JwtRealAuthTests.cs`)

Spec exige camino real: **(a)** token con clave aceptada por `[Authorize]`, **(b)** token con clave distinta rechazado con `401`. `ApiWebApplicationFactory` fakea emisión y validación, así que §5.2 **no** ejercen el camino real.

**MySQL + siembra de admin**: `AuthServicio.LoginAsync` consulta `UserManager` y `DbContext` → factoría requiere MySQL. `[MySqlFact]`: sin MySQL, `Skip` limpio. Sin siembra, `POST /api/v1/auth/login` rechaza cualquier credencial → falso positivo. `InitializeAsync` accede a `Server` (force `Build()` → `ValidateOnStart`) y siembra idempotente: (1) rol `RolesSgv.Administrador` si `!RoleExistsAsync(...)`; (2) `PersonaEntity` previa con `Nombres`/`Apellidos` no vacíos (FK obligatoria — `SgvIdentityUser.PersonaId` en `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs:7`; `PersonaConfiguracion.cs:16-17` los declara `IsRequired`); (3) `SgvIdentityUser` con `PersonaId = persona.Id` (property pública, NO asignada automáticamente por `UserManager.CreateAsync`) + `AddToRoleAsync(..., RolesSgv.Administrador)`. Idempotencia: `FindByNameAsync` antes de `CreateAsync`; `RoleExistsAsync` antes de `RoleManager.CreateAsync` — el seed corre dos veces sin romper.

**IAsyncLifetime** (cambio crítico, xUnit v2.9.2): xUnit **NO** invoca `IAsyncLifetime` sobre instancias creadas con `new ...()` en el cuerpo de un test — sólo sobre test classes que implementan la interfaz directamente, o sobre fixtures registradas vía `IClassFixture<T>` / `ICollectionFixture<T>`. `JwtRealWebApplicationFactory` **NO implementa** la interfaz; cada test invoca `InitializeAsync()` y `DisposeAsync()` explícitamente. Recomendado `using var factory = ...` para que `DisposeAsync` se dispare al salir del scope. Sin esto, el seed no corre, los tests fallan sin razón aparente y el problema es invisible al lector.

**Connection string**: `TestSgvDbContextFactory.ResolveConnectionString()` (env var → `appsettings.*.json` → default `localhost:3306`); respeta config explícita (CI exporta `ConnectionStrings__SgvDatabase`).

**Decisión**: factoría nueva `JwtRealWebApplicationFactory` (sin fakes). No se reutiliza `ApiWebApplicationFactory`.

```csharp
internal sealed class JwtRealWebApplicationFactory(string signingKey)
    : WebApplicationFactory<SGV.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Jwt:SigningKey"] = signingKey
                // Issuer/Audience omitidos: JwtOptions ya los defaulta a "SGV".
            }));

    public async Task InitializeAsync()
    {
        _ = Server; // fuerza Build() → ValidateOnStart
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgvDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<SgvIdentityUser>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        // 1) Rol (idempotente). DatosSemilla siembra via HasData en
        //    migraciones; el check evita dependencia frágil del orden.
        if (!await roleManager.RoleExistsAsync(RolesSgv.Administrador))
        {
            // assert roleResult.Succeeded
            await roleManager.CreateAsync(new IdentityRole
            {
                Id = RolesSgv.Administrador,
                Name = RolesSgv.Administrador,
                NormalizedName = RolesSgv.Administrador.ToUpperInvariant()
            });
        }

        // 2) Persona previa — SgvIdentityUser.PersonaId es FK obligatoria
        //    (SgvIdentityUserConfiguracion.cs:12-13, OnDelete=Restrict).
        //    Nombres/Apellidos son required; Id debe setearse
        //    explícitamente (ConfigurarId usa ValueGeneratedNever).
        var persona = new PersonaEntity
        {
            Id = Guid.NewGuid(),
            Nombres = "Admin",
            Apellidos = "Seed",
            IsActive = true
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync();

        // 3) Admin — UserManager.CreateAsync NO asigna PersonaId
        //    automáticamente: es property pública de SgvIdentityUser
        //    (no método), debe setearse antes de CreateAsync.
        if (await userManager.FindByNameAsync("admin") is null)
        {
            var admin = new SgvIdentityUser
            {
                UserName = "admin",
                Email = "admin@test.local",
                EmailConfirmed = true,
                PersonaId = persona.Id
            };
            // assert createResult.Succeeded
            await userManager.CreateAsync(admin, "Admin#12345");
            await userManager.AddToRoleAsync(admin, RolesSgv.Administrador);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

**Patrón A** (factoría NO implementa `IAsyncLifetime`; cada test invoca explícitamente):

```csharp
[MySqlFact]
public async Task TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200()
{
    using var factory = new JwtRealWebApplicationFactory(signingKey: TestKeys.Host);
    await factory.InitializeAsync(); // siembra idempotente: rol + persona + admin
    using var client = factory.CreateClient();

    var login = await client.PostAsJsonAsync("/api/v1/auth/login",
        new LoginRequest("admin", "Admin#12345"));
    var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/usuarios");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
}

[MySqlFact]
public async Task TokenFirmado_ConClaveDistinta_Rechazado_401()
{
    using var factory = new JwtRealWebApplicationFactory(signingKey: TestKeys.Host);
    await factory.InitializeAsync();
    using var client = factory.CreateClient();

    // Firma HS256 con clave ajena a la configurada en la factoría.
    var foreign = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: "SGV", audience: "SGV",
        claims: [new Claim(JwtRegisteredClaimNames.Sub, "attacker")],
        expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKeys.Foreign)),
            SecurityAlgorithms.HmacSha256)));

    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/usuarios");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", foreign);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
}
```

| Test | Cubre | Mecanismo |
|---|---|---|
| `TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200` `[MySqlFact]` | Spec "Firma usa clave configurada" | Login con admin sembrado (`PersonaId` válido); `Bearer <AccessToken>`; `GET /api/v1/usuarios` → `200`. |
| `TokenFirmado_ConClaveDistinta_Rechazado_401` `[MySqlFact]` | Spec "Validación rechaza token con otra clave" | Firmar `JwtSecurityToken` HS256 con clave distinta de `TestKeys.Host`; `GET /api/v1/usuarios` → `401`. |

## 6. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Placeholder dev filtrado a repo público → firma de tokens admin | Sufijo `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD`; docs en `AGENTS.md`/`decisiones-implementacion.md`. |
| Tokens pre-fix inválidos al deploy | Rechazo por `ValidateIssuerSigningKey = true` con clave nueva (§4). `CookieAuthentication.AllowRefresh = false` es salvaguarda complementaria, **no el mecanismo principal**. Expira a 60 min. |
| `ValidateOnStart` valida en startup, no en runtime | Comportamiento buscado; §4. |
| `Issuer`/`Audience` default | Out-of-scope; hardening futuro aparte. |
| Validador acepta claves trivialmente débiles (NEW-WARN-4) | ≥32 bytes UTF-8 no rechaza repetitivas ni diccionario. Out-of-scope; issue aparte. |
| Confusión con #59 / factory sin `appsettings.Development.json` | PR description: no toca migraciones ni `Ocupaciones`. Placeholder commiteado. |
| Tests de JWT real usan clave hardcodeada | Específica del test; no es secreto real. |

## 7. Plan de implementación

1. `JwtOptions.SigningKey` → `string.Empty`; placeholder pinned en `appsettings.Development.json`.
2. `Program.cs`: `AddOptions().Bind().Validate().ValidateOnStart()`; `AddJwtBearer` vacío; registrar `ConfigureJwtBearerFromJwtOptions`.
3. Crear `src/SGV.Api/Seguridad/ConfigureJwtBearerFromJwtOptions.cs` (guard `name != JwtBearerDefaults.AuthenticationScheme`).
4. Crear `JwtOptionsTests.cs` (5 tests).
5. Crear `JwtRealWebApplicationFactory.cs` (factoría sin fakes; siembra idempotente `rol + PersonaEntity + admin` dentro de `InitializeAsync()`; connection string vía `TestSgvDbContextFactory`).
6. Crear `JwtRealAuthTests.cs` (2 tests `[MySqlFact]`, cada uno invoca `factory.InitializeAsync()` explícitamente con `using var`).
7. Modificar `.github/workflows/ci.yml` para exportar `Jwt__SigningKey` (§8).
8. Actualizar `docs/decisiones-implementacion.md` y `AGENTS.md`.
9. `dotnet build SGV.slnx && dotnet test SGV.slnx`.

## 8. Verificación esperada

- `dotnet build SGV.slnx` sin warnings.
- `dotnet test SGV.slnx` en verde; 7 tests nuevos (5 `JwtOptionsTests` + 2 `JwtRealAuthTests`).
- `grep -rn "SGV-development-signing-key" src/` sin matches.
- `appsettings.Development.json` contiene el placeholder pinned; `appsettings.json` ausente.
- `docs/decisiones-implementacion.md` contiene "Gestión de secretos JWT"; `AGENTS.md` menciona `dotnet user-secrets`.
- `.github/workflows/ci.yml` exporta `Jwt__SigningKey` desde `secrets.JWT_SIGNING_KEY`. **Defense-in-depth**: el placeholder cubre los tests hoy; el export garantiza que CI **no dependa del placeholder dev**. **Decisión operativa, no bugfix**.
- PR description declara out-of-scope (#59) y referencia #97.

## Decisiones cerradas

- Validar **solo** `SigningKey` ≥32 bytes UTF-8; `Issuer`/`Audience` fuera.
- Placeholder en `appsettings.Development.json` (no override en factory).
- `IPostConfigureOptions<JwtBearerOptions>` como clase dedicada.
- Sin hot-reload ni rotación.

## Open questions

- Ninguna. `ValidAlgorithms` queda con el default de la librería (acepta `HS256`); restringir algoritmos sería un issue aparte.
