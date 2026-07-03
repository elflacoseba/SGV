# Design: Permitir editar el código de una Habilidad

## 1. Resumen y contexto

- `proposal.md` pide revertir la inmutabilidad post-alta de `Habilidad.Codigo` para corregir errores operativos sin recrear registros.
- Delivery planificado: 1 slice, stacked-to-`develop`, budget 400 líneas, `strict_tdd: true`; el RED sale de los escenarios delta.

## 2. Cambios por capa

### 2.1 Dominio

- Archivo: `src/SGV.Dominio/Habilidades/Habilidad.cs`.
- `Codigo` mantiene `private set`; cambia el contrato de `Actualizar(...)` para aceptar `codigo` y reutilizar las invariantes ya existentes de `CambiarDatos`: requerido + `maxLength 50` para `Codigo`, requerido + `maxLength 200` para `Nombre`, opcionales con límites para `Categoria`/`Descripcion`.
- No hay regex vigente en dominio ni validator; el diseño NO introduce una regla nueva.

### 2.2 Aplicación

- `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadRequests.cs`: `ActualizarHabilidadRequest` pasa a aceptar `string Codigo`.
- `src/SGV.Aplicacion/Habilidades/Comandos/Validaciones/ActualizarHabilidadRequestValidator.cs`: mismas reglas de shape que create para `Codigo`; no se mete unicidad en FluentValidation.
- `src/SGV.Aplicacion/Habilidades/Comandos/HabilidadServicioComandos.cs`:
    - pre-check `ExistsActiveCodeAsync(request.Codigo, id, ...)`;
    - si `Codigo` es igual al actual, el `excludingId` evita falso conflicto;
    - `habilidad.Actualizar(request.Codigo, request.Nombre, ...)`;
    - helper privado estilo Cargo para centralizar conflicto de código activo;
    - catch específico de `DbUpdateException` por `IX_Habilidades_ActiveCodigoUnique` para devolver `HabilidadErrorType.Conflict`/`CodigoDuplicado`; otras violaciones se propagan.

### 2.3 Persistencia (EF Core)

