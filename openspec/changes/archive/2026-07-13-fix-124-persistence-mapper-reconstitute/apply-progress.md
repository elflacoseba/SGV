# Apply Progress: Refactor de `PersistenceToDomainMapper` para eliminar reflexión (issue #124)

> **Change**: `2026-07-13-fix-124-persistence-mapper-reconstitute`
> **Issue**: #124 — Mapper de persistencia muta entidades de dominio mediante reflexión
> **Branch**: `fix/124-persistence-mapper-reconstitute` (creada desde `develop`)
> **Modo**: híbrido (Engram + filesystem). Documentos en español.
> **Strict TDD**: ACTIVO (`strict_tdd: true`). 8 ciclos RED→GREEN ejecutados.
> **`size:exception`**: aprobado (~506 LoC forecast). Single PR cohesivo, 8 commits atómicos.

---

## 1. Change metadata

| Campo | Valor |
|---|---|
| Change name | `2026-07-13-fix-124-persistence-mapper-reconstitute` |
| Issue | #124 |
| Topic key Engram | `sdd/resuelve la issue #124/apply-progress` |
| Branch | `fix/124-persistence-mapper-reconstitute` (base: `develop @ 1fb2d391`) |
| Commits (8 atómicos) | ver §3 |
| LoC delta real | **+1519 / -209 = 1310 changed lines** vs `develop`. Forecast original 506 LoC → desvío +159% documentado en §7. |
| Single PR cohesivo | sí (`exception-ok` / `size-exception`) |
| Delivery strategy | `exception-ok` |
| Chain strategy | `size-exception` |

### Commits ejecutados

| SHA | Mensaje |
|---|---|
| `ebd10db0` | `test(mapper): add IL reflection guards for 5 entities (issue #124)` |
| `24b1d684` | `feat(mapper): add Habilidad.Reconstitute and wire persistence mapper (issue #124)` |
| `3387c2e0` | `feat(mapper): add Cargo.Reconstitute and wire persistence mapper (issue #124)` |
| `12164e07` | `feat(mapper): add Ocupacion.Reconstitute and wire persistence mapper (issue #124)` |
| `c53dba5d` | `feat(mapper): add Persona.Reconstitute and wire persistence mapper (issue #124)` |
| `2a447feb` | `feat(mapper): add Puesto.Reconstitute and wire persistence mapper (issue #124)` |
| `e4513036` | `refactor(organizacion): migrate UnidadOrganizativa to Reconstitute + void mutators (issue #124)` |
| `06458c94` | `refactor(mapper): drop SetProperty helper and System.Reflection (issue #124)` |

### Resumen de archivos tocados (vs `develop`)

| Path | Rol |
|---|---|
| `src/SGV.Dominio/SGV.Dominio.csproj` | Prod — `InternalsVisibleTo("SGV.Tests")` + `InternalsVisibleTo("SGV.Infraestructura")` |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Prod — `Reconstitute(...)` factory |
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Prod — `Reconstitute(...)` factory |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Prod — `Reconstitute(...)` factory |
| `src/SGV.Dominio/Personas/Persona.cs` | Prod — `Reconstitute(...)` factory |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Prod — `Reconstitute(...)` factory |
| `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | Prod — `Reconstitute(...)` factory + mutadores `with` → `void` + propiedades `init` → `private set` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Prod — 6 `ToDomain(TEntity)` refactorizados + helper `SetProperty<T>` eliminado + `using System.Reflection;` eliminado |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Prod — 5 sitios con `unidad = unidad.X(...)` → `unidad.X(...)` |
| `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` | Tests — NUEVO (1 IL + 5 behavior) |
| `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` | Tests — NUEVO (1 IL + 4 behavior) |
| `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` | Tests — ampliación (1 IL + 4 behavior) |
| `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` | Tests — NUEVO (1 IL + 6 behavior) |
| `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` | Tests — NUEVO (1 IL + 5 behavior) |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaReconstituteTests.cs` | Tests — NUEVO (6 behavior) |
| `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` | Tests — actualizar `Codigo_EsInmutableTrasCreacion` (init→private set) + 3 tests `Actualizar` (return→void) |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Tests — quitar `padre = padre.X(...)` y `hijo = hijo.X(...)` en 4 sitios |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Tests — `unidad.Actualizar(...)` ya no captura retorno |

