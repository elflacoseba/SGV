# Apply Progress: Módulo administrable de Habilidades

## Cambio
- **Slug**: `implementa-modulo-habilidades`
- **Modo**: Strict TDD
- **Slice**: 2 de 3 (Persistencia MySQL/Pomelo)
- **Rama**: `feature/habilidades-02-persistencia` (base: `feature/habilidades-01-dominio-aplicacion`)
- **Chain strategy**: feature-branch-chain con `develop` como tracker
- **Siguiente slice**: `feature/habilidades-03-api`

## Resumen ejecutivo

Se implementó la fase de **Persistencia MySQL/Pomelo** del cambio `implementa-modulo-habilidades`. Los stubs `NotImplementedException` del `HabilidadRepository` fueron reemplazados por implementaciones reales EF Core/Pomelo siguiendo el patrón exacto de `CargoRepository`. Se agregaron los mapeos `ToEntity` y `UpdateEntity` en `DomainToPersistenceMapper` para `Habilidad`. Se registró `IHabilidadServicioComandos` en el contenedor DI. Se verificó que la migración inicial ya incluye la columna generada `ActiveCodigoUnique` con su índice único, por lo que no se requiere una nueva migración.

## Ciclo TDD Real (Solo Slice 2)

| Tarea | Archivo de tests | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|------------------|------|------------|-----|-------|-------------|----------|
| 2.1/2.2 | `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` | Integration | ✅ 544/544 | ✅ 9 tests fallan (NotImplementedException) | ✅ 12/12 pasan | ✅ Múltiples | ✅ Limpio |
| 2.3 | N/A (mapper) | Structural | N/A | N/A (creación) | ✅ Build exitoso | ➖ Sin triangulación (estructural) | ✅ Sigue patrón Cargo |
| 2.4 | N/A (verificación) | Verification | N/A | N/A | N/A | N/A | N/A |
| 2.5 | N/A (DI) | Structural | ✅ Build exitoso | N/A | N/A | N/A | N/A |

## Archivos modificados / creados (slice 2)

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` | Modificado | Reemplaza stubs `NotImplementedException` con implementación EF Core real siguiendo `CargoRepository`. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modificado | Agrega `ToEntity(Habilidad)` y `UpdateEntity(HabilidadEntity, Habilidad)`. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificado | Registra `IHabilidadServicioComandos` → `HabilidadServicioComandos`. |
| `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` | Modificado | Agrega 9 tests de escritura: Add, GetByIdForUpdate, GetByIdIncludingDeleted, Update, Delete, Reactivate, ExistsActiveCode. |
| `tests/SGV.Tests/Persistencia/RepositoryTestData.cs` | Modificado | Agrega helper `CreateHabilidad(string prefix)`. |

## Resumen de tests

- **Tests escritos en slice 2**: 9 nuevos casos xUnit (9 persistencia).
- **Tests totales pasando**: 553/553 (544 baseline + 9 nuevos).
- **0 fallos, 0 omitidos, 0 warnings, 0 errores de compilación.**

## Migración

- **Acción**: Ninguna. La migración inicial (`20260614183103_InicialSgvo.cs`) ya incluye la columna generada `ActiveCodigoUnique` con `CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END` y el índice único `IX_Habilidades_ActiveCodigoUnique`.
- `HabilidadConfiguracion.cs` ya define correctamente la columna computada y el índice. No se requieren cambios ni nueva migración.

## Commits (este slice)

(Se crearán al momento del commit, ver más abajo)

## Desviaciones del diseño

1. **`CambiarDatos` se mantiene público**: igual que en slice 1, el mapper de persistencia necesita acceso a `CambiarDatos` para hidratar desde MySQL. Documentado en slice 1.
2. **Sin migración nueva**: el diseño indicaba verificar si la migración vigente contiene `ActiveCodigoUnique`. Ya existe. No se generó migración nueva.

## Issues encontrados

- **Ninguno nuevo**. Los 553 tests pasan, build con 0 warnings.

## Tareas restantes

- [ ] 3.1–3.3 (API) → slice 3 `feature/habilidades-03-api`
- [ ] 4.1–4.2 (Verificación final) → al final de los 3 slices, antes de `sdd-verify`

## Estado

- **5/5 tareas del slice 2 completadas** (2.1, 2.2, 2.3, 2.4, 2.5).
- **10/13 tareas totales completadas** (slices 1 + 2).
- **Listo para el siguiente slice** (`feature/habilidades-03-api`).
- **No se debe pushear ni abrir PR todavía**: el orquestador manejará la rama → `develop` cuando termine la cadena de PRs.
