# Spec Delta: cargo-management — cargos-filtro-activos-eliminados

## Propósito

Extender la capacidad de gestión de cargos para consultar segmentos activos o eliminados sin mezclar conjuntos, manteniendo la reactivación existente y moviendo la paginación al backend.

## Requisitos

### REQ-CM-01: Consulta segmentada de cargos eliminados
**DADO** una lectura autenticada a `GET /api/v1/cargos/consulta` con `status=eliminadas`, `p`, `pageSize`, `search` y `sort`; **CUANDO** el controller normaliza el borde HTTP y delega el segmento eliminado; **ENTONCES** el sistema MUST devolver una página server-side con solo cargos eliminados, MUST respetar paginación/búsqueda/orden y MUST NOT mezclar cargos activos.

### REQ-CM-02: Consulta activa por defecto y normalización de estado
**DADO** una lectura autenticada a `GET /api/v1/cargos/consulta` con `status=activas`, sin `status` o con un valor desconocido; **CUANDO** la API traduce el query string al contrato de aplicación; **ENTONCES** el sistema MUST usar `CargoSegmentoListado` en aplicación/controller, MUST normalizar cualquier valor no reconocido a activas en el borde HTTP y MUST devolver solo cargos activos no eliminados.

### REQ-CM-03: Metadatos paginados provenientes del repositorio
**DADO** una consulta paginada de cargos por segmento; **CUANDO** la aplicación construye la respuesta `PagedResult`; **ENTONCES** `TotalCount` y `TotalPages` MUST provenir del repositorio consultado para ese segmento y MUST NOT calcularse en memoria a partir de `GetAllAsync`.

### REQ-CM-04: Reactivación de cargo con unicidad activa preservada
**DADO** un cargo eliminado lógicamente y el endpoint `PATCH /api/v1/cargos/{id}/reactivar`; **CUANDO** se solicita la reactivación; **ENTONCES** el sistema MUST reactivar solo si no existe otro cargo activo con el mismo código y MUST responder conflicto sin reactivar cuando la unicidad activa sea violada.

## Escenarios

### ESC-CM-01: Consulta de eliminadas no mezcla segmentos
Given cargos activos y eliminados en persistencia
When un cliente autenticado consulta `GET /api/v1/cargos/consulta?status=eliminadas&p=2&pageSize=10&search=ana&sort=nombre_desc`
Then la respuesta contiene solo eliminados y preserva `p`, `pageSize`, `search` y `sort`

## Source
- `openspec/specs/cargo-management/spec.md:34-48`
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:34-39`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:166-183,244-246`

## Verification
- Aplicación: `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas`
- Persistencia MySQL: `QueryAsync_MySql_SegmentosNoSeMezclan`
- API: `GET_consulta_status_eliminadas_RetornaSoloEliminadas`

### ESC-CM-02: Status inválido cae a activas
Given cargos activos y eliminados en persistencia
When un cliente autenticado consulta `GET /api/v1/cargos/consulta?status=archivo`
Then la API normaliza el valor a activas y devuelve solo cargos activos no eliminados

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:34-39,43-45`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:117-126,166-171`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md:11-14,56-73`

## Verification
- Aplicación/API: `NormalizeStatus_ValorDesconocido_CaeA_Activas`
- API: `GET_consulta_status_invalido_CaeA_Activas`

### ESC-CM-03: Paginación server-side devuelve metadatos consistentes
Given más resultados de los que caben en una página para un segmento
When la aplicación resuelve `GET /api/v1/cargos/consulta`
Then `TotalCount` y `TotalPages` provienen del repositorio segmentado y no de una lista completa cargada en memoria

## Source
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:35-39,44-45,54-55`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:58-61,166-171,244-246`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md:75-82`

## Verification
- Aplicación: prueba de `QueryAsync` que valida `TotalCount` y `TotalPages`
- Persistencia MySQL: prueba de `QueryAsync` con conteo segmentado
- API: prueba de contrato paginado de `GET /api/v1/cargos/consulta`

### ESC-CM-04: Reactivación rechaza código activo duplicado
Given un cargo eliminado y otro cargo activo con el mismo código
When un administrador invoca `PATCH /api/v1/cargos/{id}/reactivar`
Then la operación falla con conflicto y el cargo eliminado permanece fuera del segmento activo

## Source
- `openspec/specs/cargo-management/spec.md:131-154`
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:36-39,49-50,56-57`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:78-81,88-89,201-203`

## Verification
- Aplicación/API: `PATCH_reactivar_RetornaConflictoPorCodigoActivo`
- Persistencia MySQL: `QueryAsync_MySql_ActivaYEliminada_MismoCodigo_RetornaAmbasEnDistintosSegmentos`

## No-objetivos

- No cambiar `GET /api/v1/cargos` ni `GET /api/v1/cargos/{id}` como lecturas activas vigentes.
- No alterar el modelo de soft-delete ni la unicidad `ActiveCodigoUnique`.
- No expandir detalle, edición o creación para operar sobre cargos eliminados.
