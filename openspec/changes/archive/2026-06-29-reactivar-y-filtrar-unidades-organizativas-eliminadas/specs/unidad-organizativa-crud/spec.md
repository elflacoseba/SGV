# Delta for unidad-organizativa-crud

## ADDED Requirements

### Requirement: Consulta segmentada de unidades organizativas por estado

El sistema MUST permitir que el contrato de consulta de unidades organizativas solicite exactamente uno de dos segmentos: `activas` o `eliminadas`. La consulta MUST devolver activas por defecto, MUST devolver solo eliminadas cuando se solicite esa vista y MUST NOT mezclar ambos conjuntos en una misma respuesta.

#### Scenario: Consulta por defecto devuelve activas

- GIVEN unidades organizativas activas y eliminadas en persistencia
- WHEN un cliente consulta el listado sin indicar vista de eliminadas
- THEN el sistema MUST devolver solo unidades activas
- AND MUST excluir las eliminadas de la respuesta.

#### Scenario: Consulta explícita de eliminadas

- GIVEN unidades organizativas activas y eliminadas en persistencia
- WHEN un cliente consulta el listado solicitando la vista de eliminadas
- THEN el sistema MUST devolver solo unidades eliminadas
- AND MUST excluir las activas de la respuesta.

#### Scenario: Segmentos de lectura no se mezclan

- GIVEN unidades organizativas en ambos estados
- WHEN un cliente consume el contrato de consulta
- THEN cada respuesta MUST corresponder a un único segmento de estado
- AND MUST preservar el contrato de reactivación existente como operación separada.
