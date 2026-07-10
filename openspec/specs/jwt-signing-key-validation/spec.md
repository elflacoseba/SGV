# Especificación de validación del signing key de JWT

## Purpose

Definir el contrato de arranque de `JwtOptions.SigningKey`: presencia obligatoria y ≥32 bytes UTF-8, fail-loud vía `ValidateOnStart`. Cierra el default hardcodeado y deja fuera de alcance la configuración de `Issuer`/`Audience`.

## Requirements

### Requirement: Fallo en arranque si `Jwt:SigningKey` falta

El sistema MUST lanzar `OptionsValidationException` cuando `Jwt:SigningKey` no está presente, está vacío o contiene solo whitespace. El mensaje MUST nombrar `Jwt:SigningKey` y NO sugerir un valor por defecto embebido.

#### Scenario: Sección `Jwt` ausente

- **GIVEN** una configuración sin la sección `Jwt`
- **WHEN** el host intenta construirse
- **THEN** MUST lanzar `OptionsValidationException`
- **AND** el mensaje MUST nombrar `Jwt:SigningKey`.

#### Scenario: `Jwt:SigningKey` presente pero en blanco

- **GIVEN** `Jwt:SigningKey = ""` o solo whitespace
- **WHEN** el host intenta construirse
- **THEN** MUST lanzar `OptionsValidationException`
- **AND** el mensaje MUST indicar que la clave no puede estar vacía.

### Requirement: Fallo en arranque si `Jwt:SigningKey` mide menos de 32 bytes UTF-8

El sistema MUST lanzar `OptionsValidationException` cuando `Jwt:SigningKey` está presente pero su longitud en bytes UTF-8 es <32. El mensaje MUST indicar la longitud mínima requerida.

#### Scenario: Clave explícitamente corta

- **GIVEN** `Jwt:SigningKey = "short-key"` (<32 bytes UTF-8)
- **WHEN** el host intenta construirse
- **THEN** MUST lanzar `OptionsValidationException`
- **AND** el mensaje MUST mencionar "≥32 UTF-8 bytes".

#### Scenario: Clave en 31 bytes UTF-8

- **GIVEN** `Jwt:SigningKey` con longitud UTF-8 igual a 31
- **WHEN** el host intenta construirse
- **THEN** MUST lanzar `OptionsValidationException`.

#### Scenario: Clave en 32 bytes UTF-8

- **GIVEN** `Jwt:SigningKey` con longitud UTF-8 igual a 32
- **WHEN** el host intenta construirse
- **THEN** la validación MUST pasar sin excepción.

### Requirement: Arranque en Development con placeholder documentado

El sistema MUST permitir que `src/SGV.Api/appsettings.Development.json` provea un placeholder ≥32 bytes UTF-8 marcado dev-only para que `dotnet run` funcione sin setup. MUST NO depender de defaults hardcodeados.

#### Scenario: Placeholder dev reconocido

- **GIVEN** `appsettings.Development.json` con `Jwt:SigningKey` ≥32 bytes UTF-8 marcado dev-only
- **WHEN** un developer arranca la API con Development por defecto
- **THEN** la validación de `JwtOptions` MUST pasar
- **AND** la API MUST arrancar usando exclusivamente esa clave.

#### Scenario: Sin defaults hardcodeados

- **GIVEN** el código de la API cargado en memoria
- **WHEN** se inspecciona `JwtOptions` y sus call sites
- **THEN** ningún path MUST asignar clave embebida a `SigningKey` por defecto
- **AND** ningún path MUST materializar `new JwtOptions()` como fallback.

### Requirement: Emisión y validación usan exclusivamente la clave configurada

El sistema MUST firmar tokens JWT y validar bearer tokens usando exclusivamente `Jwt:SigningKey` de configuración. MUST NO firmar ni validar contra clave alternativa implícita.

#### Scenario: Firma usa clave configurada

- **GIVEN** `Jwt:SigningKey = K` desde configuración válida
- **WHEN** `AuthServicio.LoginAsync` emite un access token
- **THEN** la firma MUST calcularse con `K`
- **AND** la misma `K` MUST usarse al validar ese token.

#### Scenario: Validación rechaza token con otra clave

- **GIVEN** un bearer token firmado con una clave distinta de la configurada
- **WHEN** llega a un endpoint protegido
- **THEN** la validación MUST fallar
- **AND** el endpoint MUST responder `401 Unauthorized`.

### Requirement: Documentación de secretos JWT explícita por entorno

El repositorio MUST documentar cómo se obtiene `Jwt:SigningKey` por entorno: (a) dev local con placeholder de `appsettings.Development.json` y/o `dotnet user-secrets`; (b) producción y CI con variables de entorno o secret manager. MUST dejar explícito que la clave dev NO es apta para producción.

#### Scenario: Developer local encuentra instrucciones

- **GIVEN** un developer que arranca la API por primera vez
- **WHEN** consulta `AGENTS.md` y `docs/decisiones-implementacion.md`
- **THEN** encuentra `dotnet user-secrets set "Jwt:SigningKey" ... --project src/SGV.Api`
- **AND** entiende que el placeholder dev es solo dev-only.

#### Scenario: Equipo de deploy distingue entornos

- **GIVEN** un operador configurando producción o CI
- **WHEN** consulta la documentación del repo
- **THEN** encuentra la indicación de usar `Jwt__SigningKey` o secret manager
- **AND** la doc MUST dejar claro que el placeholder dev NO puede usarse en producción.
