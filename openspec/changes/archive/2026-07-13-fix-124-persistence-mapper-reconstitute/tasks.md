# Tasks: Refactor de `PersistenceToDomainMapper` para eliminar reflexión (issue #124)

## Resumen del change

Refactor técnico para eliminar `SetProperty<T>` (reflexión `PropertyInfo.SetValue` + `BindingFlags.NonPublic`) del path MySQL → Dominio. Introducimos un factory `internal static Reconstitute(...)` en 6 entidades (`Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`, `UnidadOrganizativa`) que recibe todos los campos persistibles y los asigna con setters tipados. `UnidadOrganizativa` abandona el patrón `with` para paridad total con las demás. Total estimado **~506 LoC** (producción ~250 + tests ~256). `size:exception` aprobado por maintainer (`design.md:399-412`). **Single PR**, NO chained. NO migraciones EF Core, NO cambios en `*Entity`, NO cambios en auditoría ni contratos HTTP.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~506 (producción ~250 + tests ~256) |
| 400-line budget risk | High (506 vs 400) |
| Chained PRs recommended | No (single PR por `size:exception`) |
| Suggested split | Single PR cohesivo, 8 commits atómicos (CU-1..CU-8) |
| Delivery strategy | `exception-ok` (size:exception reconocido) |
| Chain strategy | `size-exception` |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units (commits atómicos)

| CU | Goal | Files principales | Tests included |
|----|------|-------------------|----------------|
| CU-1 | Setup: `InternalsVisibleTo` + 5 IL tests en RED | `SGV.Dominio.csproj` + 4 tests nuevos + 1 ampliación | Sí (RED) |
| CU-2 | Habilidad: `Reconstitute` + mapper + behavior | `Habilidad.cs`, `PersistenceToDomainMapper.cs`, `HabilidadMapperTests.cs` | Sí |
| CU-3 | Cargo: `Reconstitute` + mapper + behavior | `Cargo.cs`, `PersistenceToDomainMapper.cs`, `CargoMapperTests.cs` | Sí |
| CU-4 | Ocupacion: `Reconstitute` + mapper + behavior | `Ocupacion.cs`, `PersistenceToDomainMapper.cs`, `OcupacionMapperTests.cs` | Sí |
| CU-5 | Persona: `Reconstitute` + mapper + behavior | `Persona.cs`, `PersistenceToDomainMapper.cs`, `PersonaMapperTests.cs` | Sí |
| CU-6 | Puesto: `Reconstitute` + mapper + behavior | `Puesto.cs`, `PersistenceToDomainMapper.cs`, `PuestoMapperTests.cs` | Sí |
| CU-7 | UO atómico: mutadores `void` + `Reconstitute` + mapper + service + tests | `UnidadOrganizativa.cs`, `PersistenceToDomainMapper.cs`, `UnidadOrganizativaServicioComandos.cs`, tests | Sí |
| CU-8 | Cleanup: borrar `SetProperty` + `using System.Reflection;` + verificación final | `PersistenceToDomainMapper.cs` | N/A |

## TDD Strategy

`strict_tdd: true` activo. Patrón de ciclo:

1. **CU-1**: agregar `InternalsVisibleTo("SGV.Tests")` para que las pruebas de comportamiento de las próximas fases compilen. Crear 5 tests IL estructurales (siguiendo el patrón de `UnidadOrganizativaRepositoryTests.cs:984-1045`: recorrer `MethodBody.GetILAsByteArray()`, decodificar tokens `0x28`/`0x6F`, resolver `MethodInfo`, fallar si encuentra `SetProperty` declarada en `PersistenceToDomainMapper`). Esos tests quedan **RED** porque `SetProperty` sigue presente.
2. **CU-2 a CU-6** (por entidad): escribir los tests de comportamiento en RED primero (round-trip, `IsActive=false`, nav opcional, validación de shape), implementar `Reconstitute` en GREEN, reescribir `ToDomain(TEntity)` en el mapper para que el IL test correspondiente pase a GREEN.
3. **CU-7**: ciclo atómico para UO — los mutadores pasan a `void`-return, lo que rompe compilación si se hace en pasos intermedios. Aplicar TODO el bloque de UO en un solo commit.
4. **CU-8**: REFACTOR — eliminar el helper `SetProperty<T>` y `using System.Reflection;` cuando ningún `ToDomain` lo invoca; verificación final con `grep` y suite completa.

