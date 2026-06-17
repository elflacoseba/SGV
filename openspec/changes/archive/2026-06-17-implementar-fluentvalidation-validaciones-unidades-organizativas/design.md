# Design: FluentValidation para validaciones de UnidadesOrganizativas

## Technical Approach

Agregar FluentValidation en `SGV.Aplicacion` para validar la forma de `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest` al inicio de `CrearAsync` y `ActualizarAsync`. Si el input es inválido, el servicio devuelve errores por campo y corta el flujo antes de consultar duplicados, tipo, padre o persistir. El dominio conserva invariantes; el servicio conserva reglas con repositorio.

## Architecture Decisions

| Tema | Decisión | Alternativas / tradeoff | Rationale |
|------|----------|--------------------------|-----------|
| Ubicación | Crear `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/CrearUnidadOrganizativaRequestValidator.cs` y `ActualizarUnidadOrganizativaRequestValidator.cs`. | Validadores en API acoplarían reglas a HTTP. | Las validaciones son de caso de uso y deben funcionar fuera de controllers. |
| Paquetes | Agregar a `SGV.Aplicacion.csproj`: `FluentValidation` y `FluentValidation.DependencyInjectionExtensions` versión 12.1.1. | `FluentValidation.AspNetCore` no se usa. | La auto-validación MVC está desaconsejada; se requiere validación manual. |
| Registro DI | Crear `src/SGV.Aplicacion/DependencyInjection.cs` con `AddAplicacionServicios()` y `services.AddValidatorsFromAssemblyContaining<CrearUnidadOrganizativaRequestValidator>(ServiceLifetime.Scoped)`. Llamarlo desde `Program.cs` antes o junto a `AddInfraestructuraServicios()`. | Registrar desde Infraestructura funciona, pero mezcla composición de aplicación con infraestructura. | Mantiene ownership de validadores en Aplicación y deja API como composition root. |
| Ejecución | Inyectar `IValidator<CrearUnidadOrganizativaRequest>` e `IValidator<ActualizarUnidadOrganizativaRequest>` en `UnidadOrganizativaServicioComandos`. | Filtro/controller reduce cambios en servicio, pero no protege otros consumidores. | El short-circuit debe vivir donde empieza el caso de uso. |
| Errores | Extender el resultado con una colección interna por campo, por ejemplo `IReadOnlyDictionary<string, string[]> FieldErrors`, o un `ValidationFailure` propio. Las claves de `FieldErrors` se emiten en camelCase (`codigo`, `nombre`, `tipoUnidadOrganizativaId`) transformando `ValidationFailure.PropertyName` dentro del servicio de comandos — no se configura `PropertyNameResolver` global para no afectar a otros validators. | Reusar sólo `Code/Message` pierde granularidad. | La spec exige `errors[field]` con casing JSON, pero mensajes/códigos no quedan como contrato público estable. |

## Data Flow

```text
Controller -> UnidadOrganizativaServicioComandos
              -> IValidator<TRequest>.ValidateAsync
                 -> inválido: Failure(Validation, FieldErrors) -> Controller ValidationProblem(errors)
                 -> válido: reglas repositorio -> dominio -> repository -> UnitOfWork
```

En `ActualizarAsync`, validar el request antes de `GetByIdForUpdateAsync`; un shape inválido responde 400 aunque el id no exista. Recién con shape válido se consultan existencia, duplicados, tipo y jerarquía.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion/SGV.Aplicacion.csproj` | Modify | Referencias NuGet de FluentValidation. |
| `src/SGV.Aplicacion/DependencyInjection.cs` | Create | Registro de validadores de Aplicación. |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/*Validator.cs` | Create | Reglas de create/update. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Modify | Inyección y short-circuit manual. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` | Modify | Soporte interno para errores por campo. |
| `src/SGV.Api/Program.cs` | Modify | Llamada a `AddAplicacionServicios()`. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modify | Mapear errores de validación por campo a `ValidationProblem`. |
| `tests/SGV.Tests/...` | Modify/Create | Tests de validadores, servicio y API. |

## Interfaces / Contracts

Reglas FluentValidation: `Codigo` requerido/no vacío y máx. 50; `Nombre` requerido/no vacío y máx. 200; `Descripcion` máx. 1000; `TipoUnidadOrganizativaId` distinto de `Guid.Empty`; `VigenteHasta >= VigenteDesde` cuando ambas fechas existen.

La respuesta HTTP debe incluir `errors[codigo]`, `errors[nombre]`, etc. Los nombres de campo deben alinearse con JSON/camelCase. Los textos y códigos internos pueden cambiar; no documentarlos como contrato estable.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|--------------|----------|
| Unit | Reglas de cada validator | xUnit `[Theory]` para campos inválidos y `[Fact]` para request válido. |
| Aplicación | Short-circuit sin repositorios ni `SaveChangesAsync` | Fakes con contadores para `ExistsActiveCodeAsync`, `GetByIdAsync`, `GetByIdForUpdateAsync`, `GetByIdAsync` de tipo y guardado. |
| API | 400 con `errors[field]`; duplicado sigue 409/ProblemDetails actual | WebApplicationFactory o controller test según patrón existente. |
| Dominio | Invariantes actuales siguen vigentes | Mantener tests existentes; agregar sólo si se detecta hueco. |

## Migration / Rollout

No requiere migración de datos ni cambios MySQL. Rollback: revertir paquetes, `AddAplicacionServicios`, validadores, inyección, mapeo de errores y tests.

## Review / Size Budget

Riesgo medio de superar 400 líneas si se agregan API tests extensos. Mantener PR único y enfocado; si crece, dividir en PR 1 validadores+servicio y PR 2 mapeo/API tests.

## Open Questions

- [x] Confirmar versión exacta de FluentValidation al implementar según restauración NuGet del entorno. Versión final: `12.1.1` (paquetes `FluentValidation` y `FluentValidation.DependencyInjectionExtensions`).
