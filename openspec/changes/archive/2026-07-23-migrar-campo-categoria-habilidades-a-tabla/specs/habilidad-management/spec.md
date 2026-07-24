# Delta for Habilidad Management

## MODIFIED Requirements

### Requirement: Crear Habilidad (MODIFIED)

El sistema MUST permitir crear una Habilidad activa proporcionando `Codigo`, `Nombre` y opcionalmente `Descripcion`. `CategoriaId` (Guid) es opcional; cuando se omite, queda `NULL`. `CategoriaId` MUST ser un Guid existente en el catálogo `CategoriasHabilidad` si se informa; cuando no coincide, la operación MUST rechazarse con error de validación (HTTP 400, `ErrorCategoria.Validation`). `Codigo` MUST ser único entre habilidades activas. La respuesta MUST incluir `CategoriaId` y `CategoriaNombre` denormalizados cuando la FK está resuelta, y ambos `null` cuando no se informa.
(Previously: el alta requería `Codigo`, `Nombre`, `Categoria` (string libre) y `Descripcion` opcional; el campo `Categoria` aceptaba cualquier texto y se persistía como `varchar(100)` nullable sin integridad referencial.)

#### Scenario: Creación exitosa con `CategoriaId` válido

- **DADO** que no existe una Habilidad activa con el `Codigo` indicado y existe `CategoriaHabilidad` con `Id = <guid>`
- **CUANDO** se solicita crear una Habilidad con `Codigo`, `Nombre`, `CategoriaId = <guid>` en `/api/v1/skills`
- **ENTONCES** el sistema MUST persistirla activa con `CategoriaId` resuelto
- **Y** MUST devolver los datos creados exponiendo `CategoriaId = <guid>` y `CategoriaNombre = "<nombre de la categoría>"`.

#### Scenario: Creación exitosa sin `CategoriaId`

- **DADO** que no existe una Habilidad activa con el `Codigo` indicado
- **CUANDO** se solicita crear una Habilidad sin informar `CategoriaId`
- **ENTONCES** el sistema MUST persistirla activa con `CategoriaId = NULL`
- **Y** MUST devolver los datos creados con `CategoriaId = null` y `CategoriaNombre = null`.

#### Scenario: Codigo duplicado activo

- **DADO** que existe una Habilidad activa con `Codigo` "COM01"
- **CUANDO** se solicita crear otra Habilidad activa con `Codigo` "COM01"
- **ENTONCES** el sistema MUST rechazar la operación con conflicto (HTTP 409).

#### Scenario: `CategoriaId` inexistente rechazado como validación

- **DADO** que `CategoriaHabilidad` con `Id = <guid-fake>` no existe
- **CUANDO** se solicita crear una Habilidad con `CategoriaId = <guid-fake>`
- **ENTONCES** el sistema MUST rechazar la operación con HTTP 400
- **Y** el `HabilidadError` resultante MUST exponer `Categoria == ErrorCategoria.Validation`
- **Y** MUST NOT persistir la Habilidad.

### Requirement: Actualizar Habilidad (MODIFIED)

La operación de update MUST aceptar `Codigo` como campo editable y MUST aplicar las mismas reglas de shape y unicidad activa que el alta. El sistema MUST permitir actualizar `Codigo`, `Nombre`, `CategoriaId` (Guid opcional) y `Descripcion` de una Habilidad existente. `Codigo` MUST conservar las mismas reglas de shape que en create y MUST seguir siendo único entre habilidades activas. `CategoriaId` MUST resolverse contra `CategoriasHabilidad` y, cuando no coincide con un Guid sembrado, la operación MUST rechazarse con `ErrorCategoria.Validation` (HTTP 400). La respuesta MUST incluir `CategoriaId` y `CategoriaNombre` actualizados.
(Previously: el update aceptaba `Categoria` como string libre y la unicidad se aplicaba sólo a `Codigo` activo.)

#### Scenario: Actualización exitosa con cambio de `CategoriaId`

- **DADO** una Habilidad activa existente y dos categorías sembradas `C1`, `C2`
- **CUANDO** se actualiza la Habilidad con `CategoriaId = C2.Id` (cambio válido) y demás campos válidos
- **ENTONCES** el sistema MUST persistir el cambio
- **Y** la respuesta MUST exponer `CategoriaId = C2.Id` y `CategoriaNombre = C2.Nombre`.

#### Scenario: Actualización eliminando `CategoriaId` (volver a NULL)

- **DADO** una Habilidad activa con `CategoriaId = C1.Id`
- **CUANDO** se solicita actualizar enviando `CategoriaId = null` y demás campos válidos
- **ENTONCES** el sistema MUST persistir `CategoriaId = NULL`
- **Y** MUST exponer `CategoriaId = null` y `CategoriaNombre = null`.

#### Scenario: Actualización sin cambiar `CategoriaId`

- **DADO** una Habilidad activa existente con `CategoriaId = C1.Id`
- **CUANDO** se actualizan `Nombre` o `Descripcion` sin modificar `CategoriaId`
- **ENTONCES** el sistema MUST persistir los demás cambios
- **Y** MUST conservar `CategoriaId = C1.Id` y `CategoriaNombre = C1.Nombre`.

