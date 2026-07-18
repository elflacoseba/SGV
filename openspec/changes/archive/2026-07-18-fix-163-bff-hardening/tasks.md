# Tasks: Hardening BFF same-origin — `GET /api/v1/personas/consulta`

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~130-200 (50-80 handler + 80-120 tests) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR (issue #163 ya lo declara) |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Hardening completo RED→GREEN→VERIFY | PR único | `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal"` | N/A — los tests son integrados, corren sobre WebApplicationFactory existente. Sin base de datos real ni setup externo. | Revertir el commit único restaura el handler previo sin efectos colaterales. |

## Phase 1: RED — 8 tests en `PersonaBuscadorModalTests.cs`

Agregar al archivo `tests/SGV.Tests/Web/Usuario/PersonaBuscadorModalTests.cs`. Patrón `_fixture.CreateUsuarioLeaseAsync(new FakeUsuarioApiClient(), new FakePersonaApiClient(), adminRole: true)`. Las aserciones usan `personaApiClient.QueryCalls` y `HttpStatusCode` directo.

- [ ] 1.1 `BFF_BuscarConSearchDe200Caracteres_ReenviaAlClienteTipado` — 200 OK; `QueryCalls.Count == 1`; `query.Search.Length == 200`
- [ ] 1.2 `BFF_BuscarConSearchDe201Caracteres_Responde400YNoLlamaCliente` — 400; `QueryCalls.Count == 0`; `ProblemDetails.Detail` contiene `"200"`
- [ ] 1.3 `BFF_BuscarConSortEmailDesc_PropagaAlClienteTipado` — 200 OK; `query.Sort == "email_desc"`
- [ ] 1.4 `BFF_BuscarConSortDocumentoAsc_Responde400YNoLlamaCliente` — 400; `QueryCalls.Count == 0`; `Detail` lista los 8 tokens de la whitelist
- [ ] 1.5 `BFF_BuscarConSortInvalido_Responde400YNoLlamaCliente` — 400; `QueryCalls.Count == 0`
- [ ] 1.6 `BFF_BuscarConSegmentoEliminadas_PropagaAlClienteTipado` — 200 OK; `query.Segmento == PersonaSegmentoListado.Eliminadas`
- [ ] 1.7 `BFF_BuscarConSegmentoInvalido_Responde400YNoLlamaCliente` — 400; `QueryCalls.Count == 0`; `Detail` menciona `activas|eliminadas`
- [ ] 1.8 `BFF_BuscarSinSortNiSegmento_AplicaDefaultsBackCompat` — 200 OK; `query.Sort == "apellidos_asc"`; `query.Segmento == PersonaSegmentoListado.Activas`

**Verificación RED**: `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal"` falla en los 8 nuevos.

## Phase 2: GREEN — implementar validaciones en `Program.cs:212-229`

Modificar el handler `app.MapGet("/api/v1/personas/consulta", ...)` en `src/SGV.Web/Program.cs:212-229`.

- [ ] 2.1 Agregar constantes `SearchMaxLength = 200`, `AllowedSorts` (`HashSet<string>` con `OrdinalIgnoreCase`, 8 tokens), `AllowedSegmentos` (`HashSet<string>` con `OrdinalIgnoreCase`, `activas|eliminadas`) antes del handler
- [ ] 2.2 Agregar parámetros `string? sort` y `string? segmento` a la firma del handler
- [ ] 2.3 Cap de search: si `search.Length > 200` → `Results.Problem(400)` sin invocar `personaApiClient`
- [ ] 2.4 Whitelist de sort: `resolvedSort = sort ?? "apellidos_asc"`; si ∉ `AllowedSorts` → `Results.Problem(400)` listando los 8 tokens
- [ ] 2.5 Whitelist de segmento: default `Activas`; si `segmento = "eliminadas"` → `Eliminadas`; si otro valor → `Results.Problem(400)`
- [ ] 2.6 Construir `PersonaListQuery` con `resolvedSort` y `resolvedSegmento`; invocar `QueryAsync`
- [ ] 2.7 Agregar comentario `// Mantener sincronizado con PersonaRepository.ApplySort:218-232` junto a `AllowedSorts`

**Verificación GREEN**: `dotnet test --filter "FullyQualifiedName~PersonaBuscadorModal"` pasa los 12 tests (8 nuevos + 4 previos).

## Phase 3: Verificación final

- [ ] 3.1 `dotnet build SGV.slnx` — 0 errores, 0 warnings nuevos
- [ ] 3.2 `dotnet test SGV.slnx` — sin regresiones
- [ ] 3.3 `bun run build` desde `src/SGV.Web` — bundle OK
- [ ] 3.4 Commit RED: `test(web): add BFF hardening tests for search/sort/segmento`
- [ ] 3.5 Commit GREEN: `feat(web): harden BFF /api/v1/personas/consulta with caps and whitelists`
- [ ] 3.6 Verificar que ningún commit contiene `Co-Authored-By` ni atribución IA
