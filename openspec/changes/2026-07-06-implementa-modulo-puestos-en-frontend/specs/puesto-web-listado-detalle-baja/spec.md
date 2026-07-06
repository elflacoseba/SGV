# Especificación de listado, detalle y baja web de puestos

## Purpose

Slice autenticado del módulo `Puestos` en `SGV.Web` con paridad operativa respecto de `Cargos`: tabla plana, baja lógica confirmada, reactivación con feedback y detalle readonly. El segmento "Eliminadas" se renderiza visible pero `disabled` (backend sin endpoint segmentado). **No** incluye vista de árbol.

## Requirements

### Requirement: Acceso autenticado al módulo de puestos

Páginas Razor protegidas para listado y detalle de `Puestos` dentro del shell autenticado.

#### Scenario: Acceso autenticado vs anónimo
- GIVEN un usuario en `SGV.Web`
- WHEN navega al módulo `Puestos`
- IF autenticado THEN MUST responder con el listado dentro del shell autenticado y el segmento `Activas` activo por defecto.
- IF anónimo THEN MUST redirigirlo a `/auth/sign-in`.

### Requirement: Listado plano con toggle deshabilitado

`Index` MUST renderizar tabla plana con columnas `Codigo`, `Nombre`, `Unidad Organizativa`, `Cargo`, `Puesto superior` (link o celda vacía), ordenar por `Codigo` y MUST ofrecer búsqueda, orden y paginación en memoria. Toggle MUST mostrarse; `Eliminadas` MUST estar `disabled` con tooltip al follow-up.

#### Scenario: Carga inicial con columnas locked
- GIVEN un usuario autenticado abre `Puestos`
- WHEN la página termina de cargar
- THEN la tabla MUST mostrar puestos activos de `GET /api/v1/puestos`
- AND cada fila MUST ofrecer `Detalle`, `Editar` y `Eliminar`.

#### Scenario: Puesto superior como link con contexto
- GIVEN un puesto activo con `PuestoSuperiorId` no nulo
- WHEN se renderiza la columna
- THEN la celda MUST contener link al detalle del superior con `p`, `search`, `sort` y `status` preservados.

#### Scenario: Toggle Eliminadas deshabilitado con tooltip
- GIVEN el control toggle
- WHEN se renderiza la barra superior
- THEN `Eliminadas` MUST estar `disabled` y MUST incluir tooltip al follow-up.

### Requirement: Baja lógica confirmada con feedback

`Index` MUST pedir confirmación SweetAlert2 antes de la baja, MUST redirigir por PRG y MUST traducir rechazos a feedback claro.

#### Scenario: Cancelación no elimina
- GIVEN una fila activa visible
- WHEN el usuario cancela la confirmación
- THEN MUST NOT ejecutar la eliminación y la fila MUST permanecer visible.

#### Scenario: Baja éxito o conflicto
- GIVEN un puesto activo eliminable
- WHEN el usuario confirma la baja
- IF backend responde 204 THEN MUST redirigir al listado con confirmación y el puesto MUST dejar de verse.
- IF backend responde 409 THEN MUST mostrar mensaje claro de conflicto y el puesto MUST permanecer visible.

### Requirement: Reactivación con feedback de conflicto

`Index` MUST permitir reactivar un puesto por `Id` (vía `LastDeletedId` en `TempData`) y MUST traducir 409 por código duplicado a feedback.

#### Scenario: Reactivación exitosa limpia banner
- GIVEN un puesto eliminado con `Id` en `LastDeletedId`
- WHEN el usuario confirma la reactivación desde Activas
- THEN MUST redirigir a Activas con confirmación y limpiar `LastDeletedId`.

#### Scenario: Reactivación con conflicto por código
- GIVEN un puesto a reactivar cuyo `Codigo` ya está usado por otro activo
- WHEN el backend responde 409
- THEN MUST permanecer en la vista de origen con banner claro del código.

### Requirement: Detalle readonly con retorno preservando contexto

`Details` MUST mostrar `Codigo`, `Nombre`, `Descripcion?`, nombres de `Unidad Organizativa`/`Cargo` y link al `Puesto superior` cuando exista.

#### Scenario: Detalle existente o no disponible
- GIVEN un puesto solicitado por id
- WHEN el usuario abre su detalle
- IF existe y activo THEN MUST mostrar datos en solo lectura y el link de retorno MUST preservar `p`, `search`, `sort` y `status`.
- IF no consultable como activo THEN MUST mostrar estado recuperable con retorno preservando contexto.

### Requirement: Entrada colapsable "Puestos" en el sidenav

`_Sidenav` MUST exponer, dentro de `Organización`, entry colapsable `Puestos` con sub-items `Listado`/`Nuevo`, y marcarla activa cuando la ruta sea `/organizacion/puestos(/...)`.

#### Scenario: Submenú visible y activo
- GIVEN un usuario autenticado en `/organizacion/puestos` o subruta
- WHEN se renderiza el sidenav
- THEN `Puestos` MUST estar expandido y MUST reflejar estado `active` en el sub-item.
