# Proposal: completar-tipos-unidad-organizativa

## Intent

El catálogo `TiposUnidadOrganizativa` debe contener 20 tipos pero en producción solo existen 7. La migración `20260730000000_SemillaTipoUnidadOrganizativaAmpliada` tiene `InsertData` con los 13 faltantes pero carece de `.Designer.cs`, haciéndola invisible para `dotnet ef migrations list` y `Database.Migrate()`. El objetivo es materializar las 20 filas en bases de datos nuevas y existentes, actualizar el spec que aún afirma 7 filas, y aclarar el test engañoso.

## Scope

### In Scope
- Nueva migración EF Core (`CompletarTiposUnidadOrganizativaSeed`) con `InsertData` para los 13 tipos faltantes usando `TipoUnidadOrganizativaConstantes`
- Actualización del spec `tipo-unidad-organizativa-catalog` (escenarios REQ-TUO-001 y REQ-TUO-002) de 7 → 20 filas seed
- Comment aclaratorio en `MigracionFailLoudTests` sobre la diferencia `EnsureCreated()` vs `Migrate()`
- Regenerar `SgvDbContextModelSnapshot` tras la nueva migración

### Out of Scope
- Eliminar o modificar la migración huérfana `20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs`
- Crear nuevos tests; solo se aclara el existente
- Cambiar aserción del test `Migracion_DatosLimpios_TiposUnidadOrganizativaCreadosCon20Seeds`
- Regenerar el script SQL standalone (`docs/migracion-inicial-sgv.sql`)

## Capabilities

### New Capabilities
- Ninguna.

### Modified Capabilities
- `tipo-unidad-organizativa-catalog` (delta spec): REQ-TUO-001 Scenario "Seed creates N static types" y REQ-TUO-002 Scenario "Returns full list" actualizan el conteo seed de 7 a 20 filas. Los 20 códigos: `Area`, `Celula`, `Coordinacion`, `Departamento`, `Direccion`, `Division`, `Equipo`, `Escuela`, `Facultad`, `Gerencia`, `Institucion`, `Oficina`, `Planta`, `Region`, `Seccion`, `Secretaria`, `Sede`, `Subgerencia`, `Sucursal`, `Vicepresidencia`.

## Approach

Crear `dotnet ef migrations add CompletarTiposUnidadOrganizativaSeed` en `SGV.Infraestructura`. La migración usa `migrationBuilder.InsertData()` con los 13 GUIDs de `TipoUnidadOrganizativaConstantes` (Sede → Escuela). El `.Designer.cs` se genera automáticamente, garantizando detección en `dotnet ef migrations list`, inclusión en el script idempotente, y aplicación por `Database.Migrate()`. La migración es forward-only (catálogo append-only).

Los 13 tipos a insertar: `SedeId`, `RegionId`, `GerenciaId`, `VicepresidenciaId`, `SubgerenciaId`, `CoordinacionId`, `SeccionId`, `OficinaId`, `EquipoId`, `CelulaId`, `PlantaId`, `SucursalId`, `EscuelaId`.

## Affected Areas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Infraestructura/Persistencia/Migraciones/` | Nueva migración | `CompletarTiposUnidadOrganizativaSeed.cs` + `.Designer.cs` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Modificado | Regenerado tras la migración |
| `openspec/specs/tipo-unidad-organizativa-catalog/spec.md` | Modificado | Delta spec actualizando 7 → 20 en REQ-TUO-001/002 |
| `tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs` | Modificado | Comment aclaratorio sobre EnsureCreated vs Migrate |

## Risks

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Migración huérfana остаётся en el filesystem sin применarsen | Media | No tocarla; la nueva migración la complementa sin reemplazarla |
| `dotnet ef migrations remove` intenta eliminar la migración huérfana | Baja | La huérfana no tiene Designer, así que `remove` debería ignorarla; si falla, revertir el intento |
| Test pasa con `EnsureCreated()` pero producción usa `Migrate()` | Baja | Comment aclaratorio ya documenta la diferencia; la aserción de 20 filas es correcta |
| Hash de snapshot inconsistente tras regenerar | Baja | Regenerar el snapshot es parte del comando de migración; verificar con `dotnet ef migrations list` |

## Rollback Plan

El catálogo `TiposUnidadOrganizativa` es **append-only**. La migración se declara `forward-only` con `Down()` lanzando `NotSupportedException`, alineado con la decisión de no eliminar filas seed. Si la migración causa problemas en despliegue:

1. Verificar que `dotnet ef migrations list` muestra la migración como aplicada (`<born>`)
2. Si falló en producción, el equipo debe investigar el error específico; no hay `Down()` seguro
3. Para bases de datos nuevas: `Database.Migrate()` aplica automáticamente la nueva migración

## Dependencies

- `dotnet ef` CLI tooling disponible en el entorno
- MySQL 8 ejecutándose para validación local
- La migración huérfana `20260730000000_SemillaTipoUnidadOrganizativaAmpliada.cs` debe permanecer intacta en el filesystem

## Success Criteria

- [ ] `dotnet ef migrations list` muestra 1 migración adicional (`CompletarTiposUnidadOrganizativaSeed`)
- [ ] `dotnet ef migrations script --idempotent` incluye los 13 `INSERT INTO TiposUnidadOrganizativa`
- [ ] `MigracionFailLoudTests.Migracion_DatosLimpios_TiposUnidadOrganizativaCreadosCon20Seeds` sigue pasando (20 filas vía `EnsureCreated`)
- [ ] El comment aclaratorio en el test menciona la diferencia `EnsureCreated()` vs `Migrate()`
- [ ] Delta spec de `tipo-ununidad-organizativa-catalog` actualiza 7 → 20 en los escenarios relevantes
- [ ] `dotnet build SGV.slnx` compila sin errores
- [ ] `dotnet test SGV.slnx --filter "FullyQualifiedName~MigracionFailLoud"` pasa (MySQL real)
