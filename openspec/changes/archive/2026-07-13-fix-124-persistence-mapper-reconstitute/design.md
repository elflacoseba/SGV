# Design: Refactor de `PersistenceToDomainMapper` para eliminar reflexión (issue #124)

> Fase: **sdd-design** (high-risk, orquestador). Mapeo técnico ejecutable de la propuesta `2026-07-13-fix-124-persistence-mapper-reconstitute`.
> Artefactos previos: `exploration.md` (filesystem + Engram obs #1043), `proposal.md` (filesystem + Engram obs #1045, topic_key `sdd/resuelve la issue #124/proposal`).
> Decisiones cerradas de producto: 6 entidades con `internal Reconstitute(...)`; `Persona` acepta `telefono`/`tipoDocumento`/`numeroDocumento` explícitos no-nullable; `Cargo.Desactivar` invariante `_puestos` fuera de scope; `docs/decisiones-implementacion.md` solo en `archive-report`.

---

## 1. Resumen del enfoque

Generalizamos el patrón vigente en `UnidadOrganizativa` (mapper con `with`/`init`-only, ver `PersistenceToDomainMapper.cs:68-108` y comentario líneas 70-74) a las 6 entidades afectadas mediante un factory estático `internal static Reconstitute(...)` por entidad que recibe todos los campos persistibles y los asigna con `private set`/`init` tipados. Se elimina el helper `SetProperty<T>` (`PersistenceToDomainMapper.cs:225-232`), el `using System.Reflection;` (línea 1) y los 12 call sites se reemplazan por invocación directa de `Entidad.Reconstitute(...)`. Se agrega `<InternalsVisibleTo Include="SGV.Tests" />` a `SGV.Dominio.csproj` (paralelo a `SGV.Infraestructura.csproj:25-29`) para que los tests puedan invocar el factory. La estrategia de tests combina 5 tests IL estructurales nuevos (patrón de `UnidadOrganizativaRepositoryTests.cs:984-1045`) en RED→GREEN + cobertura de comportamiento por entidad. No tocamos esquema, migraciones, `DomainToPersistenceMapper`, `*Entity`, auditoría, ni contratos HTTP. La consecuencia forzada: `UnidadOrganizativa` pierde el patrón `with` (paridad total con las demás), así que sus métodos mutadores pasan a `void`-return y `UnidadOrganizativaServicioComandos.cs` + los tests que asignan `unidad = unidad.Metodo(...)` deben actualizarse para consumir el `void`-return.

**Herencia de audit fields:** los campos de auditoría (`Id`, `CreatedAt`, `CreatedByUserId`, `UpdatedAt`, `UpdatedByUserId`, `IsDeleted`, `DeletedAt`, `DeletedByUserId`) **heredan de `SGV.Dominio/Comun/EntidadAuditable.cs`**. El factory los recibe como parámetros del mapper y los asigna vía `this.X = Y` con el setter heredado del base record; no se redefine ni se duplica estado.

---

## 2. Forma de `Reconstitute(...)` por entidad

Convención común (todas las entidades):

- Marcado `internal static` (alcance limitado a `SGV.Dominio` + `SGV.Tests` mediante `InternalsVisibleTo`).
- Firma con todos los campos persistibles en orden canónico: `Id` → datos primarios → `IsActive` → nav properties opcionales → audit → `IsDeleted` → (UO) `VigenteDesde`/`VigenteHasta` antes de `IsActive`. Verificado en cada firma de §2.1-§2.6: todas siguen este orden (Cargo, Habilidad, Puesto, Persona, Ocupacion, UO). UO tiene `vigenteDesde`/`vigenteHasta` agrupados con los datos primarios.
- Validación de shape **explícita** dentro del factory (replica del ctor primario cuando aplica), no se delega al ctor primario para no duplicar asignaciones.
- Asignación vía `this.X = Y` aprovechando que `record class` permite `private set` intra-clase.
- Nav properties opcionales: si el caller las pasa en `null`, el factory asigna `null` sin lanzar (paridad con el mapper actual, que tolera ausencias).

### 2.1 `Cargo.Reconstitute`

```csharp
internal static Cargo Reconstitute(
    Guid id, string codigo, string nombre, Guid nivelId, string? descripcion,
    bool isActive, NivelCargo? nivelCargo,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

- Validación: replica ctor primario (`Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `ValidarNivelId(nivelId)`, `Opcional(descripcion, 1000)`).
- Asignaciones en orden: `Id`, `CreatedAt`, `CreatedByUserId`, `UpdatedAt`, `UpdatedByUserId`, `IsDeleted`, `DeletedAt`, `DeletedByUserId`, `Codigo`, `Nombre`, `NivelId`, `Descripcion`, `IsActive`, `NivelCargo`.
- Notas: `IsActive=false` no dispara `Desactivar()` (sin validación `_puestos`); comportamiento idéntico al mapper actual.

