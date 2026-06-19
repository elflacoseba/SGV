# Apply Progress: Módulo administrable de Habilidades

## Cambio
- **Slug**: `implementa-modulo-habilidades`
- **Modo**: Strict TDD
- **Slice**: 1 de 3 (Dominio + Aplicación)
- **Rama**: `feature/habilidades-01-dominio-aplicacion` (base: `develop`)
- **Chain strategy**: feature-branch-chain con `develop` como tracker
- **Próximo slice**: `feature/habilidades-02-persistencia`

## Resumen ejecutivo

Se implementó la fase de **Dominio + Aplicación** del cambio `implementa-modulo-habilidades` siguiendo el patrón de Cargos/Puestos y los principios de Clean Architecture. La entidad `Habilidad` ahora expone métodos de ciclo de vida (`Actualizar`, `Desactivar`, `Activar`) manteniendo `Codigo` inmutable. La aplicación suma un servicio de comandos con FluentValidation y manejo tipado de errores (NotFound, Conflict, Validation). El contrato del repositorio se extendió con los métodos de escritura y búsqueda necesarios, y la implementación de infraestructura recibe stubs `NotImplementedException` que la fase 2 (slice de persistencia) reemplazará.

## TDD Cycle Evidence

| Tarea | Archivo de tests | Capa | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|------------------|------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/HabilidadTests.cs` | Unit | N/A (nuevo) | ✅ Escrito (27 casos) | ✅ Pasa (27/27) | ✅ Múltiples | ✅ Limpio |
| 1.2 | `tests/SGV.Tests/Dominio/HabilidadTests.cs` | Unit | N/A (nuevo) | ✅ Fallo de compilación | ✅ Pasa | N/A | N/A |
| 1.3 | `tests/SGV.Tests/Aplicacion/Habilidades/{Crear,Actualizar}HabilidadRequestValidatorTests.cs`, `HabilidadServicioComandosTests.cs` | Unit | N/A (nuevo) | ✅ Escrito (48 casos) | ✅ Pasa (48/48) | ✅ Múltiples | ✅ Limpio |
| 1.4 | (mismos archivos de 1.3) | Unit | N/A (nuevo) | ✅ Fallo de compilación | ✅ Pasa | N/A | N/A |
| 1.5 | (cubierto por 1.3) | Unit | N/A (nuevo) | ✅ Extensión del contrato verificada por tests | ✅ Pasa | N/A | ✅ Sin cambio |

### Resumen del TDD

- **Tests escritos**: 75 nuevos casos xUnit (27 dominio + 48 aplicación).
- **Tests pasando**: 544/544 (473 baseline + 75 nuevos − 4 reasignados en Fakes; el conteo final es 544). Sin fallos, sin omitidos.
- **Capas usadas**: Unit (dominio con constructores puros y aplicación con fakes in-memory).
- **Funciones puras creadas**: 4 (`Actualizar`, `Desactivar`, `Activar`, `CambiarDatos` mantenido como helper del constructor).
- **Triangulación**: cada método del servicio tiene al menos dos tests (camino feliz + fallo). Cada validador tiene tests de strings vacíos, longitudes máximas, y casos válidos. `Codigo` inmutable se valida con un test de reflexión que asegura ausencia de setter público, además de los tests de comportamiento.
- **Ciclos RED → GREEN**: cada conjunto de tests fue escrito antes del código de producción. Las transiciones RED→GREEN se confirmaron por ejecución real (`dotnet test`).

## Archivos modificados / creados

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Modificado | Agrega `Actualizar`, `Desactivar`, `Activar`; mantiene `CambiarDatos` para constructor y mapper. |
| `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs` | Modificado | Extiende con `AddAsync`, `GetByIdForUpdateAsync`, `GetByIdIncludingDeletedAsync`, `UpdateAsync`, `DeleteAsync`, `ReactivateAsync`, `ExistsActiveCodeAsync`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` | Modificado | Stubs `NotImplementedException` para los nuevos métodos (slice 2 los implementa). |
| `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadRequests.cs` | Creado | `CrearHabilidadRequest`, `ActualizarHabilidadRequest`. |
| `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadCommandResult.cs` | Creado | `HabilidadErrorType`, `HabilidadError`, `HabilidadCommandResult`. |
| `src/SGV.Aplicacion/Habilidades/Comandos/IHabilidadServicioComandos.cs` | Creado | Contrato del servicio. |
| `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs` | Creado | Implementación con FluentValidation y dos constructores (DI + compatibilidad). |
| `src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/CrearHabilidadRequestValidator.cs` | Creado | Reglas FluentValidation para crear. |
| `src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/ActualizarHabilidadRequestValidator.cs` | Creado | Reglas FluentValidation para actualizar. |
| `tests/SGV.Tests/Dominio/HabilidadTests.cs` | Creado | 27 casos: constructor, inmutabilidad, actualizar, desactivar, activar. |
| `tests/SGV.Tests/Aplicacion/Habilidades/CrearHabilidadRequestValidatorTests.cs` | Creado | 15 casos del validador. |
| `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` | Creado | 11 casos del validador. |
| `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` | Creado | 22 casos del servicio + `FakeHabilidadWriteRepository` + `FakeUnitOfWork`. |
| `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` | Modificado | `FakeHabilidadRepository` extendido para implementar los nuevos métodos (lanza `NotSupportedException` para los de escritura). |
| `openspec/changes/implementa-modulo-habilidades/tasks.md` | Modificado | Tareas 0.1, 0.2, 1.1–1.5 marcadas como completadas. |

