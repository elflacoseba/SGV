# H-02-06 — Configurar `AllowedOrigins` para producción

En `Development` la API acepta cualquier origen (fallback permisivo); en cualquier otro ambiente, el host **falla loud** si `AllowedOrigins` está vacío. Este how-to explica cómo salir del fallback dev y declarar el contrato estricto para staging/producción.

---

## Prerrequisitos

- Acceso al sistema de configuración del entorno destino (Kubernetes Secrets / ConfigMap, AWS Parameter Store, variables en CI, etc.).
- Lista de orígenes exactos que consumirán la API (no subdominios wildcard).

---

## Paso 1 — Entender el fail-loud

La política CORS se arma en `src/SGV.Api/Program.cs` (líneas 386-416). El callback `AddDefaultPolicy` corre en el host start, antes del primer request:

```csharp
if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "SGV.Api: la sección de configuración 'AllowedOrigins' es obligatoria " +
            "fuera del ambiente Development. Configure AllowedOrigins__0, " +
            "AllowedOrigins__1, ... vía variables de entorno.");
    }
    // ...
}
```

**Verificación:** con la sección vacía en `Production`, el arranque aborta con `InvalidOperationException`. Los logs de orquestador mostrarán el mensaje completo.

---

## Paso 2 — Declarar los orígenes por variable de entorno

ASP.NET Core mapea `__` a `:` para binding de arrays. Cada índice es una entrada:

```bash
# Staging
export AllowedOrigins__0="https://staging.sgv.example.com"
export AllowedOrigins__1="https://admin-staging.sgv.example.com"

# Producción
export AllowedOrigins__0="https://sgv.example.com"
export AllowedOrigins__1="https://admin.sgv.example.com"
```

Alternativa en `appsettings.Production.json` (commiteable si no contiene secretos):

```json
{
  "AllowedOrigins": [
    "https://sgv.example.com",
    "https://admin.sgv.example.com"
  ]
}
```

> ⚠️ A verificar: NO uses `*` ni `https://*.example.com` — el wildcard no es compatible con `AllowCredentials()` (el navegador rechaza la combinación). El callback usa `policy.WithOrigins(allowedOrigins).AllowCredentials()` cuando la lista está poblada.

---

## Paso 3 — Reiniciar la API

El cambio se observa en el siguiente arranque gracias a que el callback se ejecuta dentro del `AddDefaultPolicy` (lee `IConfiguration` post-Build).

**Verificación:** el log de arranque NO emite el `InvalidOperationException`. El endpoint `GET /health/live` sigue respondiendo 200. Una request cross-origin desde `https://sgv.example.com` hacia `https://api.sgv.example.com/api/v1/...` debe traer `Access-Control-Allow-Origin: https://sgv.example.com` y `Access-Control-Allow-Credentials: true`.

---

## Paso 4 — Validar el handshake desde el shell web

Abrí DevTools del navegador en la Web de staging/producción y filtrá por `Fetch/XHR`. Un login fresco dispara `POST /api/v1/auth/login`. La respuesta debe incluir los headers CORS esperados:

| Header | Valor esperado |
|--------|----------------|
| `Access-Control-Allow-Origin` | origen exacto del request (no `*`) |
| `Access-Control-Allow-Credentials` | `true` |
| `Vary` | `Origin` |

**Verificación:** ningún error rojo en la consola del navegador del estilo `CORS policy: No 'Access-Control-Allow-Origin' header is present`. La cookie `sgv.rt` persiste después del login.

---

## Paso 5 — Manejar múltiples ambientes con overrides

Si manejás staging y producción desde el mismo repositorio de infra, distinguí por `ASPNETCORE_ENVIRONMENT`:

```bash
export ASPNETCORE_ENVIRONMENT=Staging
export AllowedOrigins__0="https://staging.sgv.example.com"
```

`appsettings.Staging.json` puede llevar los `AllowedOrigins` de staging commiteados, y los de producción quedan sólo en el secret manager del cluster.

**Verificación:** cada ambiente rechaza orígenes del otro. Un request desde `https://sgv.example.com` contra el ambiente `Staging` (que sólo conoce `https://staging.sgv.example.com`) NO recibe `Access-Control-Allow-Origin` y la request falla en el navegador.

---

## Troubleshooting

- **El host arranca pero todos los requests fallan con error CORS en el navegador**: la Web apunta a un origen que NO está en `AllowedOrigins`. Listá los orígenes con `kubectl exec` + `env | grep AllowedOrigins` o equivalente en tu plataforma.
- **Falla con `The value of the 'Access-Control-Allow-Origin' header in the response must not be the wildcard '*' when the request's credentials mode is 'Include'`**: la lista está poblada con `*` o vacío y `AllowCredentials()` está activo. Cambiá a orígenes explícitos.
- **El orquestador reinicia la API en bucle por `InvalidOperationException`**: falta declarar `AllowedOrigins` para el ambiente actual. Agregá la env var antes del próximo deploy.
- **CORS falla sólo en Safari / mobile**: Safari es más estricto con cookies `SameSite=None; Secure`. La cookie `sgv.rt` requiere HTTPS en producción (configurada en `SGV.Web/Program.cs`).

---

## Referencias

- `src/SGV.Api/Program.cs` (líneas 386-416) — `AddCors` + `InvalidOperationException`.
- `src/SGV.Api/appsettings.Development.json` — `AllowedOrigins` dev con dos entradas localhost.
- `../tutorials/01-levantar-sistema-local.md` — CORS permisivo en Development.
- [R-03-05](../reference/05-configuracion-opciones-secretos.md) —
  Referencia de la sección `AllowedOrigins` y demás opciones de CORS.
- [R-03-06](../reference/06-pipeline-middleware-api.md) — Pipeline
  middleware de la API, donde se aplica `UseCors()`.
