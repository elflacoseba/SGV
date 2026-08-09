# Proposal: Filtrar estado Cubierta del dropdown de edición de Vacante

## Intent

El dropdown de estados en la página Edit de Vacante expone la opción "Cubierta", pero el servicio `CambiarEstadoAsync` exige un `PersonaId` cuando el destino es Cubierta. El form de Edit no tiene ese campo, entonces la transición falla silenciosamente (error de validación huérfano). Se filtra `EsCubierta=true` del dropdown para que solo se muestren estados editables directamente.

## Scope

### In Scope
- Extender `EstadoVacanteDto` con `EsCubierta` (sexto parámetro posicional).
- Poblar el flag en `MapToDto` de `EstadoVacanteServicioConsulta`.
- Filtrar `EsCubierta=true` en `EditModel.LoadStatesAsync` (dropdown de edición).
- Test web de regresión: verificar que el dropdown del GET no contiene opciones con `EsCubierta=true`.

### Out of Scope
- Permitir la transición a Cubierta desde Edit (debe seguir yendo por Postulación ganadora).
- Agregar selector de PersonaId en Edit.
- Cambios al API controller o al servicio de comandos.

## Capabilities

### Modified Capabilities
- `gestion-vacantes`: el dominio observable de la edición cambia — el dropdown solo expone estados editables sin pasar por Postulación.

## Approach

Extender el record posicional `EstadoVacanteDto` con un quinto parámetro `bool EsCubierta`. En el servicio de consulta, poblar el campo desde `estado.EsCubierta`. En `LoadStatesAsync` del EditModel, filtrar con `.Where(s => !s.EsCubierta)` antes de asignar a `EstadosVacante`. El test arma un catálogo con al menos un estado Cubierta y verifica que el HTML del GET NO contiene el option correspondiente.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/EstadoVacanteDto.cs` | Modified | Agregar `bool EsCubierta` al record. |
| `src/SGV.Aplicacion/Vacantes/Consultas/EstadoVacanteServicioConsulta.cs` | Modified | Poblar `EsCubierta` en `MapToDto`. |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs` | Modified | Filtrar `.Where(s => !s.EsCubierta)` en `LoadStatesAsync`. |
| `tests/SGV.Tests/Web/Vacantes/` | Modified/New | Test de regresión del filtro en dropdown. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Extender un record posicional rompe deserializadores JSON strict | Low | El flag va al final; deserializadores tolerantes (System.Text.Json por defecto) lo ignoran si no lo conocen. |
| Cancelada también debería filtrarse y no se documentó | Low | `EsCubierta=false` en Cancelada; no se ve afectada. |
| Test web no detecta el filtro si el fake ya viene filtrado | Med | El test debe construir catálogo con estado Cubierta y verificar que NO aparece en el HTML. |

## Rollback Plan

Revertir los 3 cambios de código (DTO + mapeo + filtro) y los tests. Sin migración de BD. Si la change está archivada, reabrir y revertir commits del change branch.

## Dependencies

Ninguna externa.

## Success Criteria

- [ ] `dotnet build SGV.slnx` verde.
- [ ] Tests web existentes siguen verdes.
- [ ] Test nuevo verifica que el dropdown de Edit NO contiene opciones con `EsCubierta=true`.
- [ ] GET a `/organizacion/vacantes/editar/{id}` no expone Cubierta como opción.
- [ ] `dotnet test SGV.slnx` verde.
