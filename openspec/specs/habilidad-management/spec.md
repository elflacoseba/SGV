# Especificación de Gestión de Habilidades

## Propósito

Gestionar el catálogo maestro de Habilidades mediante `/api/v1/skills`, conservando lectura pública y agregando creación, actualización de campos editables, desactivación y reactivación. Las asignaciones a cargos o personas quedan fuera de esta porción.

## Requisitos

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

### Requirement: Consultar Habilidades

El sistema MUST mantener `GET /api/v1/skills` y `GET /api/v1/skills/{id:guid}` como contrato legacy de lectura de habilidades activas para usuarios autenticados y MUST agregar `GET /api/v1/skills/consulta` como consulta paginada y filtrada. `status` MUST aceptar `activas|eliminadas`; si se omite o es inválido MUST caer a `activas`. `page < 1` MUST normalizarse a `1`; `pageSize < 1` MUST caer a `20`; `pageSize > 100` MUST limitarse a `100`; `sort` desconocido MUST caer a `codigo_asc`.

### Requirement: Consultar cargos asociados a una habilidad

El sistema MUST exponer `GET /api/v1/skills/{skillId}/cargos` como subrecurso readonly para cualquier usuario autenticado. La consulta MUST aceptar `page`, `pageSize`, `search`, `sort` y `status`; `status` MUST aceptar `activas|eliminadas` y MUST caer a `activas` si se omite o es inválido. La respuesta MUST usar `PagedResult<T>` y cada item MUST provenir de un DTO dedicado que exponga `CargoId`, `Codigo`, `Nombre`, `NivelId`, `NivelNombre`, `CargoEliminado`, `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`.

#### Scenario: Habilidad existente devuelve colección paginada

- **DADO** una habilidad existente con uno o más cargos asociados en el segmento consultado
- **CUANDO** un usuario autenticado solicita `GET /api/v1/skills/{skillId}/cargos`
- **ENTONCES** la API MUST responder `200 OK` con `Items`, `TotalCount`, `Page` y `PageSize`
- **Y** cada item MUST usar el DTO dedicado del subrecurso.

#### Scenario: Habilidad existente sin cargos devuelve vacío

- **DADO** una habilidad existente sin cargos en el segmento consultado
- **CUANDO** un usuario autenticado solicita el subrecurso
- **ENTONCES** la API MUST responder `200 OK` con `Items` vacíos
- **Y** MUST NOT responder `404` por colección vacía.

#### Scenario: Habilidad inexistente devuelve no encontrado

- **DADO** un `skillId` que no corresponde a una habilidad existente
- **CUANDO** un usuario autenticado solicita `GET /api/v1/skills/{skillId}/cargos`
- **ENTONCES** la API MUST responder `404 Not Found`.

#### Scenario: Listar habilidades activas legacy

- **DADO** que existen habilidades activas e inactivas
- **CUANDO** un usuario autenticado solicita `GET /api/v1/skills`
- **ENTONCES** el sistema MUST devolver solo habilidades activas.

#### Scenario: Obtener habilidad inexistente o inactiva

- **DADO** que una Habilidad no existe o está inactiva
- **CUANDO** un usuario autenticado la solicita por identificador
- **ENTONCES** el sistema MUST responder como recurso no encontrado para consultas activas.

#### Scenario: Consulta de eliminadas no mezcla segmentos

- **DADO** habilidades activas e inactivas en persistencia
- **CUANDO** un usuario autenticado consulta `GET /api/v1/skills/consulta?status=eliminadas&search=com&page=2&pageSize=10&sort=nombre_desc`
- **ENTONCES** la respuesta MUST exponer `Items` con habilidades inactivas únicamente que coincidan con `search=com`
- **Y** MUST aplicar normalización con `Page=1`, `PageSize=100`
- **Y** MUST exponer `TotalCount` consistente con el segmento `eliminadas` y `sort=nombre_desc`.

#### Scenario: Paginación o status inválidos se normalizan

- **DADO** habilidades activas e inactivas en persistencia
- **CUANDO** un usuario autenticado consulta `/api/v1/skills/consulta?status=archivo&page=0&pageSize=500`
- **ENTONCES** la respuesta MUST caer a `activas`
- **Y** MUST usar `page=1` y `pageSize=100`.

