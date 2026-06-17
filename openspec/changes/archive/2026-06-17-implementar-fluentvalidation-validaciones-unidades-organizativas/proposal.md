# Proposal: FluentValidation para validaciones de UnidadesOrganizativas

## Intent

Centralizar las validaciones de entrada de `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest` en FluentValidation dentro de `SGV.Aplicacion`, antes de consultar repositorios o persistir. Esto reduce validaciones dispersas, mejora errores por campo y mantiene el dominio como última barrera de invariantes.

## Proposal question round

Supuestos: los errores por campo deben exponerse como `ValidationProblemDetails` o equivalente; la primera tajada no debe migrar reglas con repositorio a validadores async; el contrato HTTP puede ajustarse sin cambiar reglas de negocio. Pregunta abierta: ¿los códigos de error por campo son contrato público estable?

## Scope

### In Scope
- Agregar FluentValidation en `SGV.Aplicacion` y registrar validadores por DI.
- Crear validadores para create/update: `Codigo`, `Nombre`, `Descripcion`, `TipoUnidadOrganizativaId`, `VigenteDesde`/`VigenteHasta`.
- Devolver errores por campo y mapearlos a `ValidationProblemDetails` o equivalente.
- Tests xUnit de validadores, servicio sin persistencia ante request inválido y contrato de errores.

### Out of Scope
- Cambios de esquema MySQL, migraciones, índices o constraints.
- Reemplazar invariantes del dominio por validadores.
- Refactor general de errores de todos los módulos.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `unidad-organizativa-crud`: agrega validación de entrada por campo para create/update y contrato de error estructurado.

## Approach

Usar validación manual con `IValidator<TRequest>` al inicio de `CrearAsync` y `ActualizarAsync`; evitar auto-validación MVC de FluentValidation. Candidatas a FluentValidation: requerido/no vacío, máximos (`Codigo` 50, `Nombre` 200, `Descripcion` 1000), `TipoUnidadOrganizativaId != Guid.Empty`, y rango de vigencia. Permanecen en dominio: protección contra estado interno inválido, fechas inconsistentes, identidad/autopadre del agregado y cualquier invariant no negociable. Permanecen en servicio/repositorios: código activo duplicado, existencia de tipo/padre y ciclos jerárquicos.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion` | Modified | Paquete, validadores, DI y ejecución manual. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modified | Mapeo a errores por campo. |
| `tests/SGV.Tests` | Modified | Casos de validación y contrato. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Divergencia entre dominio y FluentValidation | Med | Documentar frontera y mantener tests de dominio. |
| Cambio de contrato rompe consumidores | Med | Especificar formato y limitarlo a validación. |
| Primera tajada supera 400 líneas | Med | PR 1: paquete, validadores create/update y tests mínimos; API refinada si entra. |

## Rollback Plan

Revertir paquete, registro DI, validadores, mapeo de errores y tests asociados. Sin cambios de base de datos.

## Dependencies

- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`

## Success Criteria

- [ ] Requests inválidos de create/update devuelven errores por campo y no persisten.
- [ ] Reglas con repositorio y dominio siguen cubiertas por tests existentes.
- [ ] No hay migraciones ni cambios de esquema.
