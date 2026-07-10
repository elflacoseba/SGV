# Decisiones de Implementación

## SDK y Target Framework

Los proyectos apuntan a `net10.0` (.NET 10). El archivo `global.json` fija el SDK en `10.0.300` con roll-forward `latestMajor` para permitir compatibilidad con versiones posteriores del SDK 10.x.

## Proveedor de Base de Datos

Se utiliza Pomelo Entity Framework Core 9.x como proveedor único para MySQL 8. Los paquetes `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Pomelo.EntityFrameworkCore.MySql` permanecen en versiones 9.x porque Pomelo 9 depende de EF Core relational `>= 9.0.0 && < 9.0.999`. SQL Server no se soporta como proveedor activo.

## Índices Únicos con Soft Delete

MySQL no soporta índices filtrados como SQL Server. Para preservar las reglas de unicidad sobre registros activos (no eliminados), se utilizan columnas generadas (computed columns) con índices únicos. La columna generada devuelve el valor de la columna de negocio cuando el registro está activo (`IsDeleted = 0`) y `NULL` cuando está eliminado. MySQL permite múltiples `NULL` en índices únicos, lo que replica el comportamiento de los índices filtrados de SQL Server.

## Identity

Se mantiene `IdentityUser` con clave string, por lo que las columnas de auditoría que referencian usuarios usan `varchar(450)`. Esta decisión conserva el comportamiento estándar de ASP.NET Core Identity y evita personalización prematura.

## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por puesto y una única ocupación vigente por persona mediante columnas generadas con índices únicos. Si el negocio requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de dedicación.

## Postulantes Externos

Los postulantes externos se registran sin habilidades estructuradas en esta versión. La compatibilidad automática queda enfocada en postulantes internos vinculados a una persona.

## Auditoría

La auditoría se implementa con una tabla única `Auditorias` y un interceptor de EF Core. Se excluyen campos sensibles por nombre para evitar persistir contraseñas, tokens o stamps de seguridad en JSON.

## TestSgvDbContextFactory (separado del factory de producción)

El factory de tests (`tests/SGV.Tests/Persistencia/TestSgvDbContextFactory.cs`) es independiente de `SgvDbContextFactory`. Razones:

1. **Responsabilidades distintas:** el factory de producción está diseñado para `dotnet ef` design-time (migraciones, scripting). El de tests persigue disponibilidad inmediata.
2. **Default seguro en tests, fail-loud en producción:** `TestSgvDbContextFactory` cae a `localhost:3306;Database=sgv_test;User=root;Password=` cuando no hay configuración externa. `SgvDbContextFactory` tira `InvalidOperationException` en la misma situación — es parte de la seguridad: no exponer credenciales por defecto.
3. **Aislamiento:** los tests nunca heredan config de producción ni viceversa. Si el developer setea `ConnectionStrings__SgvDatabase`, ambos apuntan al mismo target, pero cada uno resuelve su propia cadena.
4. **El runtime de la API no usa ninguno de los dos factories:** lee `builder.Configuration.GetConnectionString("SgvDatabase")` vía DI estándar en `Program.cs`.

## SgvDbContextFactory fail-loud

El factory de producción (`src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs`) **no tiene fallback de conexión**. Si no se configura `ConnectionStrings:SgvDatabase` (vía user-secrets, env var o appsettings), lanza `InvalidOperationException` con un mensaje que orienta al developer. Históricamente tenía un placeholder `"CONEXION_STRING_AQUI"` y luego un default con credenciales hardcodeadas, ambos eliminados por razones de seguridad.

Cada developer debe configurar una vez:
```bash
dotnet user-secrets set "ConnectionStrings:SgvDatabase" \
  "Server=localhost;Port=3306;Database=SGV;User=root;Password=TU_PASSWORD" \
  --project src/SGV.Api
```
CI exporta `ConnectionStrings__SgvDatabase` directamente en `.github/workflows/ci.yml`.

