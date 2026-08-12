# Design: completar-tipos-unidad-organizativa

## Technical Approach

Nueva migración EF Core forward-only con 13 `InsertData` desde `TipoUnidadOrganizativaConstantes`, complementando la huérfana `20260730000000` (sin `.Designer.cs` → invisible a `Migrate()`). Se crea con `dotnet ef migrations add CompletarTiposUnidadOrganizativaSeed`, que genera Designer + Snapshot, y luego se hand-authoriza el `Up()`. Mapea la proposal (Opción 1) y las delta specs REQ-TUO-001 (Migrate → 20) y REQ-TUO-007 (forward-only/append-only).

## Architecture Decisions

### Decision: Hand-author del `Up()` (diff de modelo cero)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Auto-generar vía `dotnet ef migrations add` | El Designer de la última migración `20260805000000` **ya contiene los 20 seeds** en `HasData` (verificado líneas 1598-1717), igual que `DatosSemilla.cs`. Diff modelo = 0 → migración **vacía**, sin los 13 `InsertData`. | **Rechazado** |
| Revertir temporalmente `DatosSemilla` a 7 seeds para forzar el diff | Toca código fuera de scope; frágil; ensucia el historial. | **Rechazado** |
| `dotnet ef migrations add` (genera Designer + Snapshot) + hand-author del `Up()` con 13 `InsertData` y `Down()` que lanza `NotSupportedException` | El Designer/Snapshot quedan en 20 (estado objetivo); la migración es el mecanismo que lleva la BD de 7 → 20. | **Elegido** |

**Rationale**: el snapshot ya es el estado objetivo (20); la migración aporta el `InsertData` que el diff no produce. EF no valida que el `Up()` coincida con el diff del modelo.

### Decision: `InsertData()` estándar sobre `Sql("INSERT IGNORE…")`

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `migrationBuilder.InsertData()` | No es idempotente a nivel fila: si alguien aplicó a mano el SQL de la huérfana en una BD no trackeada en `__EFMigrationsHistory`, duplicaría PK/Codigo y fallaría. | **Elegido** |
| `migrationBuilder.Sql("INSERT IGNORE…")` | Idempotente a nivel fila, pero rompe la convención EF del catálogo y es MySQL-específico. | **Rechazado** |

**Rationale**: sigue la convención del catálogo (`20260616190624` y la huérfana). La idempotencia de REQ-TUO-001 se satisface a nivel migración vía `__EFMigrationsHistory` (EF no reejecuta migraciones aplicadas). El edge-case de hand-aplicación se mitiga con chequeo pre-deploy.

### Decision: Timestamp posterior a `20260805000000`; `Down()` lanza `NotSupportedException`

Nueva migración última en la cadena (timestamp autogenerado). `Down()` forward-only/append-only (REQ-TUO-007 y precedente huérfano): sin `DELETE`/`UPDATE` sobre filas seed. La huérfana `20260730000000` permanece **sin tocarse** (Out of Scope).

## Data Flow

```
DatosSemilla.HasData(20) ──┐
                           ├── dotnet ef migrations add ── <ts>_…Seed.Designer.cs (20)
Model (20) ────────────────┘                                <ts>_…Seed.cs (hand-author: 13 InsertData)
                                                            SgvDbContextModelSnapshot.cs (regenerado, 20)

Producción:  20260616 (7) ── salta huérfana (sin Designer) ── nueva (13) ──▶ 20
Test:        snapshot (20) ──▶ 20 (NO ejerce historial de migraciones)
```

**GUIDs/fechas/orden**: GUIDs desde `TipoUnidadOrganizativaConstantes` (bloque `60000000-…008`–`014`). La tabla **no tiene columnas de fecha** (solo `Id`, `Codigo`, `Nombre`) → fechas N/A. Orden de los 13 `InsertData`: idéntico a la huérfana (`Sede → … → Escuela`), GUID ascendente; `Nombre` con tildes (`Región`, `Coordinación`, `Sección`, `Célula`).

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `…/Migraciones/<ts>_CompletarTiposUnidadOrganizativaSeed.cs` | Create | `Up()`: 13 `InsertData` desde `TipoUnidadOrganizativaConstantes`; `Down()`: `NotSupportedException`. Hand-authored (diff cero). |
| `…/Migraciones/<ts>_CompletarTiposUnidadOrganizativaSeed.Designer.cs` | Create (auto) | Generado por `dotnet ef migrations add`; snapshot 20 `HasData`. Hace la migración visible a `list`/`script`/`Migrate`. |
| `…/Migraciones/SgvDbContextModelSnapshot.cs` | Modify (auto) | Regenerado; sin cambio de contenido (ya en 20). |
| `tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs` | Modify | Comment `EnsureCreated()` (20 vía snapshot) vs `Migrate()` (20 vía migraciones); doc de clase "7"→"20". Aserción sin cambios. |

La delta spec `openspec/changes/…/specs/tipo-unidad-organizativa-catalog/spec.md` (ya creada por sdd-spec; REQ-TUO-001/002/007) es referencia, no se modifica en esta fase.

## Interfaces / Contracts

Sin cambios de contrato. El `InsertData` reutiliza la fila existente (sin `CreatedAt`): `columns: new[] { "Id", "Codigo", "Nombre" }`, `values: object[,]` con los 13 pares `(TipoUnidadOrganizativaConstantes.<X>Id, "Codigo", "Nombre")`. Los pares `Codigo`/`Nombre` y GUIDs son **idénticos** a la migración huérfana `20260730000000` (fuente verbatim). `Down()` => `throw new NotSupportedException("…forward-only…append-only.")`.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Integration (MySQL) | `…TiposUnidadOrganizativaCreadosCon20Seeds` pasa (20 vía `EnsureCreated`) | MySQL 8 real (`[MySqlFact]`); sin nueva aserción. |
| Validación (manual) | `migrations list` (+1); `script --idempotent` con 13 `INSERT INTO` | CLI post-generación. |
| Build | `dotnet build SGV.slnx` | Antes de commit. |

**Gap honesto**: el escenario REQ-TUO-001 "Migrate produce 20 filas en base existente con 7 tipos" **no** tiene test automatizado (Out of Scope: no crear tests); se valida con `migrations list` + `script --idempotent` y `COUNT(*)` post-deploy.

## Migration / Rollout

Forward-only; sin `Down()` real. BD nueva: `Migrate()` → cadena → 20. BD existente (7): aplica la nueva → 7 + 13 = 20. **Pre-deploy**: `SELECT COUNT(*) FROM TiposUnidadOrganizativa` == 7; si >7 (huérfana aplicada a mano) o ya contiene alguno de los 13 GUIDs, abortar (`InsertData` no es idempotente a nivel fila).

## Open Questions

- [ ] Ninguna bloqueante. Riesgo aceptado: idempotencia a nivel fila no cubierta (ver Pre-deploy). `review_budget_lines=400` suficiente (1 archivo nuevo + Designer auto + snapshot regenerado + 1 comment).