Tests de comportamiento cubren: round-trip OK, `IsActive=false` reconstituido sin lanzar, nav properties opcionales (`null` y no-`null`), validación de shape (`Persona` documento, `Ocupacion` fechas), `EsVigente` correcto en Ocupacion (`FechaFin=null && !IsDeleted`).

## Phase 1: Setup + RED tests (CU-1)

- [x] **T-1.1** Agregar `<ItemGroup><AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo"><_Parameter1>SGV.Tests</_Parameter1></AssemblyAttribute></ItemGroup>` a `src/SGV.Dominio/SGV.Dominio.csproj` (paralelo a `SGV.Infraestructura.csproj:25-29`). Acceptance: `dotnet build SGV.slnx` verde, sin warnings de inaccesibilidad. LoC: +5.
- [x] **T-1.2** Crear `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` con `[Fact] ToDomain_Cargo_NoLlamaSetPropertyReflectionHelper`. Replicar el patrón de `UnidadOrganizativaRepositoryTests.cs:996-1045`. **RED**: el test falla porque `SetProperty` sigue siendo invocada en `ToDomain(CargoEntity)`. Dep: T-1.1. LoC: +50.
- [x] **T-1.3** Crear `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` con el IL test homólogo. **RED**. Dep: T-1.1. LoC: +40.
- [x] **T-1.4** Crear `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` con el IL test homólogo. **RED**. Dep: T-1.1. LoC: +50.
- [x] **T-1.5** Crear `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` con el IL test homólogo. **RED**. Dep: T-1.1. LoC: +50.
- [x] **T-1.6** Ampliar `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` con `[Fact] ToDomain_Ocupacion_NoLlamaSetPropertyReflectionHelper`. **RED**. Dep: T-1.1. LoC: +40.

## Phase 2: Habilidad migration (CU-2)

- [x] **T-2.1** Implementar `internal static Habilidad.Reconstitute(Guid id, string codigo, string nombre, string? categoria, string? descripcion, bool isActive, DateTime createdAt, string? createdByUserId, DateTime? updatedAt, string? updatedByUserId, bool isDeleted, DateTime? deletedAt, string? deletedByUserId)` en `src/SGV.Dominio/Habilidades/Habilidad.cs`. Validar shape con `ValidacionesDominio.Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `Opcional(categoria, 100)`, `Opcional(descripcion, 1000)`. Asignar `IsActive` por `private set`. **GREEN**. LoC: +16.
- [x] **T-2.2** En `HabilidadMapperTests.cs` agregar `[Fact] Reconstitute_MapsAllFields`, `Reconstitute_IsActiveFalsePreservaFlag`, `Reconstitute_AuditFieldsPreservados`. **GREEN** cuando T-2.1 esté aplicado. Dep: T-2.1. LoC: +25.
- [x] **T-2.3** Reescribir `ToDomain(HabilidadEntity)` (`PersistenceToDomainMapper.cs:50-67`) para invocar `Habilidad.Reconstitute(...)` directo (sin `SetProperty`). **GREEN**: T-1.3 ahora pasa. LoC: ±0.

## Phase 3: Cargo migration (CU-3)

- [x] **T-3.1** Implementar `internal static Cargo.Reconstitute(Guid id, string codigo, string nombre, Guid nivelId, string? descripcion, bool isActive, NivelCargo? nivelCargo, DateTime createdAt, string? createdByUserId, DateTime? updatedAt, string? updatedByUserId, bool isDeleted, DateTime? deletedAt, string? deletedByUserId)` en `src/SGV.Dominio/Organizacion/Cargo.cs`. Validar con `Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `ValidarNivelId(nivelId)`, `Opcional(descripcion, 1000)`. **IsActive=false NO dispara `Desactivar()`** (doc XML explícito sobre la invariante silenciada). **GREEN**. LoC: +18.
- [x] **T-3.2** Agregar tests a `CargoMapperTests.cs`: `Reconstitute_MapsAllFields`, `Reconstitute_IsActiveFalseNoDisparaValidacion`, `Reconstitute_NivelCargoNull`, `Reconstitute_NivelCargoHydrated`. **GREEN** post T-3.1. LoC: +35.
- [x] **T-3.3** Reescribir `ToDomain(CargoEntity)` (`PersistenceToDomainMapper.cs:17-39`) para usar `Cargo.Reconstitute(...)`. **GREEN**: T-1.2 ahora pasa. LoC: ±0.