### 2.2 `Habilidad.Reconstitute`

```csharp
internal static Habilidad Reconstitute(
    Guid id, string codigo, string nombre, string? categoria, string? descripcion,
    bool isActive,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

- Validación: `Requerido(codigo, HabilidadRules.CodigoMaxLength)`, `Requerido(nombre, 200)`, `Opcional(categoria, 100)`, `Opcional(descripcion, 1000)`.
- Asignaciones en orden: id + audit + `IsDeleted` → `Codigo`, `Nombre`, `Categoria`, `Descripcion`, `IsActive`.
- Notas: único `SetProperty` a reemplazar es `IsActive`.

### 2.3 `Puesto.Reconstitute`

```csharp
internal static Puesto Reconstitute(
    Guid id, Guid unidadOrganizativaId, Guid cargoId, Guid? puestoSuperiorId,
    string codigo, string nombre, string? descripcion,
    bool isActive, UnidadOrganizativa? unidadOrganizativa, Cargo? cargo,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

- Validación: `Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `Opcional(descripcion, 1000)`, `CambiarPuestoSuperior(puestoSuperiorId)` (replica invariante `puestoSuperiorId != Id`).
- Asignaciones en orden: id + audit + `IsDeleted` → `UnidadOrganizativaId`, `CargoId`, `Codigo`, `Nombre`, `Descripcion`, `PuestoSuperiorId` → `IsActive`, `UnidadOrganizativa`, `Cargo`.
- Notas: no invoca el ctor primario (que exige FK no-Empty); delega en `ValidacionesDominio.Requerido`/`Opcional` para `Codigo`/`Nombre`/`Descripcion` y reusa `CambiarPuestoSuperior` para la invariante.

### 2.4 `Persona.Reconstitute`

```csharp
internal static Persona Reconstitute(
    Guid id, string nombres, string apellidos, string? legajo, string? email,
    string? tipoDocumento, string? numeroDocumento, string? telefono,
    bool isActive,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

- Validación: `Requerido(nombres, 100)`, `Requerido(apellidos, 100)`, `Opcional(legajo, 50)`, `Opcional(email, 320)`, `Opcional(tipoDocumento, 50)`, `Opcional(numeroDocumento, 50)`, `Opcional(telefono, 50)`.
- Asignaciones en orden: id + audit + `IsDeleted` → `Nombres`, `Apellidos`, `Legajo`, `Email`, `TipoDocumento`, `NumeroDocumento`, `Telefono`, `IsActive`.
- Notas: `Telefono`/`TipoDocumento`/`NumeroDocumento` son `private set` (líneas 29-33) sin setter externo; el factory es la **única** vía de hidratación desde persistencia, decisión cerrada por el usuario.
- **Nota sobre colecciones internas:** las colecciones `_habilidades` y `_ocupaciones` (líneas 8-9 de `Persona.cs`) **NO** se reconstituyen; se inicializan vacías en field initializer y se pueblan por repositorios a través de métodos de negocio (`AgregarHabilidad`, `AgregarOcupacion`). El factory respeta este contrato: reconstituye solo el estado de fila, no colecciones de navegación que pertenecen a otros aggregates.

### 2.5 `Ocupacion.Reconstitute`

```csharp
internal static Ocupacion Reconstitute(
    Guid id, Guid personaId, Guid puestoId, DateOnly fechaInicio,
    DateOnly? fechaFin, TipoAsignacion tipoAsignacion, string? observaciones,
    Persona? persona, Puesto? puesto,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

- Validación: replica `fechaFin >= fechaInicio` (líneas 15-18 del ctor primario).
- Asignaciones en orden canónico: id + audit + `IsDeleted` → `PersonaId`, `PuestoId`, `FechaInicio`, `FechaFin`, `TipoAsignacion`, `Observaciones` → `Persona`, `Puesto`. Este orden garantiza que `EsVigente` (`FechaFin is null && !IsDeleted`, línea 48) esté bien calculado tras la reconstitución.
- Notas: no setea `IsActive` (no existe esa propiedad en `Ocupacion`); solo nav properties.

### 2.6 `UnidadOrganizativa.Reconstitute`

```csharp
internal static UnidadOrganizativa Reconstitute(
    Guid id, string codigo, string nombre, Guid tipoUnidadOrganizativaId,
    string? descripcion, Guid? unidadPadreId,
    DateOnly? vigenteDesde, DateOnly? vigenteHasta, bool isActive,
    UnidadOrganizativa? unidadPadre, TipoUnidadOrganizativa? tipoUnidadOrganizativa,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

- Validación: `Requerido(codigo, 50)`, `Requerido(nombre, 200)`, `tipoUnidadOrganizativaId != Guid.Empty`, `Opcional(descripcion, 1000)`, `ValidarVigencia(vigenteDesde, vigenteHasta)`.
- Asignaciones en orden: id + audit + `IsDeleted` → `Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `Descripcion`, `UnidadPadreId`, `VigenteDesde`, `VigenteHasta`, `IsActive` → `UnidadPadre`, `TipoUnidadOrganizativa`.
- Notas: abandona `with` para paridad. Implica que las propiedades de UO migran de `init` a `private set` (ver §7).

---

## 3. Refactor de `PersistenceToDomainMapper.cs`

### 3.1 Reemplazo de los 12 `SetProperty` (agrupados por entidad)

| Entidad destino | Línea actual | Reemplazo |
|---|---|---|
| `Cargo` | `31` (`IsActive`) | `cargo = Cargo.Reconstitute(id, codigo, nombre, nivelId, descripcion, isActive, nivelCargo, ...audit...)` |
| `Cargo` | `35` (`NivelCargo` nav) | absorbido en el `Reconstitute(...)` (param `nivelCargo`) |
| `Habilidad` | `64` (`IsActive`) | `habilidad = Habilidad.Reconstitute(id, codigo, nombre, categoria, descripcion, isActive, ...audit...)` |
| `Puesto` | `125` (`IsActive`) | absorbido en `Puesto.Reconstitute(...)` (param `isActive`) |
| `Puesto` | `129` (`UnidadOrganizativa` nav) | absorbido en `Puesto.Reconstitute(...)` (param `unidadOrganizativa`) |
| `Puesto` | `134` (`Cargo` nav) | absorbido en `Puesto.Reconstitute(...)` (param `cargo`) |
| `Persona` | `190` (`IsActive`) | absorbido en `Persona.Reconstitute(...)` |
| `Persona` | `191` (`Telefono`) | absorbido en `Persona.Reconstitute(...)` (param `telefono`) |
| `Persona` | `192` (`TipoDocumento`) | absorbido en `Persona.Reconstitute(...)` (param `tipoDocumento`) |
| `Persona` | `193` (`NumeroDocumento`) | absorbido en `Persona.Reconstitute(...)` (param `numeroDocumento`) |
| `Ocupacion` | `214` (`Persona` nav) | absorbido en `Ocupacion.Reconstitute(...)` (param `persona`) |
| `Ocupacion` | `219` (`Puesto` nav) | absorbido en `Ocupacion.Reconstitute(...)` (param `puesto`) |

### 3.2 Forma final de cada `ToDomain(TEntity)`

- `ToDomain(CargoEntity)` → `return Cargo.Reconstitute(entity.Id, entity.Codigo, entity.Nombre, entity.NivelId, entity.Descripcion, entity.IsActive, entity.NivelCargo is null ? null : ToDomain(entity.NivelCargo), entity.CreatedAt, ...);`
- `ToDomain(HabilidadEntity)` → `return Habilidad.Reconstitute(entity.Id, entity.Codigo, entity.Nombre, entity.Categoria, entity.Descripcion, entity.IsActive, entity.CreatedAt, ...);`
- `ToDomain(PuestoEntity)` → `return Puesto.Reconstitute(entity.Id, entity.UnidadOrganizativaId, entity.CargoId, entity.PuestoSuperiorId, entity.Codigo, entity.Nombre, entity.Descripcion, entity.IsActive, entity.UnidadOrganizativa is null ? null : ToDomain(entity.UnidadOrganizativa), entity.Cargo is null ? null : ToDomain(entity.Cargo), entity.CreatedAt, ...);`
- `ToDomain(PersonaEntity)` → `return Persona.Reconstitute(entity.Id, entity.Nombres, entity.Apellidos, entity.Legajo, entity.Email, entity.TipoDocumento, entity.NumeroDocumento, entity.Telefono, entity.IsActive, entity.CreatedAt, ...);`
- `ToDomain(OcupacionEntity)` → `return Ocupacion.Reconstitute(entity.Id, entity.PersonaId, entity.PuestoId, entity.FechaInicio, entity.FechaFin, entity.TipoAsignacion, entity.Observaciones, entity.Persona is null ? null : ToDomain(entity.Persona), entity.Puesto is null ? null : ToDomain(entity.Puesto), entity.CreatedAt, ...);`
- `ToDomain(UnidadOrganizativaEntity)` → `return UnidadOrganizativa.Reconstitute(entity.Id, entity.Codigo, entity.Nombre, entity.TipoUnidadOrganizativaId, entity.Descripcion, entity.UnidadPadreId, entity.VigenteDesde, entity.VigenteHasta, entity.IsActive, entity.UnidadPadre is null ? null : ToDomain(entity.UnidadPadre), entity.TipoUnidadOrganizativa is null ? null : ToDomain(entity.TipoUnidadOrganizativa), entity.CreatedAt, ...);`

### 3.3 Eliminaciones

- **Helper `SetProperty<T>`** (líneas 225-232) → eliminado completo.
- **`using System.Reflection;`** (línea 1) → eliminado.
- **Métodos auxiliares no afectados**: `ToDomain(NivelCargoEntity)`, `ToDomain(TipoUnidadOrganizativaEntity)`, `ToDomain(NivelHabilidadEntity)`, `ToDomain(CargoHabilidadEntity)`, `ToDomain(PersonaHabilidadEntity)` — siguen como object-initializer sin reflexión. No requieren cambios.

### 3.4 Firmas/returns

- Sin cambios en signatures públicas (`public static T ToDomain(TEntity)`); el cambio es interno.
- `Persona`/`Puesto`/`Ocupacion`/`UO` que antes asignaban `Id` + audit + `IsDeleted` vía object-initializer ahora pasan esos campos como parámetros a `Reconstitute(...)` (más limpio, sin perder encapsulación).

---

## 4. Cambio en `SGV.Dominio.csproj`

Agregar bloque `ItemGroup` paralelo a `SGV.Infraestructura.csproj:25-29`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>SGV.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

- **Justificación**: el factory `Reconstitute` es `internal`; sin este atributo `SGV.Tests` no puede invocarlo directamente. Cambio trivial, paralelo al patrón vigente en Infraestructura.
- **Verificación**: `grep -rn "InternalsVisibleTo" src/SGV.Dominio/` post-cambio debe retornar 1 hit; `dotnet build SGV.slnx` debe pasar sin warnings de "type not accessible".
- **Otros InternalsVisibleTo**: confirmado que solo Infraestructura lo usa hoy (`src/SGV.Infraestructura.csproj:25-29`). No hay colisión.

---

## 5. Estrategia de tests (TDD estricto)

### 5.1 RED primero (5 tests IL estructurales nuevos)

Patrón a replicar: `UnidadOrganizativaRepositoryTests.cs:984-1045`. Cada test recorre el IL de `ToDomain(TEntity)`, decodifica tokens `0x28 (call)` / `0x6F (callvirt)`, resuelve métodos con `module.ResolveMethod(token)` y falla si encuentra `SetProperty` declarado en `PersistenceToDomainMapper`. Estos tests **deben quedar en RED antes del refactor** y pasan a GREEN una vez que `SetProperty` desaparece del cuerpo IL.

| Test | Ubicación |
|---|---|
| `ToDomain_Cargo_NoLlamaSetPropertyReflectionHelper` | `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` (nuevo) |
| `ToDomain_Habilidad_NoLlamaSetPropertyReflectionHelper` | `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` (nuevo) |
| `ToDomain_Puesto_NoLlamaSetPropertyReflectionHelper` | `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` (nuevo) |
| `ToDomain_Persona_NoLlamaSetPropertyReflectionHelper` | `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` (nuevo) |
| `ToDomain_Ocupacion_NoLlamaSetPropertyReflectionHelper` | ampliar `OcupacionMapperTests.cs` (existente) |

### 5.2 GREEN después (tests de comportamiento)

Patrón modelo: `OcupacionMapperTests.cs` (8 tests). Por cada entidad (5 nuevos + ampliación de Ocupacion), cubrir:

- **Round-trip OK**: `entity -> Reconstitute -> fields iguales`.
- **`IsActive=false` reconstituido**: persiste el flag sin lanzar.
- **Nav properties opcionales**: tanto `null` como no-`null` reconstituyen correctamente.
- **Validación de shape**: para `Persona`, no permitir todos los campos de documento nulos (decisión cerrada por el usuario); para `Ocupacion`, no permitir `FechaFin < FechaInicio` (replica invariante del ctor primario).
- **`EsVigente` correcto tras reconstitución** (Ocupacion): con `FechaFin=null && !IsDeleted` → `true`; con `FechaFin!=null` o `IsDeleted=true` → `false`.

Archivos: `tests/SGV.Tests/Persistencia/{Cargo|Habilidad|Puesto|Persona}MapperTests.cs` (4 nuevos) + ampliación de `OcupacionMapperTests.cs` (existente). Sin tocar `UnidadOrganizativaRepositoryTests.cs` (su test IL sigue vigente y se mantiene en GREEN).

### 5.3 Tests existentes que deben seguir verdes

- `UnidadOrganizativaRepositoryTests.cs:984-1045` (IL guard existente).
- `CargoRepositoryTests`, `HabilidadRepositoryTests`, `PuestoRepositoryTests`, `PersonaRepositoryTests`, `OcupacionRepositoryTests` (round-trip end-to-end con DB real).
- Tests de aplicación que consumen mutadores (`UnidadOrganizativaServicioComandosTests.cs` líneas 378, 383, 408, 447 — ver §7.3 — y resto de suites) deben actualizarse solo donde asignan `unidad = unidad.Metodo(...)`.

---

## 6. Orden de ejecución de apply

| Paso | Acción | Validación |
|---|---|---|
| a | Tests RED primero: agregar los 5 tests IL estructurales + tests de comportamiento para los Reconstitutes nuevos. | `dotnet test SGV.slnx --filter "MapperTests"` debe fallar en los IL tests. |
| b | Agregar `Reconstitute(...)` por entidad, en orden: `Habilidad` (menos acoplada) → `Cargo` → `Ocupacion` → `Persona` → `Puesto` → `UnidadOrganizativa` (última por la migración `with → private set`). | `dotnet build src/SGV.Dominio/SGV.Dominio.csproj` verde por capa. |
| c | Actualizar `PersistenceToDomainMapper.cs`: eliminar `using System.Reflection;`, eliminar helper `SetProperty<T>`, reemplazar los 12 call sites por invocación directa de `Entidad.Reconstitute(...)`. | `grep -rn "SetProperty\|PropertyInfo" src/` → 0 hits. |
| d | Agregar `<InternalsVisibleTo Include="SGV.Tests" />` a `SGV.Dominio.csproj` (paralelo a Infraestructura). | `dotnet build SGV.slnx` sin warnings. |
| e | Reescribir `UnidadOrganizativa.Actualizar`/`DefinirVigencia`/`CambiarUnidadPadre`/`Activar`/`Desactivar` con `private set` + `void`-return (ver §7). | `dotnet build src/SGV.Dominio/` verde. |
| f | Actualizar consumidores UO: `UnidadOrganizativaServicioComandos.cs` (**5 líneas completas**: 88, 134, 191, 230, 267) — quitar la asignación de retorno (`unidad = unidad.X(...);` → `unidad.X(...);`) en cada una: `unidad.DefinirVigencia(...)` (línea 88), `unidad.Actualizar(...)` (línea 134), `unidad.CambiarUnidadPadre(...)` (línea 191), `unidad.Desactivar()` (línea 230), `unidad.Activar()` (línea 267) + reescribir `PersistenceToDomainMapper.cs:106-107` para usar `UnidadOrganizativa.Reconstitute(...)` directamente (ver §3.2) + actualizar tests que asignan `unidad = unidad.Metodo(...)` (`UnidadOrganizativaServicioComandosTests.cs:378, 383, 408, 447`). | `dotnet build SGV.slnx` verde. |
| g | `dotnet build SGV.slnx` + `dotnet test SGV.slnx` verdes. | Todas las suites (Dominio, Aplicacion, Persistencia, API, Web, Compatibilidad) verdes. |
| h | Verificación final: 0 hits de `PropertyInfo.SetValue` en `src/`. | `grep -rn "PropertyInfo\|SetProperty" src/` → 0 hits. |

**Atomicidad de los pasos (b) y (e) para UO:** los pasos (b) "agregar `Reconstitute` a UO" y (e) "reescribir mutadores de UO a `void`-return" NO son secuenciales compilables por sí solos en UO: entre (b) y (e) la rama no compila, porque el mapper aún encadena `unidad = ... .DefinirVigencia(...) with { IsActive = ... }` mientras los mutadores ya no retornan `UnidadOrganizativa`. **Se aplican en commit atómico conjunto** dentro del cambio de UO (ambas transformaciones se commitean juntas); o alternativamente ejecutar (e) antes que (b) para UO. Verificable con `dotnet build SGV.slnx` post-paso-`f` → verde antes de pasar al (g).

---

## 7. Migración de UO desde `with` a `Reconstitute`

### 7.1 Estado actual

`UnidadOrganizativa.cs:43-61` declara propiedades `init`-only excepto `IsActive` (línea 61). Mutadores vigentes (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) usan `this with { X = Y }` y retornan `UnidadOrganizativa` (líneas 71-136). El mapper `ToDomain(UnidadOrganizativaEntity)` aprovecha ese retorno encadenando `with { IsActive = ... }` (líneas 106-107).

### 7.2 Estrategia de migración

Reescribir las propiedades de UO de `init` a `private set` (paridad con las otras 5 entidades) y convertir los 5 mutadores a `void`-return con asignación directa. Esto es **delicado** porque rompe la API actual de los servicios.

**Mutadores reescritos**:

```csharp
public void Actualizar(string nombre, string? descripcion,
    Guid tipoUnidadOrganizativaId, Guid? unidadPadreId,
    DateOnly? vigenteDesde, DateOnly? vigenteHasta)
{
    ValidarVigencia(vigenteDesde, vigenteHasta);
    if (tipoUnidadOrganizativaId == Guid.Empty)
        throw new ArgumentException("El tipo de unidad organizativa es obligatorio.", nameof(TipoUnidadOrganizativaId));
    if (unidadPadreId == Id)
        throw new InvalidOperationException("Una unidad organizativa no puede ser padre de sí misma.");

    Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
    Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    TipoUnidadOrganizativaId = tipoUnidadOrganizativaId;
    UnidadPadreId = unidadPadreId;
    VigenteDesde = vigenteDesde;
    VigenteHasta = vigenteHasta;
}

public void DefinirVigencia(DateOnly? desde, DateOnly? hasta)
{
    ValidarVigencia(desde, hasta);
    VigenteDesde = desde;
    VigenteHasta = hasta;
}

public void CambiarUnidadPadre(Guid? unidadPadreId)
{
    if (unidadPadreId == Id)
        throw new InvalidOperationException("Una unidad organizativa no puede ser padre de sí misma.");
    UnidadPadreId = unidadPadreId;
}

public void Activar() => IsActive = true;
public void Desactivar() => IsActive = false;
```

### 7.3 Invariantes preservadas

- `Codigo` se mantiene `private set` (línea 43) → sigue siendo inmutable post-ctor. La invariante "Codigo solo se asigna en el constructor" se mantiene, ahora gracias a `private set` en vez de `init`.
- Validaciones de `Actualizar`/`DefinirVigencia`/`CambiarUnidadPadre` se mantienen dentro del cuerpo del método (mismas excepciones).
- Colecciones internas (`_unidadesHijas`, `_puestos`, líneas 18-19) sobreviven intactas — el mapper nunca las reconstituye.

### 7.4 Consumidores a actualizar

- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` (5 líneas, **todas requieren cambio** porque los 5 mutadores pasan de retornar `UnidadOrganizativa` a `void`):
  - **Línea 88:** `unidad = unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);` → `unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);`
  - **Línea 134:** `unidad = unidad.Actualizar(request.Nombre, ...);` → `unidad.Actualizar(request.Nombre, ...);`
  - **Línea 191:** `unidad = unidad.CambiarUnidadPadre(request.UnidadPadreId);` → `unidad.CambiarUnidadPadre(request.UnidadPadreId);`
  - **Línea 230:** `unidad = unidad.Desactivar();` → `unidad.Desactivar();`
  - **Línea 267:** `unidad = unidad.Activar();` → `unidad.Activar();`
  - **Descubrimiento exhaustivo de consumidores (recomendado en apply):** correr antes del commit para detectar archivos adicionales que la pasada anterior no listó:
    ```
    grep -rn "\.Actualizar\|\.DefinirVigencia\|\.CambiarUnidadPadre\|\.Activar\|\.Desactivar" src/ tests/
    ```
    Output esperado: `UnidadOrganizativaServicioComandos.cs` + `UnidadOrganizativaServicioComandosTests.cs` + (si existe) tests de Dominio UO en `tests/SGV.Tests/Dominio/Organizacion/`. Si aparecen archivos adicionales, actualizarlos también.
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs`:
  - Línea 378: `padre = padre.Desactivar();` → `padre.Desactivar();`
  - Línea 383: `hijo = hijo.Desactivar();` → `hijo.Desactivar();`
  - Línea 408: `hijo = hijo.Desactivar();` → `hijo.Desactivar();`
  - Línea 447: `padre = padre.DefinirVigencia(...)` → `padre.DefinirVigencia(...);`
- Tests de Dominio de UO (verificar `tests/SGV.Tests/Dominio/Organizacion/`): si hay tests que invocan `var x = unidad.Actualizar(...)` deben actualizarse a `unidad.Actualizar(...);`.

---

## 8. Riesgos técnicos detallados y mitigaciones

| Sev | Riesgo | Mitigación concreta |
|---|---|---|
| **MED** | `Cargo.IsActive=false` reconstituido silencia la invariante `_puestos` activos (ya sucede hoy por reflexión). | `Cargo.Reconstitute` no invoca `Desactivar()` (doc XML explicito: "hidrata el flag sin disparar validación de transición"). Documentar en `archive-report` y abrir issue aparte para endurecer `Cargo.Desactivar()` con test de invariante explícito. |
| **MED** | `Persona` sin setters externos para `Telefono`/`TipoDocumento`/`NumeroDocumento`. | `Persona.Reconstitute` acepta los 3 parámetros explícitos no-nullable y los asigna vía `private set`. Decisión cerrada por el usuario (proposal §Approach). Test `PersonaMapperTests.Reconstitute_MapsAllDocumentFields` cubre el path. |
| **MED** | Inexistencia de tests IL para las 5 entidades restantes. | El change entrega esos tests como deliverable explícito (paso `a` del §6). Cobertura RED → GREEN obligatoria por `strict_tdd: true`. |
| **LOW** | `Ocupacion.Reconstitute` debe validar `FechaFin >= FechaInicio`. | Replicar validación dentro del factory (líneas 15-18 del ctor primario) ANTES de asignar `FechaFin`. Test `OcupacionMapperTests.Reconstitute_FechaFinBeforeFechaInicio_Lanza`. |
| **LOW** | `InternalsVisibleTo` faltante en `SGV.Dominio.csproj`. | Agregar al `.csproj` en el mismo PR (paso `d` del §6). Smoke test post-build: `dotnet build` + ejecutar `PersonaMapperTests.Reconstitute_*` debe compilar y pasar. |
| **LOW** | Orden de operaciones en `Reconstitute` afecta a `EsVigente` (Ocupacion). | Documentar orden canónico en XML doc de cada factory: `audit + IsDeleted` → `FechaFin` → nav. Validado por `OcupacionMapperTests.MapPersistenceToDomain_Deleted_MapsIsDeletedAndNotVigente`. |
| **MED** (descubierto en design) | `UnidadOrganizativa.Actualizar`/`DefinirVigencia`/`CambiarUnidadPadre`/`Activar`/`Desactivar` cambian de retorno `UnidadOrganizativa` a `void`. Consumidores en `UnidadOrganizativaServicioComandos.cs` + tests rompen compilación. | Aplicar cambio a consumidores en el mismo PR (paso `f` del §6). Verificado con grep `\.Actualizar\(.*nombre.*descripcion` y revisión manual. |
| **MED** (descubierto en design) | `PersistenceToDomainMapper.cs:106-107` encadena `unidad.DefinirVigencia(...) with { IsActive = ... }` — rompe al pasar a `void`-return. | Reescribir la última línea del `ToDomain(UnidadOrganizativaEntity)` para usar `UnidadOrganizativa.Reconstitute(...)` directamente (un solo call que setea vigencia + `IsActive` en el orden correcto). |

---

## 9. Estimación de LoC desglosada

| Archivo | Tipo | Acción | LoC |
|---|---|---|---|
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Prod | Modificar (agregar `Reconstitute`) | +18 |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Prod | Modificar | +16 |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Prod | Modificar | +22 |
| `src/SGV.Dominio/Personas/Persona.cs` | Prod | Modificar | +20 |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Prod | Modificar | +20 |
| `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | Prod | Reescribir mutadores + `Reconstitute` | +30 / -25 (≈ +5 netos, `with` → `private set` + `void`-return) |
| `src/SGV.Dominio/SGV.Dominio.csproj` | Prod | Agregar `InternalsVisibleTo` | +5 |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Prod | Eliminar helper + `using`, reescribir 6 `ToDomain` | -10 / +15 (≈ +5 netos) |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Prod | Quitar asignaciones `unidad = ...` en 5 líneas | ±0 |
| `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` | Tests | Nuevo (1 IL + 4 comportamiento) | +85 |
| `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` | Tests | Nuevo (1 IL + 3 comportamiento) | +65 |
| `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` | Tests | Nuevo (1 IL + 4 comportamiento) | +90 |
| `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` | Tests | Nuevo (1 IL + 5 comportamiento) | +100 |
| `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` | Tests | Ampliar (1 IL + 2 comportamiento) | +50 |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Tests | Actualizar 4 asignaciones `=` | ±0 |
| `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` (si existe) | Tests | Verificar asignaciones `=` | ±0 |
| **Total** | | | **≈ 506 LoC** |

**Comparación con budget (400 LoC)**: ⚠️ **EXCEDE** por ~106 LoC. La reescritura de UO (con 5 tests nuevos por comportamiento) y el quinto test estructural IL (`Ocupacion`) son los principales impulsores. Esta desviación fue **explícitamente aprobada por el maintainer como `size:exception`** en la sesión SDD del 2026-07-13; ver §11 para justificación. **Alcance final:** mantener cobertura completa (5 IL tests + tests de comportamiento por entidad) sin recortar ni diferir.

---

## 10. Plan de rollback

- **Estrategia**: `git revert` del PR único (todos los cambios conviven en un solo PR cohesivo). Como no tocamos migraciones ni schema, el revert deja el repositorio equivalente al estado previo.
- **Rollback granular** (si solo una entidad rompe): commits atómicos por entidad (Habilidad → Cargo → Ocupacion → Persona → Puesto → UnidadOrganizativa) permiten `git revert <commit>` selectivo.
- **DB**: NO requiere rollback de DB (no se aplican migraciones). El interceptor de auditoría sigue trabajando sobre `*Entity` con `public set`, no se ve afectado.
- **Tests**: los tests nuevos son aditivos (5 archivos + ampliación de 1); al revertir el commit de producción correspondiente, los tests quedan como evidencia histórica sin callers — aceptable, se eliminan en cleanup posterior.

---

## Archivos afectados (resumen)

| Path | Rol |
|---|---|
| `src/SGV.Dominio/Organizacion/Cargo.cs` | Prod — `Reconstitute` |
| `src/SGV.Dominio/Organizacion/Puesto.cs` | Prod — `Reconstitute` |
| `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | Prod — `Reconstitute` + reescritura de 5 mutadores (`with → void/private set`) |
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Prod — `Reconstitute` |
| `src/SGV.Dominio/Personas/Persona.cs` | Prod — `Reconstitute` |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Prod — `Reconstitute` |
| `src/SGV.Dominio/SGV.Dominio.csproj` | Prod — `InternalsVisibleTo("SGV.Tests")` |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Prod — eliminar `SetProperty` + `using System.Reflection;` + reescribir 6 `ToDomain` |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Prod — 5 líneas (quitar `unidad = ` en 88, 134, 191, 230, 267) |
| `tests/SGV.Tests/Persistencia/CargoMapperTests.cs` | Tests (NUEVO) |
| `tests/SGV.Tests/Persistencia/HabilidadMapperTests.cs` | Tests (NUEVO) |
| `tests/SGV.Tests/Persistencia/PuestoMapperTests.cs` | Tests (NUEVO) |
| `tests/SGV.Tests/Persistencia/PersonaMapperTests.cs` | Tests (NUEVO) |
| `tests/SGV.Tests/Persistencia/OcupacionMapperTests.cs` | Tests (AMPLIAR) |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Tests (UPDATE 4 líneas) |

---

## Decisiones de arquitectura (síntesis)

| Decisión | Choice | Alternativa descartada | Rationale |
|---|---|---|---|
| Forma del factory | `internal static Reconstitute(...)` | `public factory` o `init`-only con `with` | `internal` mantiene encapsulación + `InternalsVisibleTo` ya es patrón vigente en Infraestructura. |
| Alcance UO | Migrar también a `Reconstitute` (paridad) | Dejar UO con `with` y demás con `private set` | El usuario cerró paridad total; mantiene un único patrón mental en el codebase. |
| Validación dentro de factory | Replicar invariantes del ctor primario | Delegar al ctor primario y luego `private set` | Asignar con `private set` evita duplicar asignaciones; el factory controla el orden canónico. |
| Orden canónico de asignaciones | `audit + IsDeleted` → datos primarios → `IsActive`/`FechaFin` → nav | Orden variable según entidad | Garantiza que derivados (`EsVigente`) estén bien calculados al final. |
| Tests IL estructurales | 5 nuevos, uno por entidad afectada | Mantener solo el de UO | Aceptado por `strict_tdd: true`; replican patrón vigente. |
| Reescritura UO mutadores | `void`-return con `private set` | Mantener `with`-return | Paridad con las otras 5 entidades (todas `void`-return). Impacto acotado a `UnidadOrganizativaServicioComandos.cs` y 4 tests. |
| Documentación diferida | `docs/decisiones-implementacion.md` solo en `archive-report` | Actualizar en este change | Decisión del usuario; mantiene el change acotado a código + tests. |
| Excess LoC | Mantener alcance completo + `size:exception` aprobado por maintainer (2026-07-13) | Diferir comportamiento a PR aparte o recortar | Justificación en §11: refactor estrictamente local (Dominio + Infraestructura/Mapeos + UO consumers + tests); no toca migraciones, schema, contratos HTTP ni auditoría. Recortar tests reduciría ROI de la red IL contra reintroducción de `PropertyInfo.SetValue`. |

---

## 11. Size exception acknowledged

Estimación total ~506 LoC excede el budget de review de 400 LoC por ~106 LoC.
Esta desviación fue explícitamente aprobada por el maintainer como `size:exception`
en la sesión SDD del 2026-07-13. Justificación:

- El refactor es estrictamente local (Dominio + Infraestructura/Mapeos + UO consumers + tests).
- No toca migraciones, schema, contratos HTTP ni auditoría.
- El alcance completo (producción + tests de comportamiento + tests IL) maximiza la
  confianza de no regresión en una zona sensible (boundary Dominio ↔ Infraestructura).
- Tests de comportamiento diferidos a un PR posterior reducirían el ROI de la red de
  seguridad IL contra reintroducción de `PropertyInfo.SetValue`.

No requiere chained PRs: el alcance cabe en un PR cohesivo.

---

## Open Questions

- Ninguna que bloquee el diseño. Las decisiones del usuario cierran toda ambigüedad.
- ~~Punto pendiente de orquestador: confirmación de la opción (c) para resolver el exceso de LoC.~~ **CERRADO** — `size:exception` aprobado el 2026-07-13 (ver §11).