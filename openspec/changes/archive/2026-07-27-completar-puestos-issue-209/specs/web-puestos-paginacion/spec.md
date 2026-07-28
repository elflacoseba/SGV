# Especificación de paginación web de puestos

## Propósito

Delta sobre `puesto-web-listado-detalle-baja`: reemplaza el listado en memoria por la consulta segmentada del issue #209 y activa la vista Eliminadas, manteniendo el contexto PRG y el feedback de conflictos.

## MODIFIED Requirements

## REQ-PTO-020 — Listado web paginado y segmentado

CUANDO un usuario autenticado abre `Puestos/Index`, la página SHALL consultar `IPuestosApiClient.QueryAsync` y renderizar paginación server-side con `TotalPages`, búsqueda y orden. El toggle SHALL permitir `Activas` y `Eliminadas`, activas SHALL ser el valor inicial, y el botón Crear SHALL ocultarse en Eliminadas. La página SHALL preservar `p`, `search`, `sort` y `status` en navegación y PRG. (Previously: consultaba `GetAllAsync`, filtraba/ordenaba en memoria y mostraba Eliminadas deshabilitado.)

#### Scenario: Carga inicial paginada
- GIVEN un usuario autenticado abre el índice sin filtros
- WHEN la página carga
- THEN consulta el endpoint segmentado para Activas
- AND muestra las filas de la página y sus controles de paginación.

#### Scenario: Toggle de eliminadas
- GIVEN el usuario está en Activas con búsqueda y orden
- WHEN selecciona Eliminadas
- THEN navega con `status=eliminadas`, conserva búsqueda y orden y reinicia `p=1`
- AND no muestra Crear.

#### Scenario: Contexto al cambiar de página
- GIVEN una vista segmentada con búsqueda y orden
- WHEN el usuario selecciona otra página
- THEN conserva segmento, búsqueda y orden y muestra solo la página solicitada.

#### Scenario: Baja rechazada por ocupaciones
- GIVEN un puesto visible cuya baja responde 409 con `PuestoConOcupacionesActivas`
- WHEN el usuario confirma la eliminación
- THEN muestra feedback específico y conserva el puesto visible
- AND no muestra confirmación de éxito.

#### Scenario: Error de transporte
- GIVEN el cliente HTTP lanza `HttpRequestException` o `TaskCanceledException`
- WHEN se carga el índice
- THEN la excepción conserva el contrato transversal de `web-apiclient-transport-contract` y la página muestra un estado recuperable sin falsear éxito.

**Referencias:** `cargo-web-listado-detalle-baja` REQ-CW-01/04 y `unidad-organizativa-web-listado` para toggle, paginación y estados vacíos.

## Spec coverage

| Criterio de aceptación del proposal | Requisito |
|---|---|
| Segmentos, búsqueda, sort y paginación server-side | REQ-PTO-001, 002 |
| Endpoint legado sin breaking change | REQ-PTO-002 |
| Guarda y 409 estable | REQ-PTO-010 |
| Toggle, paginación, Crear oculto y PRG | REQ-PTO-020 |
| Feedback 409 sin falso éxito | REQ-PTO-020 |
| Build/tests y cobertura | Verificación TDD de los tres requisitos |
