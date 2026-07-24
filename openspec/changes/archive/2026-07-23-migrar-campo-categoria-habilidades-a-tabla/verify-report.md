# Verify Report: Migrar campo Categoría de Habilidades a Tabla

## Smoke Test

| Suite | Resultado |
|-------|-----------|
| `dotnet test SGV.slnx` | **2,891/2,891 PASS** — 0 Failed, 0 Skipped |
| `bun install && bun run build` (src/SGV.Web) | ✅ Sin errores |

## Criterios de Aceptación vs. Evidencia

| AC (de proposal.md) | Evidencia | Estado |
|---------------------|-----------|--------|
| Migración limpia + 7 habilidades resueltas o NULL | Migración `AddCategoriaHabilidadCatalog`: CreateTable → InsertData (4 seeds) → Backfill LOWER() JOIN → Auditoría → FK Restrict → DropColumn. Tests MySqlFact validan backfill y escenarios NULL. | ✅ |
| Sin `Habilidades.Categoria`; FK e índices presentes | Migración: DropIndex IX_Habilidades_Categoria, DropColumn Categoria, FK CategoriaId → CategoriasHabilidad.Id (OnDelete Restrict), IX_Habilidades_CategoriaId. Tests MySqlFact validan estructura post-migración. | ✅ |
| `dotnet test SGV.slnx` pase | **2,891/2,891 PASS** | ✅ |
| `bun run build` pase | Gulp build exitoso (plugins + styles + inspiniaPages) | ✅ |
| Docs actualizados | `docs/decisiones-implementacion.md`: bloque `72000000-…` (líneas 116-117), §Migración CategoriasHabilidad (líneas 722-769) | ✅ |

## Spec Deltas Mergeados

| Dominio | Acción | Detalle |
|---------|--------|---------|
| `sgv-persistence-architecture` | **Updated** | Añadida 4ª invocación REQ-SPA-EVOLUTION-001 (CategoriasHabilidad, opt-in relajada). 1 nuevo requisito + 3 escenarios. |
| `web-apiclient-transport-contract` | **Updated** | 2 nuevos requisitos: ICategoriaHabilidadApiClient read-only (6 escenarios) + HabilidadApiClient traducción errores CategoriaId (3 escenarios). |
| `commandresult-error-taxonomy` | **Updated** | 2 nuevos requisitos: mapeo CategoriaHabilidad a ErrorCategoria (2 escenarios) + HabilidadErrorType.CategoriaInexistente (2 escenarios). |
| `categoria-habilidad-catalog` | **Created** | Nuevo spec completo (catálogo inmutable, endpoints read-only, seed 72000000-…, paridad DatosSemilla). |
| `habilidad-web-crear-editar` | **Updated** | MODIFIED "Campos visibles y Codigo editable…" (dropdown catálogo). ADDED: poblado dropdown + guardado CategoriaId. |
| `habilidad-management` | **Updated** | MODIFIED "Crear Habilidad" y "Actualizar Habilidad" con CategoriaId. ADDED: contrato read-only CategoriaId/CategoriaNombre, backfill histórico. |
| `sgv-database` | **Updated** | 3 nuevos requisitos: tabla CategoriasHabilidad, FK CategoriaId on Delete Restrict, migración opt-in relajada. |

## Commands Executed

```bash
# Smoke test suite completa
dotnet test SGV.slnx --nologo 2>&1 | tail -n 5
# → Passed!  - Failed: 0, Passed: 2891, Skipped: 0, Total: 2891

# Bundle frontend
cd src/SGV.Web && bun install && bun run build
# → gulp build: plugins + styles + inspiniaPages — sin errores
```

## Caveats Conocidos

1. **Dropdown filtro en Cargos/Personas (tasks 2.4.1-2.4.6)**: diferido a cambio futuro. El cambio no incluye el filtro de habilidades por categoría en los formularios de asignación de Cargos y Personas.
2. **Variante opt-in relajada**: los valores legacy de `Categoria` sin match en el seed (e.g. `"Otra"`) quedan con `CategoriaId = NULL`. La auditoría registra la transición para remediación post-deploy. Esto es intencional por diseño (cuarta invocación de REQ-SPA-EVOLUTION-001).
3. **CHANGELOG.md**: el proyecto no usa este archivo. La documentación del cambio reside en `docs/decisiones-implementacion.md` (§Migración CategoriasHabilidad líneas 722-769 + bloque 72000000-… líneas 116-117).
4. **Migración forward-only**: `Down()` lanza `NotSupportedException`. Rollback = revert merge git + restaurar schema desde backup pre-migración.

## Resultado

**VERIFIED** ✅ — Todos los criterios de aceptación se cumplen. Smoke test 2,891/2,891 PASS. Bundle frontend compila sin errores. Docs actualizados. 6 canonical specs mergeados + 1 spec nuevo creado.
