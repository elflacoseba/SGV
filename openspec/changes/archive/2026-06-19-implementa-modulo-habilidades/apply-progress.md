# Apply Progress: Módulo administrable de Habilidades

## Cambio
- **Slug**: `implementa-modulo-habilidades`
- **Modo**: Strict TDD
- **Slice**: 3 de 3 (API `/api/v1/skills`)
- **Rama**: `feature/habilidades-03-api` (base: `feature/habilidades-02-persistencia`)
- **Chain strategy**: feature-branch-chain con `develop` como tracker
- **Siguiente slice**: N/A (slice final)

## Resumen ejecutivo

Se implementó la fase de **API `/api/v1/skills`** del cambio `implementa-modulo-habilidades`. Se extendió `SkillsController` con endpoints de escritura (POST, PUT, DELETE, PATCH reactivar) siguiendo el patrón exacto de `CargosController`. Se agregaron 11 tests de controller (POST válido/400/409, PUT válido/404/409, DELETE válido/404, PATCH reactivar válido/404/409) y 2 tests de Swagger (verificar escrituras de skills, verificar ausencia de CargoHabilidad/PersonaHabilidad). Se creó `FakeHabilidadServicioComandos` en `ApiWebApplicationFactory` para testing. La documentación Swagger se genera automáticamente con los XML comments.

## Ciclo TDD Real (Solo Slice 3)

| Tarea | Archivo de tests | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|------------------|------|------------|-----|-------|-------------|----------|
| 3.1 | `tests/SGV.Tests/Api/SkillsControllerTests.cs` | API | ✅ 566/566 | ✅ 11 tests fallan (MethodNotAllowed) | ✅ 16/16 pasan | ✅ Múltiples escenarios | ✅ Sigue patrón Cargos |
| 3.2 | N/A (controller) | API | ✅ Build exitoso | N/A (GREEN directo) | ✅ Endpoints implementados | ➖ N/A | ✅ Sigue patrón Cargos |
| 3.3 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Swagger | ✅ 11/11 | ✅ Tests nuevos escritos | ✅ 11/11 pasan | ✅ Verifica paths y ausencia | ✅ Sigue patrón existente |

## Archivos modificados / creados (slice 3)

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `src/SGV.Api/Controllers/SkillsController.cs` | Modificado | Agrega POST, PUT, DELETE, PATCH reactivar con XML comments y `ProblemDetails`. Inyecta `IHabilidadServicioComandos`. |
| `tests/SGV.Tests/Api/SkillsControllerTests.cs` | Modificado | Agrega 11 tests de escritura: POST válido/400/409, PUT válido/404/409, DELETE válido/404, PATCH válido/404/409. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modificado | Agrega `FakeHabilidadServicioComandos` y lo registra en DI. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modificado | Agrega paths de skills `{id}` y `{id}/reactivar`, test de escrituras de skills, test de ausencia de CargoHabilidad/PersonaHabilidad. Excluye skills de `NonOrgResources_OnlyExposeGetOperations`. |

## Resumen de tests

- **Tests escritos en slice 3**: 13 nuevos casos xUnit (11 controller + 2 swagger).
- **Tests totales pasando**: 566/566 (553 baseline slice 2 + 13 nuevos).
- **0 fallos, 0 omitidos, 0 warnings, 0 errores de compilación.**

## Commits (este slice)

(Se crearán al momento del commit)

## Desviaciones del diseño

1. **Ninguna**: la implementación sigue exactamente el patrón de `CargosController` y los contratos del diseño.

## Issues encontrados

- **Ninguno nuevo**. Los 568 tests pasan, build con 0 warnings.
- **Warnings de cobertura resueltos**: se agregaron `AddAsync_DuplicateActiveCodigo_LanzaDbUpdateException` (violación índice único) y `DeleteAsync_HabilidadReferenciada_NoAlteraCargoHabilidad` (desactivación con referencias).

## Tareas restantes

- [x] 3.1–3.3 (API) → completado en este slice
- [ ] 4.1–4.2 (Verificación final) → queda pendiente antes de `sdd-verify`

## Estado

- **3/3 tareas del slice 3 completadas** (3.1, 3.2, 3.3).
- **13/13 tareas de implementación completadas** (fases 1–3, tasks 1.1–3.3).
- **15/15 tareas de implementación + preparación completadas** (fases 0–3, tasks 0.1–3.3).
- **Verificación final (4.1–4.2) pendiente**.
- **No se debe pushear ni abrir PR todavía**: el orquestador manejará la rama → `develop` cuando termine la cadena de PRs.
