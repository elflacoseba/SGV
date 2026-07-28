# Spec: web-ocupaciones-listado

## Purpose

Definir el cliente tipado y el listado Razor paginado de Ocupaciones para usuarios autenticados, con acciones administrativas y feedback uniforme.

## Scope

Incluye cliente, DI, `Index`, sidenav, segmentos, filtros y acciones por fila. Excluye formularios y navegación cruzada, definidos en specs dependientes.

## Cambios

- Nuevos: `SGV.Web/Integration/Ocupaciones/{IOcupacionApiClient,OcupacionApiClient}.cs` e `Index.{cshtml,cshtml.cs}`.
- Modificados: `Program.cs`, `_Sidenav.cshtml`, factory/fake web y pruebas del módulo.
- Endpoints consumidos: listado/detalle y mutaciones bajo `OcupacionApiRoutes`.

## ADDED Requirements

### Requirement: REQ-OCC-LST-001 — Cliente API tipado

CUANDO Web consume Ocupaciones, SHALL usar `IOcupacionApiClient`/`OcupacionApiClient`, registrado en DI con bearer, timeout vigente y métodos de consulta, creación, edición, finalización, baja y reactivación.

#### Escenarios

#### Scenario: Resolución por DI
- GIVEN el host Web iniciado
- WHEN se resuelve `IOcupacionApiClient`
- THEN SHALL obtenerse el cliente tipado autenticado.

#### Scenario: Cancelación previa
- GIVEN un token ya cancelado
- WHEN se invoca cualquier método
- THEN SHALL cancelarse sin enviar HTTP.

#### Scenario: Falla nativa
- GIVEN timeout o conectividad fallida
- WHEN el pipeline termina
- THEN SHALL propagar `TaskCanceledException` o `HttpRequestException`.

### Requirement: REQ-OCC-LST-002 — Listado paginado y filtrable

CUANDO un usuario abre `Index`, la página SHALL consultar server-side, mostrar vigentes por defecto y permitir búsqueda, `PersonaId`, `PuestoId`, `Status` y paginación sin filtrar el universo en Web.

#### Escenarios

#### Scenario: Carga inicial
- GIVEN ocupaciones vigentes
- WHEN se abre `Index`
- THEN SHALL mostrar la primera página y sus metadatos.

#### Scenario: Filtros aplicados
- GIVEN filtros y búsqueda informados
- WHEN se consulta
- THEN SHALL enviarlos a la API y renderizar solo `Items` recibidos.

#### Scenario: Sin coincidencias
- GIVEN una consulta válida sin resultados
- WHEN carga la página
- THEN SHALL mostrar estado vacío y conservar filtros.

### Requirement: REQ-OCC-LST-003 — Toggle vigentes/historial

CUANDO se cambia el toggle, `Index` SHALL usar `status=activas|eliminadas`, preservar búsqueda y filtros, reiniciar `p=1` y no mezclar estados.

#### Escenarios

#### Scenario: Cambio a historial
- GIVEN la vista activa filtrada
- WHEN se selecciona Historial
- THEN SHALL navegar con `status=eliminadas` y `p=1`.

#### Scenario: Regreso a vigentes
- GIVEN la vista histórica
- WHEN se selecciona Vigentes
- THEN SHALL usar `status=activas` conservando filtros.

#### Scenario: Crear en historial
- GIVEN `status=eliminadas`
- WHEN se renderiza la barra
- THEN SHALL ocultar la acción Nuevo.

### Requirement: REQ-OCC-LST-004 — Feedback y taxonomía uniforme

CUANDO API o transporte fallan, la página SHALL usar `CommandResultMapper`, `ErrorCategoryMapper` y `PageFeedback`, mostrar mensajes funcionales y no falsear éxito.

#### Escenarios

#### Scenario: Error HTTP tipado
- GIVEN una respuesta 400, 401, 403, 404 o 409
- WHEN el cliente la procesa
- THEN SHALL producir la categoría de la matriz inferior.

#### Scenario: Transporte recuperable
- GIVEN el cliente propaga una excepción nativa
- WHEN `Index` carga o muta
- THEN SHALL mostrar feedback recuperable sin stack trace.

#### Scenario: Sin falso éxito
- GIVEN una operación rechazada
- WHEN vuelve por PRG
- THEN SHALL no mostrar confirmación ni retirar la fila.

### Requirement: REQ-OCC-LST-005 — Navegación en sidenav

CUANDO se renderiza Organización, `_Sidenav` SHALL mostrar Ocupaciones/Listado a todo autenticado, Nuevo solo a Administrador y estado activo en rutas del módulo.

#### Escenarios

#### Scenario: Usuario autenticado
- GIVEN un usuario autenticado no-admin
- WHEN ve el menú
- THEN SHALL ver Listado y no Nuevo.

#### Scenario: Administrador
- GIVEN un Administrador
- WHEN ve el menú
- THEN SHALL ver Listado y Nuevo.

#### Scenario: Ruta activa
- GIVEN una ruta de Ocupaciones
- WHEN se renderiza el menú
- THEN SHALL expandir y marcar el submenú correspondiente.

### Requirement: REQ-OCC-LST-006 — Acciones por fila

CUANDO se renderiza una fila, SHALL ofrecer Ver a todo autenticado y SHALL limitar Editar, Eliminar o Reactivar a Administrador según `OcupacionEstado`.

#### Escenarios

#### Scenario: Vigente admin
- GIVEN una fila `Vigente` y un Administrador
- WHEN se renderiza
- THEN SHALL mostrar Ver, Editar y Eliminar.

#### Scenario: Histórica admin
- GIVEN una fila `Finalizada` o `Eliminada`
- WHEN la ve un Administrador
- THEN SHALL mostrar Ver y Reactivar, no Editar ni Eliminar.

#### Scenario: Usuario readonly
- GIVEN cualquier fila y un no-admin autenticado
- WHEN se renderiza
- THEN SHALL mostrar solo Ver.

## Modelo de Datos

`Index` SHALL consumir `OcupacionListQuery` y `PagedResult<OcupacionDto>` definidos en `web-ocupaciones-contrato-api`; el cliente SHALL devolver `OcupacionCommandResult` para mutaciones.

## Errores y Taxonomía

| Caso | Categoría / UX |
|---|---|
| 400 | `Validation` / feedback de consulta |
| 401 | `Unauthorized` / sesión requerida |
| 403 | `Forbidden` / `/error/403` |
| 404 | `NotFound` / recurso no disponible |
| 409 | `Conflict` / mensaje funcional |
| Excepción/408/5xx | `Transport` / reintento |

## Dependencias

- LST-001 depende de `web-ocupaciones-contrato-api` y `web-apiclient-transport-contract`.
- LST-002/003/006 dependen de LST-001; LST-004 es transversal.
- Base para `web-ocupaciones-crear-editar` y `web-ocupaciones-navegacion-contextual`.
