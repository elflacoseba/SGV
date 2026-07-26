# Design: Permitir crear y editar Personas con legajo nulo (issue #202)

## 1. Resumen y motivación

Issue #202: alinear wire + UI + auditoría al hecho de que el Dominio ya admite `Persona.Legajo = null`. Cierra tres desalineaciones (web exige `[Required]`, wire es `string` no-nullable, `SetupServicio` aplica `?? string.Empty` como workaround) y agrega fila explícita en `Auditorias` cuando se limpia un legajo persistido. Honra `docs/decisiones-implementacion.md` § Auditoría y § ErrorCategoria (#125). Link: `proposal.md`, `exploration.md`, deltas `specs/persona-management/spec.md` + `specs/web-apiclient-transport-contract/spec.md`.

## 2. Cambios por capa

- **Dominio** — sin cambios (`Persona.Legajo` ya `string?`).
- **Aplicación** — `PersonaServicioComandos` agrega `IAuditoriaServicio` + `IUsuarioActual` al ctor primario; ctor de back-compat para tests con `NoopAuditoriaServicio` interno. En `ActualizarAsync`, captura `legajoAnterior = persona.Legajo` antes de `CambiarDatos(...)`; si la transición es no-nulo → null, invoca `RegistrarAsync("Persona", id, "UpdateLegajo", userId, {"LegajoAnterior": previo}, {"LegajoNuevo": null})` tras `SaveChangesAsync`.
- **Contratos** — `CrearPersonaRequest.Legajo` y `ActualizarPersonaRequest.Legajo`: `string` → `string?` (única posición obligatoria).
- **Infraestructura** — `SetupServicio.cs:104`: `Legajo: request.Legajo ?? string.Empty` → `Legajo: request.Legajo`. `SetupRequest.Legajo` ya es `string?` y `Persona` acepta null.
- **Web** — `PersonaInputModel.Legajo`: quitar `[Required]`, `[StringLength(20)]` → `[StringLength(50)]`, `string` → `string?` (sin default). `Create.cshtml.cs:111` y `Edit.cshtml.cs:174`: `Input.Legajo.Trim()` → `string.IsNullOrWhiteSpace(Input.Legajo) ? null : Input.Legajo.Trim()`. `Edit.cshtml.cs:118`: `Input.Legajo = persona.Legajo ?? string.Empty` → `Input.Legajo = persona.Legajo`. `_Form.cshtml`: agregar `<span class="text-warning small" data-legajo-context-warning hidden></span>` debajo del campo Legajo (slot reservado). `IPersonaForm` gana `bool ShowLegajoContextWarning { get; }` con default `false`.

## 3. Wire-type transition

```csharp
public sealed record CrearPersonaRequest(string? Legajo, string Nombres, string Apellidos, ...);
public sealed record ActualizarPersonaRequest(string? Legajo, string Nombres, string Apellidos, ...);
```

Payloads equivalentes a `Legajo == null`: `{"legajo": null}`, `{"legajo": ""}` (string vacío tratado como ausente por `CheckUniquenessAsync`), o clave ausente. 24 invocaciones verificadas con named args; `SetupServicio` cambia explícitamente. `PersonaApiClient` NO pre-procesa; falla el build si se reintroduce `Trim`/normalización en `CreateAsync/UpdateAsync/ReactivarAsync`.

## 4. Normalización UI

```csharp
static string? NormalizarLegajo(string? raw)
    => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
```

Aplicado al construir `CrearPersonaRequest`/`ActualizarPersonaRequest` en `Create.cshtml.cs:110-117` y `Edit.cshtml.cs:173-180`. En `Edit.cshtml.cs:118` (pre-carga GET) se asigna `persona.Legajo` directo.

## 5. Auditoría explícita al limpiar Legajo

Punto exacto en `PersonaServicioComandos.ActualizarAsync`, dentro del bloque `try` posterior a `CheckUniquenessAsync`:

```csharp
var legajoAnterior = persona.Legajo;
persona.CambiarDatos(request.Nombres, request.Apellidos,
                     request.Legajo, request.Email, request.Telefono);
persona.CambiarDocumento(request.TipoDocumentoId, request.NumeroDocumento);
await repository.UpdateAsync(persona, cancellationToken);
await unitOfWork.SaveChangesAsync(cancellationToken);
if (legajoAnterior is not null && persona.Legajo is null)
{
    await auditoriaServicio.RegistrarAsync(
        "Persona", persona.Id.ToString(), "UpdateLegajo",
        usuarioActual.UserId,
        new Dictionary<string, object?> { ["LegajoAnterior"] = legajoAnterior },
        new Dictionary<string, object?> { ["LegajoNuevo"] = null },
        cancellationToken);
}
return PersonaCommandResult.Success(MapToDto(persona));
```

Independiente del origen. El interceptor central sigue emitiendo su fila `Modificacion`; ambas coexisten con `Operation` distinta y mismo `CorrelationId`. Si `RegistrarAsync` lanza, propaga (paridad con `UsuarioServicioComandos`). `CrearAsync` no emite (no hay transición previa).

## 6. Advertencia UI contextual

Render en `_Form.cshtml` debajo del campo Legajo, Bootstrap `text-warning small` y atributo `hidden` por default. `IPersonaForm.ShowLegajoContextWarning` lo activa (default `false`); el módulo downstream futuro lo setea a `true` cuando aplique. No bloquea el submit, no usa `validation-summary`, no deshabilita el botón.

