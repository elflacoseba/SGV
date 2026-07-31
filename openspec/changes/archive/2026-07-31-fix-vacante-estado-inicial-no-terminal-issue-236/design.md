# Design: fix-vacante-estado-inicial-no-terminal-issue-236

## Resumen técnico

Se agrega una validación de negocio en `VacanteServicioComandos.CrearAsync` (capa Aplicación) que rechaza estados iniciales terminales (`EsTerminal = true`) antes de cualquier escritura. La regla se modela como un `VacanteCommandResult.Failure` con `ErrorCategoria.Validation`, código `VacanteErrorCodigo.EstadoTerminalInmutable` y `FieldErrors["estadoVacanteId"]`. La capa API NO se modifica: `VacantesController.Create` ya enruta `Validation` + `FieldErrors` a `400 ValidationProblemDetails` (líneas 147-148 vía `ApiResults.ToValidationProblemResult`). El dominio tampoco se toca: `EstadoVacante.EsTerminal` ya expone la semántica necesaria.

## Cambios por capa

### Dominio
- Sin cambios. `EstadoVacante.EsTerminal` (línea 25) y sus seeds (`Abierta`/`EnSeleccion` no terminal; `Cubierta`/`Cancelada` terminal) ya cubren la semántica.

### Aplicación
- Archivo: `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs`
- Inserción: tras el lookup exitoso de `estadoVacante` (línea 130, post `if (estadoVacante is null)`) y antes de `ExistsAbiertaByPuestoAsync` (línea 132).
- Bloque:
  ```csharp
  // El estado inicial no puede ser terminal: abre la semántica de "abrir vacante".
  // Nota: el código es el mismo que CambiarEstadoAsync (409 Conflict); aquí es 400
  // porque la solicitud es inválida antes de persistir. Ver design §Decisiones.
  if (estadoVacante.EsTerminal)
  {
      var mensaje = "El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada).";
      return VacanteCommandResult.Failure(
          new VacanteError(
              ErrorCategoria.Validation,
              VacanteErrorCodigo.EstadoTerminalInmutable,
              mensaje),
          new Dictionary<string, string[]> { ["estadoVacanteId"] = [mensaje] });
  }
  ```
- `FieldErrors` con clave `estadoVacanteId` para consistencia con `Crear_EstadoVacanteIdVacio_RetornaValidationFailure` (líneas 97-111) y la spec delta §"Estado inicial terminal rechazado".

### API
- Sin cambios. `VacantesController.Create` (líneas 139-151) bifurca `result.FieldErrors` → `ApiResults.ToValidationProblemResult` (400 con `errors["estadoVacanteId"]`); el resto → `ApiResults.ToProblemResult`. La firma del resultado del servicio encaja sin modificaciones.

### Web
- Sin cambios (fuera de scope). Se revisará `VacantesApiClient` durante apply por si asume 409 uniforme (ver Riesgos).

## Decisiones técnicas

### Reutilización de `VacanteErrorCodigo.EstadoTerminalInmutable`
- **Justificación**: el `Code` identifica la condición semántica ("estado terminal no permitido"), no el endpoint. El HTTP lo define el contexto:
  - `CrearAsync` con estado inicial terminal → `400 Bad Request` (`ErrorCategoria.Validation`): la solicitud es inválida *antes* de persistir.
  - `CambiarEstadoAsync` con transición desde terminal → `409 Conflict` (`ErrorCategoria.Conflict`, líneas 229-236): la vacante ya está cerrada.
- **Riesgo**: clientes que asuman HTTP uniforme por `Code`. **Mitigación**: comentario inline en ambos puntos del servicio + nota en este design.
- **Alternativa rechazada**: crear `EstadoInicialTerminalInvalido` como nuevo código. Descartado porque duplica semántica y rompe el catálogo único de condiciones.

### Validación en Servicio vs Validador
- `CrearVacanteRequestValidator` (FluentValidation) solo ve el DTO; no tiene `IEstadoVacanteRepository`, por lo que no puede evaluar `EsTerminal`. La regla **DEBE** vivir en el servicio, que ya resuelve el estado vía repo (línea 120-122).
- **No** se duplica en el validador: inyectar el repo rompería el patrón del repo (los validadores no inyectan servicios) y agregaría un round-trip extra. Defensa en profundidad no se justifica frente al costo.
- **Orden**: la validación se hace post-lookup (no antes) para reutilizar `estadoVacante` ya cargado en lugar de un segundo fetch. `EstadoVacanteInexistente` (404) sigue siendo el camino para IDs inexistentes.

## Compatibilidad y migraciones
- Sin migraciones. No se modifican entidades ni columnas.
- **Backward compatibility**: la regla NUEVA rechaza estados que antes se aceptaban. Clientes que envíen `EstadoVacanteId` terminal ahora reciben `400` en lugar de `201`. No invalida datos existentes: la regla aplica solo a futuras inserciones (no hay `UPDATE` ni data fix-up).

## Pruebas

### Unit (xUnit + fakes existentes)
- `Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure`: `CrearRequestValido(estadoVacanteId: EstadoCubiertaId)` (helper línea 39; GUID en línea 31). Assert `IsSuccess == false`, `ErrorCategoria.Validation`, `Error.Code == EstadoTerminalInmutable`, `FieldErrors.ContainsKey("estadoVacanteId")`, `uow.SaveChangesCount == 0`, `repo.Datos` vacío.
- `Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure`: análogo con `EstadoCanceladaId` (línea 32).
- `FakeEstadoVacanteRepository` ya siembra ambos estados con `EsTerminal=true` (líneas 458-459), sin cambios al fake.

### Integración API (WebApplicationFactory + fake `IVacanteServicioComandos`)
- `Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails`: patrón `Create_ValidacionFalla_Returns400WithProblemDetails` (líneas 199-229). Fake `CrearHandler` devuelve `Failure(Validation, EstadoTerminalInmutable, ...)` con `FieldErrors["estadoVacanteId"]`. Assert `HttpStatusCode.BadRequest`, `ValidationProblemDetails.Errors` contiene `"estadoVacanteId"`.

## Riesgos residuales

| Riesgo | Likelihood | Mitigation |
|---|---|---|
| Web client asume solo `409` para `EstadoTerminalInmutable` y no maneja `400` con el mismo `Code` | Low | Revisar `src/SGV.Web/Integration/VacantesApiClient*.cs` durante apply; si no distingue, agregar manejo explícito. |
| Cambio de contrato implícito: antes `201`, ahora `400` con estado terminal | Medium | Documentado en spec/proposal. Clientes que ya asumen no-terminales siguen funcionando idéntico. |
| Tests existentes dependen de la aceptación previa de terminales | Low | Búsqueda en `VacanteServicioComandosTests` confirma que solo `Cancelar*`/`CambiarEstado*` usan estados terminales; `Crear_*` siempre usa `EstadoAbiertaId`. |

## Plan de rollback
1. Revertir el bloque `if (estadoVacante.EsTerminal)` en `VacanteServicioComandos.CrearAsync`.
2. Eliminar los 2 tests unitarios y 1 test API agregados en esta fase (apply).
3. `dotnet build SGV.slnx && dotnet test SGV.slnx` → verde sin el cambio.