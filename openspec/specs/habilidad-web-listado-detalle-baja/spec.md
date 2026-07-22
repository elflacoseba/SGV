# Spec: habilidad-web-listado-detalle-baja

## Purpose

Definir el slice web autenticado de `Habilidades` en `SGV.Web` con paridad funcional de listado segmentado respecto de `Cargos`, preservando el alcance en catálogo maestro y excluyendo asignaciones.

## Requirements

### Requirement: Acceso autenticado al módulo de habilidades

El sistema MUST exponer páginas Razor protegidas para listado y detalle de `Habilidades` dentro del shell autenticado.

#### Scenario: Usuario autenticado abre el módulo

- GIVEN un usuario autenticado en `SGV.Web`
- WHEN navega al módulo `Habilidades`
- THEN la aplicación MUST responder con el listado dentro del shell autenticado.

#### Scenario: Usuario anónimo intenta acceder

- GIVEN un usuario no autenticado
- WHEN solicita la URL del listado o del detalle de habilidades
- THEN la aplicación MUST redirigirlo a `/auth/sign-in`.

### Requirement: Listado segmentado server-side con búsqueda y orden

La página `Index` MUST consumir `GET /api/v1/skills/consulta`, MUST mostrar `activas` por defecto y MUST permitir alternar a `eliminadas` sin mezclar segmentos. La UI MUST preservar búsqueda y orden al cambiar de segmento y MUST resetear la página al volver a consultar otro segmento. La búsqueda y el orden del catálogo maestro MUST operar sobre `Codigo`, `Nombre`, `Categoria` y `Descripcion`, sin exponer filtros o entradas por nivel.

#### Scenario: Carga inicial en activas

- GIVEN un usuario autenticado abre `Habilidades`
- WHEN la página termina de cargar
- THEN la grilla MUST mostrar la vista `activas` por defecto
- AND MUST renderizar resultados y metadatos provenientes del backend segmentado.

#### Scenario: Cambio a eliminadas preserva contexto

- GIVEN un usuario está en `activas` con búsqueda u orden aplicados
- WHEN cambia a `eliminadas`
- THEN la navegación MUST preservar búsqueda y orden
- AND MUST reiniciar la página del listado.

#### Scenario: Búsqueda sin coincidencias

- GIVEN un segmento válido ya cargado
- WHEN la búsqueda no devuelve coincidencias
- THEN la interfaz MUST mostrar un estado vacío entendible
- AND MUST mantener visible el selector de segmento.

### Requirement: Acciones contextuales por segmento

La vista `activas` MUST mostrar `Detalle`, `Cargos`, `Personas`, `Editar` y `Eliminar`. `Cargos` y `Personas` MUST navegar a sus subrecursos preservando `p`, `search`, `sort` y `status`. La vista `eliminadas` MUST ocultar `Detalle`, `Cargos`, `Personas`, `Editar` y `Eliminar`, y MUST mostrar solo `Reactivar`.
(Previously: la vista activa no incluía la acción `Personas`; la vista eliminada solo excluía las acciones existentes.)

#### Scenario: Vista activa muestra acciones de consulta

- GIVEN una habilidad activa visible en la grilla
- WHEN se renderiza la vista `activas`
- THEN la fila MUST exponer `Detalle`, `Cargos`, `Personas`, `Editar` y `Eliminar`.

#### Scenario: Navegación a cargos preserva contexto del listado

- GIVEN un usuario está en `Habilidades/Index` con `p`, `search`, `sort` y `status` vigentes
- WHEN hace click en `Cargos` sobre una fila activa
- THEN la solicitud MUST llegar a `/organizacion/habilidades/{habilidadId}/cargos` con el `habilidadId` correcto
- AND MUST preservar en la URL los valores de `p`, `search`, `sort` y `status`.

#### Scenario: Vista eliminada mantiene solo reactivación

- GIVEN una habilidad eliminada visible en la grilla
- WHEN se renderiza la vista `eliminadas`
- THEN la fila MUST mostrar solo `Reactivar`
- AND MUST ocultar `Personas`, `Detalle`, `Cargos`, `Editar` y `Eliminar`.

### Requirement: REQ-HLD-NEW — Botón Personas por habilidad activa

Cada fila activa de `Pages/Organizacion/Habilidades/Index.cshtml` MUST exponer un botón `Personas`, con icono `ti ti-users` y clases `btn-primary btn-icon btn-sm rounded-circle`, que navegue a `Pages/Organizacion/Habilidades/Personas` con el id de la habilidad.

#### Scenario: Navegación desde una fila activa

- GIVEN una habilidad activa en `Habilidades/Index`
- WHEN el usuario selecciona `Personas`
- THEN el enlace MUST incluir el identificador correcto y apuntar a la página de personas.

### Requirement: REQ-HLD-NEW-VISIBILITY — Visibilidad sin gating de rol

El botón MUST renderizarse solo cuando `!Model.IsDeletedView` y MUST ser accesible para cualquier usuario autenticado, sin exigir rol `Administrador`.

#### Scenario: Segmento eliminado o usuario autenticado no administrador

- GIVEN una fila eliminada o un usuario autenticado sin rol administrador en activas
- WHEN se renderiza el listado
- THEN el primer caso MUST ocultar el botón y el segundo MUST mostrarlo.

### Requirement: REQ-HLD-NEW-POSITION — Orden de acciones

El botón `Personas` MUST ubicarse en la columna `Acciones`, entre `Cargos` y `Editar`.

#### Scenario: Orden visual estable

- GIVEN una fila activa con todas sus acciones
- WHEN se renderiza la columna `Acciones`
- THEN `Personas` MUST aparecer después de `Cargos` y antes de `Editar`.

### Requirement: Detalle readonly y baja/reactivación con feedback claro

El sistema MUST mostrar un detalle readonly con retorno seguro al listado, MUST confirmar baja y reactivación antes de ejecutar la acción y MUST traducir conflictos de reactivación o eliminación a feedback visible.

#### Scenario: Detalle existente

- GIVEN una habilidad activa existente
- WHEN el usuario abre su detalle desde la grilla
- THEN la página MUST mostrar sus datos en modo solo lectura
- AND MUST ofrecer una acción visible para volver al listado.

#### Scenario: Baja lógica exitosa

- GIVEN una habilidad activa visible en la grilla
- WHEN el usuario confirma la baja y el backend responde éxito
- THEN la interfaz MUST volver al listado activo con confirmación visible
- AND la fila eliminada MUST dejar de verse en `activas`.

#### Scenario: Reactivación con conflicto por código activo

- GIVEN una habilidad eliminada cuyo `Codigo` ya está ocupado por otra activa
- WHEN el usuario confirma `Reactivar`
- THEN la interfaz MUST permanecer en `eliminadas`
- AND MUST mostrar un error claro y accionable.

## Out of scope

- No incluye asignaciones `habilidad↔cargo` ni `habilidad↔persona`.
- No agrega filtros server-side por nivel a `/api/v1/skills/consulta`.
- No expone filtro por nivel de habilidad en el listado del catálogo maestro porque la entidad `Habilidad` no modela nivel propio.
- No habilita edición de registros eliminados desde la grilla.
