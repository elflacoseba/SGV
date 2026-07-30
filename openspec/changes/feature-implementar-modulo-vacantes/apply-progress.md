# Apply Progress — Implementar el módulo de Vacantes

> Sub-lanzamiento 1 de 3 del slice 1 backend (work unit 1.1 → 1.7).
> Modo: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`).
> Persistencia: híbrido (OpenSpec + Engram).

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` | Unit | N/A (new file) | ✅ Written — CS1061 × 9 (`ActualizarObservaciones` no existía) | ✅ Passed 6/6 | ✅ 4 cases extra (Trim, >500, SoloEspacios, Vacio) | ➖ None needed (single-line impl sobre `ValidacionesDominio.Opcional`) |
| 1.2 | mismo | Unit | — | ✅ Written (incluida en 1.1) | ✅ Passed 6/6 | ✅ Trim + length + whitespace comparten file | ➖ Same |
| 1.3 | mismo | Unit | — | ✅ Written (incluida en 1.1) | ✅ Passed 6/6 | ✅ SoloEspacios + Vacio complementan el caso nulo | ➖ Same |
| 1.4 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural type (constants only, no branching) | ✅ Build succeeded | ➖ Single (constants son el contrato) | ➖ None |
| 1.5 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural records (sin lógica) | ✅ Build succeeded | ➖ Single | ➖ None |
| 1.6 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural records | ✅ Build succeeded | ➖ Single | ➖ None |
| 1.7 | N/A — wire-type | — | N/A (structural) | ➖ Triangulation skipped: structural types (`VacanteCommandResult.Success/Failure` se cubren en tests de integración API del work unit 3.x) | ✅ Build succeeded | ➖ Single | ➖ None |

### Test Summary
- **Total tests written**: 6 (`VacanteTests.ActualizarObservaciones_*`)
- **Total tests passing**: 6 (ejecutado `dotnet test --filter "VacanteTests"` → `Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6`)
- **Layers used**: Unit (6), Integration (0), E2E (0)
- **Approval tests** (refactoring): None — no se refactorizó código existente
- **Pure functions created**: 1 (`Vacante.ActualizarObservaciones` — single statement, no side effects fuera del setter privado)

### Focused Test Command and Exact Result
```
dotnet test tests/SGV.Tests/SGV.Tests.csproj \
  --filter "FullyQualifiedName~VacanteTests" \
  --nologo --no-build
→ Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 2 ms
```

### Workload / PR Boundary
- **Mode**: feature-branch-chain (slice 1 backend, sub-lanzamiento 1 de 3)
- **Cadena**: `feature/implementar-modulo-vacantes` ← este PR (1.x). PRs posteriores del slice 2 (web) apilarán sobre éste.
- **Current work unit**: 1.1 → 1.7 (Dominio + Contracts)
- **Líneas añadidas (2 commits)**: 287 (100 dominios/tests + 187 contracts). Bajo presupuesto 400.
- **PR boundary**: listo para `feat(vacantes) backend sub-PR1 — dominio + wire-types`.

### Rollback Boundary
- `git revert` de los dos commits (95ec28e y f57b207) devuelve HEAD a main sin tocar otros módulos.
- Archivos removibles sin efectos colaterales:
  - `src/SGV.Dominio/Vacantes/Vacante.cs` (modificado — quitar el método `ActualizarObservaciones`).
  - `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` (nuevo).
  - `src/SGV.Contracts/Vacantes/` (directorio completo — sin referencias externas todavía porque ningún controller/ApiClient/Servicio se compila contra estos tipos aún).
- Sin migraciones EF. Sin cambios en `Program.cs`/`DependencyInjection.cs`. La app sigue arrancando con el comportamiento de antes.

## Commits

