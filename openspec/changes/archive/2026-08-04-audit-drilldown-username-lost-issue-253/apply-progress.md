# Apply Progress: Issue #253 — Auditoría drill-down preserva `userName`

## Status

**applyState**: ready → work complete, ready for `sdd-verify`
**Mode**: Strict TDD
**Delivery**: single-pr, scoped fix
**Date**: 2026-08-04

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | Integration (Web) | ✅ 4/4 pass | ✅ Theory written | ✅ Both cases pass | ✅ 2 cases (jperez / null) | ➖ None needed (minimal fix) |
| 2.1–2.2 | `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | Production | N/A (no tests inline) | ✅ Confirmed RED: 1/6 failed | ✅ Confirmed GREEN: 6/6 pass | ➖ Single behavior | ➖ None needed |

## Detailed Steps

### Phase 1 — RED (regression test)

- Added `[Theory] Get_Details_RoundTripPreservesUserNameFilter` (2 inline data cases) to `AuditoriasDetailsTests.cs`.
- Reuse: `CreateAuditoriaLeaseAsync`, `MakeAuditoriaDetalleDto`, `SgvWebApplicationFactory`, `FakeAuditoriaApiClient` (no new helpers).
- RED proof: `dotnet test --filter "FullyQualifiedName~AuditoriasDetailsTests"` → `Failed: 1, Passed: 5` (el caso `("jperez", True)` falló como se esperaba; el caso `(null, False)` pasó por default).

### Phase 2 — GREEN (minimal fix)

- 4 surgical edits en `Details.cshtml.cs`:
  1. doc-comment `/// <summary>Filtro vigente: userId.</summary>` → `/// <summary>Filtro vigente: userName.</summary>`
  2. property `public string? UserId { get; private set; }` → `public string? UserName { get; private set; }`
  3. `[FromQuery(Name = "userId")] string? userId = null` → `[FromQuery(Name = "userName")] string? userName = null`
  4. `UserId = Normalize(userId);` → `UserName = Normalize(userName);`
  5. `BuildBackUrl()` route value `userId = UserId` → `userName = UserName`
- Sin cambios en `Index.cshtml.cs`, `Details.cshtml`, API, contratos, persistencia ni migraciones.
- GREEN proof: `dotnet test --filter "FullyQualifiedName~AuditoriasDetailsTests"` → `Passed: 6/6`.

### Phase 3 — Verificación

- `dotnet build SGV.slnx` → 0 errores, 4 warnings (pre-existing pruning hints).
- `dotnet test SGV.slnx` → 3415/3415 pass en rerun (la primera corrida tuvo 4 failures de MySqlFact por contención de estado compartido; no relacionadas con el fix).
- `dotnet test --filter "FullyQualifiedName~Auditoria"` → 97/97 pass.
- Diff scope: 73 added / 5 deleted across 2 files (dentro del forecast 35–70 / budget 800).

## Archivos modificados

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs` | Modified | Renombrado `UserId`→`UserName`, `[FromQuery(Name="userId")]`→`[FromQuery(Name="userName")]`, route value `userName=UserName` en `BuildBackUrl`. |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs` | Modified | Agregada `[Theory] Get_Details_RoundTripPreservesUserNameFilter` (2 casos). |

## Desviaciones del diseño

Ninguna. La implementación coincide 1:1 con `design.md`.

## Issues encontrados

Ninguno en scope. Los 4 failures de MySqlFact en la primera corrida del suite fueron contención de estado compartido preexistente; el rerun confirmó limpio.

## Workload / PR Boundary

- Mode: single PR
- Work unit: 1 (preserve `userName` round-trip + regression test)
- Diff: 73 lines added / 5 deleted, 2 files
- El diff del PR queda cómodamente bajo el budget de 800 líneas.

## Next Steps

1. `sdd-verify` para confirmar que la implementación cumple los escenarios de la spec.
2. `sdd-archive` una vez verificado.
