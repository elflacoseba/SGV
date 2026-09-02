# Patrón Reconstitute + `InternalsVisibleTo` en Dominio

## La motivación: setters públicos rompen invariantes

Los modelos de dominio en SGV son `record class` con propiedades
`init` o `private set`. Esto bloquea el patrón "agarrar la entidad y
settear sus campos uno por uno desde la capa de aplicación". La
construcción está restringida al constructor primario, donde las
validaciones (rangos, longitudes, dependencias entre propiedades) se
aplican como guard.

Pero la persistencia tiene un problema opuesto: cuando EF Core carga
una fila desde MySQL, necesita poblar las propiedades sin pasar por
el constructor. Las migraciones tampoco pueden saltarse los guards
del ctor — un `Cargo` con `Codigo` nulo generado por un JOIN mal
armado sería un bug difícil de detectar.

La solución histórica fue exponer `setters` públicos. La consecuencia
fue que cualquier consumidor podía asignar `IsDeleted = true` o
`Codigo = "HACKED"` sin pasar por la lógica de transición. Las
invariantes quedaban a merced de la disciplina de cada repositorio.

## El patrón `Reconstitute`

A partir del change #124, las seis entidades principales de dominio
que se reconstituyen desde persistencia — `Cargo`, `Habilidad`,
`Puesto`, `Persona`, `Ocupacion`, `UnidadOrganizativa` — exponen un
factory `internal static Reconstitute(...)` con la signatura exacta
que necesita `PersistenceToDomainMapper`. El factory es `internal`:
sólo se invoca desde `SGV.Infraestructura` (vía `InternalsVisibleTo`)
y desde `SGV.Tests` (idéntica razón). El resto del código de
aplicación no puede saltarse el ctor ni mediante reflexión porque el
factory mismo delega en `private set`.

La firma típica, sobre `UnidadOrganizativa`, es:

```csharp
internal static UnidadOrganizativa Reconstitute(
    Guid id, string codigo, string nombre, Guid tipoUnidadOrganizativaId,
    string? descripcion, Guid? unidadPadreId, DateOnly? vigenteDesde,
    DateOnly? vigenteHasta, bool isActive,
    UnidadOrganizativa? unidadPadre, TipoUnidadOrganizativa? tipoUnidadOrganizativa,
    DateTime createdAt, string? createdByUserId,
    DateTime? updatedAt, string? updatedByUserId,
    bool isDeleted, DateTime? deletedAt, string? deletedByUserId)
```

El factory asigna primero `Id` + audit (`CreatedAt`, `CreatedByUserId`,
`UpdatedAt`, `UpdatedByUserId`, `IsDeleted`, `DeletedAt`,
`DeletedByUserId`), después los datos primarios (`Codigo`,
`Nombre`, etc.) y por último las nav properties (`UnidadPadre`,
`TipoUnidadOrganizativa`). El orden importa porque el dominio
asume invariantes entre campos que deben satisfacerse antes de que
otros los lean.

## `InternalsVisibleTo` no es transitivo

`SGV.Dominio` declara `InternalsVisibleTo("SGV.Tests")` y
`InternalsVisibleTo("SGV.Infraestructura")`. Cada uno se otorga
explícitamente porque `InternalsVisibleTo` no se propaga a través
de las `ProjectReference`. Si `SGV.Infraestructura` necesita
visibilidad, su `csproj` debe declarar la suya propia — no hereda
la del Dominio. Esta disciplina evita que un proyecto intermedio
exponga por accidente tipos internos que un cuarto consumidor no
debería ver.

`SGV.Api` y `SGV.Web` no declaran `InternalsVisibleTo` sobre
Dominio. Eso significa que cualquier código del shell que intente
llamar a `Cargo.Reconstitute(...)` desde fuera de
`SGV.Infraestructura` no compila. La asimetría entre
`InternalsVisibleTo` y `public` API es lo que sostiene el límite
"infraestructura reconstituye, aplicación consume".

## Por qué el orden de asignación en `Reconstitute` importa

La firma no es casual. El orden canónico es:

1. **`Id` y campos de audit.** Tienen que asignarse primero porque las
   validaciones posteriores pueden asumir que la fila ya tiene
   `CreatedAt`. Si una validación lanzara con `CreatedAt =
   default(DateTime)`, los logs serían confusos.
2. **`IsDeleted` y compañía.** Asignar `IsDeleted` antes que los
   primarios evita que una validación de primarios se ejecute con
   `IsDeleted` aún en `default(bool)` y rechace una fila que en
   realidad está marcada como borrada (un false positive legítimo).