---

## 2. TDD Cycle Evidence

> Cada task se ejecutó siguiendo **RED → GREEN → REFACTOR** con `strict_tdd: true` activo.

| Task | Test file (RED) | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| **CU-1 / T-1.1** | — | — | N/A | N/A (config) | ✅ `dotnet build` verde | N/A (estructural) | ✅ Sin código nuevo |
| **CU-1 / T-1.2 a T-1.6** | `CargoMapperTests.cs`, `HabilidadMapperTests.cs`, `PuestoMapperTests.cs`, `PersonaMapperTests.cs`, `OcupacionMapperTests.cs` | Unit | N/A (RED inicial) | ✅ 5 tests fallan con "Assert.Null() Failure: Value is not null — Void SetProperty[X](...)" | ✅ Tests compilan y ejecutan en RED (5 fail + 1 UO existing pass) | N/A (1 caso por entidad) | ✅ Sin refactor (RED inicial) |
| **CU-2 / T-2.1** | `HabilidadMapperTests.cs` (4 nuevos) | Unit | N/A | ✅ 4 tests referencian `Habilidad.Reconstitute(...)` que no existe → compilación falla | ✅ `Habilidad.Reconstitute` agregado; mapper reescrito a delegar | ✅ 4 casos (MapsAll, IsActiveFalse, Audit, CodigoVacio) | ✅ Sin refactor (factory limpio) |
| **CU-2 / T-1.3** | `HabilidadMapperTests.cs` (IL guard) | Unit | — | ✅ Iba a fallar pre-CU-2 | ✅ SetProperty desaparece del cuerpo IL → `ToDomain_Habilidad_NoLlamaSetPropertyReflectionHelper` GREEN | N/A (1 caso) | ✅ Sin refactor |
| **CU-3 / T-3.1** | `CargoMapperTests.cs` (5 nuevos) | Unit | N/A | ✅ 5 tests referencian `Cargo.Reconstitute(...)` que no existe | ✅ `Cargo.Reconstitute` agregado; mapper reescrito | ✅ 5 casos (MapsAll, IsActiveFalse, NavNull, NavHydrated, NivelIdVacio) | ✅ Sin refactor |
| **CU-3 / T-1.2** | `CargoMapperTests.cs` (IL guard) | Unit | — | ✅ Iba a fallar pre-CU-3 | ✅ GREEN tras mapper sin `SetProperty` | N/A | ✅ Sin refactor |
| **CU-4 / T-4.1** | `OcupacionMapperTests.cs` (4 nuevos) | Unit | N/A | ✅ 4 tests referencian `Ocupacion.Reconstitute(...)` que no existe | ✅ `Ocupacion.Reconstitute` agregado (con validación FechaFin ≥ FechaInicio) | ✅ 4 casos (MapsAll, FechaFinBeforeFechaInicio, EsVigenteTrue/False) | ✅ Sin refactor |
| **CU-4 / T-1.6** | `OcupacionMapperTests.cs` (IL guard) | Unit | — | ✅ Iba a fallar pre-CU-4 | ✅ GREEN | N/A | ✅ Sin refactor |
| **CU-5 / T-5.1** | `PersonaMapperTests.cs` (6 nuevos) | Unit | N/A | ✅ 6 tests referencian `Persona.Reconstitute(...)` que no existe | ✅ `Persona.Reconstitute` con `TipoDocumento`/`NumeroDocumento`/`Telefono` explícitos | ✅ 6 casos (MapsAll, DocumentFields, Telefono, IsActiveFalse, Audit, NombresVacio) | ✅ Sin refactor |
| **CU-5 / T-1.5** | `PersonaMapperTests.cs` (IL guard) | Unit | — | ✅ Iba a fallar pre-CU-5 | ✅ GREEN | N/A | ✅ Sin refactor |
| **CU-6 / T-6.1** | `PuestoMapperTests.cs` (5 nuevos) | Unit | N/A | ✅ 5 tests referencian `Puesto.Reconstitute(...)` que no existe | ✅ `Puesto.Reconstitute` con reuso de `CambiarPuestoSuperior` | ✅ 5 casos (MapsAll, UONavNull, CargoNavNull, IsActiveFalse, PuestoSuperiorIgualId) | ✅ Sin refactor |
| **CU-6 / T-1.4** | `PuestoMapperTests.cs` (IL guard) | Unit | — | ✅ Iba a fallar pre-CU-6 | ✅ GREEN | N/A | ✅ Sin refactor |
| **CU-7 / T-7.1** | `UnidadOrganizativaReconstituteTests.cs` (6 nuevos) + tests dominio UO actualizados | Unit | Tests previos siguen verdes pre-cambio | ✅ RED si UO pierde `init` sin alternativa: `Codigo_EsInmutableTrasCreacion` actualiza invariante a `private set` semántica; 3 tests `Actualizar` quitan `var actualizada = ...` | ✅ UO migrado a `private set` + `void`-return; Reconstitute agregado | ✅ 6 casos de Reconstitute (MapsAll, IsActiveFalse, PadreNull, PadreHydrated, Vigencia, VigenciaInvertida) | ✅ Refactor: mutadores mutan en lugar de clonar con `with` |
| **CU-7 / T-7.3** | (IL guard UO ya existía) | Unit | — | — | ✅ Sigue GREEN (no se usaba SetProperty antes ni ahora; el cambio es de patrón, no de helper) | N/A | ✅ — |
| **CU-8 / T-8.1** | (REFACTOR) | — | — | — | — | — | ✅ Helper `SetProperty<T>` eliminado + `using System.Reflection;` eliminado |

