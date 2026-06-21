# Especificación de Identity User Role Management

## Propósito

Administrar usuarios autenticables SGV vinculados a Personas existentes, con un catálogo fijo de roles (Administrador, GestorVacantes, Consultor) y autenticación mediante Identity como preocupación de Infraestructura.

## Requisitos

### Requirement: Usuario Vinculado a Persona Existente

El sistema MUST crear y administrar usuarios autenticables solo cuando estén asociados a una `Persona` existente. Un usuario MUST NOT existir como cuenta standalone sin Persona asociada.

#### Escenario: Crear usuario para Persona existente

- **DADO** que existe una Persona registrada
- **CUANDO** se solicita crear un usuario para esa Persona con credenciales válidas
- **ENTONCES** el sistema MUST crear el usuario vinculado a esa Persona
- **Y** el vínculo MUST ser observable desde las operaciones administrativas de usuarios.

#### Escenario: Rechazar usuario sin Persona válida

- **DADO** que no existe una Persona para el identificador informado
- **CUANDO** se solicita crear un usuario
- **ENTONCES** el sistema MUST rechazar la operación sin crear la cuenta.

### Requirement: Catálogo Fijo de Roles

El sistema MUST reconocer únicamente los roles `Administrador`, `GestorVacantes` y `Consultor` en este primer corte. Los consumidores MUST NOT crear, renombrar ni eliminar roles mediante operaciones de SGV.

#### Escenario: Consultar roles disponibles

- **DADO** que el sistema expone roles asignables
- **CUANDO** se consultan los roles disponibles
- **ENTONCES** el sistema MUST devolver solo `Administrador`, `GestorVacantes` y `Consultor`.

#### Escenario: Rechazar rol fuera del catálogo

- **DADO** una solicitud que referencia un rol distinto del catálogo fijo
- **CUANDO** se intenta usarlo para un usuario
- **ENTONCES** el sistema MUST rechazar la solicitud como rol no soportado.

### Requirement: Asignación de Roles a Usuarios

El sistema MUST permitir asignar a un usuario existente uno o más roles del catálogo fijo. Toda asignación MUST respetar el catálogo aprobado y MUST NOT introducir roles nuevos por efecto lateral.

#### Escenario: Asignar rol válido

- **DADO** que existe un usuario vinculado a una Persona
- **CUANDO** se le asigna el rol `GestorVacantes`
- **ENTONCES** el usuario MUST quedar asociado a ese rol.

#### Escenario: Rechazar asignación a usuario inexistente

- **DADO** que no existe el usuario objetivo
- **CUANDO** se solicita asignarle un rol válido
- **ENTONCES** el sistema MUST rechazar la operación sin modificar asignaciones.
