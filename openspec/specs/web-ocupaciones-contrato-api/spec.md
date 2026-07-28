# Spec: web-ocupaciones-contrato-api

## Purpose

Definir el contrato wire compartido y la consulta REST server-side que habilitan el módulo web de Ocupaciones sin acoplar `SGV.Web` a capas internas.

## Scope

Incluye contratos, segmentación, filtros, paginación, taxonomía y autorización vigente. Excluye migraciones de datos, subrecursos anidados y cambios de reglas de negocio.

## Cambios

- Nuevos: `SGV.Contracts/Ocupaciones/{Consultas/Dtos,Comandos}/**` y `OcupacionApiRoutes`.
- Modificados: DTOs/comandos de Aplicación, `OcupacionesController`, servicio de consulta, `IOcupacionRepository` y repositorio EF.
- Endpoint modificado: `GET /api/v1/ocupaciones?status=&personaId=&puestoId=&page=&pageSize=&search=`.
- Endpoints preservados: `GET/{id}`, `POST`, `PUT/{id}`, `PATCH/{id}/finalizar`, `PATCH/{id}/reactivar`, `DELETE/{id}`.
- Persistencia: SHALL reutilizar índices existentes; no requiere migración.

## ADDED Requirements

### Requirement: REQ-OCC-API-001 — Wire-types compartidos

CUANDO API y Web intercambian Ocupaciones, el sistema SHALL usar exclusivamente los tipos de `SGV.Contracts/Ocupaciones/`, preservar el JSON observable y mantener `SGV.Contracts` sin referencias a otros proyectos.

#### Escenarios

#### Scenario: DTO serializable
- GIVEN una Ocupación consultada
- WHEN la API serializa `OcupacionDto`
- THEN SHALL incluir ids, nombres, fechas, tipo, observaciones y estado.

#### Scenario: Enums estables
- GIVEN los contratos compilados
- WHEN se inspeccionan sus enums
- THEN SHALL existir `OcupacionEstado`, `OcupacionSegmentoListado` y `OcupacionTipoAsignacion` con los valores definidos abajo.

#### Scenario: Contratos leaf
- GIVEN el grafo de proyectos
- WHEN se inspecciona `SGV.Contracts.csproj`
- THEN SHALL no referenciar Dominio, Aplicación, API, Web ni Infraestructura.

### Requirement: REQ-OCC-API-002 — Listado segmentado

CUANDO se consulta el listado, la API SHALL aceptar `status=activas|eliminadas`, usar `activas` por defecto y SHALL retirar `includeHistory` del contrato público.

#### Escenarios

#### Scenario: Activas por defecto
- GIVEN ocupaciones de todos los estados
- WHEN se omite `status`
- THEN SHALL devolver únicamente estado `Vigente`.

#### Scenario: Historial
- GIVEN ocupaciones vigentes, finalizadas y eliminadas
- WHEN `status=eliminadas`
- THEN SHALL devolver finalizadas y eliminadas, sin vigentes.

#### Scenario: Contrato legado retirado
- GIVEN la firma y OpenAPI del endpoint
- WHEN se inspeccionan sus parámetros
- THEN SHALL no exponer ni usar `includeHistory`.

### Requirement: REQ-OCC-API-003 — Filtros contextuales server-side

CUANDO se informan `personaId` o `puestoId`, la consulta SHALL aplicar cada filtro antes de contar y paginar; si ambos existen, SHALL combinarlos con AND y SHALL no filtrar en memoria.

#### Escenarios

#### Scenario: Filtro por Persona
- GIVEN ocupaciones de varias personas
- WHEN se consulta con `personaId`
- THEN SHALL devolver solo esa Persona.

#### Scenario: Filtro por Puesto
- GIVEN ocupaciones de varios puestos
- WHEN se consulta con `puestoId`
- THEN SHALL devolver solo ese Puesto.

#### Scenario: Filtros combinados sin coincidencia
- GIVEN ids válidos sin relación entre sí
- WHEN se envían ambos filtros
- THEN SHALL devolver `Items=[]` y `TotalCount=0`.

### Requirement: REQ-OCC-API-004 — `OcupacionCommandResult` con `ErrorCategoria`

