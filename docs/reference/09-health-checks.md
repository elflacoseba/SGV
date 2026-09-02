# R-03-09 — Health checks

Referencia de los endpoints de liveness/readiness expuestos por `SGV.Api` y `SGV.Web`. Ambos se sirven con `HealthCheckResponseWriter` (`src/SGV.Api/Infrastructure/Health/HealthCheckResponseWriter.cs`, linkeado por `<Compile Include>` en `src/SGV.Web/SGV.Web.csproj`).

## Endpoints

### API — `SGV.Api`

| Endpoint | Predicate | Check ejecutado | Authz |
| --- | --- | --- | --- |
| `GET /health/live` | `_ => false` | ninguno (siempre 200) | `AllowAnonymous` |
| `GET /health/ready` | `check => check.Tags.Contains("ready")` | `SgvDbContextReadinessHealthCheck("mysql", tags: ["ready"])` | `AllowAnonymous` |

### Web — `SGV.Web`

| Endpoint | Predicate | Check ejecutado | Authz |
| --- | --- | --- | --- |
| `GET /health/live` | `_ => false` | ninguno (siempre 200) | `AllowAnonymous` |
| `GET /health/ready` | `check => check.Tags.Contains("ready")` | `SgvApiUpstreamHealthCheck("sgv-api-upstream", tags: ["ready"])` | `AllowAnonymous` |

## Semántica

| Endpoint | Significado | Falla típica |
| --- | --- | --- |
| `/health/live` | El proceso está vivo y la pipeline responde | Sin chequeo de dependencias — siempre 200 si el host arrancó |
| `/health/ready` (API) | MySQL alcanzable (SELECT 1) | `ConnectionString` inválida, MySQL down, timeout de handshake |
| `/health/ready` (Web) | La API upstream responde `/health/live` | API caída, timeout 3 s del `SgvApiHealthProbeHttpClient` |

## Formato de respuesta

`HealthCheckResponseWriter.WriteJson` produce un JSON estable con la forma:

```json
{
  "status": "Healthy",
  "results": {
    "mysql": {
      "status": "Healthy",
      "description": "...",
      "data": { ... },
      "duration": "00:00:00.0123456"
    }
  },
  "totalDuration": "00:00:00.0123456"
}
```

`status` agregado puede ser `Healthy`, `Degraded` o `Unhealthy`. Cuando un check devuelve `Unhealthy`, el endpoint responde con HTTP 503 (default de `MapHealthChecks`); cuando es `Healthy`/`Degraded`, responde 200.

> ⚠️ A verificar: el `WriteJson` vive en `src/SGV.Api/Infrastructure/Health/HealthCheckResponseWriter.cs` y se linkea desde `SGV.Web.csproj` vía `<Compile Include>`. Si la ruta del archivo cambia, el `<Compile Include>` debe actualizarse en consecuencia. Verificar contra `src/SGV.Web/SGV.Web.csproj` antes de mover el archivo.

## Checks concretos

### `SgvDbContextReadinessHealthCheck`

Implementa `IHealthCheck`. Registrado con tag `ready`. Su implementación:

1. Abre un `MySqlConnection` raw usando `ConnectionStrings:SgvDatabase`.
2. Ejecuta `SELECT 1`.
3. Devuelve `Healthy` si la conexión y la query tuvieron éxito.
4. Devuelve `Unhealthy` con la excepción capturada si algo falló.

> El check NO usa `SgvDbContext` ni `ServerVersion.AutoDetect` para evitar la carga de reflexión del modelo EF y el handshake bloqueante de la inicialización.

### `SgvApiUpstreamHealthCheck`

Implementa `IHealthCheck`. Registrado con tag `ready`. Su implementación:

1. Resuelve `SgvApiHealthProbeHttpClient` (nominal; `BaseAddress=SgvApi:BaseUrl`, `Timeout=3s`).
2. Hace `GET {BaseAddress}/health/live`.
3. Devuelve `Healthy` si la respuesta HTTP es 2xx.
4. Devuelve `Unhealthy` con la causa raíz si la respuesta fue no-2xx o si hubo `HttpRequestException`/`TaskCanceledException`.

> El Web no consulta MySQL directamente: la cadena de dependencias termina en el `/health/ready` de la API. Si la API está caída, el reporte `ready` del Web también cae.

## Diagnóstico de jerarquía al arranque

Independiente de los health checks, `Program.cs` de la API registra `IDiagnosticoJerarquiaService.DiagnosticarAsync()` en `ApplicationStarted`. Si detecta ciclos en `UnidadesOrganizativas`, emite logs `WARNING` con los ids participantes pero **no** falla el arranque. La corrección se hace con el script `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` (deshabilitando los triggers, reparando, rehabilitando).

## Diagnóstico de health runtime

Para inspeccionar el estado de los checks:

```bash
curl -s http://localhost:5000/health/ready | jq .
curl -s http://localhost:5000/health/live | jq .
```

En el Web shell, los mismos paths detrás del puerto del host (típicamente 5000/5001 en Development, detrás del proxy reverso en producción).

## Consideraciones operativas

- El response writer fija `Content-Type: application/json; charset=utf-8` y nunca emite HTML.
- El timeout de `SgvApiHealthProbeHttpClient` (3 s) es estricto: una API lenta degrada `ready` pero no afecta `live`.
- El `OnRejected` del rate limiter agrega `Retry-After` en 429 pero no afecta health checks.

## Referencias

- How-to: [Diagnosticar ciclos jerárquicos](../how-to/01-diagnosticar-ciclos-jerarquia.md)
- How-to: [Levantar MySQL Docker para tests](../how-to/07-levantar-mysql-docker-para-tests.md)
- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- R-03-06 — Pipeline middleware API (orden del pipeline donde se montan los health checks)
- R-03-07 — Pipeline arranque Web (orden del pipeline donde se montan los health checks)
