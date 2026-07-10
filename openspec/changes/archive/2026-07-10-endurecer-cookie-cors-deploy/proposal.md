# Proposal: Endurecer cookie Web y CORS API para deploy real

## Resumen

Endurecer dos puntos de la frontera runtime antes de cualquier deploy real de SGV: la cookie de autenticación de `SGV.Web` debe exigir `SecurePolicy = Always` fuera de `Development`, y la política CORS de `SGV.Api` debe fallar al arrancar si `AllowedOrigins` está vacío fuera de `Development`, eliminando la combinación peligrosa `AllowAnyOrigin().AllowCredentials()`. Se documenta la matriz ambiente ↔ seguridad y se agregan tests que prueban ambos invariantes.

## Motivación

Evidencia del audit (issue #101):

- `src/SGV.Web/Program.cs:24-26` registra la cookie de autenticación con `SecurePolicy = SameAsRequest`. Detrás de un proxy mal configurado la cookie puede emitirse sin marca `Secure`, y `ApiBearerTokenHandler` reenvía el ticket como `Authorization: Bearer` a la API: si la cookie viaja en claro, el JWT puede filtrarse.
- `src/SGV.Api/Program.cs:111-125` lee `AllowedOrigins` desde configuración. Si la sección está vacía (caso real: `src/SGV.Api/appsettings.json` no existe y solo `appsettings.Development.json` define los origins), el código cae en `AllowAnyOrigin().AllowCredentials()`. Esta combinación es inválida para los browsers modernos y abre la puerta a que cualquier dominio invoque la API con credenciales si la versión del middleware no la rechaza.

Ambas decisiones son deudas de seguridad que conviene cerrar antes del primer deploy productivo. Severidad alta.

## Cambios propuestos

| # | Punto | Cambio |
|---|-------|--------|
| 1 | **Cookie Web** | `SecurePolicy` pasa a `Always` cuando `ASPNETCORE_ENVIRONMENT != Development`; en `Development` queda `SameAsRequest`. Atributos `HttpOnly` y `SameSite = Lax` no se tocan. |
| 2 | **CORS API** | Validar `AllowedOrigins` al construir el host: si el ambiente no es `Development` y la sección está vacía o ausente, lanzar `InvalidOperationException` con mensaje operativo. Reemplazar la rama `AllowAnyOrigin().AllowCredentials()` por una rama explícita solo `Development` (sin credentials o documentar el fallback acotado). |
| 3 | **Forwarded headers** | Documentar en `decisiones-implementacion.md` cómo habilitar `app.UseForwardedHeaders(...)` con `KnownProxies`/`KnownNetworks` cuando se despliega detrás de un reverse proxy, sin activar la lógica en `Development`. |
| 4 | **Documentación** | Agregar matriz ambiente ↔ seguridad en `docs/decisiones-implementacion.md` (cookie attributes, CORS, JWT, HSTS, HTTPS forzado) y replicar el resumen en `AGENTS.md`. |
| 5 | **Tests** | (a) Test de configuración Web con environment forzado a `Production` que verifica `CookieOptions.SecurePolicy == Always`. (b) Test de configuración API con environment `Production` y `AllowedOrigins` vacío que espera fallo al arrancar. (c) Tests existentes deben seguir verdes sin cambios. |

## Specs afectadas

### Nuevas capacidades

- `api-cors-allowed-origins-validation`: cubre el fail-loud en arranque cuando `AllowedOrigins` está vacío fuera de `Development`, y la prohibición de combinar `AllowAnyOrigin()` con `AllowCredentials()`. Ningún spec existente modela este contrato; queda como capacidad transversal nueva de la API.

### Capacidades modificadas

- `sgv-web-authentication`: agregar un Requirement que defina los atributos de la cookie de autenticación por ambiente (`HttpOnly`, `SameSite = Lax`, `SecurePolicy = Always` cuando `ASPNETCORE_ENVIRONMENT != Development`). Hoy el spec cubre el flujo de login/logout y la centralización de rutas, pero no los atributos runtime de la cookie.

### Capabilities sin cambios

- `sgv-web-shell`, `sgv-readonly-api`, `web-apiclient-transport-contract`, `jwt-signing-key-validation`, `sgv-database`: no se modifican. El hardening CORS no toca catálogos read-only ni el contrato cliente→API. La cookie no se cruza con la validación de `Jwt:SigningKey`.

## Enfoque técnico

- **Cookie**: en `src/SGV.Web/Program.cs` introducir un ternario sobre `builder.Environment.IsDevelopment()` para asignar `SecurePolicy`. Sin cambios en el handler `ApiBearerTokenHandler`.
- **CORS**: en `src/SGV.Api/Program.cs` leer `AllowedOrigins` antes de `AddCors` y lanzar temprano si la validación falla. Separar la rama `Development` (fallback acotado, sin credentials o con un único origin local) de la rama no-Development (`WithOrigins(...).AllowCredentials()` con al menos un origin).
- **Configuración**: no tocar `appsettings.Development.json` (ya trae `AllowedOrigins` correctos). Documentar que producción debe inyectar `AllowedOrigins__0`, `AllowedOrigins__1`, ... vía env vars o secret manager.
- **Tests**: usar `WebApplicationFactory<TEntryPoint>` con `UseEnvironment("Production")` y overrides de `IConfiguration` para los casos fail-loud; para el test de cookie, inspeccionar las opciones registradas vía DI o assert contra el resultado del middleware.

## Áreas afectadas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Web/Program.cs` | Modificado | Línea 26: `SecurePolicy` condicional por ambiente. |
| `src/SGV.Api/Program.cs` | Modificado | Líneas 111-125: validación fail-loud + reemplazo del fallback `AllowAnyOrigin`+`AllowCredentials`. |
| `docs/decisiones-implementacion.md` | Modificado | Nueva sección "Hardening runtime: cookie y CORS por ambiente" con la matriz. |
| `AGENTS.md` | Modificado | Resumen de la matriz y enlaces al doc detallado. |
| `tests/SGV.Tests/Web/` | Nuevo | Test cookie `SecurePolicy` en `Production`. |
| `tests/SGV.Tests/Api/` | Nuevo | Test fail-loud API sin `AllowedOrigins` en `Production`. |
| `openspec/specs/sgv-web-authentication/spec.md` | Modificado (delta) | Requirement de atributos de cookie por ambiente. |
| `openspec/specs/api-cors-allowed-origins-validation/spec.md` | Nuevo | Spec transversal CORS API. |

## No-goals

- No se cambia el formato ni la firma del JWT. La validación de `Jwt:SigningKey` se mantiene intacta (ver `jwt-signing-key-validation`).
- No se introduce rate limiting, captcha u otra defensa contra fuerza bruta.
- No se reescribe el handler `ApiBearerTokenHandler`. La fuga se cierra en la cookie, no en el bridge.
- No se modifica la política de autorización del API (fallback policy, `[Authorize(Roles = Administrador)]`); este change es ortogonal a `sgv-readonly-api` y a la hardening de autorización previa.
- No se agregan policies de CORS por ambiente más allá de la dicotomía `Development` / no-`Development` (origen único o lista cerrada). Variantes por staging quedan fuera.
- No se cambian los placeholders de `appsettings.Development.json` ni la clave `DEV-PLACEHOLDER` JWT.

## Riesgos y consideraciones

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| CI ejecuta tests con environment distinto a `Development` y la nueva validación CORS rompe la suite. | Baja | El default de `WebApplicationFactory` y `dotnet test` es `Development`. El test fail-loud usa `UseEnvironment("Production")` explícito, no toca el resto. Verificar primero con `dotnet test SGV.slnx`. |
| Deploy productivo sin configurar `AllowedOrigins` por env var → el pod no arranca. | Media (esperado) | Fail-loud intencional: el operador debe setear `AllowedOrigins__0=...` antes del deploy. Documentar en `decisiones-implementacion.md` y `AGENTS.md`. |
| Browser en `Development` con HTTP plano podría verse afectado si el operador sube `SecurePolicy = Always` también en dev. | Baja | El ternario deja `SameAsRequest` en `Development`. Cobertura con test explícito. |
| Combinación nueva `WithOrigins(...).AllowCredentials()` exige origins exactos: un deploy con `https://app.example.com/` (slash final) rompe. | Baja | Documentar que los origins deben ir sin slash final; agregar nota en el doc. |
| Tests existentes que dependan de `AllowAnyOrigin`+`AllowCredentials` (poco probable: `WebApplicationFactory` testea server-side). | Baja | Los tests del repo no emiten CORS preflight; la API no los cubre. Si aparecen, agregar `AllowedOrigins` en el override de configuración del test. |

## Plan de tareas de alto nivel

1. Implementar el ternario de `CookieSecurePolicy` en `src/SGV.Web/Program.cs`.
2. Implementar la validación fail-loud de `AllowedOrigins` en `src/SGV.Api/Program.cs` y reemplazar la rama `AllowAnyOrigin()`+`AllowCredentials()` por una rama explícita `Development`-only.
3. Documentar la matriz ambiente ↔ seguridad en `docs/decisiones-implementacion.md` y replicar resumen en `AGENTS.md`, incluyendo el setup de `UseForwardedHeaders` detrás de proxy.
4. Crear la nueva spec `openspec/specs/api-cors-allowed-origins-validation/spec.md` con el contrato fail-loud y la prohibición `AllowAnyOrigin`+`AllowCredentials`.
5. Crear delta spec sobre `openspec/specs/sgv-web-authentication/spec.md` con los atributos de cookie por ambiente.
6. Agregar test Web que verifica `CookieOptions.SecurePolicy == Always` con `UseEnvironment("Production")` en `tests/SGV.Tests/Web/`.
7. Agregar test API que verifica `InvalidOperationException` al arrancar con `UseEnvironment("Production")` y `AllowedOrigins` vacío en `tests/SGV.Tests/Api/`.
8. Correr `dotnet build SGV.slnx` y `dotnet test SGV.slnx --no-build --configuration Release`. La suite completa debe quedar verde; ningún test existente debería romperse.

## Plan de rollback

- Revertir el commit del PR restaura los valores previos de `Program.cs` (cookie `SameAsRequest`, CORS con `AllowAnyOrigin().AllowCredentials()`). No hay migraciones ni cambios de esquema, así que el rollback es atómico.
- Si el problema se detecta post-merge y el host productivo ya no arranca, el operador debe inyectar `AllowedOrigins__0` antes del restart o revertir la imagen. El fail-loud es la red de seguridad: no queda un modo silenciosamente inseguro en producción.

## Dependencias

- No se introducen paquetes NuGet nuevos.
- No se requieren migraciones de EF Core.
- No hay dependencias con otros cambios en curso. Verificar contra el listado de `openspec/changes/` para evitar solapamientos.

## Criterios de éxito

- [ ] `SGV.Web` aplica `CookieSecurePolicy.Always` cuando `ASPNETCORE_ENVIRONMENT != Development` y mantiene `SameAsRequest` en `Development`. Cubierto por test.
- [ ] `SGV.Api` lanza `InvalidOperationException` con mensaje operativo al construir el host con `AllowedOrigins` vacío y `ASPNETCORE_ENVIRONMENT` distinto de `Development`. Cubierto por test.
- [ ] `SGV.Api` no contiene ningún path que combine `AllowAnyOrigin()` con `AllowCredentials()` en el código vigente. Verificable con `grep -R "AllowAnyOrigin" src/`.
- [ ] `docs/decisiones-implementacion.md` contiene la matriz ambiente ↔ seguridad y la guía de `UseForwardedHeaders`. `AGENTS.md` referencia el doc.
- [ ] Spec nueva `api-cors-allowed-origins-validation/spec.md` y delta en `sgv-web-authentication/spec.md` commiteadas y archivadas.
- [ ] `dotnet test SGV.slnx --no-build --configuration Release` corre verde, incluidos los tests nuevos.
