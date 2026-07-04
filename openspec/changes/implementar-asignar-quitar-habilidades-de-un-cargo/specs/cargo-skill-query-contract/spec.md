# Delta de contrato de consulta de habilidades por cargo

## Purpose

Actualizar el contrato de lectura de `GET /api/v1/cargos/{cargoId}/skills` para alinearlo con la edición web del vínculo `CargoHabilidad`, manteniendo el subrecurso acotado al GET y sin reinyectar habilidades dentro del payload padre de `Cargo`.

## Requirements

### Requirement 1: Respuesta enriquecida y editable del vínculo

El sistema MUST devolver cada asociación de habilidad de un cargo con `skillId`, `nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill` y `nivel`. `skill` MUST exponer `{ id, codigo, nombre, descripcion, categoria }`. `nivel` MUST exponer `{ id, codigo, nombre, valorNumerico, orden }`.

#### Scenario: Devolver ids, flags y catálogos anidados

- GIVEN un `Cargo` con una o más habilidades asociadas
- WHEN un cliente solicita `GET /api/v1/cargos/{cargoId}/skills`
- THEN cada item MUST incluir `skillId`, `nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill` y `nivel`
- AND los identificadores MUST seguir poblados aun cuando existan objetos anidados.

#### Scenario: Colección vacía sin cambiar el shape

- GIVEN un `Cargo` existente sin habilidades asociadas
- WHEN un cliente solicita el subrecurso
- THEN la API MUST responder éxito con una colección vacía.

### Requirement 2: Alineación con los campos editables del vínculo

El contrato GET MUST exponer exactamente los datos que la UI necesita para rehidratar la tabla editable sin deducciones locales sobre `Ponderacion`, `EsObligatoria` ni `NivelRequeridoId`.

#### Scenario: Rehidratar una fila editable desde lectura

- GIVEN una asociación persistida con valores explícitos del vínculo
- WHEN `SGV.Web` consulta el subrecurso para mostrar la tabla
- THEN la respuesta MUST contener el valor real de `Ponderacion` y `EsObligatoria`
- AND MUST contener `NivelRequeridoId` junto con el objeto `nivel` mostrado al usuario.

### Requirement 3: Alcance acotado del contrato

El sistema MUST aplicar este enriquecimiento solo al GET del subrecurso de skills del cargo. Los payloads padres de `Cargo` MUST permanecer sin habilidades embebidas.

#### Scenario: No contaminar el contrato padre de `Cargo`

- GIVEN un cliente consume `GET /api/v1/cargos` o `GET /api/v1/cargos/{id}`
- WHEN se evalúa este cambio contractual
- THEN esos endpoints MUST conservar su shape actual
- AND MUST NOT empezar a devolver colecciones embebidas de skills por este cambio.

### Requirement 4: Ejecución acotada y autorización vigente

`GET /api/v1/cargos/{cargoId}/skills` MUST seguir requiriendo autenticación y MUST resolver la colección enriquecida sin caer en carga fila-por-fila de catálogos relacionados.

#### Scenario: Consulta autenticada y acotada

- GIVEN un usuario autenticado y múltiples asociaciones para un mismo `Cargo`
- WHEN solicita el subrecurso
- THEN la API MUST responder `2xx` con el contrato enriquecido
- AND la obtención de `skill` y `nivel` MUST permanecer acotada por request.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita `GET /api/v1/cargos/{cargoId}/skills`
- THEN la API MUST responder `401 Unauthorized`.

## Modificaciones

- Se agrega `ponderacion` al DTO de lectura del subrecurso.
- Se agrega `esObligatoria` al DTO de lectura del subrecurso.
- El identificador del nivel requerido pasa a ser parte explícita del contrato como `nivelRequeridoId`.
- Se mantiene la presencia de `skill` y `nivel` anidados y se preserva el alcance GET-only del contrato.

> **Nota**: el contrato de lectura del subrecurso expone exclusivamente `nivelRequeridoId`. NO se mantiene un alias legado `nivelId`: este delta rompe compatibilidad con cualquier cliente que esperase ese nombre y reemplaza el campo por su versión explícita.
