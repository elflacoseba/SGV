# Design: Hacer inmutable el `Codigo` de `UnidadOrganizativa` después de creada

## Technical Approach

`UnidadOrganizativa` migra a `sealed record class : EntidadAuditable` con propiedades `init`. `Codigo` se asigna únicamente en el constructor primario. Toda mutación posterior (`Actualizar`, `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar`) devuelve una nueva instancia vía `with` y **nunca** expone `codigo` como parámetro. La superficie pública (`Actualizar(...)` con la firma de `Puesto.Actualizar`) garantiza el invariante a nivel de API de dominio; la persistencia usa `with` en el mapper en lugar de `BindingFlags.NonPublic`/`SetProperty`. El request de update deja de llevar `Codigo`; el binding JSON ignora silenciosamente cualquier `codigo` extra. La capa web refleja la inmutabilidad ocultando el input en edit. Sin migraciones, sin tocar `Cargo`/`Puesto`.

## Architecture Decisions

### Decision 1: Convertir `UnidadOrganizativa` a `record class` con `init`

**Choice**: `public sealed record class UnidadOrganizativa : EntidadAuditable` con `Codigo`, `Nombre`, `TipoUnidadOrganizativaId`, `UnidadPadreId`, `Descripcion`, `VigenteDesde`, `VigenteHasta`, `IsActive` declaradas `init`. Las colecciones y las nav props (`UnidadPadre`, `TipoUnidadOrganizativa`) también `init`.
**Alternatives**: (a) Mantener `sealed class` con `private set` (estilo actual de `Puesto`); (b) `record class` con `private set` mixtos.
**Rationale**: El success criterion #4 pide explícitamente sacar `SetProperty`/`BindingFlags.NonPublic` para `IsActive`, `UnidadPadre` y `TipoUnidadOrganizativa`. Con `record class` + `init` el mapper usa `with` y queda libre de reflexión. El constructor primario queda como único punto de asignación de `Codigo`, eliminando la puerta trasera `CambiarDatos(codigo, ...)`. La asimetría con `Puesto` se documenta como excepción deliberada; no se toca `Puesto`.

### Decision 2: Eliminar `CambiarDatos` y añadir `Actualizar(...)` que NO acepta `Codigo`

**Choice**: `CambiarDatos` se elimina del dominio. `Actualizar(string nombre, string? descripcion, Guid tipoUnidadOrganizativaId, Guid? unidadPadreId, DateOnly? vigenteDesde, DateOnly? vigenteHasta)` devuelve `this with { ... }`. `DefinirVigencia`, `CambiarUnidadPadre`, `Activar`, `Desactivar` también devuelven `with`. El constructor primario ahora acepta `descripcion` (alineado con `Puesto`).
**Alternatives**: Mantener `CambiarDatos` interno solo para create.
**Rationale**: La spec exige "`Codigo` no es parámetro del método post-create". Mantener `CambiarDatos` con `codigo` deja una puerta abierta al test helper y al mapper; eliminarlo cierra el invariante por construcción.

### Decision 3: `ActualizarUnidadOrganizativaRequest` sin `Codigo`

**Choice**: `public sealed record ActualizarUnidadOrganizativaRequest(string Nombre, Guid TipoUnidadOrganizativaId, string? Descripcion = null, Guid? UnidadPadreId = null, DateOnly? VigenteDesde = null, DateOnly? VigenteHasta = null)`.
**Alternatives**: Mantener `Codigo` opcional y marcar `[Obsolete]`.
**Rationale**: System.Text.Json con binding por defecto descarta cualquier propiedad JSON ausente en el target. Esto cumple la spec textualmente: "un `codigo` extra en JSON de update queda fuera de contrato". Eliminar la propiedad es la expresión más clara del contrato.

### Decision 4: Validator y servicio consistentes con el nuevo contrato

