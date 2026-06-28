# Delta for unidad-organizativa-crud

## ADDED Requirements

### Requirement: Resumen legible de unidad padre en lecturas

El sistema MUST enriquecer `UnidadOrganizativaDto` con `unidadPadreCodigo` y `unidadPadreNombre` para respuestas de lectura, manteniendo `unidadPadreId` como referencia estable y sin exigir consultas adicionales para mostrar detalle o edición web.

#### Scenario: Lectura de unidad con padre

- GIVEN una unidad organizativa activa con padre `RECT` / `Rectorado`
- WHEN un cliente consulta la unidad por id o dentro de `consulta`
- THEN la respuesta MUST incluir `unidadPadreId`
- AND MUST incluir `unidadPadreCodigo = "RECT"` y `unidadPadreNombre = "Rectorado"`.

#### Scenario: Lectura de unidad raíz

- GIVEN una unidad organizativa activa sin padre
- WHEN un cliente consulta la unidad por id o dentro de `consulta`
- THEN la respuesta MUST mantener `unidadPadreId` nulo
- AND `unidadPadreCodigo` y `unidadPadreNombre` MUST ser nulos.
