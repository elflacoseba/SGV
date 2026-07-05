# Status: MODIFIED — habilidad-management

## Purpose

Actualizar la gestión HTTP de `Habilidades` para admitir lectura readonly del subrecurso `GET /api/v1/skills/{skillId}/cargos`, manteniendo fuera de alcance las operaciones write del vínculo `CargoHabilidad`.

## Delta desde la spec base

- **ADDED**: subrecurso readonly `GET /api/v1/skills/{skillId}/cargos` con `page`, `pageSize`, `search`, `sort` y `status=activas|eliminadas`.
- **ADDED**: DTO dedicado de lectura con datos del `Cargo` y del vínculo (`NivelRequeridoId`, `Ponderacion`, `EsObligatoria`).
- **REMOVED**: la exclusión total de `habilidad↔cargo` en lectura; la exclusión pasa a aplicar solo a writes del vínculo.

## ADDED Requirements

### Requirement: Consultar cargos asociados a una habilidad

El sistema MUST exponer `GET /api/v1/skills/{skillId}/cargos` como subrecurso readonly para cualquier usuario autenticado. La consulta MUST aceptar `page`, `pageSize`, `search`, `sort` y `status`; `status` MUST aceptar `activas|eliminadas` y MUST caer a `activas` si se omite o es inválido. La respuesta MUST usar `PagedResult<T>` y cada item MUST provenir de un DTO dedicado que exponga `CargoId`, `Codigo`, `Nombre`, `NivelId`, `NivelNombre`, `CargoEliminado`, `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`.

#### Scenario: Habilidad existente devuelve colección paginada

- GIVEN una habilidad existente con uno o más cargos asociados en el segmento consultado
- WHEN un usuario autenticado solicita `GET /api/v1/skills/{skillId}/cargos`
- THEN la API MUST responder `200 OK` con `Items`, `TotalCount`, `Page` y `PageSize`
- AND cada item MUST usar el DTO dedicado del subrecurso.

#### Scenario: Habilidad existente sin cargos devuelve vacío

- GIVEN una habilidad existente sin cargos en el segmento consultado
- WHEN un usuario autenticado solicita el subrecurso
- THEN la API MUST responder `200 OK` con `Items` vacíos
- AND MUST NOT responder `404` por colección vacía.

#### Scenario: Habilidad inexistente devuelve no encontrado

- GIVEN un `skillId` que no corresponde a una habilidad existente
- WHEN un usuario autenticado solicita `GET /api/v1/skills/{skillId}/cargos`
- THEN la API MUST responder `404 Not Found`.

## MODIFIED Requirements

### Requirement: Excluir Asignaciones Iniciales

El sistema MUST NOT incluir en esta porción endpoints ni comandos de escritura para asignar Habilidades a cargos o personas. El sistema MAY incluir lecturas readonly del subrecurso `GET /api/v1/skills/{skillId}/cargos` sin extender ese alcance a creación, edición o baja del vínculo.

(Previously: esta porción excluía cualquier operación relacionada con `CargoHabilidad` o `PersonaHabilidad`.)

#### Scenario: Operaciones write de asignación no disponibles

- GIVEN que el módulo de Habilidades está publicado con el subrecurso readonly
- WHEN un cliente revisa el contrato de `/api/v1/skills`
- THEN MAY encontrar `GET /api/v1/skills/{skillId}/cargos`
- AND MUST NOT encontrar operaciones write de `CargoHabilidad` ni `PersonaHabilidad`.

### Requirement: Autorización de endpoints de habilidades

`SkillsController` MUST requerir autenticación a nivel de controller. `GET /api/v1/skills`, `GET /api/v1/skills/{id}`, `GET /api/v1/skills/consulta` y `GET /api/v1/skills/{skillId}/cargos` MUST permitir cualquier usuario autenticado. `POST`, `PUT`, `DELETE` y `PATCH /reactivar` MUST requerir rol `Administrador`.

(Previously: las lecturas autenticadas no incluían el subrecurso `GET /api/v1/skills/{skillId}/cargos`.)

#### Scenario: Lecturas autenticadas exitosas

- GIVEN un usuario autenticado
- WHEN solicita una lectura de `SkillsController`, incluido `GET /api/v1/skills/{skillId}/cargos`
- THEN la API MUST responder `2xx` con el contrato documentado.

#### Scenario: Acceso anónimo rechazado

- GIVEN un cliente sin credenciales
- WHEN solicita una lectura o mutación de `SkillsController`, incluido el subrecurso de cargos por habilidad
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Mutación protegida por rol administrador

- GIVEN una solicitud válida de create, update, delete o reactivate
- WHEN la ejecuta un usuario autenticado sin rol `Administrador`
- THEN la API MUST responder `403 Forbidden`
- AND si la ejecuta un `Administrador`, MUST conservar su contrato `2xx` vigente.
