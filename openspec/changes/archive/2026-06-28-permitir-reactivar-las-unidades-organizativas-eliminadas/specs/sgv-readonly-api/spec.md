# Delta for sgv-readonly-api

## ADDED Requirements

### Requirement: Contrato documentado de reactivación de unidades organizativas

La documentación HTTP MUST describir `PATCH /api/v1/unidades-organizativas/{id}/reactivar` como una operación soportada para unidades organizativas. La documentación MUST reflejar respuesta exitosa con `UnidadOrganizativaDto` y MUST documentar conflictos previsibles de reactivación sin inventar reglas nuevas.

#### Scenario: Descubrir el endpoint de reactivación

- GIVEN un cliente inspecciona la documentación de unidades organizativas
- WHEN revisa las operaciones disponibles
- THEN la documentación MUST incluir `PATCH /api/v1/unidades-organizativas/{id}/reactivar`.

#### Scenario: Documentar respuesta exitosa

- GIVEN un cliente inspecciona el endpoint de reactivación
- WHEN revisa la respuesta satisfactoria
- THEN la documentación MUST indicar `200 OK`
- AND MUST describir que devuelve `UnidadOrganizativaDto`.

#### Scenario: Documentar errores previsibles

- GIVEN un cliente inspecciona el endpoint de reactivación
- WHEN revisa sus errores posibles
- THEN la documentación MUST incluir `404 Not Found`
- AND MUST incluir `409 Conflict` para código activo duplicado o padre inactivo/eliminado.