#### Scenario: Búsqueda sin coincidencias devuelve página vacía

- **DADO** un usuario autenticado consulta un segmento válido
- **CUANDO** `search` no coincide con ninguna habilidad del segmento
- **ENTONCES** el sistema MUST responder `200 OK` con `items` vacíos
- **Y** MUST conservar `totalCount=0` y metadatos de la página solicitada normalizada.

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

### Requirement: Edición de Codigo con unicidad activa

La actualización de una Habilidad MUST aplicar el nuevo `Codigo` cuando se provee un valor válido y MUST rechazarlo con conflicto cuando colisiona con otra Habilidad activa.

#### Scenario: Update con Codigo de otra Habilidad activa

- **DADO** una Habilidad activa existente
- **Y** existe otra Habilidad activa con el `Codigo` solicitado
- **CUANDO** se intenta actualizar la primera Habilidad con ese `Codigo`
- **ENTONCES** el sistema MUST rechazar la operación con conflicto
- **Y** MUST conservar sin cambios el estado persistido de la Habilidad editada.

#### Scenario: Update con el mismo Codigo actual

- **DADO** una Habilidad activa existente con un `Codigo` vigente
- **CUANDO** se solicita actualizarla enviando ese mismo `Codigo` junto con otros cambios válidos o sin cambios adicionales
- **ENTONCES** el sistema MUST aceptar la operación
- **Y** MUST mantener el mismo `Codigo` sin tratarlo como duplicado.

#### Scenario: Update con Codigo de una Habilidad eliminada

- **DADO** una Habilidad activa existente
- **Y** existe una Habilidad eliminada con el `Codigo` solicitado
- **CUANDO** se actualiza la Habilidad activa con ese `Codigo`
- **ENTONCES** el sistema MUST aceptar la operación
- **Y** MUST persistir el nuevo `Codigo` porque la unicidad solo aplica a registros activos.

### Requirement: Desactivar y Reactivar Habilidad

El sistema MUST permitir baja lógica y reactivación de Habilidades sin eliminación física. La desactivación MUST NOT modificar asignaciones existentes a cargos o personas; gestionar esas asignaciones queda fuera de alcance.

#### Scenario: Desactivación exitosa

- **DADO** una Habilidad activa, con o sin referencias existentes
- **CUANDO** se solicita desactivarla
- **ENTONCES** el sistema MUST marcarla inactiva sin eliminar el registro
- **Y** MUST NOT alterar relaciones existentes.

#### Scenario: Reactivación sin conflicto

- **DADO** una Habilidad inactiva con `Codigo` "COM01"
- **Y** no existe otra Habilidad activa con ese `Codigo`
- **CUANDO** se solicita reactivarla
- **ENTONCES** el sistema MUST restaurar su estado activo conservando `Codigo`.

#### Scenario: Reactivación con conflicto activo

- **DADO** una Habilidad inactiva con `Codigo` "COM01"
- **Y** existe otra Habilidad activa con `Codigo` "COM01"
- **CUANDO** se solicita reactivarla
- **ENTONCES** el sistema MUST rechazar la operación con conflicto.

### Requirement: Excluir Asignaciones Iniciales

El sistema MUST NOT incluir en esta porción endpoints ni comandos de escritura para asignar Habilidades a cargos o personas. El sistema MAY incluir lecturas readonly del subrecurso `GET /api/v1/skills/{skillId}/cargos` sin extender ese alcance a creación, edición o baja del vínculo.

#### Scenario: Operaciones de asignación no disponibles

- **DADO** que el módulo de Habilidades está publicado con el subrecurso readonly
- **CUANDO** un cliente revisa el contrato de `/api/v1/skills`
- **ENTONCES** MAY encontrar `GET /api/v1/skills/{skillId}/cargos`
- **Y** MUST NOT encontrar operaciones write de `CargoHabilidad` ni `PersonaHabilidad`.

### Requirement: Publicar catálogo HTTP de niveles de habilidad

El sistema MUST exponer `GET /api/v1/niveles-habilidad` como catálogo consumer-safe para `NivelHabilidad`, ordenado ascendentemente por `Orden`, sin mezclar reglas de asignación a cargos o personas.

#### Scenario: Catálogo de niveles disponible para web

