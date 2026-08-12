# Exploration: completar-tipos-unidad-organizativa

## Current State

The `TiposUnidadOrganizativa` catalog should have 20 seed types but only 7 are materialized in production databases. The migration `20260730000000_SemillaTipoUnidadOrganizativaAmpliada` contains `InsertData` for 13 additional types (Sede, Region, Gerencia, Vicepresidencia, Subgerencia, Coordinacion, Seccion, Oficina, Equipo, Celula, Planta, Sucursal, Escuela) but lacks a `.Designer.cs` file, making it invisible to EF Core.

As documented in `docs/decisiones-implementacion.md` § "Limitación documentada — migración sin Designer":

- `dotnet ef migrations list` shows 17 migrations (missing the orphaned one)
- The standalone SQL script (`docs/migracion-inicial-sgv.sql`) omits the 13 inserts
- `Database.Migrate()` skips them in production
- Result: only 7 types exist via the original migration `20260616190624_CambiarTipoUnidadATablaTipoUnidadOrganizativa`

**Critical inconsistency detected**: The `SgvDbContextModelSnapshot` already contains all 20 types in `HasData` (lines 1595–1715), and `DatosSemilla.cs` (lines 121–141) also seeds all 20. The snapshot and DatosSemilla are consistent with each other but inconsistent with what `Migrate()` actually produces.

Tests use `EnsureCreated()` which creates schema from the snapshot's `HasData`, so `MigracionFailLoudTests.Migracion_DatosLimpios_TiposUnidadOrganizativaCreadosCon20Seeds` passes with 20 seeds — giving a false sense of completeness because `EnsureCreated()` bypasses migration history entirely.

The archived spec at `openspec/changes/archive/.../tipo-unidad-organizativa-catalog/spec.md` still references "exactly 7 rows" throughout REQ-TUO-001 and REQ-TUO-002 and needs updating to 20.

## Affected Areas

| File | Why Affected |
|------|-------------|
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs` | Huérfana sin Designer.cs; debe mantenerse intacta |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Ya tiene los 20 tipos en `HasData`; estado objetivo correcto |
| `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` | Ya tiene los 20 seeds (líneas 121–141); correcto |
| `src/SGV.Infraestructura/Persistencia/Catalogos/TipoUnidadOrganizativaConstantes.cs` | Fuente de verdad para los 20 GUIDs; 13 para insertar |
| `openspec/changes/archive/.../tipo-unidad-organizativa-catalog/spec.md` | Dice "7 rows" en REQ-TUO-001/002; debe actualizarse a 20 |
| `tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs` | Test pasa con 20 via EnsureCreated pero producción recibe 7 via Migrate |

## The 13 Missing Types

| GUID (constante) | Código | Nombre |
|-------------------|--------|--------|
| `60000000-...-008` | SedeId | Sede |
| `60000000-...-009` | RegionId | Región |
| `60000000-...-00a` | GerenciaId | Gerencia |
| `60000000-...-00b` | VicepresidenciaId | Vicepresidencia |
| `60000000-...-00c` | SubgerenciaId | Subgerencia |
| `60000000-...-00d` | CoordinacionId | Coordinación |
| `60000000-...-00e` | SeccionId | Sección |
| `60000000-...-00f` | OficinaId | Oficina |
| `60000000-...-010` | EquipoId | Equipo |
| `60000000-...-011` | CelulaId | Célula |
| `60000000-...-012` | PlantaId | Planta |
| `60000000-...-013` | SucursalId | Sucursal |
| `60000000-...-014` | EscuelaId | Escuela |

## Approaches

### Approach 1: Nueva migración correctiva (RECOMENDADA)

Crear `dotnet ef migrations add CompletarTiposUnidadOrganizativaSeed` que ejecute `InsertData` con los 13 tipos faltantes usando `TipoUnidadOrganizativaConstantes`. El Designer.cs se genera automáticamente.

- **Pros**: Sigue patrones EF Core; Designer.cs automático; detectable por `dotnet ef migrations list`; incluida en script standalone; rollback trivial vía Down()
- **Cons**: Pequeño tiempo adicional en despliegues existentes
- **Effort**: Low

### Approach 2: Regenerar Designer.cs para la migración huérfana

Adaptar manualmente un Designer.cs para `20260730000000_SemillaTipoUnidadOrganizativaAmpliada` copiando la estructura de una migración adyacente.

- **Pros**: Sin nueva migración ni cambio de spec
- **Cons**: Frágil; cualquier diferencia de hash causa inconsistencia; `dotnet ef migrations remove` podría intentar eliminar la migración huérfana
- **Effort**: Medium

### Approach 3: Solo actualizar spec, sin tocar código

Actualizar `tipo-unidad-organizativa-catalog/spec.md` para indicar 20 filas. No resuelve el problema real de producción.

- **Pros**: Mínimo cambio
- **Cons**: No materializa los 13 tipos en BD; producción sigue con 7
- **Effort**: Low

## Recommendation

**Approach 1 (nueva migración correctiva)**. El usuario ya autorizó su implementación. La migración debe usar `migrationBuilder.InsertData` con los 13 GUIDs de `TipoUnidadOrganizativaConstantes`. El Designer.cs se genera automáticamente y garantiza detección en `dotnet ef migrations list`, inclusión en el script standalone, y aplicación por `Database.Migrate()`.

Acciones complementarias:
1. Actualizar el spec `REQ-TUO-001 Scenario: Seed creates N static types` de 7 a 20 filas
2. Agregar comment en `MigracionFailLoudTests` aclarando la diferencia EnsureCreated vs Migrate
3. No modificar ni eliminar la migración huérfana `20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs`

## Risks

1. **Migración huérfana persiste en el filesystem** — Si alguien regenera snapshots o usa `dotnet ef migrations remove`, podría intentar eliminarla o generar conflicto de hashes. Recommendation: no tocarla; la nueva migración la complementa sin reemplazarla.

2. **Test engañoso** — `Migracion_DatosLimpios_TiposUnidadOrganizativaCreadosCon20Seeds` pasa por usar `EnsureCreated()` (snapshot) no `Migrate()`. El test es correcto en su aserción (20 seeds via EnsureCreated) pero no refleja el path de producción (7 via Migrate). Un comment aclaratorio es suficiente; no cambiar la aserción.

3. **Spec desactualizado** — `REQ-TUO-001` y `REQ-TUO-002` dicen "7 rows". Sin actualizar el spec, queda inconsistency entre el documento y la realidad post-correctiva.

## Ready for Proposal

**Sí.** El usuario ya autorizó la implementación de una nueva migración correctiva. La exploración confirma que la vía correcta es Approach 1. El alcance es claro: INSERT los 13 registros faltantes, actualizar el spec de 7 a 20 filas, y aclarar el test con un comment. No hay ambigüedad sobre el enfoque.
