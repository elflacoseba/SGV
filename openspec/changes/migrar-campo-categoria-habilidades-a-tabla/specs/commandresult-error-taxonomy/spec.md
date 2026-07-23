# Delta for CommandResult Error Taxonomy

## ADDED Requirements

> Delta introducida por el change `migrar-campo-categoria-habilidades-a-tabla`. Resuelve el mapeo de los códigos de error de `CategoriaHabilidad` dentro de la taxonomía común `ErrorCategoria` y garantiza que `HabilidadApiClient` expone los códigos apropiados al `PageModel`.

### Requirement: `CategoriaHabilidad` errores mapean a `ErrorCategoria`

El sistema MUST exponer los siguientes códigos de error de dominio para el catálogo `CategoriaHabilidad`, mapeados a `ErrorCategoria` según el canon vigente:

| Código de error | HTTP | `ErrorCategoria` |
|-----------------|------|------------------|
| `CategoriaHabilidadNoExiste` (referenciado en payloads de Habilidad) | 400 | `Validation` |
| `CodigoHabilidadInvalido`, `NombreHabilidadInvalido`, `CategoriaIdInvalido` | 400 | `Validation` |

La variante `Validation` cubre tanto los payloads que viajan con `CategoriaId = <guid-fake>` (404-like para un recurso opcional referenciado) como los payloads con campos de `Habilidad` legacy mal formados. Los códigos de dominio deben traducirse a `Validation` (HTTP 400) — NO a `NotFound` ni a `Conflict` — porque la FK opcional se evalúa como validación del payload, en línea con el patrón vigente de `NivelRequeridoId` en `cargo-skill-asignar-editar`.

#### Scenario: `CategoriaId` inexistente se mapea a `ErrorCategoria.Validation`

- **GIVEN** un request `POST /api/v1/skills` con `CategoriaId = <guid-fake>` (no sembrado)
- **WHEN** el backend responde 400 con `ValidationProblemDetails` y código `CategoriaHabilidadNoExiste`
- **THEN** `HabilidadApiClient.CreateAsync` MUST devolver `HabilidadCommandResult.Failure` con `Categoria == ErrorCategoria.Validation`
- **AND** el `Code` MUST preservarse verbatim como `CategoriaHabilidadNoExiste`
- **AND** el `StatusCode == 400` MUST preservarse como metadata de diagnóstico.

#### Scenario: Preservación de `FieldErrors` cuando el body es `ValidationProblemDetails`

- **GIVEN** un request `PUT /api/v1/skills/{id}` con `CategoriaId = <guid-fake>`
- **WHEN** el backend responde 400 con `ValidationProblemDetails` y `errors.categoriaId = ["Categoría inexistente"]`
- **THEN** el `HabilidadCommandResult.Failure` resultante MUST exponer `FieldErrors["categoriaId"] = ["Categoría inexistente"]`
- **AND** MUST poder renderizarse en el `PageModel` como error inline del campo `Categoria`.

### Requirement: `HabilidadErrorType` añade variante `CategoriaInexistente`

El sistema MUST extender el enum de dominio `HabilidadErrorType` (en `SGV.Contracts.Habilidades`) con la variante `CategoriaInexistente` y MUST registrar el mapeo bidireccional en `ErrorCategoriaMappers.ToCategoria(HabilidadErrorType)`:

| `HabilidadErrorType` | `ErrorCategoria` |
|---------------------|------------------|
| `CategoriaInexistente` | `Validation` |

El mapeo inverso (`ErrorCategoria → HabilidadErrorType`) MUST mantener `Validation → Validation` (sin cambios). Los `HabilidadApiClient.CreateAsync` y `UpdateAsync` MUST producir un `HabilidadError` con `Type == HabilidadErrorType.CategoriaInexistente` cuando el backend rechaza con código `CategoriaHabilidadNoExiste`.

#### Scenario: `CategoriaInexistente` produce `Categoria == Validation`

- **GIVEN** un test unitario del mapper
- **WHEN** se invoca `ToCategoria(HabilidadErrorType.CategoriaInexistente)`
- **THEN** MUST devolver `ErrorCategoria.Validation`.

#### Scenario: `HabilidadError`携带 `CategoriaInexistente` se traduce a HTTP 400

- **GIVEN** un `HabilidadError { Type = CategoriaInexistente, Categoria = Validation }`
- **WHEN** `ApiResults.ToProblemResult(error)` se invoca
- **THEN** el `ObjectResult` resultante MUST tener `StatusCode == 400`
- **AND** el `ProblemDetails.Type` MUST coincidir con el slug canónico del error de dominio.