## Phase 4: Ocupacion migration (CU-4)

- [x] **T-4.1** Implementar `internal static Ocupacion.Reconstitute(Guid id, Guid personaId, Guid puestoId, DateOnly fechaInicio, DateOnly? fechaFin, TipoAsignacion tipoAsignacion, string? observaciones, Persona? persona, Puesto? puesto, DateTime createdAt, string? createdByUserId, DateTime? updatedAt, string? updatedByUserId, bool isDeleted, DateTime? deletedAt, string? deletedByUserId)` en `src/SGV.Dominio/Ocupaciones/Ocupacion.cs`. **Replicar validación `fechaFin >= fechaInicio`** (líneas 15-18 del ctor primario). Orden canónico: id+audit+`IsDeleted` → datos primarios → nav. **GREEN**. LoC: +20.
- [x] **T-4.2** Agregar a `OcupacionMapperTests.cs`: `Reconstitute_MapsAllFields`, `Reconstitute_FechaFinBeforeFechaInicio_Lanza`, `Reconstitute_EsVigenteTrueSinFechaFin`, `Reconstitute_EsVigenteFalseConFechaFin`. **GREEN** post T-4.1. LoC: +30.
- [x] **T-4.3** Reescribir `ToDomain(OcupacionEntity)` para usar `Ocupacion.Reconstitute(...)`. **GREEN**: T-1.6 ahora pasa. LoC: ±0.

## Phase 5: Persona migration (CU-5)

- [x] **T-5.1** Implementar `internal static Persona.Reconstitute(Guid id, string nombres, string apellidos, string? legajo, string? email, string? tipoDocumento, string? numeroDocumento, string? telefono, bool isActive, DateTime createdAt, string? createdByUserId, DateTime? updatedAt, string? updatedByUserId, bool isDeleted, DateTime? deletedAt, string? deletedByUserId)` en `src/SGV.Dominio/Personas/Persona.cs`. Validar con `Requerido(nombres, 100)`, `Requerido(apellidos, 100)`, `Opcional(...)` para el resto. **GREEN**. LoC: +20.
- [x] **T-5.2** Agregar a `PersonaMapperTests.cs`: `Reconstitute_MapsAllFields`, `Reconstitute_MapsAllDocumentFields`, `Reconstitute_TelefonoAsignado`, `Reconstitute_IsActiveFalsePreservaFlag`, `Reconstitute_AuditFieldsPreservados`. **GREEN** post T-5.1. LoC: +50.
- [x] **T-5.3** Reescribir `ToDomain(PersonaEntity)` (líneas ~170-200) para usar `Persona.Reconstitute(...)`. **GREEN**: T-1.5 ahora pasa. LoC: ±0.

## Phase 6: Puesto migration (CU-6)

