# Proposal: fix-vacante-estado-inicial-no-terminal-issue-236

## Intent

El endpoint `POST /api/v1/vacantes` acepta cualquier `EstadoVacanteId` existente como estado inicial —incluso terminales (Cubierta, Cancelada)— contradiciendo la semántica de "abrir una vacante". La regla de negocio requiere que el estado inicial sea no terminal; de lo contrario se devuelve `400 Bad Request` con código `VacanteErrorCodigo.EstadoTerminalInmutable`.

## Scope

### In Scope
- Regla de validación en `VacanteServicioComandos.CrearAsync`: rechazar estado inicial terminal.
- Nuevo escenario en `openspec/specs/vacante-management/spec.md`.
- Tests unitarios en `VacanteServicioComandosTests`.
- Tests de integración en `VacantesControllerTests`.

### Out of Scope
- Cambios en `CrearVacanteRequestValidator` (carece de acceso al repo de `EstadoVacante`).
- Nuevos tests para validador (no se modifica).
- Cambios en otros endpoints.

## Capabilities

### Modified Capabilities
- `vacante-management` (delta): agregar escenario "Estado inicial terminal rechazado" al requisito "Crear Vacante".

## Approach

Validación en `VacanteServicioComandos.CrearAsync` tras el lookup de `estadoVacante` (línea ~123) y antes de `ExistsAbiertaByPuestoAsync` (línea ~132). Evaluación de `estadoVacante.EsTerminal`; si es `true`, retornar `ValidationFailure` con `ErrorCategoria.Validation`, código `EstadoTerminalInmutable`, `FieldErrors = ["estadoVacanteId"]` y mensaje: `"El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."`.

El código `EstadoTerminalInmutable` se reutiliza (ya existe en `VacanteErrorCodigo.cs:9`); el HTTP varía según contexto: `409 Conflict` en `CambiarEstadoAsync` vs `400 Bad Request` en `CrearAsync`. El código identifica la condición semántica; el HTTP depende del endpoint.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modified | Validación `EsTerminal` en `CrearAsync` |
| `openspec/specs/vacante-management/spec.md` | Modified | Nuevo escenario delta |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modified | Test unitario `Crear_EstadoInicialTerminal_RetornaValidationFailure` |
| `tests/SGV.Tests/Api/VacantesControllerTests.cs` | Modified | Test API con fake `IVacanteServicioComandos` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `EsTerminal` no persiste en fake de test | Low | Configurar `EsTerminal = true` en fake `EstadoVacante` del helper de tests |
| Código `EstadoTerminalInmutable` reusado con HTTP diferente confunde | Low | Documentar en comentario que el código indica condición semántica; HTTP lo determina el endpoint |

## Rollback Plan

Revertir el `if (estadoVacante.EsTerminal)` agregado en `VacanteServicioComandos.CrearAsync`. Eliminar el escenario delta del spec. Eliminar los tests agregados. `dotnet build SGV.slnx` y `dotnet test SGV.slnx` deben pasar sin los cambios.

## Dependencies

- `EstadoVacanteConstantes.cs` y seeds en `DatosSemilla.cs` (ya existente, `EsTerminal` disponible).
- `VacanteErrorCodigo.EstadoTerminalInmutable` (ya existe en `VacanteErrorCodigo.cs:9`).

## Success Criteria

- [ ] `POST /api/v1/vacantes` con `EstadoVacanteId` terminal devuelve `400 Bad Request` + `ErrorCategoria.Validation` + código `EstadoTerminalInmutable`.
- [ ] `openspec/specs/vacante-management/spec.md` incluye escenario "Estado inicial terminal rechazado" en requisito "Crear Vacante".
- [ ] `dotnet test SGV.slnx` verde en `VacanteServicioComandosTests` y `VacantesControllerTests`.
- [ ] Sin regresión en tests existentes.
