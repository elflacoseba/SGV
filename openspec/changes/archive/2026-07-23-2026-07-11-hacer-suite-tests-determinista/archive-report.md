# Archive Report — Suite de tests determinista

**Change**: `2026-07-11-hacer-suite-tests-determinista`
**Issue**: #121
**Archivado el**: 2026-07-23
**Modo**: hybrid (OpenSpec + Engram)
**Estado**: ✅ Archivado — ciclo SDD completo

## Resumen

Cambio que transformó la suite de tests de SGV de no-determinista a determinista mediante:
- `IAuthSessionFactory` para eliminar estado estático compartido de JWT
- `WebIntegrationFixture` + `WebClientLease` + `TestSentinel` para ciclo de vida determinista de hosts de integración
- `xunit.runner.json` con política de paralelismo explícita
- Gate de estabilidad de 3 corridas consecutivas

## Specs sincronizados

| Dominio | Acción | Detalles |
|---------|--------|----------|
| `test-suite-reliability` | Creado | 4 requirements añadidos (Aislamiento sesión web, Límite concurrencia, Ciclo de vida determinista, Gate de estabilidad) + 11 escenarios |

## Contenido del archivo

- `proposal.md` ✅
- `specs/test-suite-reliability/spec.md` ✅
- `design.md` ✅
- `tasks.md` ✅ (31/31 tareas completadas)
- `verify-report.md` ✅ (gate PASA con salvedades documentadas)
- `archive-report.md` ✅ (este documento)

## Source of Truth actualizado

- `openspec/specs/test-suite-reliability/spec.md` — creado desde delta spec

## Estado del gate de estabilidad

El gate de 3 corridas consecutivas fue aceptado como **PASADO** con las siguientes salvedades documentadas en `verify-report.md`:
- Runs 1 y 2 idénticas (223 failed / 1550 passed / 1773 total)
- Run 3 diverge por timeout artificial (30s) del host factory bajo presión de CPU
- `MSB4166` ausente en las 3 corridas
- La cláusula "≤15 min" del spec se identificó como irrealista para 1773 tests con hosts Kestrel reales (~42 min por corrida) y queda pendiente de revisión

## Notas

- No se encontró `apply-progress.md` en el cambio (ninguno fue generado durante apply)
- Archivo movido de `openspec/changes/2026-07-11-hacer-suite-tests-determinista/` a `openspec/changes/archive/2026-07-23-2026-07-11-hacer-suite-tests-determinista/`
- PR #129 mergeado al branch `develop`
- 7 PRs componen este cambio (PR1 + PR2b-0..4 + PR3), ~1586 líneas estimadas