## Gestión de secretos JWT

`JwtOptions.SigningKey` cumple el mismo principio fail-loud que `SgvDbContextFactory`: no hay default embebido. Si la sección `Jwt:SigningKey` falta, está vacía, contiene solo whitespace o mide menos de 32 bytes UTF-8, el host **no arranca** y `Program.cs` propaga un `Microsoft.Extensions.Options.OptionsValidationException` con el mensaje `Jwt:SigningKey must be configured and ≥32 UTF-8 bytes`. Este contrato se valida en `WebApplicationFactory<TEntryPoint>.CreateClient()` vía `ValidateOnStart`, así que cualquier arranque — development, CI o producción — cae en el mismo fail-loud.

**Dev local.** `src/SGV.Api/appsettings.Development.json` provee un placeholder pinned (≥32 bytes UTF-8, sufijo `DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000`) para que `dotnet run` funcione sin setup adicional. Para pruebas locales con tokens propios, cada developer debe generar una clave aleatoria propia y persistirla con:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes ASCII>" --project src/SGV.Api
```

> **El placeholder dev NO es apto para producción.** Es público en el repo. Cualquier deploy que arranque con él es vulnerable a falsificación de tokens admin. La diferencia entre el placeholder y una clave real es detectable con `grep "DEV-PLACEHOLDER" config.json` en cualquier review.

**Producción / CI.** No se commitea ninguna clave. Las opciones soportadas son:

1. Variable de entorno `Jwt__SigningKey` (ASP.NET Core convierte `__` en `:` para `IConfiguration`).
2. Secret manager del proveedor (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault, etc.) inyectado como env var al arranque del pod.

**Operación del secreto en GitHub Actions.** El job de tests exporta `Jwt__SigningKey` desde `secrets.JWT_SIGNING_KEY` (defense-in-depth: aunque el placeholder dev cubre el caso normal, este export garantiza que la suite no dependa de él). El valor es un secreto dedicado (≥32 bytes), independiente del placeholder dev, y se rota manualmente. Para crearlo o rotarlo:

```bash
openssl rand -base64 48
```

…y guardar el resultado en *Settings → Secrets and variables → Actions → JWT_SIGNING_KEY* del repositorio, scope `Environment: production` si aplica.

## Inmutabilidad de `Codigo` en `UnidadOrganizativa`

`UnidadOrganizativa.Codigo` es la identidad lógica de la unidad. Una vez creada, **no puede cambiar**. El contrato se sostiene en tres capas, cada una con un mecanismo distinto pero convergente:

1. **Dominio** — `UnidadOrganizativa` es `sealed record class : EntidadAuditable` con propiedades `init`. `Codigo` se asigna únicamente en el constructor primario. Toda mutación posterior (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) devuelve una nueva instancia vía `with` y **nunca** expone `Codigo` como parámetro. El método legacy `CambiarDatos(codigo, ...)` está eliminado. La asimetría con `Puesto` (que mantiene `sealed class` con `private set`) es deliberada: no se quiere acoplar `Puesto` a esta restricción.

2. **Contrato HTTP** — `ActualizarUnidadOrganizativaRequest` no tiene `Codigo`. El binding de System.Text.Json descarta silenciosamente cualquier `codigo` extra en el body de `PUT /api/v1/unidades-organizativas/{id}`. El campo queda **fuera de contrato**: no se persiste, no se valida, no genera error. Esta propiedad aplica también a clientes maliciosos que envíen `{"codigo":"HACKED", ...}` — el servidor devuelve la unidad con su `Codigo` original intacto. La capa web refuerza la regla ocultando el input en `Edit` (PR3).

3. **Persistencia** — `PersistenceToDomainMapper.ToDomain(UnidadOrganizativaEntity)` no usa `SetProperty` / `BindingFlags.NonPublic` para `IsActive`, `UnidadPadre` ni `TipoUnidadOrganizativa`. Esas propiedades se aplican con `with { ... }` sobre el record. La razón: `PropertyInfo.SetValue` (que es lo que envuelve el helper `SetProperty`) no respeta el modifier `IsExternalInit` en runtime, así que podría saltarse el `init`-only del record. El `with` del compilador sí lo respeta y mantiene el invariante end-to-end. La suite incluye un test estructural (`ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper`) que recorre el IL del método y falla si alguien re-introduce el helper.

**Reactivación** — `ReactivarAsync` es el único flujo que sigue validando conflicto por código activo. La validación se hace contra `unidad.Codigo` (el código persistido en el record cargado), **no** contra un valor enviado por el cliente, porque el cliente nunca envía código en update. El índice único computado `ActiveCodigoUnique` (`CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`) en `UnidadOrganizativaConfiguracion` sigue siendo la red de seguridad a nivel DB.

## Autorización del API

La API adopta una postura **default-deny** desde el change `2026-07-09-agregar-autorizacion-api-restantes` (issue #96). El patrón vigente replica los precedentes de `CargosController` (archive `2026-07-01-2026-07-01-cargos-crear-autorizacion-admin`) y `PuestosController` (issue #90).

### Reglas

1. **Fallback policy global en `Program.cs`** — `AddAuthorization(opts => opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. Cualquier endpoint sin `[Authorize]` explícito falla cerrado con `401 Unauthorized`. Es la red de seguridad para controllers futuros: si se suma un controller nuevo sin `[Authorize]`, ya no queda público por default.
2. **Decoración explícita por controller** — Los controllers que requieren autenticación usan `[Authorize]` a nivel clase. Los controllers con mutaciones (`POST`, `PUT`, `PATCH`, `DELETE`) sobre-ponen `[Authorize(Roles = RolesSgv.Administrador)]` por acción, usando la constante `RolesSgv.Administrador` (sin literales de string repetidos). Las lecturas autenticadas (`GET`) heredan `[Authorize]` de la clase y permiten cualquier rol válido.
3. **Única excepción anónima: `AuthController.Login`** — El handler `Login` (`POST /api/v1/auth/login`) lleva `[AllowAnonymous]` explícito para sobrevivir la fallback policy global. Es la única ruta del API accesible sin credenciales; cualquier otro endpoint sin token devuelve `401`.

### Catálogos read-only autenticados

`NivelesCargoController` (`GET /api/v1/niveles-cargo*`) y `TipoUnidadesOrganizativasController` (`GET /api/v1/tipos-unidad-organizativa*`) pasan de anónimos a autenticados. Esto rompe el contrato histórico de la spec `sgv-readonly-api/spec.md`, que ahora queda reescrita para reflejar esta postura. Los consumidores externos que leían catálogos sin token deben autenticarse o recibir `401`.

### Precedentes y outliers

- **Controllers ya endurecidos** (no tocados por este change): `CargosController`, `PuestosController`, `UsuariosController`, `SkillsController`. Su `[Authorize]` sigue vigente.
- **No se introducen policies nominales nuevas**: el patrón `RolesSgv.Administrador` literal se mantiene para evitar indirección. Si en el futuro se requieren policies compuestas, se decidirá en un change separado.
- **Ventana de exposición por JWT**: el sistema valida firma, issuer, audience y lifetime del JWT pero NO reconsulta roles contra la DB por request. Un usuario cuyo rol cambia de `Administrador` a `GestorVacantes` conserva permisos de mutación hasta que su JWT expire. Esta ventana es inherente a JWT y no se aborda en este change.
- **Sub-recursos**: la decoración `[Authorize]` a nivel clase se hereda a sub-recursos anidados (e.g. `PUT /api/v1/personas/{id}/skills/{skillId}`). El sub-recurso `PersonasController.UpsertSkill`/`DeleteSkill` queda protegido automáticamente; no requiere override adicional porque la mutación ya exige `RolesSgv.Administrador` por la convención adoptada.
