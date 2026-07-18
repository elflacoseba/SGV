# Diseño: Hardening BFF same-origin — `GET /api/v1/personas/consulta`

## Enfoque

Validación local en el handler minimal-API de `SGV.Web` (`Program.cs:212`) antes de invocar `IPersonaApiClient`. Tres chequeos con `400 + ProblemDetails` vía `Results.Problem(...)`, en orden: **longitud de `search` → whitelist de `sort` → whitelist de `segmento`**. Defaults back-compat (`apellidos_asc` + `Activas`) se preservan cuando faltan. No se tocan `PersonaListQuery`, `ApplySort`, `FakePersonaApiClient` ni se introduce `PersonaSort`: el cambio vive 100 % en el BFF.

## Decisiones de arquitectura

| Decisión | Alternativas descartadas | Motivo |
|---|---|---|
| `HashSet<string>` con `OrdinalIgnoreCase` | `List<string>` + `ToLowerInvariant()` por request | O(1) sin asignar string por request. |
| Constantes locales al handler | Archivo `PersonaConsultaWhitelist` separado | BFF cabe en `Program.cs`; extraer cuando haya otro handler. |
| `search` antes que `sort`/`segmento` | Orden alfabético | `search` es el riesgo DoS (RIS-001); cortar primero, O(1). |
| NO introducir `enum PersonaSort` | Enum + `JsonStringEnumConverter` | `Sort` ya es `string?`; el enum duplicaría la fuente de verdad con `ApplySort`. |
| `Results.Problem(...)` directo | Helper Web o `ApiResults.ToProblemResult` | `ApiResults` vive en `SGV.Api` (no referenciado por Web); `Results.Problem` produce `ProblemDetails` + `application/problem+json` sin `AddProblemDetails()`. |

## Flujo

```
Browser → SGV.Web (cookie auth)
        → MapGet("/api/v1/personas/consulta")
            ├─ [1] search.Length > 200          → 400 ProblemDetails (no toca cliente)
            ├─ [2] sort ∉ whitelist (8 tokens)  → 400 ProblemDetails (no toca cliente)
            ├─ [3] segmento ∉ {activas,eliminadas} → 400 ProblemDetails (no toca cliente)
            └─ [4] PersonaListQuery → IPersonaApiClient.QueryAsync → 200 OK
```

## Cambios por archivo

### `src/SGV.Web/Program.cs:212-229`

```csharp
const int SearchMaxLength = 200;
static readonly HashSet<string> AllowedSorts = new(StringComparer.OrdinalIgnoreCase)
{
    "apellidos_asc", "apellidos_desc",
    "nombres_asc", "nombres_desc",
    "legajo_asc", "legajo_desc",
    "email_asc", "email_desc",
};
static readonly HashSet<string> AllowedSegmentos = new(StringComparer.OrdinalIgnoreCase)
{
    "activas", "eliminadas",
};

app.MapGet("/api/v1/personas/consulta", async (
    int p,
    int pageSize,
    string? search,
    string? sort,
    string? segmento,
    bool? soloSinUsuario,
    IPersonaApiClient personaApiClient,
    CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrEmpty(search) && search.Length > SearchMaxLength)
    {
        return Results.Problem(
            title: "Parámetro 'search' fuera de rango",
            detail: $"El parámetro 'search' excede el límite de {SearchMaxLength} caracteres.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    string resolvedSort = string.IsNullOrWhiteSpace(sort) ? "apellidos_asc" : sort.Trim();
    if (!AllowedSorts.Contains(resolvedSort))
    {
        return Results.Problem(
            title: "Parámetro 'sort' inválido",
            detail: $"El parámetro 'sort' debe ser uno de: {string.Join(", ", AllowedSorts.OrderBy(s => s))}.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    PersonaSegmentoListado resolvedSegmento = PersonaSegmentoListado.Activas;
    if (!string.IsNullOrWhiteSpace(segmento))
    {
        if (segmento.Equals("eliminadas", StringComparison.OrdinalIgnoreCase))
            resolvedSegmento = PersonaSegmentoListado.Eliminadas;
        else if (!segmento.Equals("activas", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                title: "Parámetro 'segmento' inválido",
                detail: "El parámetro 'segmento' debe ser 'activas' o 'eliminadas'.",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    var query = new PersonaListQuery(
        Page: Math.Max(1, p),
        PageSize: Math.Clamp(pageSize, 1, 100),
        Search: search,
        Sort: resolvedSort,
        Segmento: resolvedSegmento,
        SoloSinUsuario: soloSinUsuario);
    var result = await personaApiClient.QueryAsync(query, cancellationToken);
    return Results.Ok(result);
}).RequireAuthorization();
```

