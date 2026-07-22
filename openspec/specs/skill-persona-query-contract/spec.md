# skill-persona-query-contract — especificación

## Propósito

Definir el contrato backend↔web, exclusivamente de lectura, para consultar las personas asociadas a una habilidad mediante `GET /api/v1/skills/{skillId}/personas`.

## Requisitos

### Requirement: REQ-SPQC-01 — Respuesta paginada de personas por habilidad

El sistema MUST retornar una lista paginada de personas que poseen la habilidad solicitada. Una habilidad existente sin coincidencias MUST retornar una página válida y vacía.

#### Scenario: Habilidad con personas asociadas

* **GIVEN** una habilidad existente con personas asociadas
* **WHEN** se solicita `GET /api/v1/skills/{skillId}/personas`
* **THEN** la API MUST responder `200 OK` con los elementos paginados.

#### Scenario: Habilidad sin personas en el segmento

* **GIVEN** una habilidad existente sin personas en el segmento consultado
* **WHEN** se solicita el subrecurso
* **THEN** la API MUST responder `200 OK` con `Items` vacío y metadatos válidos.

### Requirement: REQ-SPQC-02 — Parámetros y normalización de consulta

El endpoint MUST aceptar `page` (por defecto `1`), `pageSize` (por defecto `20`, máximo `100`), `search` y `status` (`activas` por defecto, o `eliminadas`). `search` MUST buscar por substring sin distinguir mayúsculas en legajo, nombres y apellidos. `sort` MUST aceptar únicamente `legajo_asc|legajo_desc|apellidos_asc|apellidos_desc|nombres_asc|nombres_desc`, con `apellidos_asc` por defecto.

#### Scenario: Consulta con filtros y orden válidos

* **GIVEN** una solicitud con `search`, `sort=legajo_desc`, `status=eliminadas` y `pageSize=50`
* **WHEN** se procesa la consulta
* **THEN** MUST aplicar búsqueda, segmento, orden y tamaño solicitados.

#### Scenario: Valores fuera de rango

* **GIVEN** `page=0` o `pageSize=500` y un `sort` desconocido
* **WHEN** se procesa la consulta
* **THEN** MUST normalizar a `page=1`, `pageSize=100` y `sort=apellidos_asc`.

### Requirement: REQ-SPQC-03 — Shape de `SkillPersonaDetailDto`

El wire-type MUST exponer `PersonaDto Persona`, `NivelHabilidadDto Nivel` y propiedades init-only `PersonaId`, `HabilidadId` y `NivelHabilidadId`, para permitir acceso directo a los identificadores sin navegar objetos anidados.

#### Scenario: Serialización de una asociación

* **GIVEN** una asociación persona-habilidad con nivel
* **WHEN** se serializa un `SkillPersonaDetailDto`
* **THEN** MUST conservar `Persona`, `Nivel`, `PersonaId`, `HabilidadId` y `NivelHabilidadId` en el payload.

### Requirement: REQ-SPQC-04 — Mapeo del query HTTP

`HabilidadPersonasListQuery` MUST mapear `page`, `pageSize`, `search`, `sort` y `status`; el valor HTTP `status` MUST normalizarse internamente a `PersonaSegmentoListado`.

#### Scenario: Status se interpreta como segmento de Persona

* **GIVEN** `status=eliminadas`
* **WHEN** se construye la consulta interna
* **THEN** el segmento MUST ser `PersonaSegmentoListado.Eliminadas`, no un segmento de habilidad.

### Requirement: REQ-SPQC-05 — Orden estable antes de proyectar

La consulta MUST ordenar por campos de la entidad Persona antes de proyectar al DTO, preservando el orden solicitado también con Pomelo/MySQL.

#### Scenario: Orden por apellido

* **GIVEN** varias personas asociadas con apellidos diferentes
* **WHEN** se solicita `sort=apellidos_asc`
* **THEN** los resultados MUST llegar ordenados por el campo de Persona antes de la proyección y paginación.

### Requirement: REQ-SPQC-06 — Validación de habilidad padre

El servicio de consulta MUST rechazar `Guid.Empty` con `ArgumentException` y MUST distinguir una habilidad inexistente de una colección vacía mediante resultado `404 Not Found`.

#### Scenario: Identificador inválido o padre inexistente

* **GIVEN** `skillId=Guid.Empty` o un identificador sin habilidad padre
* **WHEN** se ejecuta la consulta
* **THEN** el primer caso MUST producir `ArgumentException` y el segundo MUST producir `404 Not Found`.

### Requirement: REQ-SPQC-07 — Resultado paginado transportable

El endpoint MUST responder con `PersonaHabilidadesPageResult`, conteniendo `Items`, `Page`, `PageSize`, `Total`, `Sort` y `Segmento`.

#### Scenario: Metadatos consistentes

* **GIVEN** una consulta paginada válida
* **WHEN** la API responde exitosamente
* **THEN** `Page`, `PageSize`, `Total`, `Sort` y `Segmento` MUST describir la consulta aplicada y `Items` MUST pertenecer a esa página.
