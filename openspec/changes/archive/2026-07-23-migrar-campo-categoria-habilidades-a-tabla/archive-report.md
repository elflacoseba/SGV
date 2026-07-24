# Archive Report: Migrar campo Categoría de Habilidades a Tabla

## Metadata

| Campo | Valor |
|-------|-------|
| Change | `migrar-campo-categoria-habilidades-a-tabla` |
| Fecha archive | 2026-07-23 |
| Modo | hybrid (OpenSpec + Engram) |
| Estado | **success** — sdds cycle complete |
| TDD | strict (2891/2891 tests pass) |

## Specs Sync

| Domain | Action | Requirements Added/Modified |
|--------|--------|----------------------------|
| `sgv-persistence-architecture` | Updated | 4th invocation REQ-SPA-EVOLUTION-001 (CategoriasHabilidad opt-in relajada) + 4 escenarios |
| `web-apiclient-transport-contract` | Updated | ICategoriaHabilidadApiClient read-only (6 escenarios) + HabilidadApiClient CategoriaId errors (3 escenarios) |
| `commandresult-error-taxonomy` | Updated | CategoriaHabilidad → ErrorCategoria mapping (2 escenarios) + HabilidadErrorType.CategoriaInexistente (2 escenarios) |
| `categoria-habilidad-catalog` | **Created** | Nuevo spec completo (inmutabilidad, endpoints auth, seed, paridad) |
| `habilidad-web-crear-editar` | Updated | MODIFIED dropdown catálogo + ADDED poblado en caliente + guardado CategoriaId |
| `habilidad-management` | Updated | MODIFIED Crear/Actualizar (CategoriaId) + ADDED contrato read-only + backfill histórico |
| `sgv-database` | Updated | 3 nuevos requisitos: tabla CategoriasHabilidad, FK CategoriaId, migración opt-in relajada |

## Task Reconciliation

**Total tasks:** 90 (84 implementadas y verificadas, 6 diferidas a cambio futuro)

| Grupo | [x] | [ ] | Nota |
|-------|-----|-----|------|
| PR #1 — Backend (1.1-1.7) | 46 | 0 | ✅ PR #193 mergeado, tests + build verificados |
| PR #2 — Frontend (2.1-2.3, 2.5-2.6) | 28 | 0 | ✅ PR #194 mergeado, tests + bun build verificados |
| PR #2 — Filtro Cargos/Personas (2.4) | 0 | 6 | 🔲 Diferido intencionalmente (`diferido` explicito en task). No implementado en este cambio. |
| Verificación final | 4 | 0 | ✅ dotnet test 2891/2891, bun build, CHANGELOG (vía docs), docs actualizados |

### Notas sobre tareas específicas

- **CHANGELOG.md (task 1.6.3 + verificación final)**: El proyecto NO utiliza CHANGELOG.md. La convención vigente es `docs/decisiones-implementacion.md` como bitácora histórica. La entrada del cambio está documentada en §Migración CategoriasHabilidad (líneas 722-769) + bloque `72000000-…` (líneas 116-117). No se creó CHANGELOG.md por instrucción explícita del orquestador y la convención del proyecto.
- **Tasks 2.4.1-2.4.6 (Filtro dropdown Cargos/Personas)**: Diferidas. El código no fue implementado. Quedan como tareas pendientes para un cambio futuro. El archive es **intentional-with-warnings** para estas 6 tareas diferidas.

## Archive Contents

```
openspec/changes/archive/2026-07-23-migrar-campo-categoria-habilidades-a-tabla/
├── proposal.md           ✅ (propuesta original)
├── specs/                ✅ (7 deltas)
│   ├── categoria-habilidad-catalog/
│   ├── commandresult-error-taxonomy/
│   ├── habilidad-management/
│   ├── habilidad-web-crear-editar/
│   ├── sgv-database/
│   ├── sgv-persistence-architecture/
│   └── web-apiclient-transport-contract/
├── design.md             ✅ (diseño técnico)
├── tasks.md              ✅ (90 tareas, 84 [x], 6 [ ] diferidas)
├── apply-progress.md     ✅ (resumen de implementación)
└── verify-report.md      ✅ (verificación: 2891/2891 PASS)
└── archive-report.md     ✅ (este documento)
```

## Verification Commands

| Comando | Resultado |
|---------|-----------|
| `dotnet test SGV.slnx` | **2,891/2,891 PASS** |
| `bun install && bun run build` (src/SGV.Web) | ✅ |
| `dotnet build SGV.slnx` | ✅ |

## Source of Truth Updated

Los siguientes specs canónicos reflejan ahora el nuevo comportamiento:
- `openspec/specs/sgv-persistence-architecture/spec.md`
- `openspec/specs/web-apiclient-transport-contract/spec.md`
- `openspec/specs/commandresult-error-taxonomy/spec.md`
- `openspec/specs/categoria-habilidad-catalog/spec.md`
- `openspec/specs/habilidad-web-crear-editar/spec.md`
- `openspec/specs/habilidad-management/spec.md`
- `openspec/specs/sgv-database/spec.md`

## SDD Cycle Complete

El cambio `migrar-campo-categoria-habilidades-a-tabla` ha sido completamente planificado, implementado, verificado y archivado. El ciclo SDD está completo para este cambio.

**Riesgos remanentes:**
- 6 tareas (2.4.1-2.4.6) diferidas para filtro de categorías en Cargos/Personas
- Migración forward-only (rollback requiere revert git + restaurar backup)
- Variante opt-in relajada: datos legacy sin match quedan NULL con auditoría post-deploy
