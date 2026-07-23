# Delta for Web ApiClient Transport Contract

## ADDED Requirements

> Delta introducida por el change `migrar-campo-categoria-habilidades-a-tabla`. Define el contrato del cliente HTTP tipado read-only de `CategoriasHabilidad` y la traducción de sus errores via `CommandResultMapper`.

### Requirement: `ICategoriaHabilidadApiClient` es read-only y delega a `CommandResultMapper`

`SGV.Web` MUST exponer `ICategoriaHabilidadApiClient` con dos operaciones read-only: `GetAllAsync(CancellationToken)` y `GetByIdAsync(Guid id, CancellationToken)`. El cliente MUST delegar la clasificación de respuestas HTTP en `CommandResultMapper.Map` para producir el resultado tipado (mismo seam que el resto de los `*ApiClient` administrativos). El cliente MUST NOT exponer operaciones de escritura (`POST`, `PUT`, `PATCH`, `DELETE`). El cliente MUST propagar `HttpRequestException` y `TaskCanceledException` nativas, sin traducirlas a resultados funcionales. El cliente MUST respetar un `CancellationToken` pre-cancelado y MUST NOT iniciar el envío HTTP cuando la cancelación ya fue solicitada.

#### Scenario: `GetAllAsync` con catálogo poblado

- **GIVEN** `ICategoriaHabilidadApiClient.GetAllAsync`
- **AND** el backend responde `200 OK` con un array JSON de 4 categorías
- **WHEN** el `PageModel` de Crear o Editar Habilidad lo invoca
- **THEN** el resultado MUST ser `IReadOnlyList<CategoriaHabilidadDto>` con 4 elementos
- **AND** cada elemento MUST exponer `Id`, `Codigo` y `Nombre` consumer-safe.

#### Scenario: `GetByIdAsync` con id existente

- **GIVEN** `ICategoriaHabilidadApiClient.GetByIdAsync(<guid>)`
- **AND** el backend responde `200 OK` con la categoría solicitada
- **WHEN** se completa la llamada
- **THEN** el resultado MUST ser `CategoriaHabilidadDto?` con el id consultado
- **AND** MUST exponer `Codigo` y `Nombre`.

#### Scenario: `GetByIdAsync` con id inexistente responde 404 → tipado

- **GIVEN** el backend responde `404 Not Found` para `GET /api/v1/categorias-habilidad/<guid-fake>`
- **WHEN** se invoca `GetByIdAsync(<guid-fake>)`
- **THEN** el resultado MUST ser `null` (recurso inexistente)
- **AND** MUST NO lanzar excepción.

#### Scenario: Backend no disponible no se traduce a resultado de negocio

- **GIVEN** `ICategoriaHabilidadApiClient.GetAllAsync`
- **AND** el pipeline HTTP finaliza con `HttpRequestException` (DNS, conexión rechazada)
- **WHEN** se invoca la operación
- **THEN** la excepción MUST propagarse al consumidor (por ejemplo, el `PageModel`)
- **AND** MUST NOT devolverse como un resultado funcional.

#### Scenario: Cancelación o timeout del transporte

- **GIVEN** una operación de `ICategoriaHabilidadApiClient` en ejecución
- **WHEN** el pipeline HTTP finaliza con `TaskCanceledException`
- **THEN** la excepción MUST propagarse al consumidor
- **AND** la operación MUST NOT devolverse como un resultado funcional.

#### Scenario: Token pre-cancelado

- **GIVEN** un consumidor entrega un `CancellationToken` ya cancelado
- **WHEN** invoca `GetAllAsync` o `GetByIdAsync`
- **THEN** la operación MUST finalizar como cancelada
- **AND** el envío HTTP MUST NOT iniciarse.

#### Scenario: Cliente read-only expone solo `GET`

- **GIVEN** la superficie pública de `ICategoriaHabilidadApiClient`
- **WHEN** se inspeccionan sus métodos
- **THEN** MUST exponer únicamente `GetAllAsync` y `GetByIdAsync`
- **AND** MUST NOT exponer `CreateAsync`, `UpdateAsync` ni `DeleteAsync`.

### Requirement: `HabilidadApiClient` traduce fallos de `CategoriaId` a `HabilidadError.CategoriaInexistente`

`HabilidadApiClient.CreateAsync` y `HabilidadApiClient.UpdateAsync` MUST traducir las respuestas 400 con código de error `CategoriaHabilidadNoExiste` (proveniente del backend) a `HabilidadCommandResult.Failure(HabilidadError { Type = HabilidadErrorType.CategoriaInexistente, Categoria = ErrorCategoria.Validation })`, delegando en `CommandResultMapper.Map`. La traducción MUST preservar `StatusCode = 400`, `Code`, `Message` y `FieldErrors` cuando aplique, en línea con el requisito **"Clientes HTTP administrativos usan `CommandResultMapper`"** de esta misma spec.

#### Scenario: `CreateAsync` con `CategoriaId` inexistente se traduce a `CategoriaInexistente`

- **GIVEN** un `POST /api/v1/skills` con `CategoriaId = <guid-fake>`
- **WHEN** el backend responde 400 con `ValidationProblemDetails` y código `CategoriaHabilidadNoExiste`
- **THEN** `HabilidadApiClient.CreateAsync` MUST devolver `HabilidadCommandResult.Failure(HabilidadError { Type = CategoriaInexistente, Categoria = Validation })`
- **AND** MUST preservar `StatusCode = 400` como metadata.

#### Scenario: `UpdateAsync` con `CategoriaId` inválido no persiste

- **GIVEN** un `PUT /api/v1/skills/{id}` con `CategoriaId = <guid-fake>`
- **WHEN** el backend responde 400 con código `CategoriaHabilidadNoExiste`
- **THEN** `HabilidadApiClient.UpdateAsync` MUST devolver `HabilidadCommandResult.Failure` con `Categoria == Validation`
- **AND** MUST NOT haber producido cambios persistidos (la operación ni siquiera llegó al servicio de aplicación).

#### Scenario: Cliente usa `CommandResultMapper` para clasificar la respuesta

- **GIVEN** cualquier respuesta no exitosa del backend a `HabilidadApiClient.CreateAsync`, `UpdateAsync`, `GetAllAsync` o `GetByIdAsync`
- **WHEN** el cliente procesa la respuesta
- **THEN** la `Categoria` resultante MUST provenir de `CommandResultMapper.Map`
- **AND** el cliente MUST NO mantener una matriz `switch` privada que duplique el mapper común.