#### Scenario: `CategoriaId` inexistente en update

- **DADO** una Habilidad activa existente
- **CUANDO** se solicita actualizar con `CategoriaId = <guid-fake>`
- **ENTONCES** el sistema MUST rechazar la operación con HTTP 400
- **Y** el `HabilidadError` MUST exponer `Categoria == ErrorCategoria.Validation`
- **Y** MUST NOT persistir cambios parciales.

#### Scenario: Codigo inválido en update

- **DADO** una Habilidad activa existente
- **CUANDO** se solicita actualizarla con `Codigo` vacío, demasiado largo o fuera del formato admitido por la regla vigente
- **ENTONCES** el sistema MUST rechazar la operación por validación
- **Y** MUST NOT persistir cambios parciales de la actualización.

## ADDED Requirements

### Requirement: Contrato read-only expone `CategoriaId` y `CategoriaNombre` (REQ-CAT-07)

`SkillsController` MUST exponer en cada respuesta de `GET /api/v1/skills`, `GET /api/v1/skills/{id}` y `GET /api/v1/skills/consulta` los campos `CategoriaId` (Guid?) y `CategoriaNombre` (string?). Cuando la Habilidad no tiene categoría asociada, ambos campos MUST ser `null`. El campo legacy `Categoria: string?` MUST NOT estar presente en el wire contract.

#### Scenario: Listado expone `CategoriaId` y `CategoriaNombre`

- **DADO** 7 habilidades existentes tras el backfill y migración del change
- **WHEN** un usuario autenticado solicita `GET /api/v1/skills/consulta?status=activas&page=1&pageSize=50`
- **THEN** cada item MUST exponer `categoriaId` y `categoriaNombre` consistentes con la FK resuelta
- **AND** el campo legacy `categoria` (string) MUST NOT aparecer.

#### Scenario: Habilidad sin categoría expone ambos campos `null`

- **DADO** una Habilidad activa con `CategoriaId = NULL` post-backfill
- **WHEN** un usuario autenticado la solicita por `GET /api/v1/skills/{id}`
- **THEN** la respuesta MUST incluir `categoriaId = null` y `categoriaNombre = null`.

#### Scenario: Wire contract libre de `Categoria` (string)

- **DADO** cualquier endpoint vigente de `SkillsController`
- **WHEN** se inspecciona el cuerpo de respuesta JSON
- **THEN** el campo `categoria` (string legacy) MUST NOT estar presente.

### Requirement: Backfill histórico resuelve las 7 habilidades semilla (REQ-CAT-04)

La migración MUST ejecutar un backfill determinista que asigne `Habilidades.CategoriaId = <Guid>` cuando el valor legacy de `Categoria` (string) coincide exactamente con `CategoriasHabilidad.Nombre` (sin distinguir acentos ni mayúsculas/minúsculas). Las habilidades sin match exacto MUST quedar con `CategoriaId = NULL`. Las 7 habilidades semilla del sistema resuelven al menos uno de los cuatro códigos del catálogo; las restantes (si las hubiera) MUST quedar `NULL` para remediación post-deploy.
(Previously: la columna `Categoria` se almacenaba como `varchar(100)` sin integridad ni normalización.)

#### Scenario: Backfill resuelve "Conducción de vehículos" a `Conduccion`

- **DADO** que existe la habilidad "Conducción de vehículos" con `Categoria = "Conducción"`
- **CUANDO** la migración corre
- **ENTONCES** la fila queda con `CategoriaId = <Guid-Conduccion>`
- **Y** la columna string `Categoria` MUST ser eliminada con `DROP COLUMN` una vez completado el backfill.

#### Scenario: Habilidad sin match queda con `CategoriaId = NULL`

- **DADO** que existe una habilidad con `Categoria = "Otra cosa"` (no presente en el seed)
- **CUANDO** la migración corre
- **ENTONCES** la fila queda con `CategoriaId = NULL`
- **Y** la auditoría registra la transición `legacy string → NULL` para remediación post-deploy.

#### Scenario: Migración aborta si la columna `Categoria` no se puede eliminar

- **DADO** que el backfill dejó al menos una fila sin match
- **WHEN** la migración continúa con `DROP COLUMN`
- **ENTONCES** la columna string `Categoria` MUST eliminarse tras el backfill
- **AND** el interceptor de auditoría MUST registrar la transición por cada fila afectada.

### Requirement: Excluir Asignaciones (sin cambios)

> Sin cambios respecto al requisito vigente. La gestión de asignaciones `Habilidad↔Cargo` y `Habilidad↔Persona` queda fuera del alcance de este cambio.

#### Scenario: Operaciones de asignación no disponibles

- **DADO** que el módulo de Habilidades está publicado con el subrecurso readonly
- **WHEN** un cliente revisa el contrato de `/api/v1/skills`
- **THEN** MAY encontrar `GET /api/v1/skills/{skillId}/cargos`
- **AND** MUST NOT encontrar operaciones write de `CargoHabilidad` ni `PersonaHabilidad`.