- `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs`: el índice único activo ya existe mediante computed column `ActiveCodigoUnique`; se relee, no se cambia.
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs`: `UpdateEntity(HabilidadEntity, Habilidad)` debe copiar `Codigo` además de los demás campos.
- NO hay migración nueva: la columna generada recalcula automáticamente cuando cambia `Codigo` y el schema no cambia.

### 2.4 API (Controllers)

- `src/SGV.Api/Controllers/SkillsController.cs`: `PUT /api/v1/skills/{id}` mantiene la lógica de delegación, pero cambia su request body por el nuevo `ActualizarHabilidadRequest(Codigo, Nombre, Categoria, Descripcion)`.
- Contrato observable: `200` éxito, `400` shape inválido, `404` no existe, `409` código activo duplicado.
- Es un breaking change contractual del `PUT`; debe quedar explícito en changelog/PR.

### 2.5 Web (Razor Pages)

- `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml`: remover la rama `readonly`; dejar un único `<input asp-for="Input.Codigo" ...>` editable.
- `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml.cs`: construir `new ActualizarHabilidadRequest(Input.Codigo, Input.Nombre, ...)`; mantener PRG a `Details` en éxito y formulario corregible ante error.
- `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` y `HabilidadApiClient.cs`: actualizar comentario/shape de `UpdateAsync`; el payload ya viaja serializando el request, así que sólo cambia el DTO.
- El `409` se sigue mostrando sobre `Input.Codigo`; no se agrega catálogo ni `NivelId`.

## 3. Manejo del conflicto de unicidad

- Pre-check: si `request.Codigo` pertenece a otra habilidad activa, se rechaza antes de guardar con conflicto funcional.
- Árbitro final: `IX_Habilidades_ActiveCodigoUnique` en MySQL protege carreras entre el pre-check y `SaveChangesAsync`.
- Traducción: replicar el patrón de `CargoServicioComandos.IsActiveCodigoUniqueViolation(DbUpdateException)` pero apuntando a `IX_Habilidades_ActiveCodigoUnique`, sin introducir dependencia de Pomelo/MySql en `SGV.Aplicacion`.
- Idempotencia: enviar el mismo `Codigo` actual NO es conflicto; `ExistsActiveCodeAsync(..., excludingId: id)` ya soporta ese caso.

## 4. Tests (mapeo scenario → test)

| Scenario delta | Test planeado |
|---|---|
| Web: editar código existente | `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` → `Post_Edit_WhenCodigoChanges_RedirectsWithUpdatedCodigo` |
| Web: editar otros campos sin cambiar código | mismo archivo → `Post_Edit_WhenCodigoUnchanged_UpdatesOtherFields` |
| Web: código inválido | `HabilidadEditPageTests` + `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs` |
| Web: código duplicado activo | `HabilidadEditPageTests.Post_Edit_WhenConflictOnCodigo_ReturnsFieldError` (ajustar) |
| Web: reutilizar código de baja lógica | `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` + smoke web con fake |
| Web: edit muestra código editable | reemplaza `HabilidadEditPageTests.EditPage_MuestraCodigoComoReadonly_O_Disabled` |
| Web: PRG con cambio de código | `HabilidadEditPageTests.Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation` (ajustar request/asserciones) |
| Management: update con código de otra activa | `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs` → `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar` |
| Management: update con mismo código | `HabilidadServicioComandosTests` → `ActualizarAsync_MismoCodigo_NoSeTrataComoDuplicado` |
| Management: update con código de eliminada | `HabilidadServicioComandosTests` + `HabilidadRepositoryTests` → `UpdateAsync_CodigoSoftDeleted_PermiteReutilizarCodigo` |
| Management: actualización exitosa con cambio/sin cambio | `HabilidadServicioComandosTests` y `tests/SGV.Tests/Api/SkillsControllerTests.cs` |
| Management: código inválido en update | `ActualizarHabilidadRequestValidatorTests` + API `Put_InvalidCodigo_Returns400WithFieldErrors` |

Actualizar/eliminar tests existentes: `tests/SGV.Tests/Dominio/HabilidadTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioComandosTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/ActualizarHabilidadRequestValidatorTests.cs`, `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs`, `tests/SGV.Tests/Api/SkillsControllerTests.cs`, `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`, `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs`. Sin tests para getters/setters, DI ni controllers que sólo delegan.

## 5. Migración

No requiere migración nueva: `HabilidadConfiguracion` y la migración inicial ya materializan `ActiveCodigoUnique` + `IX_Habilidades_ActiveCodigoUnique`; el cambio es sólo de comportamiento en dominio/aplicación/web.

## 6. Estrategia de commits (work units)

1. Dominio (`Habilidad` + tests).
2. Aplicación (request, validator, servicio, tests de conflicto).
3. Persistencia (mapper + tests MySQL de update).
4. API (contrato `PUT` + tests API/fakes).
5. Web (Razor, ApiClient, fakes/tests web).
6. Delta/archive en su fase correspondiente.

Un único slice sigue siendo viable; si el diff amenaza el budget, el apply deberá recortar alcance accidental, no inventar otra feature.

## 7. Riesgos y mitigaciones

- Breaking change del `PUT` para consumidores externos → documentarlo explícitamente.
- `500` por violación cruda del índice → catch específico `DbUpdateException`.
- Drift con Cargos (`NivelId`) → mantener y correr `tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs`.
- Tests actuales protegen la regla opuesta → reemplazo explícito en el mismo work unit.
- Baselines vigentes aún contradicen el cambio hasta `archive` → no usar esos baselines como verdad de implementación durante apply.

## 8. Rollback

Rollback funcional: revertir `ActualizarHabilidadRequest`, restaurar `Habilidad.Actualizar(...)` sin `Codigo`, volver a `readonly` en `_Form.cshtml` y dejar de mapear `Codigo` en persistencia. No hay rollback de schema.

## 9. Success criteria

- Un administrador puede editar `Codigo` desde `SGV.Web`.
- `PUT /api/v1/skills/{id}` acepta `Codigo` y persiste el cambio sin duplicado activo.
- Un duplicado activo responde coherentemente sin degradar a `500`.
- Se mantiene la ausencia de `NivelId` en `Habilidad` y la política de soft delete + unicidad activa.
- El handler de conflicto por `IX_Habilidades_ActiveCodigoUnique` queda testeado en aplicación.