- [x] **T-6.1** Implementar `internal static Puesto.Reconstitute(Guid id, Guid unidadOrganizativaId, Guid cargoId, Guid? puestoSuperiorId, string codigo, string nombre, string? descripcion, bool isActive, UnidadOrganizativa? unidadOrganizativa, Cargo? cargo, DateTime createdAt, string? createdByUserId, DateTime? updatedAt, string? updatedByUserId, bool isDeleted, DateTime? deletedAt, string? deletedByUserId)` en `src/SGV.Dominio/Organizacion/Puesto.cs`. Validar `Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `Opcional(descripcion, 1000)`, e invocar `CambiarPuestoSuperior(puestoSuperiorId)` para la invariante `puestoSuperiorId != Id`. **GREEN**. LoC: +22.
- [x] **T-6.2** Agregar a `PuestoMapperTests.cs`: `Reconstitute_MapsAllFields`, `Reconstitute_UnidadOrganizativaNavNull`, `Reconstitute_CargoNavNull`, `Reconstitute_IsActiveFalsePreservaFlag`. **GREEN** post T-6.1. LoC: +40.
- [x] **T-6.3** Reescribir `ToDomain(PuestoEntity)` (líneas ~115-140) para usar `Puesto.Reconstitute(...)`. **GREEN**: T-1.4 ahora pasa. LoC: ±0.

## Phase 7: UnidadOrganizativa atómica (CU-7)

> **Atomicidad obligatoria**: el cambio de UO no compila en pasos intermedios porque los mutadores pasan a `void`-return mientras el mapper y los servicios los invocan con `=`. Aplicar TODO este bloque en un solo commit.

- [x] **T-7.1** Reescribir `UnidadOrganizativa.Actualizar`/`DefinirVigencia`/`CambiarUnidadPadre`/`Activar`/`Desactivar` con `private set` + `void`-return (eliminar `with`-chain). Migrar propiedades de `init` a `private set` (paridad con las demás). Ver formas exactas en `design.md:246-282`. `Codigo` se mantiene `private set` (no `init`) para preservar la invariante "Codigo solo se asigna en el constructor". **GREEN**. LoC: +30/-25.
- [x] **T-7.2** Implementar `internal static UnidadOrganizativa.Reconstitute(Guid id, string codigo, string nombre, Guid tipoUnidadOrganizativaId, string? descripcion, Guid? unidadPadreId, DateOnly? vigenteDesde, DateOnly? vigenteHasta, bool isActive, UnidadOrganizativa? unidadPadre, TipoUnidadOrganizativa? tipoUnidadOrganizativa, DateTime createdAt, string? createdByUserId, DateTime? updatedAt, string? updatedByUserId, bool isDeleted, DateTime? deletedAt, string? deletedByUserId)` en `UnidadOrganizativa.cs`. Validar `Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `tipoUnidadOrganizativaId != Guid.Empty`, `Opcional(descripcion, 1000)`, `ValidarVigencia(vigenteDesde, vigenteHasta)`. **GREEN**. LoC: +30.
- [x] **T-7.3** Reescribir `ToDomain(UnidadOrganizativaEntity)` (`PersistenceToDomainMapper.cs:68-108`) para invocar `UnidadOrganizativa.Reconstitute(...)` directamente (eliminar la cadena `with` actual). Acceptance: el test IL existente `ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper` sigue GREEN. LoC: ±0.
- [x] **T-7.4** Actualizar `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` líneas **88, 134, 191, 230, 267** — quitar `unidad = ` en cada una: `unidad.DefinirVigencia(...)` (88), `unidad.Actualizar(...)` (134), `unidad.CambiarUnidadPadre(...)` (191), `unidad.Desactivar()` (230), `unidad.Activar()` (267). Acceptance: `dotnet build SGV.slnx` verde. LoC: ±0 (5 líneas editadas).
- [x] **T-7.5** Actualizar `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` líneas **378, 383, 408, 447** — quitar asignaciones `padre = padre.X(...)` y `hijo = hijo.X(...)`. **Descubrimiento exhaustivo**: correr `grep -rn "\.Actualizar\|\.DefinirVigencia\|\.CambiarUnidadPadre\|\.Activar\|\.Desactivar" src/ tests/` para detectar consumidores adicionales (p.ej. `tests/SGV.Tests/Dominio/Organizacion/`) y actualizarlos también. Acceptance: suite verde. LoC: ±0.
- [x] **T-7.6** Crear `tests/SGV.Tests/Persistencia/UnidadOrganizativaReconstituteTests.cs` con: `Reconstitute_MapsAllFields`, `Reconstitute_IsActiveFalsePreservaFlag`, `Reconstitute_UnidadPadreNull`, `Reconstitute_UnidadPadreHydrated`, `Reconstitute_VigenteDesdeHasta`. **GREEN** post T-7.2. LoC: +50.

## Phase 8: Cleanup + verificación final (CU-8)

