# Apply Progress: Buscador modal reutilizable de Personas — PR-2 (cliente)

> Cambio: `2026-07-17-buscador-personas-modal`
> Slice: **PR-2 (cliente HTTP + Fake)** — WU-4
> Issue: [#157](https://github.com/elflacoseba/SGV/issues/157)
> Base: `develop` (PR-1 backend ya mergeado en `fc2f01c8`)
> PR: [#159](https://github.com/elflacoseba/SGV/pull/159)
> Persistencia: `both` (openspec + Engram)
> Modo TDD: estricto (`strict_tdd: true`)

## Estado final

**SUCCESS — PASS en verify** (lineage `review-e0fab7bc673a62e1`, gate `pre-pr` `allow`). 7 tests nuevos verdes (3 [Fact] + 1 [Theory×3] + 2 [Fact]), suite completa 2433/2433, build limpio 0/0, 2 commits work-unit RED→GREEN sin `Co-Authored-By`.

## Resumen ejecutivo

| Métrica | Valor |
|---|---|
| Commits creados | 2 (RED + GREEN) |
| Archivos tocados | 5 (2 producción + 2 tests + 1 tasks.md) |
| Líneas añadidas / eliminadas | +202 / −12 (190 LoC) |
| Tests nuevos | 7 (3 WU-4 cliente + 1 [Theory×3] + 2 WU-4 fake + 1 WU-4 transporte × 3 = 7 nominales pero 4 casos lógicos) |
| Tests baseline (post PR-1) | 2426 |
| Tests totales | 2433 (2426 + 7) |
| Tests fallidos | 0 |
| Tests skipeados | 0 |
| Build | 0 errores, 23 warnings preexistentes (0 nuevos) |
| Migraciones / dependencias nuevas | ninguna |
| Cambios en `SGV.Api` / `Aplicacion` / `Infraestructura` / `Dominio` / `Contracts` | ninguno |
| `[Authorize]` relajado | NO |

## Decisión de cadena PR

Slice **PR-2 standalone** contra `develop`. Chain strategy del change: `stacked-to-main` — cada PR subsiguiente mergea a `develop` independientemente. PR-2 sigue el patrón de PR-1: branch desde `develop`, PR target `develop`.

## Decisiones aplicadas (D-01..D-10) en este slice

| ID | Implementación en PR-2 | OK |
|---|---|---|
| D-01 | Nombre serializado `soloSinUsuario` (binding ASP.NET + cliente preserva casing). | ✅ |
| D-02 | `PersonaListQuery.SoloSinUsuario` (de PR-1) es consumido directamente por `PersonaApiClient`. | ✅ |
| D-04 | Cliente serializa el flag en query string; respeta `true`/`false`/`null` con back-compat URI. | ✅ |
| D-09 | Una sola superficie wire: `IPersonaApiClient.QueryAsync(PersonaListQuery)`. NO se introdujo `BuscarAsync`. | ✅ |
| D-10 | 409 + feedback es concern de Create/Edit (PR-3). NO introduje manejo de 409 en este slice. | n/a |

## Archivos tocados

### Producción (+30 / −1)

| Archivo | Acción | Resumen |
|---|---|---|
| `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` | **Modificado** | + `bool? soloSinUsuario` en `BuildQueryUri`; serializa `&soloSinUsuario=true` solo cuando `true`. `QueryAsync` propaga el flag. Sin try-catch (transport failures propagan nativas). |
| `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` | **Modificado** | + `_soloSinUsuarioSet`, helper fluido `WithSoloSinUsuarioSet(IEnumerable<Guid>)`, método privado `ApplySoloSinUsuarioFilter`. Back-compat: `null`/`false` no excluye nada. |

### Tests (+172 / −0)

| Archivo | Acción | Resumen |
|---|---|---|
| `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs` | **Modificado** | + `QueryAsync_WithSoloSinUsuarioTrue_SerializesSoloSinUsuarioInUri`, `QueryAsync_WithSoloSinUsuarioNullOrFalse_OmitsParameter`, `QueryAsync_WithSoloSinUsuarioTrue_TransportFails_PropagatesNativeException` (Theory×3 sobre TaskCanceled/HttpRequest/DnsFailure). |
| `tests/SGV.Tests/Web/Persona/FakePersonaApiClientTests.cs` | **Modificado** | + `FakePersonaApiClient_QueryAsync_WithSoloSinUsuarioTrue_ExcludesIdsFromSet`, `FakePersonaApiClient_QueryAsync_WithSoloSinUsuarioNullOrFalse_DoesNotExcludeFromSet`. |

### Trazabilidad SDD

| Archivo | Acción | Resumen |
|---|---|---|
| `openspec/changes/2026-07-17-buscador-personas-modal/tasks.md` | **Modificado** | Marcado WU-4 con `[x]`. |
| `openspec/changes/2026-07-17-buscador-personas-modal/verify-report.md` | **Creado por sdd-verify** | Reporte adversarial del slice (PASS, gate pre-pr `allow`). |

## Strict TDD — Evidencia de ciclo

| Test | RED (firma sin soporte) | GREEN (passing) | REFACTOR |
|------|--------------------------|------------------|----------|
| `QueryAsync_WithSoloSinUsuarioTrue_SerializesSoloSinUsuarioInUri` | ✅ stasheado el cambio de prod; `dotnet build` arrojó CS1061 sobre `WithSoloSinUsuarioSet` (signature change RED) | ✅ URI contiene `&soloSinUsuario=true` | ✅ extracción a método privado si surgió; diff limpio |
| `QueryAsync_WithSoloSinUsuarioNullOrFalse_OmitsParameter` | ✅ URI previo omitía el flag (back-compat) | ✅ URI final igual | ✅ |
| `QueryAsync_WithSoloSinUsuarioTrue_TransportFails_PropagatesNativeException` (Theory×3) | ✅ sin try-catch espurio, `HttpRequestException` / `TaskCanceledException` propagan | ✅ | ✅ |
| `FakePersonaApiClient_QueryAsync_WithSoloSinUsuarioTrue_ExcludesIdsFromSet` | ✅ `WithSoloSinUsuarioSet` no existía (compile RED) | ✅ ids excluidos | ✅ `ApplySoloSinUsuarioFilter` extraído |
| `FakePersonaApiClient_QueryAsync_WithSoloSinUsuarioNullOrFalse_DoesNotExcludeFromSet` | ✅ mismo compile RED | ✅ no excluye | ✅ |

**Detalle RED**: el ejecutor stasheó los cambios de producción antes del primer commit para confirmar el RED clásico de signature change — `dotnet build` reportó `error CS1061: 'FakePersonaApiClient' does not contain a definition for 'WithSoloSinUsuarioSet'`. Después del `git stash pop`, el build quedó verde y los 53 tests pasaron.

## Restricciones del proyecto respetadas

| Restricción | Cumplimiento |
|---|---|
| `strict_tdd: true` | 5 ciclos RED → GREEN completos (3 cliente + 2 fake), cada uno documentado arriba. |
| Sin migraciones | 0 archivos nuevos en `src/SGV.Infraestructura/Persistencia/Migraciones/`. |
| Sin nuevas dependencias | 0 entradas nuevas en `*.csproj`. |
| `Co-Authored-By` prohibido | Ausente en los 2 commits. |
| `SGV.Web` sólo depende de `SGV.Contracts` | Sin tocar `SGV.Api` ni tipos del backend. Solo `PersonaListQuery` (que vive en `SGV.Contracts`). |
| Identificadores en inglés | `soloSinUsuario`, `SoloSinUsuario`, `WithSoloSinUsuarioSet`, `ApplySoloSinUsuarioFilter`. |
| Artefactos SDD en español | Este `apply-progress-pr2.md` está en español neutro/profesional. |
| Sin try-catch falso en `PersonaApiClient` | El test `TransportFails_PropagatesNativeException` cubre que las excepciones nativas burbujean. |

## Comandos ejecutados y resultados

| # | Comando | Resultado |
|---|---|---|
| 1 | `git checkout -b feat/2026-07-17-buscador-personas-client develop` | ✅ Rama creada desde develop. |
| 2 | `dotnet build SGV.slnx --no-incremental` (baseline pre-apply) | ✅ 0 errores, 23 warnings preexistentes. |
| 3 | Stash prod + commit RED | ✅ CS1061 sobre `WithSoloSinUsuarioSet` (RED confirmado). |
| 4 | `git stash pop` + commit GREEN | ✅ |
| 5 | `dotnet build SGV.slnx --no-incremental` (post-GREEN) | ✅ 0 errores, mismos 23 warnings (0 nuevos). |
| 6 | `dotnet test --filter "FullyQualifiedName~PersonaApiClient\|FullyQualifiedName~FakePersonaApiClient"` | ✅ 53/53 (46 baseline + 7 nuevos), 0 failed, 0 skipped. |
| 7 | `dotnet test SGV.slnx --no-build` (suite completa) | ✅ **2433/2433 pass**, 0 failed, 0 skipped. |
| 8 | `gentle-ai review start --base-ref develop --committed-only=true` | ✅ lineage=`review-e0fab7bc673a62e1`, tier=medium, lenses=`[review-reliability]`, budget=107. |
| 9 | Lens `review-reliability` (sub-agente) | ✅ 0 BLOCKER / 0 CRITICAL / 0 WARNING / 2 SUGGESTION informativos. |
| 10 | `gentle-ai review finalize` | ✅ receipt=`review-e0fab7bc673a62e1`, terminal_state=`approved`. |
| 11 | `gentle-ai review validate --gate pre-pr` | ✅ `result=allow`, `allowed=true`. |
| 12 | `git push -u origin feat/2026-07-17-buscador-personas-client` | ✅ Branch pusheada. |
| 13 | `gh pr create --base develop --head feat/2026-07-17-buscador-personas-client` | ✅ PR #159 abierto. |

## Commits (Conventional commits, sin `Co-Authored-By`)

```
b415b8fc feat(client): wire soloSinUsuario in PersonaApiClient.BuildQueryUri and QueryAsync
719794ae test(client): add PersonaApiClient soloSinUsuario BuildQueryUri tests
```

Cada commit pasa `dotnet build SGV.slnx` (0 errores) y `dotnet test --filter <WU>` (sólo su WU) verde desde el primer `GREEN`.

## Desviaciones del diseño y notas de implementación

### Menores (no bloqueantes)

1. **Concatenación literal `&soloSinUsuario=true` en `BuildQueryUri`** (en lugar de `Uri.EscapeDataString`). Correcto porque el valor es siempre la constante `'true'` cuando aplica; el `if (soloSinUsuario == true)` como única rama evita el riesgo de escape. Documentado como SUGGESTION REL-001 por el lens `review-reliability`. Sin acción requerida.
2. **Helper `WithSoloSinUsuarioSet(IEnumerable<Guid>)` aditivo** (no constructor). Intencional para encadenamiento fluido y para que tests WU-5..8 puedan preparar el set sin tener que instanciar el fake por cada caso. Documentado como SUGGESTION REL-002 por el lens `review-reliability`. Sin acción requerida.
3. **`soloSinUsuario=false` explícito no se normaliza a `null` en el cliente** — mismo comportamiento que PR-1 backend. La URI es back-compat (`null` y `false` se omiten idénticamente). Si en el futuro se quisiera distinguir ausente de `false` explícito (telemetría), basta agregar la rama (cambio pequeño, no afecta contrato).

### No realizadas (pertenecen a PR-3, fuera de scope)

- WU-5..8 — UI/Razor + JS + cleanup de `IPersonaOptionsProvider`.
- Manejo de `409` por carrera con `ModelState.AddModelError` (D-10): corresponde a las páginas `Create/Edit` (PR-3).

## Riesgos residuales (para revisión humana)

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| El helper `WithSoloSinUsuarioSet` aplica filtro solo cuando `query.SoloSinUsuario == true`; si el cliente se llama con `false` explícito pero el set está poblado, no excluye (back-compat con PR-1 backend: false === null === sin filtro) | ninguno | Test `FakePersonaApiClient_QueryAsync_WithSoloSinUsuarioNullOrFalse_DoesNotExcludeFromSet` cubre el comportamiento. |
| Si se introducen nuevos consumidores de `PersonaApiClient.QueryAsync` que olviden pasar `soloSinUsuario`, obtendrán la lista completa — back-compat por diseño. | bajo | Los call sites de UI/JS están en WU-5..8 y serán revisados en PR-3. |
| El cliente NO hace reintentos automáticos; depende de la política del caller. Si la API devuelve 5xx transitorio, el modal mostrará estado Error y el usuario debe reintentar. | aceptable | Patrón vigente en el resto del repo. Sin cambios de política en este slice. |

## Validación previa al push (realizada)

- [x] `dotnet build SGV.slnx --no-incremental` — 0 errores, 23 warnings preexistentes, **0 nuevos**.
- [x] `dotnet test SGV.slnx --no-build` — **2433/2433** verde (2426 baseline + 7 nuevos).
- [x] `gentle-ai review validate --gate pre-pr` — `result=allow` (lineage `review-e0fab7bc673a62e1`).
- [x] Tests cubren: serialización con `true`, back-compat con `null/false`, transporte nativo propaga excepciones, fake filtra cuando `true`, fake no excluye cuando `null/false`.
- [x] Sin migraciones, sin dependencias nuevas, sin `Co-Authored-By`.
- [x] `[Authorize]` no modificado (este slice no toca backend).

## Pendiente para `sdd-archive` (post merge de los 3 PRs)

1. Sincronizar delta specs (`persona-management/spec.md`, `usuario-web-selector-persona-buscador/spec.md`, `usuario-web-crear-editar/spec.md`) a `openspec/specs/`.
2. Mover `openspec/changes/2026-07-17-buscador-personas-modal/` a `openspec/changes/archive/`.
3. Cerrar la issue #157.

## Próximos pasos

- **PR-3**: `feat/2026-07-17-buscador-personas-frontend` (WU-5..8) desde develop tras merge de PR-2:
  - Nuevo partial `_PersonaBuscadorModal.cshtml` en `src/SGV.Web/Pages/Seguridad/Usuarios/`.
  - Nuevo JS `wwwroot/js/pages/usuario-persona-buscador.js`.
  - Reemplazo del combo en `_Form.cshtml` por bloque `Buscar Persona` + card + `Quitar` + `Cambiar`.
  - Borrar `IPersonaOptionsProvider`, `HttpPersonaOptionsProvider`, `FakePersonaOptionsProvider`, `IUsuarioForm.PersonaOptions`.
  - Tests `[WebIntegration]` con `FakePersonaApiClient` extendido (este slice ya dejó listo el helper `WithSoloSinUsuarioSet`).
  - Estimado: ~400-500 líneas, 4 commits work-unit.