| SHA | Tipo | Descripción |
|-----|------|-------------|
| `95ec28e` | feat(vacantes) | `feat(vacantes): add ActualizarObservaciones to Vacante aggregate` |
| `f57b207` | feat(contracts) | `feat(contracts): add Vacante wire-types (routes, DTOs, requests, results)` |

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Dominio/Vacantes/Vacante.cs` | Modified | Added `ActualizarObservaciones(string?)` validation; uses `ValidacionesDominio.Opcional(observaciones, nameof(Observaciones), 500)`. |
| `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` | Created | 6 unit tests (2 mandated + 4 triangulation) for `ActualizarObservaciones`. |
| `src/SGV.Contracts/Vacantes/VacanteApiRoutes.cs` | Created | Routes constants + status/sort whitelist. |
| `src/SGV.Contracts/Vacantes/Enums/VacanteSegmentoListado.cs` | Created | `Abiertas=0, Cerradas=1, Todas=2`. |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDto.cs` | Created | Consumer-safe list DTO (no audit fields). |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDetailDto.cs` | Created | Detail with `IReadOnlyList<HistorialEstadoVacanteDto>`. |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/HistorialEstadoVacanteDto.cs` | Created | Single history entry wire-type. |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/EstadoVacanteDto.cs` | Created | Catalog DTO (read-only). |
| `src/SGV.Contracts/Vacantes/Consultas/VacanteListQuery.cs` | Created | `Segmento=Abiertas` default (PB-5). |
| `src/SGV.Contracts/Vacantes/Comandos/CrearVacanteRequest.cs` | Created | Required `Motivo`, optional `Observaciones`. |
| `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs` | Created | Optional `Motivo` (PB-3) + optional `Observaciones` (OQ-1 + OQ-3). |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteError.cs` | Created | `ErrorCategoria` canon, sin legacy `[Obsolete]` enum. |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` | Created | Curated error codes. |
| `src/SGV.Contracts/Vacantes/Comandos/VacanteCommandResult.cs` | Created | `Success/Failure(Failure)` factories; `Value=VacanteDetailDto?`. |
| `openspec/changes/feature-implementar-modulo-vacantes/tasks.md` | Modified | Phase 1 marcada [x] para 1.1–1.7. |

## Deviations from Design

- **CambiarEstadoVacanteRequest** ahora incluye `Observaciones` (opcional). El interface del `design.md §Interfaces / Contracts` mostraba sólo `(EstadoVacanteId, Motivo?)`, pero la tarea 1.6 explícitamente pide el campo `Observaciones` y la OQ-3 está resuelta con "Observaciones opcional". Decisión coherente con `tasks.md` (1.6, OQ-3 Resuelta) y con el commit 1 (1.1 cubre la mutación en el agregado).
- **`VacanteCommandResult.Value` se tipó como `VacanteDetailDto?`** (no `VacanteDto?` como en `OcupacionCommandResult`). Coincide con `design.md §Interfaces / Contracts` (último bloque). Implicación: para GET/listados se sigue pudiendo devolver DTOs más livianos (`VacanteDto` directo desde el servicio de consulta) sin pasar por `CommandResult`.

## Issues Found

- Ninguno funcional. Nota menor: el módulo `SGV.Contracts` ya estaba añadido al grafo (`csproj` referencia sólo `Microsoft.IdentityModel.Tokens 8.14.0`). Los nuevos archivos no requieren ningún package extra.

## Remaining Tasks (slice 1)

- [ ] 2.1 `VacanteRepository` con segmentación y atomicidad (`GetByIdAsync`, `ListarAsync(segmento)`, `ExistsAbiertaByPuestoAsync`, `GetByIdForUpdateAsync`).
- [ ] 2.2 Mappers `ToDomain`/`ToEntity` (PersistenceToDomain + DomainToPersistence).
- [ ] 2.3/2.4 RED tests de repository.
- [ ] 3.1–3.5 Servicios de aplicación + controllers + DI.
- [ ] 3.6–3.7 Tests de servicio y de controller.
- [ ] 3.8 `EstadoVacanteConstantes` (catálogo) + test paridad.
- [ ] 3.9 Bloque GUID `20000000-…` en `docs/decisiones-implementacion.md`.
- [ ] (Slice 2 web) 4.x + 5.x: Index/Create/Edit/Details + ApiClient + `_Sidenav` + smoke tests.
