# Delta for sgv-readonly-api

## ADDED Requirements

### Requirement: Contrato documentado de filtro de listado de unidades organizativas

La documentación HTTP MUST describir que `GET /api/v1/unidades-organizativas/consulta` acepta un filtro de estado para consultar `activas` o `eliminadas`. La documentación MUST indicar que la vista por defecto es `activas`, MUST reutilizar el mismo contrato de respuesta y MUST NOT documentar una grilla mixta ni cambios al contrato del árbol.

#### Scenario: Descubrir el filtro de estado del listado

- GIVEN un cliente inspecciona la documentación de unidades organizativas
- WHEN revisa el endpoint `GET /api/v1/unidades-organizativas/consulta`
- THEN la documentación MUST describir el filtro para elegir `activas` o `eliminadas`
- AND MUST indicar que `activas` es la vista por defecto.

#### Scenario: Documentar la respuesta de eliminadas

- GIVEN un cliente inspecciona la consulta documentada de unidades organizativas
- WHEN revisa la respuesta para la vista de eliminadas
- THEN la documentación MUST describir el mismo contrato `UnidadOrganizativaDto`
- AND MUST dejar claro que la respuesta contiene solo unidades eliminadas.

#### Scenario: Mantener fuera de alcance el listado mixto y el árbol

- GIVEN un cliente compara las operaciones documentadas de lectura
- WHEN revisa el cambio del listado
- THEN la documentación MUST NOT presentar una respuesta mixta de activas y eliminadas
- AND MUST mantener el árbol documentado como una lectura separada sin este filtro.
