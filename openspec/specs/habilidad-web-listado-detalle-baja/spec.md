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

La vista `activas` MUST mostrar `Detalle`, `Editar` y `Eliminar`; la vista `eliminadas` MUST ocultar esas acciones y MUST mostrar solo `Reactivar` por fila.

#### Scenario: Vista activas muestra acciones de catálogo activo

- GIVEN una habilidad activa visible en la grilla
- WHEN se renderiza la vista `activas`
- THEN la fila MUST exponer `Detalle`, `Editar` y `Eliminar`.

#### Scenario: Vista eliminadas muestra solo reactivación

- GIVEN una habilidad eliminada visible en la grilla
- WHEN se renderiza la vista `eliminadas`
- THEN la fila MUST mostrar solo `Reactivar`
- AND MUST ocultar `Detalle`, `Editar` y `Eliminar`.

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
