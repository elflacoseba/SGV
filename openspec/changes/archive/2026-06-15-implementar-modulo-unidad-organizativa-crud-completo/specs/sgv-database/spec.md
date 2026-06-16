# Delta for SGV Database

## ADDED Requirements

### Requirement: Unicidad de Código Activo de Unidad Organizativa

El sistema MUST impedir que dos unidades organizativas activas compartan el mismo `Codigo`.

#### Escenario: Rechazar código activo duplicado

- DADO que existe una unidad organizativa activa con un `Codigo`
- CUANDO se guarda otra unidad activa con el mismo `Codigo`
- ENTONCES el sistema DEBE rechazar el cambio.

#### Escenario: Permitir reutilización tras baja lógica

- DADO que una unidad organizativa con un `Codigo` fue dada de baja lógicamente
- CUANDO se guarda una nueva unidad activa con ese `Codigo`
- ENTONCES el sistema PUEDE permitir el cambio si no existe otra unidad activa con ese `Codigo`.

### Requirement: Baja Lógica de Unidad Organizativa

El sistema MUST conservar las unidades organizativas eliminadas de forma lógica y excluirlas de las consultas activas.

#### Escenario: Ocultar unidad dada de baja

- DADO que una unidad organizativa fue dada de baja lógicamente
- CUANDO se consultan unidades activas
- ENTONCES la unidad dada de baja NO DEBE aparecer en los resultados.

## MODIFIED Requirements

### Requisito: Jerarquía de Unidades Organizativas

El sistema DEBE persistir unidades organizativas en una jerarquía padre-hijo que permita representar un árbol organizacional completo. La aplicación o la base de datos DEBE rechazar relaciones que generen ciclos.
(Anteriormente: la jerarquía exigía persistir relaciones padre-hijo y evitar padre propio, pero no explicitaba ciclos por descendientes.)

#### Escenario: Crear unidad hija

- DADO que existe una unidad organizativa
- CUANDO se crea una nueva unidad con esa unidad como padre
- ENTONCES la nueva unidad DEBE referenciar a la unidad padre
- Y debe ser posible recorrer el árbol desde el padre hacia sus hijos.

#### Escenario: Evitar padre propio

- CUANDO una unidad se guarda con ella misma como padre
- ENTONCES la base de datos o la aplicación DEBE rechazar el cambio.

#### Escenario: Evitar padre descendiente

- DADO que una unidad organizativa tiene descendientes
- CUANDO se guarda usando uno de sus descendientes como padre
- ENTONCES el sistema DEBE rechazar el cambio.