## 7. SetupServicio cleanup

`SetupServicio.cs:104`: `Legajo: request.Legajo ?? string.Empty` → `Legajo: request.Legajo`. Los tests `SetupServicioTests` y `SetupConcurrencyMySqlFactTests` siguen pasando (no dependen del valor concreto).

## 8. Tests (TDD estricto)

| Capa | Test | Tipo |
|---|---|---|
| Aplicación | `CrearAsync_LegajoNull_PermitidoYGuarda` — assert `IsSuccess`, `AddCallCount==1`, `auditoriaCount==0` | `[Fact]` |
| Aplicación | `ActualizarAsync_LimpiarLegajo_RegistraAuditoria` — `Legajo="L-001"→null`, assert `accion=="UpdateLegajo"`, `LegajoAnterior=="L-001"`, `LegajoNuevo==null` | `[Fact]` |
| Aplicación | `ActualizarAsync_LegajoSinTransicion_NoEmiteAuditoriaLegajo` — `Legajo="L-001"→"L-001"`, assert `auditoriaCount==0` | `[Fact]` |
| Aplicación | `ActualizarAsync_LegajoDuplicado_SigueRechazando` (regresión) | regresión |
| Web seam | `CreateAsync_LegajoNull_SerializaLegajoNull` — `RecordingHandler` captura body, assert `ValueKind == Null` | `[Fact]` |
| Web seam | `CreateAsync_LegajoVacio_SerializaLegajoVacio` — `Legajo=""`, assert `ValueKind == String` | `[Fact]` |
| Web seam | `UpdateAsync_LegajoConEspaciosNoTrimeaCliente` — `Legajo="  L-7  "`, assert body contiene `"  L-7  "` literal | `[Fact]` |
| Web page | `EditPageTests.OnPostAsync_LegajoWhitespace_NormalizaANull` — `Input.Legajo="   "`, assert `client.UpdateAsync` recibe `Legajo=null` | `[Fact]` |
| API | `Post_LegajoNullEnBody_Retorna201ConLegajoNull` — `{"legajo": null}`, assert 201 y `PersonaDto.Legajo==null` | `[Fact]` |
| API | `Put_LimpiarLegajo_Retorna200YRegistraUpdateLegajo` — fake captura invocación, assert 200 | `[Fact]` |
| API | `Put_LegajoSinClave_Retorna200` — body omite `legajo`, assert 200 | `[Fact]` |
| MySQL | `PersonaRepositoryTests.PersistirPersona_LegajoNull_LecturaPosterior` — crea + lee con `Legajo=null` | `[MySqlFact]` |

Helper `FakeAuditoriaServicio` (contador + última invocación) en `tests/SGV.Tests/Aplicacion/Personas/`. El ctor de back-compat de `PersonaServicioComandos` acepta `IAuditoriaServicio?` opcional (default `NoopAuditoriaServicio`) para no inflar los 11 tests vigentes.

## 9. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Source-breaking en call-sites posicionales | 24 call-sites usan named args (verificado); build fail-loud. |
| `ExistsActiveLegajoAsync(legajo="")` matchea vacíos | Guarda `!string.IsNullOrEmpty` ya vigente; tests de regresión. |
| Serialización JSON divergente | Normalización única en PageModel; tests seam cubren los 3 casos. |
| Fila explícita vs fila del interceptor | Mismo `SgvDbContext` scoped, mismo `CorrelationId`; coexisten. |
| `IUsuarioActual` ausente en tests | Ctor de back-compat con `NoopAuditoriaServicio`. |
| Warning UI se vuelve ruido | Slot `hidden` por default; módulo downstream decide. |

## 10. Plan de rollback

Revertir los 6 archivos (`PersonaRequests.cs`, `PersonaInputModel.cs`, `Create.cshtml.cs`, `Edit.cshtml.cs`, `SetupServicio.cs`, `PersonaServicioComandos.cs`), quitar el `<span>` en `_Form.cshtml`, borrar los 8 tests nuevos. Cero migración, cero cambio de esquema. La pre-carga de Edit vuelve a `?? string.Empty` y los wire-types a `string` no-nullable. La fila `UpdateLegajo` deja de emitirse sin afectar el interceptor central.

## 11. Dependencias y supuestos

- **Sin nuevos paquetes NuGet**. `IAuditoriaServicio` ya registrado (`SGV.Infraestructura.DependencyInjection:40`); `IUsuarioActual` ya en DI; `SGV.Contracts` sigue siendo leaf.
- **Cero migraciones**. La columna `Personas.Legajo` ya es `varchar(50) NULL`; el interceptor no se toca.
- **No se introduce `ErrorCategoria` nuevo** ni se relaja la unicidad activa. Las reglas FluentValidation permanecen idénticas: `Legajo` opcional, `MaximumLength(50)` cuando hay valor.
- **El módulo downstream que active la advertencia UI es out-of-scope**; el slot queda reservado.
- **El cambio `setup-admin-inicial-issue-195` no se toca** (verificado en `exploration.md` §1.9).
- **Threat matrix N/A** — no toca routing, shell, subprocess, VCS/PR automation ni process integration.
