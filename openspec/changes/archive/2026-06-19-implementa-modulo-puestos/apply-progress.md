# Apply Progress — implementa-modulo-puestos

## Resumen de implementación

Se implementó el módulo administrable de Puestos siguiendo los patrones establecidos por Cargos y Unidades Organizativas. El alcance incluyó:

- Reglas de dominio para `Puesto` con campos obligatorios (`Codigo`, `Nombre`), `PuestoSuperiorId` opcional, baja lógica y reactivación.
- Contratos de aplicación: requests, validadores con FluentValidation, resultado tipado `PuestoCommandResult` y servicio de comandos.
- Persistencia MySQL: métodos write en `PuestoRepository`, mapeos y verificación del índice único activo existente.
- API REST: endpoints `POST`, `PUT`, `DELETE` (soft-delete) y `PATCH {id}/reactivar` en `PuestosController`.
- Cobertura de pruebas en dominio, aplicación, persistencia y API.

No se requirió migración de base de datos porque el esquema actual ya contenía la columna calculada `ActiveCodigoUnique` y el índice único activo.

## Remediación post-verify

Durante la verificación se identificaron tres advertencias. Estado tras esta remediación:

1. **Validación de existencia/estado activo de `PuestoSuperiorId`** — ✅ resuelta.
   - Se agregó `ValidarPuestoSuperiorAsync` en `PuestoServicioComandos`.
   - Se valida en `CrearAsync` y `ActualizarAsync`; el superior debe existir y estar activo.
   - Se agregó validación de autorreferencia explícita en actualización.
   - Se agregaron 7 pruebas unitarias nuevas en `PuestoServicioComandosTests`.

2. **Formato estricto de evidencia TDD en `apply-progress.md`** — ✅ resuelta.
   - La tabla ahora incluye las columnas requeridas por `strict-tdd.md`: `SAFETY NET`, `RED`, `GREEN`, `TRIANGULATE` y `REFACTOR`.

3. **Falla preexistente en `UnidadOrganizativaRepositoryTests.QueryAsync_FiltroPorTipoUnidadOrganizativaId_RetornaSoloCoincidencias`** — ✅ resuelta.
   - Causa: el test filtraba por `TipoUnidadOrganizativaConstantes.DireccionId`, un tipo que ya tenía datos sembrados en la base de pruebas.
   - Fix: se crea un `TipoUnidadOrganizativaEntity` dedicado dentro del test y se filtra por ese id, aislando el test de los datos de seed.

## TDD Cycle Evidence

> Modo Strict TDD activo. Las tareas de prueba se marcan con RED/GREEN/TRIANGULATE/REFACTOR siguiendo el formato exigido por `strict-tdd.md`. Las tareas estructurales o de verificación se marcan según su estado real sin inventar evidencia.

| Tarea | Archivo de prueba / artefacto | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-------|-------------------------------|------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/Organizacion/PuestoTests.cs` | Unit | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ Campos requeridos, superior opcional, actualizar sin cambiar `Codigo`, desactivar y activar | ✅ Clean |
| 1.2 | `src/SGV.Dominio/Organizacion/Puesto.cs` | Domain | N/A (nuevo) | ✅ Tests 1.1 | ✅ Passed | ➖ Cubierto por 1.1 | ✅ Clean |
| 1.3 | `tests/SGV.Tests/Aplicacion/Organizacion/CrearPuestoRequestValidatorTests.cs`<br>`ActualizarPuestoRequestValidatorTests.cs` | Unit | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ Obligatorios, longitudes y GUID vacío | ✅ Clean |
| 1.4 | `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/*Puesto*Validator.cs` | Unit | N/A (nuevo) | ✅ Tests 1.3 | ✅ Passed | ➖ Cubierto por 1.3 | ✅ Clean |
| 2.1 | `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioComandosTests.cs` | Unit | ✅ 13/13 baseline | ✅ Written | ✅ Passed | ✅ Crear, actualizar, duplicado activo, referencias inválidas, superior válido/inexistente/inactivo, autorreferencia, baja lógica y reactivación | ✅ Clean |
| 2.2 | `src/SGV.Aplicacion/Organizacion/Comandos/PuestoServicioComandos.cs` | Unit | ✅ 13/13 previo | ✅ Tests 2.1 | ✅ Passed | ➖ Cubierto por 2.1 | ✅ Clean |
| 2.3 | `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoRepository.cs` | Contract | ➖ No aplica | ➖ No aplica | ✅ Implementado | ➖ No aplica | ✅ Clean |
| 3.1 | `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs` | Integration | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ Unicidad activa, reutilización tras baja, reactivación e includes | ✅ Clean |
| 3.2 | `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Integration | N/A (nuevo) | ✅ Tests 3.1 | ✅ Passed | ➖ Cubierto por 3.1 | ✅ Clean |
| 3.3 | `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Integration | N/A (nuevo) | ✅ Tests 3.1/3.2 | ✅ Passed | ➖ No aplica | ✅ Clean |
| 3.4 | `src/SGV.Infraestructura/Persistencia/Configuraciones/PuestoConfiguracion.cs` | Verification | ➖ No aplica | ➖ No aplica | ✅ Snapshot alineado | ➖ No aplica | ➖ No aplica |
| 4.1 | `tests/SGV.Tests/Api/PuestosControllerTests.cs` | Integration | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ POST, PUT, DELETE, PATCH reactivar, errores 400/404/409 y JSON camelCase | ✅ Clean |
| 4.2 | `src/SGV.Api/Controllers/PuestosController.cs` | Integration | N/A (nuevo) | ✅ Tests 4.1 | ✅ Passed | ➖ Cubierto por 4.1 | ✅ Clean |
| 4.3 | `src/SGV.Infraestructura/DependencyInjection.cs` | Integration | ➖ No aplica | ➖ No aplica | ✅ Registrado | ➖ No aplica | ✅ Clean |
| 5.1 | `dotnet build` | Verification | ✅ Build previo limpio | ➖ No aplica | ✅ Passed | ➖ No aplica | ➖ No aplica |
| 5.2 | `dotnet test` | Verification | ✅ Suite previa | ➖ No aplica | ✅ Passed | ➖ No aplica | ➖ No aplica |

## Workload / PR Boundary

- **Estrategia**: `single-pr-default` con `size:exception` aprobado por mantenedor.
- **Presupuesto de revisión**: 800 líneas cambiadas.
- **Nota**: El forecast original (`tasks.md`) indica riesgo alto de presupuesto de 400 líneas y recomienda PRs encadenados; para esta remediación post-verify se mantiene el cambio en un único PR con excepción de tamaño explícita.

## Evidencia de ejecución

- `dotnet build`: exitoso, 0 advertencias, 0 errores.
- `dotnet test --filter "FullyQualifiedName~Puesto"`: 123/123 verdes (incluye 7 pruebas nuevas de validación de `PuestoSuperiorId`).
- `dotnet test`: 473/473 verdes; la falla preexistente de `UnidadOrganizativaRepositoryTests` quedó resuelta.

## Issues conocidos

- Ninguno pendiente. Las tres advertencias de verify quedaron resueltas.