Comentario obligatorio sobre la whitelist apuntando a `PersonaRepository.ApplySort` (líneas 218-232) para que cualquier cambio en una fuerce la sincronización con la otra.

### `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs`

Strict TDD: RED → GREEN. Patrón `_fixture.CreateUsuarioLeaseAsync(new FakeUsuarioApiClient(), new FakePersonaApiClient(), adminRole: true)` ya presente.

| # | Test | Aserciones clave |
|---|---|---|
| 1 | `BFF_BuscarConSearchDe200Caracteres_ReenviaAlClienteTipado` | 200; `QueryCalls.Count == 1`; `query.Search.Length == 200`. |
| 2 | `BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente` | 400; `QueryCalls.Count == 0`; `ProblemDetails.Detail` contiene `"200"`. |
| 3 | `BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado` | 200; `query.Sort == "email_desc"`. |
| 4 | `BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente` | 400; `QueryCalls.Count == 0`; `Detail` lista los 8 tokens. |
| 5 | `BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente` | 400; `QueryCalls.Count == 0`. |
| 6 | `BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado` | 200; `query.Segmento == Eliminadas`. |
| 7 | `BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente` | 400; `QueryCalls.Count == 0`; `Detail` nombra `activas|eliminadas`. |
| 8 | `BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat` | 200; `query.Sort == "apellidos_asc"`; `query.Segmento == Activas`. |

El test #4 ya existente cubre implícitamente los defaults; el #8 los vuelve explícitos para anclar back-compat ante refactors.

## Trade-offs

- **Cap 200**: cubre búsqueda razonable; 256/500 amplificarían DoS sin valor funcional.
- **Whitelist cerrada vs fallback silencioso**: hoy `ApplySort` cae a `apellidos_asc`; cerrar en BFF **falla ruidosamente** ante valores no implementados.
- **Validación en BFF vs backend**: cortar antes de salir del proceso reduce latencia, logs y superficie de ataque contra `SGV.Api`.

## Riesgos y mitigaciones

- **Desalineación `AllowedSorts` ↔ `ApplySort`**: si el backend suma/quita tokens, la whitelist queda stale. **Mitigación**: tests #3-#5 cubren los 8 tokens + un inválido; comentario en `Program.cs` referenciando `ApplySort:218-232` para forzar sincronización en el mismo PR.
- **`documento_*` fuera**: trade-off del proposal; el 400 ruidoso es preferible al fallback silencioso.
- **Flaky tests web**: setup ya existente, se reusa literal; sin variabilidad nueva.

## Interfaces / Contratos

Sin cambios. `PersonaListQuery` sigue `(Page, PageSize, Search, Sort, Segmento, SoloSinUsuario)`; `PersonaSegmentoListado` mantiene `Activas=0, Eliminadas=1`.

## Migración / Rollout

Sin migración, feature flag ni cambio de config. Revertir el commit restaura el handler previo; los defaults preservan back-compat del modal y de cualquier consumidor que mande `p`, `pageSize`, `search`, `soloSinUsuario` sin `sort`/`segmento`.

## Verificación

- `dotnet build SGV.slnx` — 0 errores, 0 warnings nuevos.
- `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal"` — 8 nuevos verdes + 4 previos.
- `dotnet test SGV.slnx` — sin regresiones.
- `bun run build` desde `src/SGV.Web` — bundle OK.

## Fuera de alcance

Extender `ApplySort` con `documento_*` u otros tokens; modificar `FakePersonaApiClient`, `PersonaRepository`, `PersonaListQuery` o migraciones; introducir `enum PersonaSort`; mover el handler fuera de `Program.cs`; cambiar el cap de 200 o sustituir `PersonaSegmentoListado`. Sin preguntas abiertas: whitelist cerrada y orden de checks fijados por el orchestrator y la spec.