- **DADO** que existen niveles de habilidad sembrados en persistencia
- **CUANDO** un cliente solicita `GET /api/v1/niveles-habilidad`
- **ENTONCES** el sistema MUST responder `200 OK` con elementos ordenados por `orden`
- **Y** cada elemento MUST exponer solo campos consumer-safe del nivel.

#### Scenario: Catálogo vacío sigue siendo válido

- **DADO** que no existen niveles de habilidad publicados
- **CUANDO** un cliente solicita `GET /api/v1/niveles-habilidad`
- **ENTONCES** el sistema MUST responder `200 OK` con una colección vacía.

### Requirement: Autorización de endpoints de habilidades

`SkillsController` MUST requerir autenticación a nivel de controller. `GET /api/v1/skills`, `GET /api/v1/skills/{id}`, `GET /api/v1/skills/consulta` y `GET /api/v1/skills/{skillId}/cargos` MUST permitir cualquier usuario autenticado. `POST`, `PUT`, `DELETE` y `PATCH /reactivar` MUST requerir rol `Administrador`.

#### Scenario: Lecturas autenticadas exitosas

- **DADO** un usuario autenticado
- **CUANDO** solicita una lectura de `SkillsController`, incluido `GET /api/v1/skills/{skillId}/cargos`
- **ENTONCES** la API MUST responder `2xx` con el contrato documentado.

#### Scenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita una lectura o mutación de `SkillsController`, incluido el subrecurso de cargos por habilidad
- **ENTONCES** la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- **DADO** una solicitud válida de create, update, delete o reactivate
- **CUANDO** la ejecuta un usuario autenticado sin rol `Administrador`
- **ENTONCES** la API MUST responder `403 Forbidden`
- **Y** si la ejecuta un `Administrador`, MUST conservar su contrato `2xx` vigente.

### Requirement: REQ-HM-NEW-PAGE — Página de personas por habilidad

El sistema MUST exponer `Pages/Organizacion/Habilidades/Personas.cshtml` para mostrar las personas que poseen una habilidad específica. La página MUST ofrecer paginación, búsqueda, orden y toggle entre `activas` y `eliminadas`, mostrando legajo, apellidos, nombres, email y nivel.

#### Scenario: Consulta paginada con segmento

- **DADO** una habilidad válida y un usuario autenticado
- **CUANDO** abre `Habilidades/Personas` con búsqueda, orden o `status`
- **ENTONCES** la grilla MUST mostrar los resultados paginados del segmento solicitado.

#### Scenario: Sin resultados

- **DADO** una habilidad sin personas que coincidan con el filtro
- **CUANDO** se carga la página
- **ENTONCES** MUST mostrar un estado vacío legible y conservar los controles de búsqueda y segmento.

### Requirement: REQ-HM-NEW-AUTH — Acceso de lectura autenticado

La página MUST usar `[Authorize]` sin restricción de rol; cualquier usuario autenticado puede consultar y un anónimo MUST ser redirigido al sign-in.

#### Scenario: Acceso anónimo y autenticado

- **DADO** una solicitud anónima o de un usuario autenticado
- **CUANDO** se solicita la página
- **ENTONCES** el anónimo MUST ser redirigido al sign-in y el autenticado MUST poder cargarla.

### Requirement: REQ-HM-NEW-READONLY — Sin gestión de asociaciones

La página MUST ser solo lectura y MUST NOT exponer formularios de alta, modificación o baja de habilidad sobre persona. La gestión MUST continuar exclusivamente en `Pages/Personas/PersonaHabilidades`.

#### Scenario: Acciones de la página

- **DADO** una página cargada con personas asociadas
- **CUANDO** se inspeccionan sus acciones
- **ENTONCES** MUST existir navegación de consulta, pero MUST NOT existir alta, baja o edición del vínculo.

### Requirement: REQ-HM-NEW-LINK — Enlace al detalle de Persona

Cada fila de la grilla MUST enlazar al detalle correspondiente en `Pages/Personas/Details` usando el identificador de la persona.

#### Scenario: Navegación al detalle

- **DADO** una fila con `PersonaId` válido
- **CUANDO** el usuario selecciona la persona
- **ENTONCES** el enlace MUST apuntar al detalle de esa misma persona.

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
