# Especificación de consulta segmentada de puestos

## Propósito

Nueva capacidad del proposal de issue #209 para consultar puestos activos o eliminados con búsqueda, orden y paginación server-side. Sigue el patrón de `cargo-management` (REQ-CM-01..03) y `unidad-organizativa-web-listado`; no modifica el contrato legado de listado general.

## Requisitos

## REQ-PTO-001 — Consulta segmentada paginada

CUANDO un consumidor consulta puestos, el sistema SHALL aceptar `Page`, `PageSize`, `Search`, `Sort` y `Segmento`, y devolver `PagedResult<PuestoDto>` con `Items`, `TotalCount` y metadatos consistentes. La consulta SHALL aplicar segmento, búsqueda sobre código/nombre/descripción, orden soportado (`codigo_asc`, `codigo_desc`, `nombre_asc`, `nombre_desc`) y paginación en ese orden, sin mezclar activos y eliminados. El segmento por defecto IS `Activas`.

#### Scenario: Consulta activa por defecto
- GIVEN existen puestos activos y eliminados
- WHEN se consulta sin segmento
- THEN devuelve solo activos y `TotalCount` corresponde a ese segmento.

#### Scenario: Consulta eliminada con filtros
- GIVEN existen puestos eliminados que coinciden con una búsqueda
- WHEN se consulta `Eliminadas` con orden y página
- THEN devuelve solo coincidencias eliminadas y aplica el orden antes de `Skip/Take`.

#### Scenario: Página fuera del conjunto
- GIVEN un segmento con menos filas que la página solicitada
- WHEN se consulta una página posterior
- THEN devuelve una colección vacía sin mezclar otro segmento y conserva `TotalCount`.

**Nota:** EF Core SHALL ejecutar la consulta con lectura sin tracking e incluir Unidad Organizativa y Cargo. No se requieren migraciones.

## REQ-PTO-002 — Endpoint HTTP de consulta

CUANDO un cliente autenticado solicita `GET /api/v1/puestos/consulta`, la API SHALL mapear `page`, `pageSize`, `search`, `sort` y `status` al contrato de consulta; `status=eliminadas` selecciona eliminados y cualquier otro valor, incluido ausente, selecciona activos. El endpoint SHALL coexistir con `GET /api/v1/puestos`, que conserva su forma vigente.

#### Scenario: Endpoint devuelve página segmentada
- GIVEN un cliente autenticado solicita `status=eliminadas&page=1&pageSize=10`
- WHEN la API procesa la solicitud
- THEN responde 200 con `PagedResult<PuestoDto>` solo de eliminados.

#### Scenario: Cliente anónimo
- GIVEN una solicitud sin credenciales
- WHEN accede al endpoint de consulta
- THEN la API responde 401.

#### Scenario: Endpoint legado preservado
- GIVEN un consumidor usa `GET /api/v1/puestos`
- WHEN solicita el listado existente
- THEN recibe `IReadOnlyList<PuestoDto>` sin cambio de shape.
