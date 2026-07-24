# Exploración: refactor `PersistenceToDomainMapper` para no usar reflexión (issue #124)

**Issue GitHub**: #124 — "Mapper de persistencia muta entidades de dominio mediante reflexión"
**Change**: `2026-07-13-fix-124-persistence-mapper-reconstitute`
**Modo**: exploratorio — solo investigación, sin código, sin migración, sin tests
**Artifact store**: híbrido — Engram topic key `sdd/resuelve la issue #124/explore` + filesystem en `openspec/changes/2026-07-13-fix-124-persistence-mapper-reconstitute/exploration.md`
**Strict TDD**: ACTIVO. Ver `openspec/config.yaml:11`. Tests RED antes de implementación cuando llegue a `sdd-spec`/`sdd-tasks`.

---

## Estado actual verificable

### A.1 — El helper `SetProperty` y todos sus call sites

`src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs:225-232`:

```csharp
private static void SetProperty<T>(T target, string propertyName, object? value)
    where T : EntidadBase
{
    var property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"No se encontró la propiedad '{propertyName}' en {typeof(T).Name}.");
    property.SetValue(target, value);
}
```

**Por qué importa**: `PropertyInfo.SetValue(object, object?)` envuelve el campo de respaldo IL y **salta** el chequeo del modifier `IsExternalInit` en runtime (`docs/decisiones-implementacion.md:78-88` documenta este razonamiento para `UnidadOrganizativa`). Si una entidad migración a `record` con `init`-only en el futuro, el helper SetProperty la rompería en silencio.

**12 call sites en el mapper** (todos `SetProperty(...)` directos sin pasar por `nameof` indirecto):

| Línea | Entidad destino | Propiedad seteada | Valor (source) |
|---|---|---|---|
| 31 | `Cargo` | `IsActive` | `entity.IsActive` |
| 35 | `Cargo` | `NivelCargo` (nav) | `ToDomain(entity.NivelCargo)` |
| 64 | `Habilidad` | `IsActive` | `entity.IsActive` |
| 125 | `Puesto` | `IsActive` | `entity.IsActive` |
| 129 | `Puesto` | `UnidadOrganizativa` (nav) | `ToDomain(entity.UnidadOrganizativa)` |
| 134 | `Puesto` | `Cargo` (nav) | `ToDomain(entity.Cargo)` |
| 190 | `Persona` | `IsActive` | `entity.IsActive` |
| 191 | `Persona` | `Telefono` | `entity.Telefono` |
| 192 | `Persona` | `TipoDocumento` | `entity.TipoDocumento` |
| 193 | `Persona` | `NumeroDocumento` | `entity.NumeroDocumento` |
| 214 | `Ocupacion` | `Persona` (nav) | `ToDomain(entity.Persona)` |
| 219 | `Ocupacion` | `Puesto` (nav) | `ToDomain(entity.Puesto)` |

Línea 228 es la única que efectivamente invoca `BindingFlags.NonPublic`/`PropertyInfo.SetValue` — los demás son call sites del helper. **Cero usos de `SetProperty` fuera de este archivo** (`grep` en `src/` confirma: ningún otro consumidor; `tests/` solo cobertura XML histórica en `TestResults/`).

### A.2 — La excepción documentada: `UnidadOrganizativa` (PR2)

`PersistenceToDomainMapper.cs:68-108` ya NO usa `SetProperty`. Lo resuelve con:

