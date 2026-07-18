# Especificación: Selector modal de Persona con buscador — Hardening BFF same-origin

## Propósito

Endurecer el handler BFF same-origin `GET /api/v1/personas/consulta` de `SGV.Web` corrigiendo `RIS-001/002` del verify adversarial del issue #157. La capacidad limita `?search` a 200 caracteres, valida `?sort=` y `?segmento=` contra una whitelist cerrada antes de invocar `IPersonaApiClient`, y responde `400` con `ProblemDetails` ante entradas inválidas. Los defaults (`apellidos_asc` + `Activas`) se preservan para no romper el modal ni otros consumidores de `/consulta`.

## Requisitos

### Requirement: BFF acota `?search` a 200 caracteres

El handler BFF MUST aceptar `?search` con hasta `200` caracteres y reenviarlo al `PersonaListQuery`. Si supera `200`, MUST responder `400` con `ProblemDetails` describiendo el límite y MUST NOT invocar `IPersonaApiClient`.

#### Scenario: BFF reenvía `?search` de exactamente 200 caracteres

- **DADO** un request autenticado a `GET /api/v1/personas/consulta` con `?search=` de exactamente `200` caracteres válidos
- **CUANDO** el handler valida y construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Search` igual al valor recibido y responder `200 OK`.

#### Scenario: BFF rechaza `?search` de 201 caracteres

- **DADO** un request autenticado a `GET /api/v1/personas/consulta` con `?search=` de `201` caracteres
- **CUANDO** el handler detecta el exceso
- **ENTONCES** MUST responder `400` con `ProblemDetails` cuyo `detail` mencione el límite de `200` caracteres para `search`
- **Y** MUST NOT invocar `IPersonaApiClient.QueryAsync`.

### Requirement: BFF acepta `?sort=` con whitelist cerrada

El handler BFF MUST aceptar `?sort=` únicamente cuando su valor (case-insensitive, vía `ToLowerInvariant()`) coincide con uno de los ocho tokens `apellidos_asc`, `apellidos_desc`, `nombres_asc`, `nombres_desc`, `legajo_asc`, `legajo_desc`, `email_asc` o `email_desc`. Cualquier otro valor (incluido `documento_asc`/`documento_desc`) MUST responder `400` con `ProblemDetails` enumerando los tokens válidos y MUST NOT invocar `IPersonaApiClient`.

#### Scenario: BFF acepta un token válido de la whitelist

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?sort=email_desc`
- **CUANDO** el handler valida
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Sort="email_desc"` y responder `200 OK`.

#### Scenario: BFF rechaza token fuera de la whitelist

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?sort=documento_asc`
- **CUANDO** el handler valida
- **ENTONCES** MUST responder `400` con `ProblemDetails` cuyo `detail` liste los ocho tokens válidos para `sort`
- **Y** MUST NOT invocar `IPersonaApiClient.QueryAsync`.

### Requirement: BFF acepta `?segmento=` con whitelist cerrada

El handler BFF MUST aceptar `?segmento=` únicamente cuando su valor (case-insensitive) es `activas` o `eliminadas`, mapeando a `PersonaSegmentoListado.Activas` o `PersonaSegmentoListado.Eliminadas` respectivamente. Cualquier otro valor MUST responder `400` con `ProblemDetails` enumerando los valores válidos y MUST NOT invocar `IPersonaApiClient`.

#### Scenario: BFF acepta `?segmento=eliminadas`

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?segmento=eliminadas`
- **CUANDO** el handler construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Segmento=PersonaSegmentoListado.Eliminadas` y responder `200 OK`.

#### Scenario: BFF rechaza `?segmento=` fuera de la whitelist

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?segmento=todas`
- **CUANDO** el handler valida
- **ENTONCES** MUST responder `400` con `ProblemDetails` cuyo `detail` indique que `segmento` debe ser `activas` o `eliminadas`
- **Y** MUST NOT invocar `IPersonaApiClient.QueryAsync`.

### Requirement: BFF preserva defaults back-compat

Cuando el request no incluye `?sort=` o `?segmento=`, el handler BFF MUST aplicar los defaults `Sort="apellidos_asc"` y `Segmento=PersonaSegmentoListado.Activas`. Si sólo uno de los dos está presente y es válido, MUST respetarlo y mantener el default del otro.

#### Scenario: BFF aplica defaults cuando faltan ambos parámetros

- **DADO** un request autenticado a `GET /api/v1/personas/consulta` sin `?sort=` ni `?segmento=`
- **CUANDO** el handler construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Sort="apellidos_asc"` y `Segmento=PersonaSegmentoListado.Activas`.

#### Scenario: BFF respeta un parámetro válido y mantiene el default del otro

- **DADO** un request autenticado a `GET /api/v1/personas/consulta?sort=nombres_desc`
- **CUANDO** el handler construye el `PersonaListQuery`
- **ENTONCES** MUST invocar `IPersonaApiClient.QueryAsync` con `Sort="nombres_desc"` y `Segmento=PersonaSegmentoListado.Activas` (default preservado).

## Consideraciones fuera de alcance

- Extender `PersonaRepository.ApplySort` con `documento_asc`/`documento_desc` u otros tokens no implementados hoy.
- Modificar `FakePersonaApiClient`, `PersonaRepository`, `PersonaListQuery` o migraciones.
- Introducir un enum `PersonaSort`: `PersonaListQuery.Sort` permanece como `string?`.
- Mover el handler BFF fuera de `Program.cs` o resolver otros findings del issue #157.
- Cambiar el cap a un valor distinto de `200` o sustituir `PersonaSegmentoListado`.
