# Proposal: Hacer inmutable el `Codigo` de `UnidadOrganizativa` después de creada

## Intent

`UnidadOrganizativa` permite hoy cambiar `Codigo` post-create vía `CambiarDatos(codigo, ...)`, rompiendo la identidad lógica de la unidad (otras entidades y referencias esperan un `Codigo` estable) y contradiciendo el patrón vigente en `Puesto` (constructor con `Codigo`, `Actualizar(...)` que lo excluye). Este change **establece que `Codigo` se asigna una sola vez en create y queda inmutable**, alineando con `Puesto.Actualizar`. La implementación interna migra a `record class` con `init` para que el invariante sea enforceable por el compilador.

## Scope

### In Scope
- `UnidadOrganizativa` como `record class` con `init`; `Codigo` asignable solo en el constructor.
- Introducir `Actualizar(...)` (modelo `Puesto.Actualizar`) sin `Codigo`; `CambiarDatos` queda restringido a create o se elimina si la spec lo decide.
- Update afecta solo `Nombre`, `Descripcion`, `TipoUnidadOrganizativaId`, `UnidadPadreId`, `VigenteDesde`, `VigenteHasta`, `IsActive`/soft delete/reactivación; **nunca `Codigo`**.
- Ajustar `ActualizarUnidadOrganizativaRequest`, validador FluentValidation, cliente tipado de `SGV.Web` y Edit PageModel para que `Codigo` no sea editable.
- Reescribir/ajustar ~60 tests y agregar un test que **fije la inmutabilidad de `Codigo` en update**.

### Out of Scope
- `Cargo` y `Puesto` sin cambios; `Puesto` ya cumple el invariante.
- Sin migraciones EF Core, sin `Version`/`rowversion`, sin historial.
- Issue #59 (`ActivePuestoIdUnique`).

## Capabilities

### New Capabilities
None

### Modified Capabilities
- `unidad-organizativa-crud`: el contrato de update/PUT deja de aceptar cambios sobre `Codigo`. La delta spec debe MODIFICAR `Scenario: Update organizational unit` (aclarar que `Codigo` no se actualiza) y `Scenario: Rechazar código activo duplicado` (aplica solo a create; la reactivación sigue sujeta al invariante).
- `unidad-organizativa-web-detalle-edicion`: el formulario de edit deja de permitir modificar `Codigo`. La delta spec debe MODIFICAR `Datos visibles y editables` para excluir `Codigo` de los editables (detail sigue mostrándolo).

## Approach

1. `record class` con `init`; `Codigo` solo en el constructor. `Actualizar(string nombre, string? descripcion, Guid tipoUnidadOrganizativaId, Guid? unidadPadreId, DateOnly? vigenteDesde, DateOnly? vigenteHasta)` + `Activar`/`Desactivar`/`Reactivar`.
2. Alternativa: mantener `CambiarDatos` con `codigo` y usarlo **solo desde `Crear`**, eliminando el parámetro cuando se invoca desde update. Decisión en spec; el invariante "`Codigo` no cambia post-create" es obligatorio.
3. `PersistenceToDomainMapper.ToDomain`: record vía constructor + `with`; sin reflexión.
4. `EntidadAuditable` mantiene `public set`; el interceptor de auditoría sigue escribiendo `CreatedAt`/`UpdatedAt`/`IsDeleted`.
5. `UnidadOrganizativaServicioComandos.Actualizar`: capturar `var unidad = unidad.Actualizar(...)`; pasar el record a `UpdateAsync`. FluentValidation deja de exigir `Codigo` en update.
6. `SGV.Web`: Edit PageModel pone `Codigo` de solo lectura; cliente tipado envía update sin `Codigo` (o lo ignora).
7. Tests: asertar "post-Actualizar, `Codigo` permanece igual".

## Affected Areas

- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` (Modified): `record class` con `init`; `Actualizar(...)` sin `Codigo`.
- `src/SGV.Dominio/Comun/EntidadAuditable.cs`, `EntidadBase.cs` (Modified): mantener `public set`; documentar asimetría.
- `src/SGV.Aplicacion/Contratos/Organizacion/ActualizarUnidadOrganizativaRequest.cs` (Modified): quitar o ignorar `Codigo`.
- `src/SGV.Aplicacion/Validaciones/Organizacion/ActualizarUnidadOrganizativaRequestValidator.cs` (Modified): no validar `Codigo` en update.
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` (Modified): capturar record; no propagar `Codigo` desde update.
- `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` (Modified): eliminar `SetProperty`/`BindingFlags.NonPublic`; mapear vía constructor + `with`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` (Modified): `UpdateAsync` contra el record inmutable.
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` (Modified): contrato PUT alineado.
- `src/SGV.Web/Integration/.../IUnidadOrganizativaApiClient.cs` y DTOs (Modified): request de update sin `Codigo`.
- `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` y `UnidadOrganizativaInputModel.cs` (Modified): `Codigo` de solo lectura en edit; detail sin cambios.
- Tests (Modified): Dominio, Aplicación (servicio + validadores), Persistencia, API, Web.
- `openspec/specs/unidad-organizativa-crud/spec.md` y `unidad-organizativa-web-detalle-edicion/spec.md` (Modified vía delta): scenarios de update.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `EntidadAuditable` con `init` rompe el interceptor de auditoría | Med | Mantener `public set` en la base; documentar la excepción. |
| `PersistenceToDomainMapper` con reflexión deja de compilar | High (esperado) | Reescribir el mapper para construir el record con constructor + `with`. |
| Request de update trae `Codigo` desde UI/clientes y el backend lo ignora silenciosamente | Med | Spec decide: rechazar (`400`) si llega, o aceptarlo como no-op; documentar el contrato. |
| La UI web actual permite editar `Codigo`; el cambio rompe la edición existente | Med | Edit PageModel pasa `Codigo` a solo lectura; conserva valor pero se ignora en submit. |
| Clients externos (no web) envían `Codigo` en PUT esperando un cambio | Med | Documentar el cambio en `docs/decisiones-implementacion.md`; coordinar con administradores. |
| Cambio se propaga a `Cargo`/`Puesto` por copia de patrón | Low | Documentar alcance estricto en tasks; revisar PR. |

## Rollback Plan

Revertir el commit sobre `UnidadOrganizativa`, `ActualizarUnidadOrganizativaRequest`, `UnidadOrganizativaServicioComandos`, `PersistenceToDomainMapper`, `Edit.cshtml.cs` y los tests. Restaurar la `sealed class` mutable con `private set` y `CambiarDatos(codigo, ...)`. Restaurar `SetProperty` con `BindingFlags.NonPublic` en el mapper. Sin migraciones ni cambios de DB: rollback = revert de código + recompilación + suite previa. Aceptar `Codigo` en update como no-op convive con rollback sin crash.

## Dependencies

- Ninguna nueva dependencia NuGet. C# 14 ya soporta `record class` + `init`.
- `strict_tdd: true` permanece activo en `openspec/config.yaml`.

## Success Criteria

- [ ] `UnidadOrganizativa` es un `record class` con `init`; `Codigo` se asigna solo en create.
- [ ] La operación de update/PUT **preserva el `Codigo` original** (test que cree, actualice otros campos y verifique `Codigo` intacto).
- [ ] `Actualizar(...)` (o el método post-create equivalente) no expone `Codigo` como parámetro.
- [ ] `PersistenceToDomainMapper.ToDomain` no usa `BindingFlags.NonPublic` ni `SetProperty` para `IsActive`, `UnidadPadre` o `TipoUnidadOrganizativa`.
- [ ] `Cargo` y `Puesto` sin cambios; `Puesto` ya cumple el invariante.
- [ ] El formulario de edit en `SGV.Web` no permite editar `Codigo` (solo lectura o eliminado del form).
- [ ] Endpoints HTTP y listados `status=activas|eliminadas` siguen devolviendo el `Codigo` original en cada response.
- [ ] `dotnet test SGV.slnx` pasa completa, excluyendo los 12 tests ya fallando por issue #59.
- [ ] `AuditoriaSaveChangesInterceptor` sigue escribiendo `CreatedAt`, `UpdatedAt`, `IsDeleted` correctamente.