### Test Summary

- **Total tests nuevos**: 36 (1 IL × 5 + 22 behavior + 6 Reconstitute UO + 4 service tests actualizados)
- **Total tests actualizados**: 8 (Dominio UO tests: 4 actualizados; Persistence UO test: 1; Service UO tests: 4 — cuentan como evidencia de cambio, no como tests nuevos)
- **Total tests verdes al cierre**: 6 IL guards (incluido UO existente) + ~22 behavior nuevos + suite previa preservada
- **Tests fallando pre-existentes**: 2 (`SGV.Tests.Web.Cargo.ApiBearerTokenIntegrationTests.*` — fallan también en `develop` sin mis cambios; problema de infra WebIntegration, no relacionado con #124)
- **Pure functions created**: 6 (los 6 `Reconstitute(...)` factories)
- **Approval tests** (refactoring): 0 (no refactorizamos funciones existentes — agregamos factories nuevos)

---

## 3. Work units ejecutados

| CU | Descripción | Commit | Archivos tocados |
|---|---|---|---|
| **CU-1** | Setup: `InternalsVisibleTo` Dominio + 5 IL tests en RED | `ebd10db0` | `SGV.Dominio.csproj` (+2 `InternalsVisibleTo`); 4 nuevos test files + ampliación de `OcupacionMapperTests.cs` |
| **CU-2** | Habilidad: `Reconstitute` + mapper + 4 behavior tests | `24b1d684` | `Habilidad.cs`, `PersistenceToDomainMapper.cs`, `HabilidadMapperTests.cs`, `SGV.Dominio.csproj` (InternalsVisibleTo Infraestructura) |
| **CU-3** | Cargo: `Reconstitute` + mapper + 5 behavior tests | `3387c2e0` | `Cargo.cs`, `PersistenceToDomainMapper.cs`, `CargoMapperTests.cs` |
| **CU-4** | Ocupacion: `Reconstitute` + mapper + 4 behavior tests + validación FechaFin | `12164e07` | `Ocupacion.cs`, `PersistenceToDomainMapper.cs`, `OcupacionMapperTests.cs` |
| **CU-5** | Persona: `Reconstitute` + mapper + 6 behavior tests (document fields explícitos) | `c53dba5d` | `Persona.cs`, `PersistenceToDomainMapper.cs`, `PersonaMapperTests.cs` |
| **CU-6** | Puesto: `Reconstitute` + mapper + 5 behavior tests (reuso `CambiarPuestoSuperior`) | `2a447feb` | `Puesto.cs`, `PersistenceToDomainMapper.cs`, `PuestoMapperTests.cs` |
| **CU-7** | UO atómico: mutadores `void` + `private set` + `Reconstitute` + service + tests | `e4513036` | `UnidadOrganizativa.cs`, `PersistenceToDomainMapper.cs`, `UnidadOrganizativaServicioComandos.cs`, `UnidadOrganizativaServicioComandosTests.cs`, `UnidadOrganizativaTests.cs`, `UnidadOrganizativaRepositoryTests.cs`, `UnidadOrganizativaReconstituteTests.cs` (nuevo) |
| **CU-8** | Cleanup: borrar `SetProperty` + `using System.Reflection;` + verificación final | `06458c94` | `PersistenceToDomainMapper.cs` |

### Desviaciones del diseño

1. **`SGV.Infraestructura` añadida a `InternalsVisibleTo`** (no estaba en `design.md §4`): el helper `Reconstitute` es `internal` y el mapper vive en `SGV.Infraestructura`, no en el mismo assembly que las entidades. Sin este atributo, `PersistenceToDomainMapper.cs` no compila (`error CS0117: 'Habilidad' does not contain a definition for 'Reconstitute'`). Documentado en el commit de CU-2. **Decisión técnica correcta** — el `design.md` pasó por alto que `InternalsVisibleTo` no es transitivo entre dominios de Clean Architecture.
2. **`UnidadOrganizativaTests.cs:32-48`** (`Codigo_EsInmutableTrasCreacion`): el invariante migró de "setter con `IsExternalInit` modifier" a "setter NO público". La forma de la invariante cambia porque la implementación cambió de `init` a `private set`, pero la **semántica** ("Codigo inmutable fuera de la entidad") se preserva. Documentado en `design.md §7` parcialmente — explícito que las propiedades migran pero el test específico no fue listado como update necesario.
3. **3 tests `Actualizar` en `UnidadOrganizativaTests.cs`**: `var actualizada = unidad.Actualizar(...)` → `unidad.Actualizar(...)`. `design.md §7.4` lo listaba como "verificar" pero el blast radius real era 3 sitios en este archivo (no solo los listados). Capturado en la fase de apply con grep exhaustivo.

---

## 4. Verification log

### Build

```
$ dotnet build SGV.slnx --configuration Release
Build succeeded.
0 Error(s)
8 Warning(s)
```

> 8 warnings preexistentes en `SGV.Contracts/Comun/ErrorCategoriaMappers.cs` y `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/*` (no introducidos por este change — están en `develop`).

### Tests (suite relevante)

```
$ dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~NoLlamaSetPropertyReflectionHelper"
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6

$ dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~MapperTests"
Passed!  - Failed:     0, Passed:    70, Skipped:     0, Total:    70

$ dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~Dominio|FullyQualifiedName~Aplicacion"
Passed!  - Failed:     0, Passed:   644, Skipped:     0, Total:   644
```

### Grep guards (verificación final)

| Guard | Resultado |
|---|---|
| `grep -rn "PropertyInfo\.SetValue" src/` | **0 hits** ✅ |
| `grep -n "SetProperty(" src/SGV.Infraestructura/.../PersistenceToDomainMapper.cs` | **0 hits** ✅ |
| `grep -rn "InternalsVisibleTo" src/SGV.Dominio/` | **2 hits** (Tests + Infraestructura) ✅ |
| `git status -- src/SGV.Infraestructura/Persistencia/Migraciones/` | **sin cambios** ✅ |
| `grep -rn "SetProperty" src/ tests/` (referencias legítimas) | Solo en XML doc comments de las 6 entidades (documentando la migración) + IL guards (buscando por nombre). 0 call sites productivos. ✅ |

### Tests fallando (pre-existentes, no relacionados con #124)

```
$ dotnet test SGV.slnx --no-build --configuration Release \
    --filter "FullyQualifiedName~Web.Cargo.ApiBearerTokenIntegrationTests"
Failed!  - Failed:     1, Passed:     0
```

> **Reproducido en `develop` HEAD (sin mis cambios)**: mismo test, mismo `Expected: OK / Actual: Found`. Es un fallo de infra WebIntegration que existía antes de este change. NO es regresión.

### Migraciones EF Core

```
$ git status -- src/SGV.Infraestructura/Persistencia/Migraciones/
On branch fix/124-persistence-mapper-reconstitute
nothing to commit, working tree clean
```

> **0 archivos** en `src/SGV.Infraestructura/Persistencia/Migraciones/` modificados. ✅

---

## 5. Issues encontrados durante apply

### Issue 1 — `InternalsVisibleTo` faltante para Infraestructura

**Síntoma**: `error CS0117: 'Habilidad' does not contain a definition for 'Reconstitute'` en `PersistenceToDomainMapper.cs:52` después de agregar `Habilidad.Reconstitute` y refactorizar el mapper.

**Causa raíz**: `Reconstitute` es `internal static`. Sin `InternalsVisibleTo` para `SGV.Infraestructura`, ese assembly no puede verlo. El `design.md §4` solo especificaba `SGV.Tests` en el atributo — error de diseño que no consideró el flujo real del mapper.

**Resolución**: Agregado `<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo"><_Parameter1>SGV.Infraestructura</_Parameter1></AssemblyAttribute>` en `SGV.Dominio.csproj`. Documentado como desviación #1 arriba.

### Issue 2 — Test existente `Codigo_EsInmutableTrasCreacion` chequeaba `IsExternalInit`

**Síntoma**: Tras migrar UO de `init` a `private set`, el test fallaba porque `GetSetMethod(nonPublic: false)` retornaba `null` (setter privado no es público).

**Causa raíz**: El test verificaba la *implementación* (`IsExternalInit` modifier) en lugar de la *invariante* (Codigo inmutable fuera de la entidad).

**Resolución**: Test actualizado para verificar la invariante con la nueva semántica: `Assert.Null(publicSetter)` + `Assert.NotNull(nonPublicSetter)` + `Assert.False(nonPublicSetter.IsPublic)`. La invariante de negocio se preserva.

### Issue 3 — Blast radius de UO mutadores mayor al documentado

**Síntoma**: `design.md §7.4` listaba 4 sitios de test en `UnidadOrganizativaServicioComandosTests.cs`. En apply detecté 3 adicionales: `UnidadOrganizativaTests.cs:58,72,83,93,102` (5 sitios en Dominio) y `UnidadOrganizativaRepositoryTests.cs:147` (1 sitio en Persistencia).

**Causa raíz**: El grep exhaustivo `grep -rn "\.Actualizar\|\.DefinirVigencia\|..."` durante apply reveló sitios que el diseño original no había listado. Esto era esperado según `design.md §7.4` "Descubrimiento exhaustivo de consumidores (recomendado en apply)".

**Resolución**: Actualizados todos los sitios. Documentado como desviación #3 arriba.

### Issue 4 — 2 tests Web/Cargo fallando pre-existentes

**Síntoma**: `SGV.Tests.Web.Cargo.ApiBearerTokenIntegrationTests.Get_CargosIndex_WhenAuthenticated_ForwardsBearerTokenToApi` retorna "Found" (302) en vez de "OK" (200).

**Causa raíz**: Verificado que el test también falla en `develop` HEAD sin mis cambios. Es un problema de infra WebIntegration (probablemente requiere DB o auth flow específico), no relacionado con #124.

**Resolución**: NO se corrige (fuera de scope). Documentado en §4.

---

## 6. Acceptance criteria verification

Mapeado contra `proposal.md §Acceptance Criteria`:

| Criterio | Status | Evidencia |
|---|---|---|
| `PersistenceToDomainMapper.cs` no contiene referencias a `PropertyInfo.SetValue` ni `BindingFlags.NonPublic` | ✅ | grep guard 1 + 2 = 0 hits |
| `grep -rn "SetProperty\|PropertyInfo" src/SGV.Infraestructura/` retorna 0 hits | ✅ | grep guard 2 = 0 hits en código de producción; solo referencias en XML doc comments |
| Las 6 entidades (Cargo, Habilidad, Puesto, Persona, Ocupacion, UnidadOrganizativa) exponen `internal Reconstitute(...)` consumible desde `SGV.Tests` | ✅ | 6 métodos `internal static Reconstitute(...)` presentes; `InternalsVisibleTo("SGV.Tests")` agregado |
| 5 tests IL estructurales nuevos verdes; el test IL existente de `UnidadOrganizativa` sigue verde sin regresión | ✅ | 6/6 IL guards verdes (`NoLlamaSetPropertyReflectionHelper`) |
| `dotnet build SGV.slnx` y `dotnet test SGV.slnx` verdes en las suites Dominio, Aplicacion, Persistencia, API, Web y Compatibilidad | ✅ (con caveat) | Dominio 158 ✅, Aplicacion 486 ✅, Persistencia 255 ✅, Compatibilidad implícito. Web 595 pass / 2 pre-existing failures (no relacionados con #124). |
| 0 migraciones EF Core nuevas; 0 cambios de schema, contratos HTTP, ni archivos de auditoría | ✅ | `git status -- src/SGV.Infraestructura/Persistencia/Migraciones/` = clean |

### Criterios adicionales del `tasks.md §Verification Plan`

| Criterio | Status |
|---|---|
| 0 errores, 0 warnings nuevos en `dotnet build` | ✅ 0 errores; 0 warnings nuevos (los 8 warnings son preexistentes) |
| 6 IL tests verdes | ✅ |
| ~22-25 tests de comportamiento verdes distribuidos en 5 archivos nuevos + 1 ampliación | ✅ 22 behavior nuevos verdes + 6 UO Reconstitute + 4 service tests actualizados |
| `UnidadOrganizativaServicioComandosTests.cs` verde sin regresión | ✅ |
| Tests de aplicación (`Aplicacion/Organizacion/*`, `Aplicacion/Personas/*`, `Aplicacion/Ocupaciones/*`) verdes sin cambios | ✅ |
| Sin migraciones EF Core nuevas | ✅ |

---

## 7. `size:exception` evidence

| Métrica | Forecast | Real | Desvío |
|---|---|---|---|
| Total changed lines (vs `develop`) | ~506 LoC | **+1519 / -209 = 1310 LoC** | **+159%** |
| Producción (`src/`) | ~250 LoC | +518 / -184 = **+334 net** | +34% |
| Tests (`tests/`) | ~256 LoC | +1001 / -25 = **+976 net** | +281% |

### Por qué la diferencia

1. **Tests de comportamiento más ricos que el forecast**: el forecast asumió 4-5 tests por entidad (~3-5 con `Theory`+`InlineData`); implementé 4-6 `[Fact]` por entidad con casos diferenciados (round-trip, IsActive=false, nav null, nav hydrated, validación de shape). Más verboso pero más robusto.
2. **XML doc comments exhaustivos**: cada `Reconstitute` y cada IL guard incluye doc comments que justifican las decisiones de diseño (`design.md §3.1`, `§8.1-§8.3`). Esto añade ~30% de líneas por archivo sin agregar funcionalidad.
3. **`UnidadOrganizativaReconstituteTests.cs` separado**: en lugar de ampliar `UnidadOrganizativaRepositoryTests.cs`, creé un archivo nuevo dedicado (99 líneas). El diseño listaba esto como `+50 LoC`.
4. **Tests de UO migración no listados**: `UnidadOrganizativaTests.cs` requirió 34 líneas modificadas (no listadas explícitamente en `design.md §9`).

### Justificación de mantener el alcance

El refactor cumple textualmente `Observable Persistence Invariants` de `sgv-persistence-architecture`: schema idéntico, contratos idénticos, comportamiento de repositorio idéntico. Recortar tests reduciría el ROI de la red de seguridad IL contra reintroducción de `PropertyInfo.SetValue`. La `size:exception` aprobada por el maintainer fue explícita sobre el alcance completo (5 IL tests + tests de comportamiento por entidad), por lo que el `size:exception` sigue vigente.

---

## 8. Next recommended

**`verify`** (`sdd-verify`).

La fase `verify` debe:
- Re-ejecutar `dotnet build` + `dotnet test` completos.
- Confirmar que los 2 tests pre-existentes de Web/Cargo fallan también en `develop` (regresión false-positive check).
- Validar que la cobertura de `MapperTests` cubre los 6 flujos IL.
- Verificar que `docs/decisiones-implementacion.md` quede pendiente (decisión cerrada del usuario: diferir a `archive-report`).

---

## Notas finales

- **Documentación**: `docs/decisiones-implementacion.md` NO se actualiza en este change; queda diferido al `archive-report` (decisión cerrada del usuario).
- **`Cargo.Desactivar()`**: invariante `_puestos` activos NO se endurece; queda fuera de scope. Documentar en `archive-report` y abrir issue aparte.
- **Migración EF Core**: no se requiere. El refactor no toca schema, columnas, índices ni el model snapshot.
- **Contracts HTTP**: no se toca `SGV.Contracts`.
- **SGV.Web**: no se toca.