**Choice**: Eliminar `RuleFor(x => x.Codigo)` en `ActualizarUnidadOrganizativaRequestValidator`. Eliminar `ExistsActiveCodeAsync(request.Codigo, id, ...)` en `UnidadOrganizativaServicioComandos.ActualizarAsync`. Mantener el check en `ReactivarAsync` (la reactivación sigue validando el `Codigo` persistido, no el del request).
**Rationale**: El código que llega en update no se persiste, así que el check de duplicidad es redundante. La reactivación es el único flujo que necesita revalidar el código persistido contra colisiones activas.

### Decision 5: Web edit oculta el input de `Codigo`

**Choice**: Añadir `bool IsEdit { get; }` a `IUnidadOrganizativaForm` (mirror de `IPuestoForm`). En `_Form.cshtml` envolver `<input asp-for="Input.Codigo">` con `@if (!Model.IsEdit)` (mismo patrón que `Puestos/_Form.cshtml`).
**Rationale**: Paridad visual con `Puesto`. La tarjeta sigue mostrando el código vía `Model.Input.Codigo` en el header de `Edit.cshtml`, pero el form no permite editarlo.

## Data Flow

    Cliente HTTP → Controller.Update (PUT)
        ↓ ActualizarUnidadOrganizativaRequest (sin Codigo)
    UnidadOrganizativaServicioComandos.ActualizarAsync
        ↓ validator (sin Codigo) → GetByIdForUpdateAsync
        ↓ unidad = unidad.Actualizar(...) → nuevo record via with (Codigo intacto)
    UnidadOrganizativaRepository.UpdateAsync → UpdateEntity
        ↓ SaveChangesAsync
    DB row con Codigo original