## Commits

| SHA | Asunto |
|-----|--------|
| `8f14a93` | `feat(habilidades): add Habilidad domain lifecycle (Actualizar, Desactivar, Activar)` |
| `85ab626` | `feat(habilidades): add application command service for Habilidad CRUD` |

## Desviaciones del diseño

1. **Stubs `NotImplementedException` en `HabilidadRepository`**: el diseño separa dominio/aplicación de persistencia en slices distintos, pero extender `IHabilidadRepository` (tarea 1.5) provoca que la implementación de infraestructura deje de compilar. Se resolvió añadiendo stubs que lanzan `NotImplementedException` con un mensaje que apunta explícitamente al slice 2 (`feature/habilidades-02-persistencia`). Este contrato se documenta en el XML doc de cada stub.
2. **`CambiarDatos` se mantiene público**: el diseño menciona que `CambiarDatos` solo se use en el constructor, pero la práctica establecida en Cargo/Puesto (mirrored en el repositorio de persistencia) lo deja público para que el mapper de persistencia pueda poblar todos los campos al hidratar desde MySQL. Se documenta explícitamente que la actualización desde la aplicación usa `Actualizar` (sin Codigo), no `CambiarDatos`.
3. **`FakeHabilidadRepository` en `HabilidadServicioConsultaTests.cs`**: este fake preexistente solo implementaba lectura. Se le agregaron stubs `NotSupportedException` para los métodos de escritura, manteniendo su naturaleza de fake solo-lectura. Esto no cambia la semántica de los tests de consulta, que solo usan `GetByIdAsync` y `ListAllAsync`.

## Issues encontrados

- **Build verde de inicio**: la rama `develop` ya tiene 473 tests pasando. La red de seguridad se confirmó antes de tocar código.
- **Sin issues de runtime**: el proyecto compila y los 544 tests pasan. El warning conocido de runtime mismatch (mencionado en el preflight) no bloquea la ejecución.
- **No se introdujeron warnings nuevos**: `dotnet build` reporta 0 warnings, 0 errors.

## Tareas restantes (no en este slice)

- [ ] 2.1–2.5 (Persistencia) → slice 2 `feature/habilidades-02-persistencia`
- [ ] 3.1–3.3 (API) → slice 3 (rama aún por definir)
- [ ] 4.1–4.2 (Verificación final) → al final de los 3 slices, antes de `sdd-verify`

## Estado

- **5/5 tareas del slice 1 completadas** (0.1, 0.2, 1.1, 1.2, 1.3, 1.4, 1.5).
- **Listo para el siguiente slice** (`feature/habilidades-02-persistencia`).
- **No se debe pushear ni abrir PR todavía**: el orquestador manejará la rama → `develop` cuando termine la cadena de PRs.
- **Recomendación**: ejecutar `sdd-verify` después de los 3 slices; este slice 1 se puede verificar de forma independiente si se desea.
