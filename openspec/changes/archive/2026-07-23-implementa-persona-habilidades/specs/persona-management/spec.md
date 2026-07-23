# Delta de Persona Management

> Delta introducida por el change `implementa-persona-habilidades`. La propuesta agrega navegación al subrecurso `Persona↔Habilidad` desde `Details`, sin contaminar el payload padre ni relajar el gating vigente.

## ADDED Requirements

### Requirement: Navegación a la página de habilidades de la persona

`/personas/detalle/{id}` MUST exponer una acción visible que permita al `Administrador` acceder a `/personas/{id:guid}/habilidades` para gestionar el subrecurso `Persona↔Habilidad`. La acción MUST renderizarse solo cuando la persona sea consultable como activa, en línea con el resto de las acciones del detalle.

#### Scenario: Detalle activo expone acción hacia habilidades

- **DADO** un `Administrador` abriendo el detalle de una persona activa
- **CUANDO** la página se renderiza
- **ENTONCES** MUST existir un enlace o botón visible hacia `/personas/{id:guid}/habilidades`
- **Y** MUST estar etiquetado de forma que su propósito sea inequívoco.

#### Scenario: Detalle no consultable no expone la acción

- **DADO** que la persona no es consultable como activa (`IsNotFound == true` o estado recuperable equivalente)
- **CUANDO** la página de detalle se renderiza
- **ENTONCES** la acción hacia habilidades MUST NOT renderizarse.

#### Scenario: Persona con navegación no habilitada

- **DADO** un usuario autenticado sin rol `Administrador` en el detalle de una persona activa
- **CUANDO** la página se renderiza
- **ENTONCES** la acción hacia habilidades MUST NOT renderizarse
- **Y** el acceso al subrecurso MUST seguir bloqueado por la frontera de autorización vigente.