## File Changes

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` | Modify | Convertir a `sealed record class` con `init`. Añadir `Actualizar(...)` que devuelve `with`. Eliminar `CambiarDatos`. Constructor primario acepta `descripcion`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` | Modify | `ActualizarUnidadOrganizativaRequest` sin `Codigo`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarUnidadOrganizativaRequestValidator.cs` | Modify | Eliminar `RuleFor(x => x.Codigo)`. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Modify | `ActualizarAsync`: eliminar dedup codigo, capturar `unidad = unidad.Actualizar(...)`. `CrearAsync`: pasar descripcion al constructor; usar `DefinirVigencia` que devuelve `with`. `CambiarUnidadPadreAsync`/`ReactivarAsync`/`EliminarAsync`: capturar `with` donde corresponda. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` | Modify | `ToDomain(UnidadOrganizativaEntity)`: construir via constructor, asignar auditable heredado, usar `with { IsActive, UnidadPadre, TipoUnidadOrganizativa, Descripcion, Vigencia }`. Eliminar `CambiarDatos`/`DefinirVigencia`/`SetProperty` para esta entidad. |
| `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaForm.cs` | Modify | Añadir `bool IsEdit { get; }`. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml.cs` | Modify | Añadir `IsEdit => false`. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` | Modify | Añadir `IsEdit => true`. Eliminar `Input.Codigo` del payload a la API. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/_Form.cshtml` | Modify | Envolver input `Codigo` con `@if (!Model.IsEdit)`. |
| `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` | Modify | Tests `CambiarDatos_*` → `Actualizar_*`. Añadir `Codigo_EsInmutableTrasCreacion` y `Actualizar_CodigoNoCambia` (mirror Puesto). |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Modify | Quitar `ActualizarAsync_CodigoDuplicado_*` y tests de validación con codigo. Adaptar a nueva firma. Añadir `ActualizarAsync_PreservaCodigoOriginal` como regression crítica. |
| `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarUnidadOrganizativaRequestValidatorTests.cs` | Modify | Quitar sección "Codigo". |
| `docs/decisiones-implementacion.md` | Modify | Documentar que `Codigo` es identidad lógica inmutable post-create y que un `codigo` extra en PUT se ignora por contrato. |

## Interfaces / Contracts

```csharp
public sealed record class UnidadOrganizativa : EntidadAuditable
{
    public UnidadOrganizativa(string codigo, string nombre, Guid tipoUnidadOrganizativaId,
                              string? descripcion = null, Guid? unidadPadreId = null);
    public string Codigo { get; init; }                    // init: asignable solo en ctor / with
    public string Nombre { get; init; }
    public Guid? UnidadPadreId { get; init; }
    public UnidadOrganizativa? UnidadPadre { get; init; }
    public Guid TipoUnidadOrganizativaId { get; init; }
    public TipoUnidadOrganizativa? TipoUnidadOrganizativa { get; init; }
    public string? Descripcion { get; init; }
    public DateOnly? VigenteDesde { get; init; }
    public DateOnly? VigenteHasta { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyCollection<UnidadOrganizativa> UnidadesHijas => _unidadesHijas;
    public IReadOnlyCollection<Puesto> Puestos => _puestos;
    public UnidadOrganizativa Actualizar(string nombre, string? descripcion,
        Guid tipoUnidadOrganizativaId, Guid? unidadPadreId,
        DateOnly? vigenteDesde, DateOnly? vigenteHasta);
    public UnidadOrganizativa DefinirVigencia(DateOnly? desde, DateOnly? hasta);
    public UnidadOrganizativa CambiarUnidadPadre(Guid? unidadPadreId);
    public UnidadOrganizativa Desactivar();
    public UnidadOrganizativa Activar();
}

public sealed record ActualizarUnidadOrganizativaRequest(
    string Nombre, Guid TipoUnidadOrganizativaId,
    string? Descripcion = null, Guid? UnidadPadreId = null,
    DateOnly? VigenteDesde = null, DateOnly? VigenteHasta = null);
```

## Testing Strategy

| Capa | Qué se prueba | Cómo |
|---|---|---|
| Dominio | `Codigo` no tiene setter público (`Codigo_EsInmutableTrasCreacion`); `Actualizar` preserva `Codigo`; `Actualizar` con nombre vacío lanza; `Actualizar` con `tipoUnidadOrganizativaId == Guid.Empty` lanza; `Actualizar` con padre igual a `Id` lanza; mutadores devuelven instancia con campos actualizados. | xUnit en `Dominio/Organizacion/UnidadOrganizativaTests.cs`. |
| Aplicación | `ActualizarAsync_PreservaCodigoOriginal` (regression crítica); `ActualizarAsync_DatosValidos_RetornaDtoActualizadoYGuarda`; `ActualizarAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar`; `ActualizarAsync_UnidadInexistente_RetornaNoEncontradoYSinGuardar`; `ActualizarAsync_NombreVacio_RetornaFieldErrorsSinConsultarRepos`. Quitar `ActualizarAsync_CodigoDuplicado_*` y `ActualizarAsync_CodigoVacio_*`. `ReactivarAsync_*` y `CambiarUnidadPadreAsync_*` se mantienen. | Fakes en memoria; ya cubren short-circuit. |
| Validación | Quitar todos los tests de Codigo en `ActualizarUnidadOrganizativaRequestValidatorTests`. Conservar Nombre/Descripcion/Tipo/Vigencia. | xUnit + FluentValidation.TestHelper. |
| Persistencia | Verificar que el mapper no llama `SetProperty` para `IsActive`/`UnidadPadre`/`TipoUnidadOrganizativa`. Tests `[MySqlFact]` que ya pasan deben seguir verdes sin migraciones. | `MySqlFact` y asserts sobre el mapper. |
| API | Smoke: PUT con `{"codigo":"HACKED", ...}` persiste `Codigo` original. | `ApiWebApplicationFactory` con handler stub. |
| Web | Edit: el input `Codigo` no aparece; submit envía update sin `Codigo`. | Razor tests en `Web/UnidadOrganizativaWebTests.cs`. |

## Migration / Rollout

No migration. Solo cambio de código. Rollback = revert del commit + recompilación + suite previa. Aceptar `codigo` extra en PUT como no-op durante la ventana de coexistencia con clientes antiguos (binding JSON lo ignora sin error).

## Open Questions

Ninguna. Decisiones heredadas del patrón `Puesto.Actualizar` y de la propuesta corregida. Si la spec exige extender `CambiarDatos` a un nombre específico (p.ej. `ActualizarDatosBasicos` para distinguir del `CambiarUnidadPadre`), se hará en un change separado.