# Delta for vacante-web

## MODIFIED Requirements

### Requisito: Edit permite cambiar estado y observaciones

La página Edit DEBE permitir modificar `Observaciones` y cambiar el `EstadoVacanteId` invocando el cambio de estado del backend. El dropdown de estado NO DEBE ofrecer estados con `EsCubierta=true` — la transición a `Cubierta` se alcanza por el flujo de Postulación (que provee `PersonaId`), no por edición directa. Al transicionar a un estado terminal seleccionable (`Cancelada`), la interfaz DEBE reflejar el seteo automático de `FechaCierre` (PB-3: `Motivo` opcional). El filtrado del dropdown DEBE basarse en el flag `esCubierta` expuesto por el catálogo `GET /api/v1/estados-vacante`.

(Previously: el dropdown exponía todos los estados del catálogo, incluida Cubierta, permitiendo seleccionar un destino que el servicio rechazaba silenciosamente por faltar `PersonaId` — sin grabar, sin error y sin volver al listado.)

#### Escenario: Edit muestra datos actuales

- **DADO** una vacante existente
- **CUANDO** un usuario autorizado abre Edit
- **ENTONCES** la interfaz DEBE mostrar estado actual y observaciones prellenadas.

#### Escenario: El dropdown excluye estados Cubierta

- **DADO** un usuario `Administrador` o `GestorVacantes` autenticado
- **Y** una vacante existente en estado `Abierta` o `En Selección`
- **CUANDO** hace `GET` a `/organizacion/vacantes/editar/{id}`
- **ENTONCES** el `<select name="Input.EstadoVacanteId">` renderizado SOLO contiene `<option>` para estados con `esCubierta=false`
- **Y** NINGÚN `<option>` corresponde al estado `Cubierta` (`esCubierta=true`).

#### Escenario: Cancelada sigue siendo seleccionable

- **DADO** una vacante abierta en Edit
- **CUANDO** se renderiza el dropdown de estado
- **ENTONCES** DEBE contener un `<option>` para `Cancelada` (`esCubierta=false`)
- **Y** su `value` DEBE corresponder al GUID de `Cancelada` del catálogo sembrado.

#### Escenario: Cambio a Cancelada setea FechaCierre

- **DADO** una vacante abierta en Edit
- **CUANDO** el usuario selecciona estado `Cancelada` y guarda
- **ENTONCES** la interfaz DEBE redirigir y reflejar `FechaCierre` poblada
- **Y** DEBE mostrar mensaje visible de éxito.

#### Escenario: El catálogo expone el flag esCubierta

- **DADO** que el cliente web solicita `GET /api/v1/estados-vacante`
- **CUANDO** recibe la respuesta JSON
- **ENTONCES** cada item DEBE incluir el campo `esCubierta` (boolean)
- **Y** el campo DEBE reflejar `EstadoVacante.EsCubierta` de la BD, permitiendo al cliente filtrar el dropdown.

## ADDED Requirements

### Requisito: Cubierta no es destino directo desde Edit

El formulario de edición de Vacante NO DEBE permitir la transición directa a `Cubierta`. `Cubierta` DEBE permanecer como responsabilidad del flujo de Selección/Postulación — que provee `PersonaId` — fuera del alcance de este cambio. Las transiciones desde Edit quedan restringidas a estados no cubiertos, none de los cuales requiere `PersonaId`.

#### Escenario: Destinos del dropdown restringidos

- **DADO** un usuario autenticado con rol mutador en Edit
- **CUANDO** intenta cambiar el estado de la vacante
- **ENTONCES** los destinos visibles quedan restringidos a `Abierta`, `En Selección` y `Cancelada`
- **Y** ninguna de esas transiciones requiere `PersonaId` desde el form.

## REMOVED Requirements

Ninguno.