3. **Datos primarios.** Después de audit, se asignan `Codigo`,
   `Nombre`, `TipoUnidadOrganizativaId`, etc. Las validaciones de
   estos campos (rangos, longitudes, FK contra catálogo) se ejecutan
   dentro de las asignaciones mismas (`ValidacionesDominio.Requerido`).
4. **Nav properties.** Al final, las propiedades de navegación
   (`UnidadPadre`, `TipoUnidadOrganizativa`) se asignan aunque el
   `Reconstitute` original las acepte como null. Esto es porque una
   nav property poblada incorrectamente no afecta invariantes de
   primarios — sólo el lazy loading y el rendering posterior.

En `Cargo.Reconstitute`, por ejemplo, la asignación de `Codigo` pasa
por `ValidacionesDominio.Requerido(codigo, ...)` aunque la fila de
origen ya pasó esa validación al crearse. La razón es
defense-in-depth: si una migración mueve datos corruptos a la tabla,
la entidad reconstituida rechaza la fila en el momento del map, no
después cuando alguien intenta leerla.

## Trade-offs y la asimetría con `Id`

`Id` se declara con `public set` en `EntidadBase`, no con `init` ni
con `private set`. La razón es operativa: el EF change tracker
necesita asignar `Id` cuando materializa una entidad, y bloquear ese
camino haría que cualquier `repository.Find(id)` fallara en compilación.
La asimetría — `Id` mutable público, todo lo demás `private set` —
está documentada en el comentario XML de `EntidadBase` y se sostiene
como decisión deliberada.

El cambio `ReactivarAsync` en el dominio de `UnidadOrganizativa`
valida conflicto por `Codigo` activo usando el valor persistido en
el record cargado (no contra un valor enviado por el cliente),
porque el cliente nunca envía `Codigo` en update. Esta propiedad
viene gratis del patrón Reconstitute: si el record tiene `Codigo`
poblado desde la fila, no hay forma de que un atacante inyecte un
`Codigo` distinto vía JSON.

> ⚠️ A verificar: el comentario XML de `EntidadBase` documenta la
> asimetría `Id` mutable, pero las decisiones formales viven en
> `docs/decisiones-implementacion.md §"Generalización post #124 —
> Reconstitute(...) en las 6 entidades principales"`. Conviene
> confirmar la redacción exacta al rederivar invariantes.

## Consecuencias operativas

El test estructural más efectivo contra reintroducción del helper
`SetProperty<T>` (que usaba `PropertyInfo.SetValue` para saltar el
`init`) es uno que recorre el IL de cada `ToDomain(TEntity)` y falla
si encuentra `PropertyInfo.SetValue` o el helper. Hay seis tests
IL (uno por entidad), replicando el patrón del test preexistente
sobre `UnidadOrganizativa`. Cada uno protege una ruta de
regresión: que alguien re-introduzca el helper para "simplificar"
un mapeo.

La consecuencia operativa de esto es que cualquier nueva entidad
que se reconstituya desde persistencia debe sumarse al patrón:
declarar `internal static Reconstitute(...)`, escribir el test IL
estructural, y agregar `InternalsVisibleTo` para `SGV.Infraestructura`
si todavía no la tiene. La forma de evitar la fricción es sumar
todas las nuevas entidades de una vez, no dejar que el patrón se
introduzca por excepción.

El precio del encapsulamiento es un poco más de código boilerplate
por entidad. La defensa es que ese boilerplate es testeable de
forma estructural, mientras que un setter público sólo es testeable
por convención. Cuando los tests estructurales son baratos
(recorrer IL via `MethodInfo.GetMethodBody().GetILAsByteArray()`),
el balance favorece al encapsulamiento.

## Referencias

- `../tutorials/04-primer-cambio-clean-architecture.md` — ejemplo guiado de cómo extender el patrón al agregar una entidad nueva.
- `../reference/02-esquema-base-de-datos.md` — esquema de las seis entidades y cómo se mapean a columnas.
- `openspec/changes/archive/2026-07-13-fix-124-persistence-mapper-reconstitute/` — artefactos SDD completos del change que extendió `Reconstitute` a las 6 entidades.
- `docs/decisiones-implementacion.md` — secciones "Generalización post #124 — Reconstitute(...) en las 6 entidades principales" e "Inmutabilidad de Codigo en UnidadOrganizativa".