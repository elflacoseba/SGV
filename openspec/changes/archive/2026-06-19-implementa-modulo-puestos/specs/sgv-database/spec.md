# Delta for sgv-database

## MODIFIED Requirements

### Requisito: Puestos Concretos

El sistema DEBE persistir puestos concretos pertenecientes a unidades organizativas y basados en cargos. Cada puesto activo DEBE tener `Codigo` y `Nombre` obligatorios, DEBE conservarse mediante baja lógica cuando se elimina y DEBE poder reactivarse si no viola la unicidad activa de `Codigo`. `PuestoSuperiorId` DEBE ser opcional y NO DEBE requerir reglas jerárquicas complejas en esta versión, salvo impedir autorreferencia directa si el modelo ya lo soporta. La gestión de Ocupaciones y Vacantes NO DEBE formar parte de este cambio.
(Previously: el requisito solo exigía persistir puestos asociados a unidades organizativas y cargos, sin ciclo de vida, unicidad activa ni contrato mínimo de campos.)

#### Escenario: Puesto sin ocupante

- **DADO** que existe un puesto
- **CUANDO** no tiene ocupación activa
- **ENTONCES** el puesto DEBE seguir siendo válido y disponible para gestionar vacantes.

#### Escenario: Persistir puesto activo con campos mínimos

- **DADO** que existen una unidad organizativa y un cargo válidos
- **CUANDO** se guarda un puesto con `Codigo` y `Nombre`
- **ENTONCES** el puesto DEBE persistirse activo asociado a esa unidad y cargo.

#### Escenario: Rechazar campos obligatorios faltantes

- **DADO** una solicitud para guardar un puesto
- **CUANDO** falta `Codigo` o `Nombre`
- **ENTONCES** el sistema DEBE rechazar el cambio
- **Y** NO DEBE persistir un puesto incompleto.

#### Escenario: Unicidad de Codigo entre puestos activos

- **DADO** que existe un puesto activo con un `Codigo`
- **CUANDO** se guarda otro puesto activo con el mismo `Codigo`
- **ENTONCES** el sistema DEBE rechazar el cambio.

#### Escenario: Permitir reutilización de Codigo tras baja lógica

- **DADO** que un puesto con un `Codigo` fue dado de baja lógicamente
- **CUANDO** se crea o reactiva un puesto con ese `Codigo`
- **ENTONCES** el sistema PUEDE permitir el cambio si no existe otro puesto activo con ese `Codigo`.

#### Escenario: Baja lógica de puesto

- **DADO** que existe un puesto activo
- **CUANDO** se solicita eliminarlo
- **ENTONCES** el sistema DEBE marcarlo como inactivo sin eliminación física
- **Y** NO DEBE aparecer en consultas de puestos activos.

#### Escenario: Reactivación de puesto

- **DADO** que existe un puesto inactivo sin conflicto de `Codigo` activo
- **CUANDO** se solicita reactivarlo
- **ENTONCES** el sistema DEBE restaurar su estado activo
- **Y** DEBE conservar sus relaciones a unidad organizativa, cargo y puesto superior opcional.

#### Escenario: Puesto superior opcional

- **DADO** una solicitud para guardar un puesto sin `PuestoSuperiorId`
- **CUANDO** los demás campos requeridos son válidos
- **ENTONCES** el sistema DEBE permitir guardar el puesto sin superior.
