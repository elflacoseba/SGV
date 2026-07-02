# Spec Delta: sgv-readonly-api — cargos-filtro-activos-eliminados

## Propósito

Actualizar la capacidad de descubrimiento HTTP para que Swagger documente la nueva consulta segmentada de cargos y mantenga visible el contrato existente de reactivación.

## Requisitos

### REQ-SRA-01: Swagger documenta consulta segmentada y reactivación de cargos
**DADO** la documentación HTTP de SGV; **CUANDO** un consumidor inspecciona los endpoints de cargos; **ENTONCES** Swagger MUST exponer `GET /api/v1/cargos/consulta` con el filtro `status=activas|eliminadas`, MUST indicar que activas es el valor por defecto y MUST mantener visible `PATCH /api/v1/cargos/{id}/reactivar` con sus respuestas documentadas.

## Escenarios

### ESC-SRA-01: Swagger permite descubrir consulta y reactivación de cargos
Given un consumidor abre Swagger para revisar el recurso de cargos
When inspecciona las operaciones documentadas del controller
Then encuentra `GET /api/v1/cargos/consulta` con `status` documentado y también `PATCH /api/v1/cargos/{id}/reactivar`

## Source
- `openspec/specs/sgv-readonly-api/spec.md:89-112`
- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md:35-39,43-45`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md:91-111`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/specs/sgv-readonly-api/spec.md:5-28`

## Verification
- API/Swagger: extensión de `SwaggerConfigurationTests`
- API: prueba de documentación efectiva de `GET /api/v1/cargos/consulta` y `PATCH /api/v1/cargos/{id}/reactivar`

## No-objetivos

- No volver públicos anónimos los endpoints de cargos.
- No documentar una grilla mixta de activos y eliminados.
- No alterar el contrato de respuesta de `CargoDto` fuera del filtro de consulta.
