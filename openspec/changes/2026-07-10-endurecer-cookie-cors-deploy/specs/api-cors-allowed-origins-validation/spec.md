# Especificación de validación de AllowedOrigins en CORS de SGV.Api

## Propósito

Definir el contrato runtime de la política CORS de `SGV.Api`: cómo se valida la sección de configuración `AllowedOrigins` durante la construcción del host y qué combinaciones con credenciales están prohibidas por ser inseguras frente a navegadores modernos. Complementa a `sgv-readonly-api` (catálogos) y es ortogonal a `jwt-signing-key-validation`.

## Requisitos

### Requisito: Fail-loud cuando AllowedOrigins está vacío fuera de Development

La construcción del host de `SGV.Api` DEBE validar la sección `AllowedOrigins` durante el arranque. Si `ASPNETCORE_ENVIRONMENT` es distinto de `Development` y la sección está ausente o vacía, el host DEBE lanzar `InvalidOperationException` con un mensaje operativo que nombre `AllowedOrigins`.

#### Escenario: AllowedOrigins ausente y ambiente distinto de Development

- **DADO** que `ASPNETCORE_ENVIRONMENT` es distinto de `Development`
- **Y** la sección `AllowedOrigins` no existe en la configuración efectiva
- **CUANDO** se construye el host (durante o antes de `builder.Build()`)
- **ENTONCES** DEBE lanzar `InvalidOperationException`
- **Y** el mensaje DEBE indicar que `AllowedOrigins` debe configurarse fuera de `Development`.

#### Escenario: AllowedOrigins poblado y ambiente distinto de Development

- **DADO** que `ASPNETCORE_ENVIRONMENT` es distinto de `Development`
- **Y** la sección `AllowedOrigins` contiene al menos un origin
- **CUANDO** se construye el host
- **ENTONCES** el host DEBE arrancar sin excepción
- **Y** la política CORS por defecto DEBE estar registrada con `WithOrigins(<los configured>).AllowCredentials()`.

#### Escenario: AllowedOrigins ausente y ambiente Development

- **DADO** que `ASPNETCORE_ENVIRONMENT == "Development"`
- **Y** la sección `AllowedOrigins` está ausente o vacía
- **CUANDO** se construye el host
- **ENTONCES** el host DEBE arrancar sin excepción
- **Y** la política CORS por defecto aplicada DEBE ser un fallback explícito documentado
- **Y** esa política DEBE NO combinar `AllowAnyOrigin()` con `AllowCredentials()` en la misma expresión.

### Requisito: Prohibición de combinar AllowAnyOrigin con AllowCredentials

`SGV.Api` DEBE NO registrar una política CORS que combine `AllowAnyOrigin()` con `AllowCredentials()`. Esta combinación es inválida para navegadores modernos y habilita que cualquier dominio invoque la API con credenciales si la versión del middleware no la rechaza por sí misma.

#### Escenario: Búsqueda estática no encuentra la combinación prohibida

- **DADO** el código fuente vigente de `SGV.Api`
- **CUANDO** se busca el texto `AllowAnyOrigin` en `src/SGV.Api/`
- **ENTONCES** ningún registro CORS vigente DEBE combinar `AllowAnyOrigin()` con `AllowCredentials()` en la misma expresión o en invocaciones adyacentes dentro del mismo bloque `AddCors`.

#### Escenario: Fallback de Development con credenciales apagadas o con origins explícitos

- **DADO** que `ASPNETCORE_ENVIRONMENT == "Development"`
- **Y** la sección `AllowedOrigins` está vacía
- **CUANDO** se inspecciona la política CORS por defecto registrada
- **ENTONCES** la política DEBE tener `AllowCredentials() == false` cuando use `AllowAnyOrigin()`
- **O** DEBE enumerar origins explícitos sin comodines.
