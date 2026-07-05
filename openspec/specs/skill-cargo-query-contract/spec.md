# Spec: skill-cargo-query-contract

## Purpose

Definir el contrato GET-only y readonly de `/api/v1/skills/{skillId}/cargos` para listar cargos asociados a una habilidad sin contaminar los payloads padre de `SkillsController` ni abrir operaciones write sobre `CargoHabilidad`.

## Requirements

### Requirement: Respuesta paginada y enriquecida del subrecurso

El sistema MUST responder `GET /api/v1/skills/{skillId}/cargos` con `PagedResult<T>` usando un DTO dedicado de cargo-por-habilidad. Cada item MUST exponer `CargoId`, `Codigo`, `Nombre`, `NivelId`, `NivelNombre`, `CargoEliminado`, `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`.

#### Scenario: Devolver metadatos paginados y datos del vínculo

- GIVEN una habilidad con uno o más cargos asociados
- WHEN un cliente solicita `GET /api/v1/skills/{skillId}/cargos`
- THEN la API MUST responder `200 OK` con `Items`, `TotalCount`, `Page` y `PageSize`
- AND cada item MUST incluir los datos del `Cargo` y del vínculo `CargoHabilidad`.

#### Scenario: Colección vacía sin cambiar el shape

- GIVEN una habilidad existente sin cargos en el segmento consultado
- WHEN un cliente solicita el subrecurso
- THEN la API MUST responder `200 OK` con `Items` vacíos y metadatos paginados válidos.

### Requirement: Query y normalización del segmento

La consulta MUST aceptar `page`, `pageSize`, `search`, `sort` y `status`. `status` MUST aceptar `activas|eliminadas` y, si se omite o es inválido, MUST caer a `activas`. Ese fallback MUST responder `200 OK` y MUST NOT tratar `status` inválido como `400` en este cambio.

#### Scenario: Status inválido cae a activas

- GIVEN una habilidad con cargos activos y eliminados
- WHEN un cliente solicita `GET /api/v1/skills/{skillId}/cargos?status=archivo`
- THEN la API MUST responder `200 OK`
- AND MUST resolver la consulta como `status=activas`.

### Requirement: Autenticación y distinción entre vacío y recurso inexistente

El subrecurso MUST requerir bearer token y MUST permitir cualquier rol autenticado. La API MUST responder `401 Unauthorized` si falta el token y MUST responder `404 Not Found` solo cuando la habilidad padre no exista; una lista vacía MUST seguir respondiendo `200 OK`.

#### Scenario: Acceso sin token es rechazado

- GIVEN un cliente sin bearer token
- WHEN solicita `GET /api/v1/skills/{skillId}/cargos`
- THEN la API MUST responder `401 Unauthorized`.

#### Scenario: Habilidad inexistente devuelve 404

- GIVEN un `skillId` inexistente
- WHEN un cliente autenticado solicita el subrecurso
- THEN la API MUST responder `404 Not Found`
- AND MUST distinguir ese caso de una colección vacía válida.

### Requirement: Alcance acotado y manejo de errores no funcionales

Este contrato MUST permanecer limitado a lectura. Quedan fuera de alcance los writes del vínculo, el soft-delete de `CargoHabilidad`, la edición inline y cualquier cambio en los payloads padre ya existentes de `SkillsController`. Los consumidores MUST tratar respuestas `5xx` como errores de transporte o fallos inesperados del backend.

#### Scenario: No contaminar contratos padre ni abrir writes

- GIVEN un cliente consume `/api/v1/skills`, `/api/v1/skills/{id}` o `/api/v1/skills/consulta`
- WHEN se evalúa este contrato
- THEN esos payloads MUST conservar su shape actual
- AND el cambio MUST NOT introducir writes ni edición inline del vínculo.