CUANDO una mutación produce un resultado, `OcupacionCommandResult` SHALL exponer `Error.Categoria`, preservar `Code`, `Message`, `FieldErrors` y mapear la taxonomía HTTP común.

#### Escenarios

#### Scenario: Validación por campo
- GIVEN una request inválida
- WHEN el servicio falla
- THEN SHALL devolver `Categoria=Validation` y errores por propiedad.

#### Scenario: Recurso o conflicto
- GIVEN una mutación produce 404 o 409
- WHEN se construye el error
- THEN SHALL usar `NotFound` o `Conflict` preservando su código funcional.

#### Scenario: Éxito
- GIVEN una mutación válida
- WHEN finaliza
- THEN SHALL devolver `IsSuccess=true`, `Value` poblado y sin error.

### Requirement: REQ-OCC-API-005 — Autorización REST preservada

CUANDO se accede a los endpoints, el sistema SHALL exigir autenticación para lecturas y rol `Administrador` para escrituras, independientemente de la UI.

#### Escenarios

#### Scenario: Lectura autenticada
- GIVEN un usuario autenticado
- WHEN consulta listado o detalle
- THEN SHALL autorizar la solicitud.

#### Scenario: Solicitud anónima
- GIVEN una solicitud sin bearer
- WHEN accede a cualquier endpoint
- THEN SHALL responder 401 sin ejecutar el caso de uso.

#### Scenario: Escritura no-admin
- GIVEN un usuario autenticado sin rol Administrador
- WHEN intenta POST, PUT, PATCH o DELETE
- THEN SHALL responder 403 sin mutar datos.

### Requirement: REQ-OCC-API-006 — Paginación server-side

CUANDO se consulta una página, el sistema SHALL devolver `PagedResult<OcupacionDto>` con `Items`, `TotalCount`, `Page` y `PageSize`, calculados después de segmento, búsqueda y filtros.

#### Escenarios

#### Scenario: Página con resultados
- GIVEN más coincidencias que `PageSize`
- WHEN se solicita una página
- THEN SHALL devolver solo sus filas y el total completo.

#### Scenario: Página fuera de rango
- GIVEN una página posterior a la última
- WHEN se consulta
- THEN SHALL devolver lista vacía conservando `TotalCount`.

#### Scenario: Total filtrado
- GIVEN un filtro contextual
- WHEN se pagina
- THEN `TotalCount` SHALL contar solo coincidencias filtradas.

## Modelo de Datos

| Tipo | Shape contractual |
|---|---|
| `OcupacionDto` | `Id`, `PersonaId`, `PersonaNombre`, `PuestoId`, `PuestoNombre`, `FechaInicio`, `FechaFin?`, `TipoAsignacion`, `Observaciones?`, `Estado` |
| Requests | Crear/Actualizar: `PersonaId`, `PuestoId`, `FechaInicio`, `TipoAsignacion`, `Observaciones?`; Finalizar: `FechaFin`, `Observaciones?` |
| `OcupacionListQuery` | `Page`, `PageSize`, `Search?`, `Segmento`, `PersonaId?`, `PuestoId?` |
| `OcupacionCommandResult` | `IsSuccess`, `Value?`, `Error{Categoria,Code,Message}?`, `FieldErrors?` |
| `PagedResult<T>` | `Items`, `TotalCount`, `Page`, `PageSize` |
| Enums | Estado: `Vigente`, `Finalizada`, `Eliminada`; Segmento: `Activas`, `Eliminadas`; Tipo: `Permanente`, `Interina`, `Temporal` |
| Rutas | Base, por id, finalizar y reactivar bajo `/api/v1/ocupaciones` |

## Errores y Taxonomía

| Resultado | `ErrorCategoria` |
|---|---|
| 400 / campos inválidos | `Validation` |
| 401 | `Unauthorized` |
| 403 | `Forbidden` |
| 404 | `NotFound` |
| 409 | `Conflict` |
| 408/5xx/transporte | `Transport` |
| Otro no exitoso | `Unexpected` |

## Dependencias

- API-002/003/006 dependen de API-001; API-004 rige todas las mutaciones.
- Consumido por `web-ocupaciones-listado`, `web-ocupaciones-crear-editar` y `web-ocupaciones-navegacion-contextual`.
- Depende de `web-apiclient-transport-contract` y de los índices existentes de Ocupaciones.