- [x] **T-8.1** Eliminar helper `SetProperty<T>` (`PersistenceToDomainMapper.cs:225-232`) y `using System.Reflection;` (línea 1). **REFACTOR**. Acceptance: `grep -rn "PropertyInfo\.SetValue\|SetProperty" src/ tests/ | grep -v "TestResults"` → 0 hits. LoC: -10.
- [x] **T-8.2** Correr `dotnet build SGV.slnx` — verde, 0 errores, 0 warnings nuevos. **VERIFY**. Acceptance: build limpio.
- [x] **T-8.3** Correr `dotnet test SGV.slnx` — todas las suites verdes (Dominio, Aplicacion, Persistencia, API, Web, Compatibilidad). Acceptance: 0 failures.
- [x] **T-8.4** Verificación estructural final:
  - `grep -rn "PropertyInfo\|SetProperty" src/ tests/` → 0 hits en código (ignorar `TestResults/coverage.cobertura.xml`).
  - `grep -rn "InternalsVisibleTo" src/SGV.Dominio/` → 1 hit.
  - `git status` no debe mostrar archivos nuevos bajo `src/SGV.Infraestructura/Persistencia/Migraciones/`. **VERIFY**.

## Verification Plan

```bash
# Build completo
dotnet build SGV.slnx

# Suite completa
dotnet test SGV.slnx

# Filtrada por mapper tests (incluye los 5 IL + behavior)
dotnet test SGV.slnx --filter "FullyQualifiedName~MapperTests"

# Solo IL tests estructurales
dotnet test SGV.slnx --filter "FullyQualifiedName~NoLlamaSetPropertyReflectionHelper"

# Grep guards
grep -rn "PropertyInfo\.SetValue\|SetProperty" src/ tests/ | grep -v "TestResults"   # → 0 hits
grep -rn "InternalsVisibleTo" src/SGV.Dominio/   # → 1 hit
git status -- src/SGV.Infraestructura/Persistencia/Migraciones/   # → sin cambios
```

Criterios de aceptación agregados:

- 0 errores, 0 warnings nuevos en `dotnet build`.
- 6 IL tests verdes: `ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper` (existente) + `ToDomain_Cargo/Habilidad/Puesto/Persona/Ocupacion_NoLlamaSetPropertyReflectionHelper` (5 nuevos).
- ~22-25 tests de comportamiento verdes distribuidos en 5 archivos nuevos + 1 ampliación.
- `UnidadOrganizativaServicioComandosTests.cs` verde sin regresión tras la migración a `void`-return.
- Tests de aplicación (`Aplicacion/Organizacion/*`, `Aplicacion/Personas/*`, `Aplicacion/Ocupaciones/*`) verdes sin cambios.
- Sin migraciones EF Core nuevas.

## Rollback Plan

| Escenario | Acción |
|---|---|
| Implementación falla mid-apply | `git revert <SHA>` del CU problemático. Cada CU es atómico (producción + tests juntos), así que el revert deja el repo compilando. |
| Helper `SetProperty` reintroducido accidentalmente | El test IL de la entidad correspondiente falla en CI → `git revert <CU-N>` del mapper. |
| UO rompe compilación porConsumers no actualizados | Imposible si CU-7 se aplica atómicamente (verificación con `dotnet build SGV.slnx` post CU-7). Si ocurre, `git revert <CU-7>` restaura `with`-chain + firmas `UnidadOrganizativa`-returning. |
| Build verde pero tests rojos | Diagnosticar test por test; `git revert` por CU si es regresión. |
| Plan B extremo | `git revert` del PR entero. Como no se aplican migraciones, el revert deja el repo equivalente al estado previo (helper `SetProperty` + `using System.Reflection;` restaurados, `InternalsVisibleTo` removido de `SGV.Dominio.csproj`, `UnidadOrganizativa` con `with`-chain y firmas returning). |

Comandos de rollback granular (referencia):

```bash
git revert <SHA-CU-N>           # reversión selectiva por CU
git revert <SHA-PR>             # reversión total del PR
git checkout HEAD~1 -- src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs   # restore manual del mapper
```

## Notas finales

- **Documentación**: `docs/decisiones-implementacion.md` NO se actualiza en este change; queda diferido al `archive-report` (decisión cerrada del usuario).
- **`Cargo.Desactivar()`**: invariante `_puestos` activos NO se endurece; queda fuera de scope. Documentar en `archive-report` y abrir issue aparte.
- **Migración EF Core**: no se requiere. El refactor no toca schema, columnas, índices ni el model snapshot.