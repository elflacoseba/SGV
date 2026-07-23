# Delta de taxonomía de errores para `CommandResult` y clientes HTTP

> Delta introducida por el change `implementa-persona-habilidades`. El cambio alinea `PersonaSkill*` con el resto del módulo Habilidades, que ya vive bajo `ErrorCategoria` y expone su resultado en `SGV.Contracts.*`.

## ADDED Requirements

### Requirement: `PersonaSkillCommandResult` y `PersonaSkillError` viven en `SGV.Contracts.Personas` con `Categoria`

`SGV.Contracts.Personas.Comandos` MUST exponer `PersonaSkillCommandResult`, `PersonaSkillError`, `AsignarPersonaSkillRequest` y `PersonaSkillDto` (incluido un `PersonaSkillDeleteResult` para operaciones de baja). `PersonaSkillError` MUST incluir la propiedad `Categoria: ErrorCategoria` alineada con el resto de la taxonomía. El shape observable de las respuestas JSON MUST preservarse (mismo nombre de propiedad, misma capitalización) respecto al contrato vigente.

#### Scenario: Build compila contra `SGV.Contracts.Personas`

- **DADO** que `SGV.Aplicacion.Personas` ya no expone los wire-types `PersonaSkill*`
- **CUANDO** `SGV.Web` y `SGV.Api` compilan
- **ENTONCES** ambos MUST enlazar contra `SGV.Contracts.Personas` para esos tipos
- **Y** MUST NOT requerir duplicación de DTOs en `SGV.Aplicacion`.

#### Scenario: Wire JSON preservado

- **DADO** que el backend actualmente responde con un shape JSON concreto para `PersonaSkill*`
- **CUANDO** el cliente web deserializa la respuesta
- **ENTONCES** el binding MUST funcionar contra los mismos nombres de propiedad JSON que ya están en uso
- **Y** MUST NOT introducir cambios visibles en el contrato de transporte.

### Requirement: Mapeo `PersonaSkillErrorType → ErrorCategoria` consolidado

El sistema MUST traducir las variantes vigentes de error PersonaSkill a la matriz canónica `ErrorCategoria`. El mapeo observable MUST ser: `NotFound → ErrorCategoria.NotFound (HTTP 404)`, `Validation → ErrorCategoria.Validation (HTTP 400)`. Cualquier otra variante no documentada MUST caer en `Unexpected` o `Transport` en lugar de reintroducir un enum paralelo.

#### Scenario: `NotFound` se mapea a `ErrorCategoria.NotFound`

- **DADO** una falla de PersonaSkill con código de error `PersonaNoEncontrada`, `HabilidadNoEncontrada` o `AsociacionNoEncontrada`
- **CUANDO** el cliente web inspecciona el `PersonaSkillCommandResult.Failure`
- **ENTONCES** MUST tener `Categoria == ErrorCategoria.NotFound`
- **Y** el código HTTP observable MUST ser `404`.

#### Scenario: `Validation` se mapea a `ErrorCategoria.Validation`

- **DADO** una falla de PersonaSkill con código `NivelHabilidadNoExiste`, `DatosInvalidos` u `OperacionInvalida`
- **CUANDO** el cliente web inspecciona el `PersonaSkillCommandResult.Failure`
- **ENTONCES** MUST tener `Categoria == ErrorCategoria.Validation`
- **Y** el código HTTP observable MUST ser `400`.

#### Scenario: `PersonaSkillDeleteResult` expone `Categoria`

- **DADO** una operación `DELETE` contra `PersonaSkill`
- **CUANDO** el backend responde `204`, `400`, `404` u otro estado
- **ENTONCES** el cliente web MUST traducir la respuesta a `PersonaSkillDeleteResult`
- **Y** el resultado MUST exponer `Categoria: ErrorCategoria` y preservar `StatusCode` como metadata.

### Requirement: `PersonaSkill` no reintroduce un enum paralelo

`PersonaSkillErrorType` MUST NOT sobrevivir como tipo público expuesto al cliente web. La clasificación de fallos MUST provenir exclusivamente de `CommandResultMapper.Map` y de `ErrorCategoria`. El cliente `PersonaApiClient` MUST delegar en los mappers comunes en lugar de mantener su propia matriz `status → categoría`.

#### Scenario: Cliente usa el mapper común

- **DADO** que el backend responde `4xx` o `5xx` a una operación de `PersonaSkill`
- **CUANDO** el cliente web procesa la respuesta
- **ENTONCES** la `Categoria` resultante MUST provenir del mapper común
- **Y** el cliente MUST NO mantener una matriz `switch` privada que duplique `CommandResultMapper`.