- Constructor primario con todos los campos lógicos (`Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `Descripcion`, `UnidadPadreId`).
- Para `UnidadPadre`, `TipoUnidadOrganizativa`: encadena `this with { ... }` (líneas 94, 99).
- Para `VigenteDesde`/`VigenteHasta`: invoca `DefinirVigencia(...)` (que también devuelve `with`) y luego encadena otro `with { IsActive = ... }` para que el flag `IsActive` se asigne vía compilador (líneas 106-107).
- Comentario explícito en líneas 70-74: documenta que `SetProperty` evita el chequeo `IsExternalInit` en runtime, mientras que `with` lo respeta.

**Test estructural de cobertura** (`tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs:984-1045`): recorre el IL del método `ToDomain(UnidadOrganizativaEntity)`, decodifica tokens de métodos llamados y **falla si alguien re-introduce `SetProperty`** en ese método. Usa el patrón estándar `MethodInfo.GetMethodBody().GetILAsByteArray()` + walk de opcodes `0x28 (call)` y `0x6F (callvirt)` resolviendo cada token.

### A.3 — Entidades de dominio afectadas (estado actual)

Para cada entidad: tipo de declaración, propiedades candidatas a reconstitución, patrón actual.

#### `src/SGV.Dominio/Organizacion/Cargo.cs`

- `public sealed record class Cargo : EntidadAuditable` — extiende record, no class.
- Init/private setters: `Codigo`/`Nombre`/`Descripcion`/`NivelId` tienen `private set` (líneas 30, 32, 34, 39). `NivelCargo` nav `private set` (línea 44). `IsActive` `private set` (línea 46).
- Ctor primario (líneas 15-23): `(codigo, nombre, nivelId, descripcion?)`, valida con `ValidacionesDominio.Requerido`, fija `IsActive = true`.
- **Bypass del mapper**: `NivelCargo` nav (línea 35) e `IsActive` (línea 31).
- `Actualizar(codigo, nombre, nivelId, descripcion)` (líneas 62-69) — sí permite mutar `Codigo` (decisión vigente de spec implícita para Cargos).
- `Desactivar()` (líneas 75-84): valida que no haya `_puestos.Count > 0 && _puestos.Any(p => p.IsActive)`. **Esto es una invariante que el mapper SILENCIA cuando reconstituye un Cargo `IsActive=false`** — porque hoy la reflección solo setea `IsActive` sin disparar la lógica de validación (que no existe, solo verifica el flag).
- `private static ValidarNivelId(Guid)` (líneas 107-113): rechaza `Guid.Empty`.

#### `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` (precedente)

- `public sealed record class UnidadOrganizativa : EntidadAuditable`.
- **Todas las propiedades lógicas son `init`** (líneas 43-61) excepto `UnidadPadreId?`, `UnidadPadre?`, `TipoUnidadOrganizativaId`, `TipoUnidadOrganizativa?`, `VigenteDesde?`, `VigenteHasta?`, `IsActive`.
- Mutaciones por `with`-chain: `Actualizar` (71-102), `DefinirVigencia` (107-111), `CambiarUnidadPadre` (117-126), `Activar` (131), `Desactivar` (136).
- **Es el patrón a generalizar.**

#### `src/SGV.Dominio/Organizacion/Puesto.cs`

- `public sealed record class Puesto : EntidadAuditable`.
- Setters: la mayoría `private set` (líneas 31-49). `UnidadOrganizativa` nav y `Cargo` nav ambos `private set` (33, 37).
- Ctor primario (líneas 17-29) acepta `(unidadOrganizativaId, cargoId, codigo, nombre, puestoSuperiorId?, descripcion?)`. Valida que las FK no sean `Guid.Empty`.
- **Bypass del mapper**: `IsActive`, `UnidadOrganizativa` nav, `Cargo` nav (líneas 125, 129, 134).
- El mapper llama `puesto.CambiarDatos(entity.Codigo, entity.Nombre, entity.Descripcion)` (línea 124) **antes** de los `SetProperty`, así que reconstitución debe respetar ese orden: reasignar datos → setear `IsActive` → inyectar nav properties.
- `Actualizar` (77-82) NO toca `Codigo` (decisión asimétrica con `Cargo`).
- `CambiarPuestoSuperior` (64-72): invariante `puestoSuperiorId != Id`.

#### `src/SGV.Dominio/Personas/Persona.cs`

- `public sealed record class Persona : EntidadAuditable`.
- Setters `private set` para `Legajo`, `Nombres`, `Apellidos`, `Email`, `TipoDocumento`, `NumeroDocumento`, `Telefono`, `IsActive` (líneas 21-35).
- Ctor primario (15-19): `(nombres, apellidos, legajo?, email?)` llama `CambiarDatos` y fija `IsActive = true`.
- **Bypass del mapper**: `IsActive`, `Telefono`, `TipoDocumento`, `NumeroDocumento` (líneas 190-193). Nota: `Telefono`, `TipoDocumento`, `NumeroDocumento` tienen `private set` pero el ctor NO los asigna (solo `CambiarDatos` los asigna con `ValidacionesDominio.Opcional`); el mapper reconstituye desde persistencia sin disparar validación.
- `CambiarDatos(nombres, apellidos, legajo?, email?, telefono?)` (líneas 41-48): único setter para esos campos.
- `CambiarDocumento(tipoDocumento?, numeroDocumento?)` (líneas 50-54): único setter para documento.

#### `src/SGV.Dominio/Habilidades/Habilidad.cs`

- `public sealed record class Habilidad : EntidadAuditable`.
- Setters `private set`: `Codigo`, `Nombre`, `Descripcion`, `Categoria`, `IsActive` (líneas 22-30).
- Ctor primario (11-15): `(codigo, nombre, categoria?, descripcion?)`, llama `CambiarDatos`, fija `IsActive = true`.
- **Bypass del mapper**: solo `IsActive` (línea 64).
- `CambiarDatos(codigo, nombre, categoria?, descripcion?)` (líneas 36-42): única vía de mutación.

#### `src/SGV.Dominio/Ocupaciones/Ocupacion.cs`

- `public sealed record class Ocupacion : EntidadAuditable`.
- Setters `private set`: todas excepto `Persona` nav, `Puesto` nav que también son `private set` (28-42).
- Ctor primario (13-26): `(personaId, puestoId, fechaInicio, tipoAsignacion, fechaFin?, observaciones?)`. **Validación temporal** en línea 15-18: lanza `InvalidOperationException` si `fechaFin < fechaInicio`.
- **Bypass del mapper**: `Persona` nav y `Puesto` nav (líneas 214, 219). Nota: Ocupacion NO tiene `IsActive` propio — usa `EsVigente => FechaFin is null && !IsDeleted` (línea 48).
- `Actualizar` (73-82) y `Finalizar` (88-103) requieren `EsVigente`. La reconstitución desde persistencia **puede** caer sobre una Ocupacion con `FechaFin != null` (es un estado válido persistido), así que el ctor primario actual acepta eso sin chistar — pero esto es **frágil**: si en el futuro `Ocupacion` se volviera record con `init`, el patrón del mapper debería poder reconstituir incluso estados finalizados.
- `EliminarLogicamente` (109-115), `Reactivar` (122-134): invariantes de transición que el mapper no toca.

### A.4 — Tests existentes relevantes

| Path | Cubierto |
|---|---|
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs:984-1045` | ✅ Test estructural IL que falla si `SetProperty` se reintroduce en `ToDomain(UnidadOrganizativaEntity)`. **Es el único guard de este tipo hoy.** |
| `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` (8 `[Fact]`) | ✅ Comportamiento del mapper `ToDomain`/`ToEntity`/`UpdateEntity` para Ocupacion. Cubre `MapPersistenceToDomain_Active_MapsAllFields`, `_Finalized_MapsFechaFinAndNotVigente`, `_Deleted_MapsIsDeletedAndNotVigente`, `_IncludesNavigationProperties`, y la ronda `MapDomainToEntity_*`. **NO existe equivalente para Cargo/Habilidad/Puesto/Persona.** |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs:995-1045` | ✅ Test estructural para UO + tests de repository que cubren round-trip DB. |

NO existen tests estructurales IL para `ToDomain(CargoEntity)`, `ToDomain(HabilidadEntity)`, `ToDomain(PuestoEntity)`, `ToDomain(PersonaEntity)`, ni `ToDomain(OcupacionEntity)`. La cobertura de comportamiento del mapper se limita a `OcupacionMapperTests` (8 tests) y al round-trip end-to-end de cada repo (`CargoRepositoryTests`, `PuestoRepositoryTests`, `PersonaRepositoryTests`, `HabilidadRepositoryTests`, `UnidadOrganizativaRepositoryTests`). El refactor puede romper un `SetProperty` y solo lo detectarían los round-trip tests — lentamente, en escenarios que efectivamente muten.

### A.5 — Cómo se inicializan las entidades hoy

**Patrón actual en el repo**: cada entidad del Dominio declara:

1. Constructor privado parameterless (`private Cargo() {}` líneas 11-13) — vestigial, no lo usa EF Core porque la persistencia mapea tipos `*Entity` separados.
2. Constructor primario público con validación de invariantes.
3. Métodos de mutación (`Actualizar`, `CambiarDatos`, `Desactivar`, etc.) que exponen lógica de transición.

El path de hidratación desde MySQL es:

```
MySQL → EF Core → *Entity (con public set) → PersistenceToDomainMapper.ToDomain(entity)
                                                            ↓
                                          new Dominio(args...) { Id = entity.Id, ... }
                                                            ↓
                                          SetProperty(entidad, prop, valor) ← ACÁ USA REFLEXIÓN
```

EF Core nunca instancia las entidades de Dominio. El round-trip Dominio → MySQL (`DomainToPersistenceMapper.ToEntity(...)`) tampoco usa reflexión: usa object-initializer con `public set` sobre `*Entity`.

### A.6 — Migraciones / auditoría / soft-delete

**Migraciones pendientes**: ninguna detectable. Última migración archivada: `20260711181615_FixActivePuestoIdUniqueType.cs`. No se necesita nueva migración EF Core porque **el refactor solo cambia clases C# de Dominio + `PersistenceToDomainMapper`**, no toca columnas, shadow properties, índices ni el model snapshot.

**Auditoría** (`AuditoriaSaveChangesInterceptor.cs`):
- Intercepta `SavingChanges(Sync|Async)`, itera `ChangeTracker.Entries()`, setea `CreatedAt`/`CreatedByUserId`/`UpdatedAt`/`UpdatedBy*`/`DeletedAt`/`DeletedByUserId`/`IsDeleted` directamente sobre `AuditableEntityBase` (líneas 67-83).
- Trabaja sobre `*Entity` (la persistencia), no toca Dominio. **No se ve afectada por el refactor.**
- Las entidades de Dominio se construyen vía mapper para **lectura** (repositorios con `AsNoTracking()`); nunca pasan por el interceptor en `SaveChanges` porque ese path usa `DomainToPersistenceMapper.ToEntity` + `EntityState.Added/Modified`.

**Soft delete + columna generada**:
- `IsDeleted` se persiste en `*Entity`, se setea en Dominio solo via `EliminarLogicamente()` (`Cargo`:75-84, `Habilidad`:63-66, `Puesto`:87-90, `Persona`:60-63, `Ocupacion`:109-115).
- `IsActive` se persiste en `*Entity` (excluyendo `OcupacionEntity`, que no tiene esta columna — verificado leyendo el archivo).
- **Columnas generadas `Active*Unique`**: solo dependen de `IsDeleted` (no de `IsActive`). Ejemplos (`CargoConfiguracion.cs:31-34`, `HabilidadConfiguracion.cs:20-23`, `PuestoConfiguracion.cs:35-38`, `UnidadOrganizativaConfiguracion.cs:32-35`, `PersonaConfiguracion.cs:25-38`, `OcupacionConfiguracion.cs:42-53`). **Reconstruir `IsActive` desde persistencia NO interfiere con la unicidad activa.** Es seguro.

**Check constraints** que el mapper hoy respeta por construcción, pero un `Reconstitute` que omita el ctor primario podría romper:
- `CK_Ocupaciones_Fechas` — `FechaFin IS NULL OR FechaFin >= FechaInicio`. El ctor de Ocupacion valida esto (líneas 15-18). Si `Reconstitute` lo omite, filas con `FechaFin < FechaInicio` (ilegítimas por el check pero posibles en un dump corrupto) cargarían sin error en Dominio. **Riesgo BAJO** porque el check está en DB, pero documentable.
- `CK_Puestos_PuestoSuperior` — `PuestoSuperiorId IS NULL OR PuestoSuperiorId <> Id`. El ctor de Puesto no lo valida; solo el setter `CambiarPuestoSuperior` (líneas 64-72). El mapper actualmente setea `PuestoSuperiorId` solo vía el ctor o `CambiarPuestoSuperior` (no vía `SetProperty`).
- `CK_UnidadesOrganizativas_UnidadPadre` — `UnidadPadreId IS NULL OR UnidadPadreId <> Id`. Hoy validado en `UnidadOrganizativa.Actualizar` (95-99) y `CambiarUnidadPadre` (118-124). El mapper para UO NO setea `UnidadPadreId` por reflexión (líneas 75-90, ctor primario), solo en la carga inicial.

---

## Análisis técnico objetivo

### B.1 — Todos los call sites de `SetProperty` en el repo

Solo 12 call sites, todos concentrados en `PersistenceToDomainMapper.cs` (líneas listadas en A.1). Búsqueda exhaustiva confirma:

- `grep -rn "PropertyInfo\\.SetValue\\|GetProperty.*BindingFlags\\|SetProperty(" src/` → 13 hits (12 call sites + 1 implementación del helper).
- `grep -rn "SetProperty" tests/` → 0 hits en código (solo en `TestResults/coverage.cobertura.xml`, irrelevante).

**Conclusión**: el blast radius del refactor es **estrictamente local** a `PersistenceToDomainMapper.cs`.

### B.2 — Mapeo EF Core → Dominio (campos que se setean)

| Entidad | Campos vía ctor primario / object-initializer | Campos vía `SetProperty` |
|---|---|---|
| `Cargo` | `Codigo`, `Nombre`, `NivelId`, `Descripcion`, `Id`, `CreatedAt`, `CreatedByUserId`, `UpdatedAt`, `UpdatedByUserId`, `IsDeleted`, `DeletedAt`, `DeletedByUserId` | `IsActive`, `NivelCargo` (nav) |
| `Habilidad` | `Codigo`, `Nombre`, `Categoria`, `Descripcion`, `Id`, ..., `IsDeleted`, `DeletedAt`, `DeletedByUserId` | `IsActive` |
| `UnidadOrganizativa` | `Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `Descripcion`, `UnidadPadreId`, `Id`, ..., `IsDeleted` | (ninguno — patrón `with`) |
| `Puesto` | `UnidadOrganizativaId`, `CargoId`, `Codigo` (via `CambiarDatos`), `Nombre`, `Descripcion`, `PuestoSuperiorId` (via `CambiarPuestoSuperior`), `Id`, ..., `IsDeleted` | `IsActive`, `UnidadOrganizativa` (nav), `Cargo` (nav) |
| `Persona` | `Nombres`, `Apellidos`, `Legajo`, `Email` (via `CambiarDatos`), `Id`, ..., `IsDeleted`, `DeletedByUserId` | `IsActive`, `Telefono`, `TipoDocumento`, `NumeroDocumento` |
| `Ocupacion` | `PersonaId`, `PuestoId`, `FechaInicio`, `FechaFin`, `TipoAsignacion`, `Observaciones`, `Id`, ..., `IsDeleted`, `DeletedByUserId` | `Persona` (nav), `Puesto` (nav) |

**Patrón observado**: las nav properties (`NivelCargo`, `UnidadOrganizativa`, `Cargo`, `Persona`, `Puesto`) son siempre opcionales en el mapper (`if (entity.Nav is not null)`). El mapper tolera ausencias — no falla si EF Core no hizo eager-load. `Reconstitute` debe preservar esa tolerancia.

### B.3 — Riesgos identificados

| # | Riesgo | Severidad |
|---|---|---|
| 1 | `Cargo.Desactivar()` valida `_puestos.Count > 0 && _puestos.Any(p => p.IsActive)`. El mapper hoy SOLO setea `IsActive=false` sin chequear puestos subordinados. Si reconstituimos un Cargo `IsActive=false` con puestos subordinados cargados (por eager-load) y luego se invoca `Desactivar()` por algún path de código, **la invariante ya estaba rota en la fila persistida** — pero el mapper silenciaba eso. | MEDIO |
| 2 | `Ocupacion.ctor` valida `FechaFin >= FechaInicio`. El mapper llama el ctor primario, así que hoy se respeta esa invariante. Si `Reconstitute` la omite, podríamos cargar una fila con fechas inválidas (improbable pero posible en dump corrupto o si la migración anterior no aplicó el check). | BAJO |
| 3 | `Persona` no expone setters para `Telefono`, `TipoDocumento`, `NumeroDocumento` (todos `private set`). El ctor primario `Persona(nombres, apellidos, legajo?, email?)` no toma esos argumentos. `Reconstitute(...)` debe ser explícito sobre esos campos o agregarlos al ctor primario (lo cual cambia el contrato público de creación de Persona, decisión no trivial). | MEDIO |
| 4 | `Puesto` usa `CambiarDatos(codigo, nombre, descripcion)` desde el mapper (línea 124). Si lo cambiamos a `Reconstitute(...)`, debemos decidir si `Reconstitute` también llama internamente `CambiarDatos` (encadenamiento implícito) o expone los campos al caller. La asimetría con `UnidadOrganizativa` (que es todo `with`) sugiere normalizar a `with`-chain o `init`-only. | BAJO |
| 5 | Tests estructurales IL como el de UO (`UnidadOrganizativaRepositoryTests.cs:984-1045`) **NO existen para las otras 5 entidades**. Si refactorizamos, nadie detectará una reintroducción de `SetProperty` salvo los round-trip tests, que son lentos (cubren migración + DB real). | MEDIO |
| 6 | `SGV.Dominio` NO tiene `InternalsVisibleTo("SGV.Tests")` (verificado: `grep -rn "InternalsVisibleTo" src/SGV.Dominio/` retorna vacío). Si optamos por un ctor `internal` para `Reconstitute`, **debemos agregar el atributo** al `.csproj` para que los tests (`CargoMapperTests`, etc.) puedan invocarlo directamente. | BAJO |
| 7 | Hay riesgo de que el `EsVigente` de `Ocupacion` (línea 48) caiga en falso negativo tras reconstitución si cambia el orden de operaciones (p.ej., si reconstituimos `IsDeleted` antes que `FechaFin`). El mapper actual setea `IsDeleted` en el object-initializer ANTES de cualquier `SetProperty`, así que el orden está fijado. `Reconstitute` debe documentar ese orden o aceptar todos los parámetros de una vez. | BAJO |

### B.4 — Comparación con el patrón UO existente (¿se puede generalizar?)

**Sí, con diferencias importantes:**

1. `UnidadOrganizativa` es **100% `init`-only** para sus propiedades lógicas (líneas 43-61). `with` es natural. Las demás entidades son `record class : EntidadAuditable` con `private set`, lo que obliga a un ctor `internal Reconstitute(...)` (o equivalente) porque `with` no asigna propiedades `private set`.
2. **Diferencia de tratamiento `Codigo`**: UO veda mutación post-construcción (decisión documentada). `Cargo` sí permite `Actualizar(codigo, ...)`. `Habilidad` también. `Reconstitute` no es lo mismo que "ven mutación": debe ser un **factory de hidratación** que respeta las invariantes de cada tipo.
3. **Nav properties**: UO las trata como `init`-only (`with` para inyectarlas). Las demás entidades usan `private set` para nav. Para generalizar coherentemente, o bien (a) movemos nav properties a `init`-only en todas (invasión mayor al contrato de mutación), o bien (b) usamos un ctor `internal Reconstitute(...)` con todos los parámetros como `readonly` parameters asignados a `private set`. La opción (b) es menos invasiva.

### B.5 — Valores derivados/calculados que el mapper setea y la entidad calcula en su setter

- `Ocupacion.EsVigente`: derivado (`FechaFin is null && !IsDeleted`, línea 48). **No se setea** desde el mapper; se recalcula tras la reconstitución.
- `UnidadOrganizativa.ValidarVigencia`: aplicado por `DefinirVigencia` desde el mapper (líneas 106-107), no deriva flag computado.
- **Ningún setter de las cinco entidades restantes calcula un valor derivado** — todos son simples asignaciones tras validación de shape (`ValidacionesDominio.Requerido` / `Opcional`). El mapper pasa valores directos (sin tocar lógica derivada).

**Conclusión**: no hay valores derivados que el mapper "pise" o que la entidad recalcule. La reconstitución es esencialmente un mapeo 1-a-1 con validación de shape.

---

## Restricciones del repo que aplican

| Restricción | Evidencia | Impacto en este refactor |
|---|---|---|
| Clean Architecture: Dominio ← Aplicacion ← Infraestructura | `openspec/specs/sgv-persistence-architecture/spec.md:5-22` ("EF Persistence Model Boundary") | ✅ El refactor vive en Dominio + Infraestructura. No debe tocar Aplicacion, Api, Web ni Contracts. |
| Soft delete + columna generada para unicidad activa | `CargoConfiguracion.cs:31-34`, `HabilidadConfiguracion.cs:20-23`, `PuestoConfiguracion.cs:35-38`, `UnidadOrganizativaConfiguracion.cs:32-35`, `PersonaConfiguracion.cs:25-38`, `OcupacionConfiguracion.cs:42-53` | ✅ Ver A.6: `IsActive` no entra en el cómputo. Refactor seguro. |
| Auditoría centralizada con interceptor EF | `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` | ✅ El interceptor trabaja con `*Entity` (líneas 67-83). Refactor del Dominio no afecta. **No requiere cambio de auditoría.** |
| Identity con clave string | `Persona` hereda de `EntidadAuditable` con `Id: Guid`. Identity link es `PersonaId` en `SgvIdentityUser`. La entidad `Persona` no sabe de Identity (`sgv-persistence-architecture/spec.md:115-131`). | ✅ Sin impacto. Reconstituir `Persona` desde su `PersonaEntity` no toca Identity. |
| `Cargo.Desactivar` valida puestos subordinados activos | `Cargo.cs:75-84` | ⚠️ Riesgo B.3#1: el mapper actual silencia esto. Reconstitución puede cargar `IsActive=false` con `_puestos` cargados — invariante histórica, no validación en lectura. Decisión de diseño: ¿debe `Reconstitute` respetar o replicar la invariante? **Recomendación**: NO replicar; documentar como caso de hidratación, dejar invariante a `Desactivar()`/servicios. |
| `Ocupacion.ctor` valida `FechaFin < FechaInicio` | `Ocupacion.cs:15-18` | ⚠️ Riesgo B.3#2. Si `Reconstitute` lo omite, dump corrupto cargaría. **Recomendación**: `Reconstitute` debe replicar la validación (al menos con check DB como red de seguridad) o documentar que se asume fila válida. |
| `strict_tdd: true` | `openspec/config.yaml:11` | ✅ Phase `sdd-spec` debe proponer tests RED antes de tocar implementación. El test estructural IL para UO ya cubre `ToDomain(UnidadOrganizativaEntity)` — el apply debe **extenderlo** a `ToDomain(CargoEntity|HabilidadEntity|PuestoEntity|PersonaEntity|OcupacionEntity)`. |
| `docs/decisiones-implementacion.md:78-88` documenta el contrato UO | Sección "Inmutabilidad de `Codigo` en `UnidadOrganizativa`" | ⚠️ Si generalizamos el patrón `Reconstitute`, ese documento debería ampliarse con la sección del resto de entidades (o crear una entrada adicional) en el `archive-report`. Hoy la sección describe solo UO como precedente. |
| `Ocupacion` no tiene `IsActive` propio (usa `EsVigente`) | `Ocupacion.cs:48` + `OcupacionEntity.cs` (sin `IsActive`) | ✅ El mapper NO setea `IsActive` en Ocupacion (verificado). `Reconstitute` debe respetar esto: solo `Persona`/`Puesto` nav properties, sin `IsActive`. |
| `SGV.Dominio` no expone `InternalsVisibleTo("SGV.Tests")` | `grep -rn "InternalsVisibleTo" src/SGV.Dominio/` = vacío. Comparado con `SGV.Infraestructura.csproj:25-29` que SÍ lo tiene. | ⚠️ Decisión de diseño: si `Reconstitute` es `internal`, agregar `InternalsVisibleTo` al `.csproj` de Dominio (cambio mínimo, mismo patrón que Infraestructura). Si es `public` sealed factory, no requiere el atributo pero pierde encapsulación. |

---

## Enfoques evaluados

### Opción 1 — Ctor `internal Reconstitute(...)` por entidad + eliminar `SetProperty`

**Cómo**: agregar a cada una de las 5 entidades (`Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion`) un constructor marcado `internal` con nombre `Reconstitute(...)` que acepta **todos** los campos persistibles (incluyendo los audit fields + `IsActive` + nav properties que se reconstituirán). El ctor:

1. Asigna campos vía `this.X = Y` con `private set` (sin reflexión).
2. Replica las validaciones de invariante del ctor primario cuando apliquen (p.ej. en `Persona`, validar `Telefono`/`TipoDocumento`/`NumeroDocumento` con `ValidacionesDominio.Opcional` para mantener forma).
3. Asigna nav properties usando `this.X = Y` (con `private set`) — la diferencia con `with` es que `set` no es `init`, así que el tipo `record class` no restringe esta vía.
4. Reemplaza los 12 `SetProperty(...)` en `PersistenceToDomainMapper` por invocación directa del ctor `Reconstitute(...)`.
5. Marca el helper `SetProperty<T>` con `[Obsolete]` y/o elimina el `using System.Reflection;`.

**Ejemplo (seudo-firma para `Cargo`)**:

```csharp
internal static Cargo Reconstitute(
    Guid id, string codigo, string nombre, Guid nivelId, string? descripcion,
    bool isActive, NivelCargo? nivelCargo,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
{
    // Validación mínima de shape (mismas reglas que el ctor primario)
    ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
    ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
    ValidarNivelId(nivelId);
    Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    IsActive = isActive;
    NivelCargo = nivelCargo;
    // ... audit fields ...
    return this;  // vía asignación directa
}
```

(Firma exacta puede variar; el punto es: ctor con TODOS los campos, sin reflexión.)

**Pros**:
- Blast radius mínimo (1 archivo de Dominio por entidad + 1 archivo de Infraestructura). 12 call sites del mapper se reemplazan trivialmente.
- Preserva el contrato actual de `private set`. No invasivo: no cambia la API pública ni el shape del record.
- Sigue el patrón ya establecido en Infraestructura (`SetProperty` como `private static`) pero lleva la responsabilidad al Dominio.
- Tests existentes verdes con cero cambios (mismas firmas observables).
- Permite agregar **tests estructurales IL** por entidad, replicando el patrón del test de UO.

**Contras**:
- Ctor `internal` → requiere agregar `InternalsVisibleTo("SGV.Tests")` a `SGV.Dominio.csproj` (cambio mínimo, consistente con `SGV.Infraestructura.csproj:25-29`).
- Lista larga de parámetros en `Reconstitute`. Cada campo nuevo requiere extender la firma.
- El ctor invoca asignaciones a `private set` (válidas porque es la misma clase) → la entidad es `record class`, lo que significa que **`record` con `private set` permite asignación interna**. Esto se valida con cuidado: si la entidad se moviera a `record` puro sin setter, `Reconstitute` no compilaría sin cambios.
- Riesgo de asimetría con `UnidadOrganizativa` (que usa `with`). El equipo debería documentar por qué UO es `init`-only y las demás son `private set`. Aceptable: la asimetría ya existe.

**Tamaño de cambio**:
- 5 archivos de Dominio (1 ctor `Reconstitute` por entidad).
- 1 archivo de Infraestructura (`PersistenceToDomainMapper.cs`: 12 líneas → 12 nuevas invocaciones de `Reconstitute` + eliminar helper `SetProperty` + `using System.Reflection;`).
- 1 `.csproj` (agregar `InternalsVisibleTo`).
- 5 tests estructurales IL nuevos (uno por entidad `ToDomain`), replicando el patrón de `UnidadOrganizativaRepositoryTests.cs:984-1045`.
- ~10-15 tests de comportamiento de mapper nuevos (siguiendo el patrón de `OcupacionMapperTests.cs`).
- Estimación: ~250-350 LoC total de producción + tests. **Cumple el budget de 400 LoC del review.**

**Esfuerzo**: Bajo-Medio. Aproximadamente 1 PR cohesivo.

**Recomendación**: ✅ **RECOMENDADA**. Resuelve la deuda sin cambiar el contrato, escala la generalización del patrón UO de forma menos invasiva que la opción 2, y permite escribir tests estructurales que el repositorio ya sabe escribir.

### Opción 2 — Mover todas las entidades a `record` con `init`-only + `with`-chain (generalización total del patrón UO)

**Cómo**: cambiar todas las propiedades lógicas de `Cargo`, `Habilidad`, `Puesto`, `Persona`, `Ocupacion` (las 5 afectadas) de `private set` a `init`. Reemplazar el mapper por encadenamientos de `with` sobre un objeto construido con el ctor primario. Para nav properties, igualmente `with`. Para `IsActive`, encadenar `with { IsActive = entity.IsActive }`.

**Pros**:
- Uniformidad total con `UnidadOrganizativa`.
- El compilador rechaza cualquier intento de asignación externa (`init` lo bloquea salvo el `with` del propio record).
- Permite usar `Persona` `with { Telefono = "..." }` en servicios en lugar de un setter dedicado (cambio potencial hacia fluent mutators).

**Contras**:
- **Blast radius ENORME**. Los métodos de mutación existentes (`Actualizar`, `CambiarDatos`, `Desactivar`, `Activar`, `Reactivar`, `Finalizar`, `EliminarLogicamente`, `AgregarHabilidad`, `CambiarDocumento`, etc.) actualmente mutan `this.X = Y` con `private set`. Al pasar a `init`, **todos** esos métodos deben convertirse a `return this with { X = Y }` y actualizar TODOS sus call sites en `Aplicacion/`.
- `_habilidades`, `_puestos`, `_ocupaciones` son `List<T>` mutables internas que sobreviven a `with` (porque `with` copia el record pero no las colecciones por referencia — las listas se comparten entre instancias). Esto introduce bugs sutiles: invocar `with` y luego `AgregarHabilidad` muta la lista compartida. Hay que reescribir TODA la gestión de colecciones internas (probablemente con `[UnsafeAccessor]` o `List<T>` por instancia con factory).
- `Cargo.Desactivar()` valida `_puestos.Count > 0 && _puestos.Any(p => p.IsActive)`. Si `Cargo` se vuelve `init`-only, el método ya no puede mutar; debe retornar `this with { IsActive = false }` PERO después de validar que el `_puestos` cargado cumpla. La validación funciona sobre `this._puestos` antes del `with`. **Riesgo: la entidad devuelta por `with` no tiene la misma `_puestos` que el `this` original** porque `with` clona superficial. Si `_puestos` no se reasigna explícitamente, la instancia vieja lo conserva. Funcional, pero sutil.
- Tests existentes que invoquen métodos mutadores asumen que modifican `this` (no devuelven nueva instancia). Habría que actualizar muchos tests de aplicación.
- `record` con `init` + EF Core: para que EF Core instancie `Persona` directamente (algo que hoy no hace pero podría hacerse en el futuro), requeriría un binder que respete `init`. Esto no es un problema actual pero limita la flexibilidad.

**Tamaño de cambio**:
- 5 entidades del Dominio (cambiar TODAS las propiedades a `init` + reescribir TODOS los métodos de mutación como `with`-returns).
- Servicios de Aplicación que llaman métodos mutadores (decenas de archivos: `OcupacionServicioComandos`, `CargoServicioComandos`, `PuestoServicioComandos`, `PersonaServicioComandos`, `HabilidadServicioComandos` + tests de aplicación).
- `PersistenceToDomainMapper.cs` (reescritura completa del path de hidratación para 5 entidades).
- Tests: la mitad de los tests de aplicación que asumen mutación `this.X = Y` deben actualizarse a `var x = entity.Actualizar(...)`.
- Estimación: **600-1000+ LoC**, claramente por encima del budget de 400. Multi-PR.

**Esfuerzo**: Alto. Multi-PR, cambia contratos vigentes.

**Recomendación**: ❌ **DESCARTADA para esta iteración**. Resuelve más deuda pero el blast radius supera el budget y contradice la guía de "Preservar estrategia actual + minimal change". Queda como evolución futura documentable en el `archive-report` o issue aparte.

### Opción 3 — Híbrido: ctor `internal Reconstitute` solo para lo que NO es `init`-only + mantener `with` en UO

**Cómo**: igual que Opción 1 pero el `Reconstitute` para `Persona` permite **opcionalmente** seguir usando `private set` para los campos que no tienen ctor primario (`Telefono`, `TipoDocumento`, `NumeroDocumento`). Para `Puesto`, hacer `Reconstitute` llame internamente `CambiarDatos`. Para `Ocupacion`, `Reconstitute` valida la consistencia `FechaFin >= FechaInicio` (igual que el ctor primario).

Es lo mismo que Opción 1 pero reconociendo las variaciones por entidad. Se documenta en `design.md`/`tasks.md` con un task por entidad.

**Recomendación**: ✅ **Variante de la Opción 1, recomendada si Opción 1 se siente demasiado genérica.** En la práctica, Opción 1 ya cubre estas variaciones (cada `Reconstitute` se adapta a su entidad). Esta opción se descarta por redundancia.

### Opción 4 — Eliminar `SetProperty` con cero garantía equivalente (cambiar el mapper a invocar constructores primarios + setter explícito)

**Cómo**: eliminar `SetProperty`, reescribir el mapper para usar **únicamente** los métodos existentes del Dominio. Ejemplo:

```csharp
// Para Cargo IsActive=false:
// ... en lugar de SetProperty(cargo, nameof(Cargo.IsActive), false)
cargo.Desactivar();  // dispara validación de puestos subordinados!
```

**Contras**:
- Llama métodos con semántica de "transición de estado" desde un contexto que NO es transición — son factories, no operaciones de negocio.
- Dispararía `Cargo.Desactivar()` que valida puestos subordinados, **rompiendo la carga de lectura** de un Cargo persistido con `IsActive=false` y puestos subordinados activos. Es exactamente el riesgo B.3#1 materializado.
- Pierde la distinción entre "hidratar desde persistencia" y "transicionar de estado".

**Recomendación**: ❌ **DESCARTADA**. Confunde factory con transición.

### Tabla comparativa

| Opción | Blast radius | Riesgo | Contrato público | Migración | Tests nuevos | Esfuerzo | Recomendación |
|---|---|---|---|---|---|---|---|
| 1 — `internal Reconstitute(...)` | Mínimo (5 Dominio + 1 Infraestructura + 1 csproj) | Bajo (replica validación de ctor) | Sin cambio | Ninguna | ~5 IL + 10-15 [Fact] | Bajo-Medio (≤400 LoC) | ✅ RECOMENDADA |
| 2 — `record init` + `with` total | Enorme (5 entidades + N servicios de aplicación + tests) | Alto (colecciones internas + tests) | Cambia (métodos devuelven instancia) | Ninguna | ~50+ [Fact] | Muy Alto (>1000 LoC) | ❌ DESCARTADA |
| 3 — Híbrido Opción 1 | Igual a 1 | Igual a 1 | Igual a 1 | Ninguna | Igual a 1 | Igual a 1 | (Variante de 1) |
| 4 — Usar `Desactivar()`/`Activar()` | Mínimo código, alto riesgo semántico | CRÍTICO (rompe invariantes) | Roto para Persistencia | Ninguna | Tests fallidos | Bajo escritura, Alto fixing | ❌ DESCARTADA |

---

## Recomendación

**Opción 1 — `internal Reconstitute(...)` por entidad + eliminar `SetProperty`**.

Razones técnicas:

1. **Blast radius mínimo y acotado**: el cambio vive en `Dominio/` + `Infraestructura/Persistencia/Mapeos/`. No toca Aplicacion, Api, Web, Contracts ni migraciones. Cabe en el budget de 400 LoC del review.
2. **Respeta el patrón vigente**: las entidades siguen siendo `record class : EntidadAuditable` con `private set`. Solo agregamos un ctor `internal Reconstitute(...)` que el mapper invoca. La asimetría con `UnidadOrganizativa` (que ya usa `init` + `with`) se documenta pero no se fuerza.
3. **Refleja el precedente del equipo**: el comentario en `PersistenceToDomainMapper.cs:70-74` deja claro que `SetProperty` se mantiene solo donde la entidad subyacente tiene `private set`. Migrar a `init` everywhere es un segundo paso que puede salir en otro change si la dirección lo decide.
4. **Habilita los tests estructurales IL** que son la red de seguridad que este refactor pide. Replicar `ToDomain_UnidadOrganizativa_NoLlamaSetPropertyReflectionHelper` para las 5 entidades nuevas es un cambio de baja complejidad y alto valor.
5. **`strict_tdd: true`** se respeta naturalmente: el ciclo de apply puede ser (a) escribir los 5 tests IL en RED, (b) refactorizar el mapper en GREEN, (c) escribir los tests de comportamiento en RED para las nuevas firmas `Reconstitute`, (d) implementar los ctors en GREEN.
6. **No requiere migración EF Core** ni cambios al model snapshot. No requiere cambios en `DomainToPersistenceMapper` (el sentido Dominio → Persistencia sigue usando object-initializer sin reflexión).
7. **El interceptor de auditoría no se ve afectado**: trabaja sobre `*Entity` con `public set`. Documentado en `AuditoriaSaveChangesInterceptor.cs:67-83`.

Próximo paso natural: **`sdd-propose`** — escribir `proposal.md` con la Opción 1, scope acotado a las 5 entidades + mapper + tests, no-goals explícitos (no migramos a `record init` total, no tocamos auditoría, no tocamos contratos HTTP).

---

## Archivos afectados (resumen para `sdd-propose` / `sdd-tasks`)

| Path | Rol en el refactor |
|---|---|
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Agregar `internal static Cargo Reconstitute(...)`. Validar shape con `ValidacionesDominio`. |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Idem. `IsActive` único `SetProperty` a reemplazar. |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Idem. Reemplaza `CambiarDatos(...)` + 3 `SetProperty`. |
| `src/SGV.Dominio/Personas/Persona.cs` | Idem. Considerar agregar `internal Reconstitute` con params para `Telefono`/`TipoDocumento`/`NumeroDocumento`. |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Idem. `Reconstitute` debe validar `FechaFin >= FechaInicio` (replica invariante de ctor primario). |
| `src/SGV.Dominio/SGV.Dominio.csproj` | Agregar `<InternalsVisibleTo Include="SGV.Tests" />` (cambio mínimo, paralelo a `SGV.Infraestructura.csproj:25-29`). |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | (a) Eliminar `using System.Reflection;` (línea 1). (b) Eliminar helper `SetProperty<T>` (líneas 225-232). (c) Reemplazar 12 call sites por invocación directa de `Entity.Reconstitute(...)`. |
| `tests/SGV.Tests/Persistencia/PersistenceToDomainMapperTests.cs` (NUEVO) | 5 tests `[Fact]` que replican el patrón estructural de `UnidadOrganizativaRepositoryTests.cs:984-1045` para cada entidad afectada: `ToDomain_Cargo_NoLlamaSetPropertyReflectionHelper`, `_Habilidad`, `_Puesto`, `_Persona`, `_Ocupacion`. **Estos tests deben quedar en RED antes del refactor de producción (TDD).** |
| `tests/SGV.Tests/Persistencia/*MapperTests.cs` (NUEVOS o ampliación de `OcupacionMapperTests.cs`) | Tests de comportamiento para `Reconstitute` por entidad: round-trip OK, validación de shape, preservación de `IsActive=false`, nav properties opcionales. |
| `docs/decisiones-implementacion.md` (sección "Inmutabilidad de Codigo en UnidadOrganizativa") | En el `archive-report` (no en este change): ampliar con la nota de que el patrón `Reconstitute` se extendió a las 5 entidades restantes, manteniendo la asimetría `init`-only de UO. |
| `openspec/specs/sgv-persistence-architecture/spec.md` | No requiere delta: el refactor no toca la persistencia EF ni los tipos `*Entity`, así que las invariantes de "EF Persistence Model Boundary" y "Observable Persistence Invariants" se preservan textualmente. |

---

## Riesgos priorizados

| Severidad | Riesgo | Mitigación |
|---|---|---|
| **CRITICAL** | — | (Ninguno crítico. El refactor no toca persistencia ni migración.) |
| **HIGH** | — | (Ninguno alto. La validación de invariantes se replica en `Reconstitute` desde el ctor primario de cada entidad.) |
| **MEDIUM** | Riesgo 1 (validación silenciada de `Cargo.Desactivar`) | Documentar en `Reconstitute` que NO replica la validación; dejar a `Desactivar()`/servicios. Mencionar en `archive-report`. |
| **MEDIUM** | Riesgo 3 (`Persona.Telefono/TipoDocumento/NumeroDocumento` sin setter externo dedicado) | `Reconstitute` acepta esos parámetros explícitamente y los asigna vía `private set` interno. Si el equipo lo prefiere, podemos además marcarlos `internal set` para facilitar el path de hidratación; el resto del código sigue `private set` y `CambiarDocumento`. |
| **MEDIUM** | Riesgo 5 (falta de tests estructurales IL para las 5 entidades) | El change **introduce** esos tests como parte del deliverable. Cubrir RED → GREEN. |
| **LOW** | Riesgo 2 (`Ocupacion.Reconstitute` debe validar fechas) | Replicar validación del ctor primario en `Reconstitute`. |
| **LOW** | Riesgo 4 (`Puesto.CambiarDatos` desde mapper) | `Reconstitute` puede llamar internamente `CambiarDatos` si los args coinciden, o asignar directo a `private set` (decisión de diseño local). |
| **LOW** | Riesgo 6 (`InternalsVisibleTo` faltante en Dominio) | Agregar al `.csproj` en el mismo PR. Cambio trivial. |
| **LOW** | Riesgo 7 (`EsVigente` orden de operaciones) | Documentar el orden en `Reconstitute` (audit fields + `IsDeleted` primero, luego `FechaFin`, luego nav). |

---

## Listo para propuesta

**Sí** — `status: ok`. Toda la información necesaria está disponible para escribir `proposal.md`:

- Estado actual verificable con líneas exactas ✅
- Tests existentes y cobertura mapeados ✅
- Patrón del precedente (`UnidadOrganizativa`) documentado ✅
- Restricciones del repo evaluadas ✅
- Enfoques comparados con recomendación clara ✅
- Riesgos priorizados ✅
- Archivos afectados enumerados ✅

**Próximo paso sugerido para el orquestador**: lanzar `sdd-propose` para redactar `proposal.md` con la Opción 1,scope = "5 entidades + PersistenceToDomainMapper + SGV.Dominio.csproj + tests estructurales IL + tests de comportamiento",non-goals = "no migrar a `init`-only total, no tocar DomainToPersistenceMapper, no tocar Entity classes, no tocar migraciones, no tocar auditoría, no cambiar contratos HTTP".

---

## Cambios no triviales para guardar en Engram

Se guardó una observación consolidada en Engram con `topic_key: sdd/resuelve la issue #124/explore` (id de sync `obs-3f8952b6b8995ce8`) que captura: ubicación del helper, 12 call sites con líneas, asimetría UO vs resto, ausencia de `InternalsVisibleTo` en Dominio, decisiones de diseño (no tocar migraciones/auditoría/Entity classes), tests estructurales existentes y gap a cubrir. Esta nota NO reemplaza este `exploration.md`; ambos viven en stores distintos según la política híbrida.
