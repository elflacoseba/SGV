# Design: Vacantes Hardening

**Fecha**: 2026-08-18
**Change name**: `vacantes-hardening`
**Spec(s) base**:
- `openspec/changes/2026-08-18-vacantes-hardening/specs/vacante-identity-propagation/spec.md` (D-1)
- `openspec/changes/2026-08-18-vacantes-hardening/specs/vacante-remove-actualizar-observaciones/spec.md` (D-2)
- `openspec/changes/2026-08-18-vacantes-hardening/specs/vacante-input-model-split/spec.md` (D-3)
- `openspec/changes/2026-08-18-vacantes-hardening/specs/vacante-cubrir-concurrency-test/spec.md` (D-4)
- `openspec/changes/2026-08-18-vacantes-hardening/specs/vacante-error-codigo-cleanup/spec.md` (D-5)
- `openspec/specs/vacante-management/spec.md` (delta)
- `openspec/specs/vacante-web/spec.md` (delta)

## Resumen de la arquitectura del cambio

El change es **puramente de hardening** — no introduce funcionalidades nuevas, no toca migraciones ni la firma de los DTOs wire. El trabajo se concentra en cinco frentes independientes: (1) propagar el `UserId` del principal autenticado al `HistorialEstadoVacante` vía la abstracción ya existente `IUsuarioActual` (que vive en `SGV.Aplicacion/Seguridad/` y se registra en `SGV.Api/Program.cs:218-219`), eliminando los dos `usuarioId: null` hardcodeados en `VacanteServicioComandos.cs:351` y `OcupacionServicioComandos.cs:357`; (2) retirar la superficie huérfana `IVacanteServicioComandos.ActualizarObservacionesAsync` y sus tests — confirmado por grep que NO tiene endpoint HTTP ni cliente tipado que la consuma; (3) separar el `VacanteInputModel` actual en `VacanteCreateInputModel` (sin `EstadoVacanteId`) y `VacanteEditInputModel` (con `EstadoVacanteId` `[Required]`) para eliminar el workaround `ModelState.Remove("Input.EstadoVacanteId")` en `Create.cshtml.cs:118`; (4) escribir dos `[MySqlFact]` que ejerciten el TOCTOU de `ExistsActiveByVacanteAsync` y la carrera atómica de doble cobertura en un archivo nuevo `VacantesCubrirConcurrencyTests.cs`; (5) eliminar la constante `VacanteErrorCodigo.MotivoObligatorio` declarada pero nunca referenciada.

La complejidad del cambio está contenida por una decisión arquitectónica vigente: **`IUsuarioActual` ya existe y ya está registrada en DI** (issue #202). El trabajo de D-1 es estrictamente inyectar la abstracción en dos servicios y propagar el `UserId` en los dos call sites donde hoy se pasa `null`. El resto de los cambios son localizados, no rompen wire-types, no tocan migraciones, no cambian JWT claims.

## Cambios por capa

### `SGV.Dominio` (sin cambios)
La entidad `Vacante.CambiarEstado(estadoNuevoId, usuarioId, motivo, cerrar)` ya acepta `usuarioId` por signatura. **Cero cambios** — la decisión D-1 solo cambia quién pasa ese argumento.

### `SGV.Contracts` (D-3, D-5)

| Archivo | Acción | Descripción | LoC Δ |
|---|---|---|---|
| `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs` | Modify | Eliminar línea 29 (`public const string MotivoObligatorio = nameof(MotivoObligatorio);`) y su XML doc precedente (D-5) | −7 |

### `SGV.Aplicacion` (D-1, D-2 — núcleo del cambio)

| Archivo | Acción | Descripción | LoC Δ |
|---|---|---|---|
| `src/SGV.Aplicacion/Vacantes/Comandos/IVacanteServicioComandos.cs` | Modify | Quitar bloque XML doc + signatura `ActualizarObservacionesAsync` (líneas 41-49) (D-2) | −9 |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modify | Añadir `IUsuarioActual _usuarioActual` (campo + constructor principal + convenience ctor). Reemplazar `usuarioId: null` en `CambiarEstadoAsync` por `_usuarioActual.UserId` con guard contra null (D-1). Quitar método `ActualizarObservacionesAsync` entero (líneas 380-450) (D-2) | +18 / −71 |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | Modify | Añadir `IUsuarioActual _usuarioActual` (campo + ctors). Reemplazar `usuarioId: null` en `CrearOcupacionCubriendoVacanteAsync` (línea 357) por `_usuarioActual.UserId` con guard (D-1) | +18 / −0 |

### `SGV.Infraestructura` (sin cambios)
Las migraciones EF y los repositorios no se tocan. `IVacanteRepository.RegistrarCambioEstadoAsync` ya propaga el `HistorialEstadoVacante` con su `ChangedByUserId` poblado.

### `SGV.Api` (sin cambios)
- `src/SGV.Api/Program.cs:218-219` ya tiene `AddHttpContextAccessor()` + `AddScoped<IUsuarioActual, UsuarioActualHttpContext>()`. **No requiere DI adicional**.
- `src/SGV.Api/Controllers/VacantesController.cs` no requiere cambios — la signatura de `CambiarEstadoAsync(Guid, CambiarEstadoVacanteRequest, CancellationToken)` no cambia. El controller ya tiene `[Authorize(Roles = RolesSgv.RolesSgvMutacion)]` en `POST` y `PATCH` (líneas 132, 183) que garantiza un principal autenticado.
- `src/SGV.Api/Seguridad/UsuarioActualHttpContext.cs` ya implementa `IUsuarioActual` resolviendo `ClaimTypes.NameIdentifier` desde `HttpContext.User`. Cero cambios.

### `SGV.Web` (D-3 + triviales)

| Archivo | Acción | Descripción | LoC Δ |
|---|---|---|---|
| `src/SGV.Web/Integration/Vacantes/VacanteCreateInputModel.cs` | Create | Split de `VacanteInputModel`: mismas props **excepto** `EstadoVacanteId` (D-3) | +30 |
| `src/SGV.Web/Integration/Vacantes/VacanteEditInputModel.cs` | Create | Split de `VacanteInputModel`: mismas props **incluyendo** `EstadoVacanteId [Required]` (D-3) | +35 |
| `src/SGV.Web/Integration/Vacantes/VacanteInputModel.cs` | Delete | Reemplazado por los dos modelos anteriores (D-3) | −35 |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` | Modify | Cambiar `[BindProperty] public VacanteInputModel Input` por `VacanteCreateInputModel`. Quitar `ModelState.Remove("Input.EstadoVacanteId")` en `OnPostAsync`. Agregar `Input.FechaApertura = DateTime.Today` en `OnGetAsync` (T-2/T-3) | +2 / −1 |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml` | Modify | Sin cambios funcionales — el tag helper `asp-for` sigue funcionando con el nuevo tipo. Verificado: el form no referencia `Input.EstadoVacanteId` | 0 |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs` | Modify | Cambiar `[BindProperty] public VacanteInputModel Input` por `VacanteEditInputModel`. Agregar guard de redirect en `OnGetAsync` cuando `current.EsCerrada` (T-2) | +5 |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml` | Modify | Sin cambios — el tag helper funciona con el nuevo tipo | 0 |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Index.cshtml.cs:48` | Modify | Reemplazar `"Administrador"`/`"GestorVacantes"` literales por `RolesSgv.Administrador` / `RolesSgv.GestorVacantes` (T-1) | +0 / −0 (swap 1:1) |

### `tests/SGV.Tests` (D-2, D-4 + back-compat)

| Archivo | Acción | Descripción | LoC Δ |
|---|---|---|---|
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modify | Quitar los 4 tests `ActualizarObservacionesAsync_*` (líneas ~819-893) (D-2). Actualizar `CrearServicio` helper para pasar `IUsuarioActual` (D-1 back-compat) | −80 / +10 |
| `tests/SGV.Tests/Aplicacion/Vacantes/FakeUsuarioActual.cs` | Create | Stub `IUsuarioActual` para tests (D-1 back-compat) | +25 |
| `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | Modify | Actualizar helper de construcción para pasar `IUsuarioActual` (D-1 back-compat). Tests que asumen `ChangedByUserId = null` ahora deben esperar el `UserId` del stub | +10 / −0 |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modify | Quitar `ActualizarObservacionesAsync` de `FakeVacanteServicioComandos` (líneas 1341-1358) (D-2) | −18 |
| `tests/SGV.Tests/Api/Vacantes/VacantesCubrirConcurrencyTests.cs` | Create | Dos `[MySqlFact]` que ejercitan TOCTOU + carrera atómica de doble cobertura (D-4) | +180 |

**Total LoC estimado**: ~150 modificadas / creadas, ~210 eliminadas. **Neto**: −60 LoC. Bajo el review budget de 400 LoC.

## D-1: Propagación de identidad de usuario — diseño detallado

### Decisión arquitectónica confirmada

El codebase ya tiene la abstracción requerida en `src/SGV.Aplicacion/Seguridad/IUsuarioActual.cs`:

```csharp
namespace SGV.Aplicacion.Seguridad;

public interface IUsuarioActual
{
    string? UserId { get; }
    Guid? PersonaId { get; }
    IReadOnlyCollection<string> Roles { get; }
    Guid? CorrelationId { get; }
}
```

Y la implementación en `src/SGV.Api/Seguridad/UsuarioActualHttpContext.cs` (líneas 10-33) ya resuelve `UserId` desde `HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)`. La composición root ya está registrada:

```csharp
// src/SGV.Api/Program.cs:218-219
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttpContext>();
```

**No se crea abstracción nueva**. El diseño sólo agrega el parámetro `IUsuarioActual` a los dos constructores y propaga `_usuarioActual.UserId` en los dos call sites.

### Lifetime (registro ya vigente)

`IUsuarioActual` está registrado como **Scoped** (línea 219). Esto es correcto y consistente con el resto de la aplicación (todos los `IServicioXxx` son Scoped). El `IHttpContextAccessor` que inyecta `UsuarioActualHttpContext` también es Scoped por defecto (`AddHttpContextAccessor()`). El ciclo de vida es coherente: la request HTTP crea el scope, el servicio de comandos vive dentro del scope, la abstracción vive dentro del scope, todos ven el mismo `HttpContext.User`.

### Constructor de `VacanteServicioComandos`

```csharp
public VacanteServicioComandos(
    IVacanteRepository vacanteRepository,
    IEstadoVacanteRepository estadoVacanteRepository,
    IUnitOfWork unitOfWork,
    IConstraintViolationDetector constraintDetector,
    ILogger<VacanteServicioComandos> logger,
    IValidator<CrearVacanteRequest> crearValidator,
    IValidator<CambiarEstadoVacanteRequest> cambiarEstadoValidator,
    IOcupacionRepository ocupacionRepository,
    IUsuarioActual usuarioActual)            // ← NUEVO (último parámetro)
{
    ArgumentNullException.ThrowIfNull(usuarioActual);
    // ... resto de null checks ...
    this.usuarioActual = usuarioActual;
}
```

Se agrega un **convenience constructor** que delega al principal usando `NullUsuarioActual.Instance` para mantener back-compat con los call sites que hoy construyen el servicio sin usuario (tests que no llaman `CambiarEstadoAsync`, factories de tests históricos). Esto sigue el patrón ya existente en el codebase (`NullUsuarioActual` ya existe en `src/SGV.Aplicacion/Seguridad/NullUsuarioActual.cs:10`).

### DI registration (sin cambios)

`src/SGV.Infraestructura/DependencyInjection.cs:95` ya tiene:

```csharp
services.AddScoped<IVacanteServicioComandos, VacanteServicioComandos>();
```

ASP.NET Core resuelve automáticamente el constructor con más parámetros. Como el principal constructor ahora tiene 9 parámetros y el convenience ctor 8, **el contenedor elegirá el principal** automáticamente (regla "longest match"). No se requiere cambio de DI.

Para `OcupacionServicioComandos`: misma lógica — agregar `IUsuarioActual` al principal constructor; el contenedor elige el de 11 parámetros.

### Call site actualizado en `VacanteServicioComandos.CambiarEstadoAsync`

**Antes** (línea 349-353):

```csharp
var historial = vacante.CambiarEstado(
    estadoNuevoId: request.EstadoVacanteId,
    usuarioId: null,                  // ← bug
    motivo: request.Motivo,
    cerrar: estadoNuevo.EsTerminal);
```

**Después**:

```csharp
var usuarioId = usuarioActual.UserId;
if (string.IsNullOrWhiteSpace(usuarioId))
{
    return VacanteCommandResult.Failure(
        new VacanteError(
            ErrorCategoria.Unauthorized,
            VacanteErrorCodigo.DatosInvalidos,  // código funcional neutro; el controller mapea por Categoría
            "No se pudo resolver el usuario autenticado para registrar el cambio de estado."));
}

var historial = vacante.CambiarEstado(
    estadoNuevoId: request.EstadoVacanteId,
    usuarioId: usuarioId,
    motivo: request.Motivo,
    cerrar: estadoNuevo.EsTerminal);
```

**Misma lógica** en `OcupacionServicioComandos.CrearOcupacionCubriendoVacanteAsync` (línea 354-359 del código actual).

### Comportamiento defensivo ante principal anónimo

Por la composición del pipeline (`SGV.Api/Program.cs:208-211`):

```csharp
builder.Services.AddAuthorization(opts =>
    opts.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
```

…todo request que llega al controller ya pasó por `[Authorize]` (sin y con `Roles`). En la práctica `User.Identity?.IsAuthenticated == true` siempre es `true` cuando el handler se ejecuta. Sin embargo, **defense-in-depth**: si por algún motivo el `IUsuarioActual` resuelve `null` (caso anómalo de `HttpContext` nulo en un host de prueba o un futuro background job que olvidó poblar el contexto), el servicio **falla el guardado de estado con `ErrorCategoria.Unauthorized`** en lugar de persistir `ChangedByUserId = null` silenciosamente.

El controller (`VacantesController.CambiarEstado` línea 190-203) ya mapea `ErrorCategoria.Unauthorized` a `401 Unauthorized` vía `ApiResults.ToProblemResult(...)` por convención del módulo de errores. Verificado por la rama existente:

```csharp
// src/SGV.Api/Infrastructure/Results/ApiResults.cs (mapeo vigente)
Unauthorized → Status401Unauthorized with ProblemDetails
```

### Test stubs

Para los tests de aplicación, se crea un helper único:

```csharp
// tests/SGV.Tests/Aplicacion/Vacantes/FakeUsuarioActual.cs
internal sealed class FakeUsuarioActual : IUsuarioActual
{
    public string? UserId { get; set; } = "test-user-id";
    public Guid? PersonaId { get; set; } = Guid.Parse("...");
    public IReadOnlyCollection<string> Roles { get; set; } = ["Administrador"];
    public Guid? CorrelationId { get; set; } = Guid.NewGuid();

    public static IUsuarioActual Anonymous { get; } = new FakeUsuarioActual { UserId = null };
}
```

El `CrearServicio` helper de `VacanteServicioComandosTests.cs:898` se actualiza:

```csharp
private static VacanteServicioComandos CrearServicio(
    IVacanteRepository vacanteRepo,
    IEstadoVacanteRepository estadoRepo,
    IUnitOfWork uow,
    IOcupacionRepository? ocupacionRepo = null,
    IUsuarioActual? usuarioActual = null)
{
    return new VacanteServicioComandos(
        vacanteRepo, estadoRepo, uow,
        new FakeConstraintViolationDetector(),
        new FakeLogger<VacanteServicioComandos>(),
        ocupacionRepo ?? new FakeOcupacionLookupRepository(),
        usuarioActual ?? new FakeUsuarioActual());   // ← default = FakeUsuarioActual
}
```

Tests pre-existentes que asumían `ChangedByUserId = null` (los que haya) ahora deben leer `resultado.Value.Historial.Single().ChangedByUserId` y esperar `"test-user-id"`. **No se conoce todavía el conteo exacto** — el task phase lo determinará con `grep "ChangedByUserId" tests/`.

### Resumen D-1

| Aspecto | Decisión |
|---|---|
| Abstracción | `SGV.Aplicacion.Seguridad.IUsuarioActual` (ya existente) |
| Implementación | `SGV.Api.Seguridad.UsuarioActualHttpContext` (ya registrada) |
| Lifetime | Scoped (sin cambios) |
| Back-compat en tests | Convenience ctor + `FakeUsuarioActual` con `UserId = "test-user-id"` por default |
| Defensa ante anónimo | Early `Failure` con `ErrorCategoria.Unauthorized` antes de invocar `vacante.CambiarEstado` |

## D-2: Eliminación de `ActualizarObservacionesAsync` — diseño detallado

### Auditoría de referencias (grep confirma cero consumidores externos)

Auditoría completa con `grep -rn "ActualizarObservacionesAsync" src/ tests/`:

| Archivo | Línea | Naturaleza |
|---|---|---|
| `src/SGV.Aplicacion/Vacantes/Comandos/IVacanteServicioComandos.cs` | 46-49 | Definición de interfaz — **eliminable** |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | 380-450 | Implementación (71 LoC, incluye `catch DbUpdateException`, helper `MapToDetailDto`) — **eliminable** |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | 819, 829, 849, 866, 888 | 4 tests (`ActualizarObservacionesAsync_*`) — **eliminables** |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | 1341-1358 | `FakeVacanteServicioComandos.ActualizarObservacionesAsync` (mock que ya no compila si se quita la interfaz) — **eliminable** |

**NO existe** referencia en:
- `src/SGV.Web/Integration/Vacantes/` — `IVacanteApiClient` y `VacanteApiClient` no exponen el método
- `src/SGV.Api/Controllers/` — no hay endpoint HTTP
- `src/SGV.Contracts/` — no hay DTO/Request/Response

→ **Cero impacto en wire-types**. La operación "actualizar observaciones" sigue funcionando como side-effect de `CambiarEstadoAsync` vía `CambiarEstadoVacanteRequest.Observaciones` (líneas 355-358 de `VacanteServicioComandos.cs`).

### Orden de eliminación (de adentro hacia afuera)

1. **Tests primero** (red-flag para el compilador): borrar los 4 `ActualizarObservacionesAsync_*` tests + sección `// ── ActualizarObservacionesAsync ───` (líneas 819-894) → `dotnet build` fallará en `VacanteServicioComandos.cs:391`.
2. **Implementación**: borrar el método entero (líneas 380-450) → `dotnet build` fallará en `IVacanteServicioComandos.cs:46`.
3. **Interfaz**: borrar bloque XML doc + signatura (líneas 41-49) → `dotnet build` fallará en `ApiWebApplicationFactory.cs:1341`.
4. **Mock**: borrar `FakeVacanteServicioComandos.ActualizarObservacionesAsync` (líneas 1341-1358) → `dotnet build` verde.

Este orden maximiza la "firma de regresión" del compilador en cada paso — cualquier referencia que se pase por alto explota en el paso siguiente. El spec `vacante-remove-actualizar-observaciones` se valida en el mismo orden.

### Tests actualizados

El `CrearServicio` helper ya no necesita el cambio de D-1 (es el mismo helper). Los 4 tests eliminados:

- `ActualizarObservacionesAsync_ActualizaTextoYGuarda`
- `ActualizarObservacionesAsync_LimpiaNull`
- `ActualizarObservacionesAsync_RechazaMasDe500Caracteres`
- `ActualizarObservacionesAsync_VacanteInexistente_RetornaNotFound`

Cobertura equivalente ya existe en el test `CambiarEstadoAsync_ActualizaObservacionesComoSideEffect` (verificar existencia en la fase de tasks). Si no existe, **NO se recrea** — el spec `vacante-remove-actualizar-observaciones` explícitamente dice: "El comportamiento existente — `CambiarEstadoVacanteRequest.Observaciones` actualiza las observaciones de la vacante como side-effect de la transición — DEBE preservarse intacto" sin pedir cobertura nueva.

## D-3: Split de `VacanteInputModel` — diseño detallado

### Decisión de ubicación

**El spec propone** `src/SGV.Contracts/Vacantes/Modelos/VacanteCreateInputModel.cs` (modelos nuevos en Contracts).

**El codebase actual** aloja `VacanteInputModel` en `src/SGV.Web/Integration/Vacantes/VacanteInputModel.cs` (namespace `SGV.Web.Integration.Vacantes`), NO en Contracts.

**Decisión**: seguir la **convención vigente** — los input models Razor-bound con `[Required]`/`[StringLength]` viven en `SGV.Web/Integration/Vacantes/`. La razón es que estos tipos dependen exclusivamente de `System.ComponentModel.DataAnnotations` (que es Web/Razor), no se comparten con otros clientes HTTP, y Tests los consume vía `SGV.Web.Pages.Organizacion.Vacantes.*` (PageModel), no desde otros módulos. Moverlos a Contracts sería over-engineering sin un consumidor que lo justifique. **Path final**: `src/SGV.Web/Integration/Vacantes/VacanteCreateInputModel.cs` y `VacanteEditInputModel.cs`. El viejo `VacanteInputModel.cs` se elimina.

### `VacanteCreateInputModel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Bound input for the Create vacante form. Does NOT contain
/// <c>EstadoVacanteId</c> — toda vacante nueva arranca en estado
/// "Abierta" resuelto por la capa de Aplicación
/// (<c>VacanteServicioComandos.CrearAsync</c>).
/// </summary>
public sealed class VacanteCreateInputModel
{
    [Required(ErrorMessage = "Debe escoger un puesto.")]
    [Display(Name = "Puesto")]
    public Guid? PuestoId { get; set; }

    [Required(ErrorMessage = "La fecha de apertura es obligatoria.")]
    [Display(Name = "Fecha de apertura")]
    [DataType(DataType.Date)]
    public DateTime? FechaApertura { get; set; }

    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Motivo { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
```

### `VacanteEditInputModel.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SGV.Web.Integration.Vacantes;

/// <summary>
/// Bound input for the Edit vacante form. <c>EstadoVacanteId</c> is
/// required because Edit allows state transitions; Create does not.
/// </summary>
public sealed class VacanteEditInputModel
{
    [Required(ErrorMessage = "Debe escoger un puesto.")]
    [Display(Name = "Puesto")]
    public Guid? PuestoId { get; set; }

    [Required(ErrorMessage = "Debe escoger un estado.")]
    [Display(Name = "Estado")]
    public Guid? EstadoVacanteId { get; set; }

    [Required(ErrorMessage = "La fecha de apertura es obligatoria.")]
    [Display(Name = "Fecha de apertura")]
    [DataType(DataType.Date)]
    public DateTime? FechaApertura { get; set; }

    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Motivo { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
}
```

### Cambios en `Create.cshtml.cs`

```csharp
// Línea 35-36 — ANTES
[BindProperty]
public VacanteInputModel Input { get; set; } = new();

// DESPUÉS
[BindProperty]
public VacanteCreateInputModel Input { get; set; } = new();
```

```csharp
// Línea 118 — ELIMINAR
ModelState.Remove("Input.EstadoVacanteId");
```

```csharp
// Línea 75-96 OnGetAsync — AGREGAR después de los checks de CanMutate y PuestoPreseleccionado
Input.FechaApertura = DateTime.Today;
```

### Cambios en `Edit.cshtml.cs`

```csharp
// Línea 24-25 — ANTES
[BindProperty]
public VacanteInputModel Input { get; set; } = new();

// DESPUÉS
[BindProperty]
public VacanteEditInputModel Input { get; set; } = new();
```

```csharp
// Línea 51-67 OnGetAsync — AGREGAR después de `var current = await LoadCurrentAsync(...)`
if (current is null) return Page();   // ya existe
// AGREGAR:
// Guard: redirect to Details when the vacante is terminal (Cubierta/Cancelada).
// El backend rechazaría CambiarEstadoAsync con 409 EstadoTerminalInmutable; lo evitamos acá.
var viewModel = VacanteDetailViewModel.FromDto(current);
if (viewModel.EsCerrada)
{
    return RedirectToPage("/Organizacion/Vacantes/Details", new { id });
}
PopulateInput(current);
```

### Cambios en `Create.cshtml` / `Edit.cshtml`

**Sin cambios**. Los tag helpers `asp-for="Input.PuestoId"`, `asp-for="Input.FechaApertura"`, etc. siguen funcionando con el nuevo tipo porque los nombres de las propiedades se preservan. El form HTML es estable. El tag `<select asp-for="Input.EstadoVacanteId">` en `Edit.cshtml:49-50` sigue funcionando con `VacanteEditInputModel.EstadoVacanteId`. El form de Create nunca envió `EstadoVacanteId` (issue #273 Slice A), así que `Create.cshtml` no tiene referencia al campo — el split no lo afecta.

### Cobertura por reflexión (defense-in-depth)

El spec `vacante-input-model-split/spec.md` requiere que se pueda inspeccionar el tipo por reflexión. Tests nuevos a agregar en `VacantesCreateInputModelTests` (suite seam existente, archivo nuevo o extensión):

```csharp
[Fact]
public void VacanteCreateInputModel_NoExponeEstadoVacanteId()
{
    var prop = typeof(VacanteCreateInputModel).GetProperty("EstadoVacanteId");
    Assert.Null(prop);
}

[Fact]
public void VacanteEditInputModel_EstadoVacanteId_EsRequerido()
{
    var prop = typeof(VacanteEditInputModel).GetProperty("EstadoVacanteId");
    Assert.NotNull(prop);
    Assert.Equal(typeof(Guid?), prop!.PropertyType);
    Assert.NotNull(prop.GetCustomAttribute<RequiredAttribute>());
}
```

(Tests de **defensa** contra drift futuro — el split podría revertirse accidentalmente. **Bajos en valor si la cobertura es estable**, pero el spec los pide explícitamente.)

## D-4: Tests de concurrencia Cubrir — diseño detallado

### Decisión: archivo nuevo vs. extender el existente

**Decisión**: **archivo nuevo** `tests/SGV.Tests/Api/Vacantes/VacantesCubrirConcurrencyTests.cs`.

**Razones**:

1. **Cohesión distinta**: `VacantesConcurrenciaTests.cs` testea la constraint `IX_Vacantes_ActivePuestoIdUnique` (race de **Crear Vacante**). El nuevo testea la constraint `IX_Ocupaciones_VacanteIdUnique` y el TOCTOU de `ExistsActiveByVacanteAsync` (race de **Cubrir Vacante** vía Ocupacion). Son constraints y paths de defensa diferentes.
2. **Naming**: el sufijo `ConcurrenciaTests` es ambiguo entre "constraint de Crear" vs "constraint de Cubrir". Un archivo dedicado con sufijo específico elimina esa ambigüedad.
3. **Mismo namespace / mismo folder**: ambos viven en `SGV.Tests.Api.Vacantes`. Co-localizados pero separados.
4. **Patrón espejo**: el nuevo archivo replica `VacantesConcurrenciaTests` (cleanup helper, `UniqueSuffix`, `MySqlFact`). Mantener separados reduce el riesgo de merges accidentales entre los dos dominios de constraint.

### Setup pattern (espejo de `VacantesConcurrenciaTests`)

```csharp
public sealed class VacantesCubrirConcurrencyTests
{
    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// D-4 escenario 1 (TOCTOU): dos POST /api/v1/ocupaciones en paralelo
    /// contra el mismo VacanteId. Una gana, la otra pierde con
    /// OcupacionErrorCodigo.VacanteYaCubierta.
    /// </summary>
    [MySqlFact]
    public async Task CubrirVacante_Concurrencia_TOCTOU_SoloUnaCoberturaExitosa() { /* ... */ }

    /// <summary>
    /// D-4 escenario 2 (atomicidad): la segunda cobertura concurrente
    /// encuentra la vacante ya Cubierta y es rechazada con
    /// EstadoTerminalInmutable.
    /// </summary>
    [MySqlFact]
    public async Task CubrirVacante_Concurrencia_DobleCobertura_ConstraintUnica() { /* ... */ }

    private static async Task LimpiarOcupacionesAsync(
        SgvDbContext context,
        PuestoEntity puesto,
        CargoEntity cargo,
        UnidadOrganizativaEntity unidad,
        params object[] extras) { /* mirror de LimpiarVacantesAsync */ }
}
```

### Escenario 1 — TOCTOU (carrera lógica, defensa en memoria)

**Setup**: una vacante `Abierta` (sin Ocupacion vinculada) + dos personas activas + el mismo `VacanteId` en dos requests HTTP en paralelo.

**Flow**: ambos requests pasan `ExistsActiveByVacanteAsync(V)` (devuelve `false` para ambos porque la BD está en `t₀` antes de cualquier commit) → ambos construyen su `Ocupacion` → ambos llaman `SaveChangesAsync`. La primera commit gana; la segunda, al llegar a `ExistsActiveByVacanteAsync` en su propio context scoped EF, **debería** ver la fila. **PERO** en EF Core con `AsNoTracking()` esto depende del orden de operaciones: la carrera real de defensa atómica es la constraint única `IX_Ocupaciones_VacanteIdUnique`.

**Resultado esperado por el spec**:
- Una request → `2xx` con `Ocupacion` creada
- Otra request → `409 Conflict` con `OcupacionErrorCodigo.VacanteYaCubierta`

**Nota**: el spec `vacante-cubrir-concurrency-test/spec.md` escenario "TOCTOU" dice "una cobertura, una rechazada". En la práctica, dado que EF Core usa una transacción con isolation level `REPEATABLE READ` por defecto en MySQL, **el segundo commit puede bloquear hasta que el primero commitee**, momento en el cual el `ExistsActiveByVacanteAsync` re-evaluado lo detecta y devuelve `409 VacanteYaCubierta` por el camino lógico (líneas 278-285 de `OcupacionServicioComandos.cs`). Si la implementación confía en el check lógico + constraint única como red de seguridad, **ambos paths defienden correctamente** y el test debe pasar.

### Escenario 2 — Atomicidad (constraint única)

**Setup**: una vacante `Abierta`, dos personas activas. Los dos requests invocan `OcupacionServicioComandos.CrearAsync(request with VacanteId = V)`.

**Flow**: ambos pasan el check lógico `ExistsActiveByVacanteAsync` en paralelo (devuelve `false` para los dos porque la transacción todavía no committeó). Ambos llaman `SaveChangesAsync` → ambos `Ocupacion` rows intentan el INSERT con `VacanteId = V`. La constraint única `IX_Ocupaciones_VacanteIdUnique` rechaza el segundo INSERT con `ER_DUP_ENTRY` (1062).

**Mapeo de la constraint**: el catch `DbUpdateException ex when constraintDetector.IsConstraintViolation(ex)` en `OcupacionServicioComandos.cs:370-376` mapea a `ErrorCategoria.Conflict + "DatosInvalidos"`. Esto **NO coincide** con lo que pide el spec (`OcupacionErrorCodigo.VacanteYaCubierta`).

**⚠️ Hallazgo de diseño**: el spec escenario 2 dice que la segunda debe fallar con `EstadoTerminalInmutable`, pero el código actual mapea cualquier `DbUpdateException` a `DatosInvalidos`. Para que el test pase como pide el spec, **se requiere refactor menor del catch** en `OcupacionServicioComandos.CrearOcupacionCubriendoVacanteAsync` para distinguir la constraint `IX_Ocupaciones_VacanteIdUnique` y mapearla a `OcupacionErrorCodigo.VacanteYaCubierta`. Esto NO estaba en el proposal original.

**Recomendación**:
- **Opción A (mínima)**: actualizar el test para verificar `Conflict` con cualquier código (más laxo que el spec).
- **Opción B (alineada al spec)**: agregar al `IConstraintViolationDetector` la capacidad de discriminar por nombre de constraint, similar a como `VacanteServicioComandos.CrearAsync:237-244` ya distingue `ActivePuestoIdUnique`. Esto requiere:
  1. Modificar el catch en `CrearOcupacionCubriendoVacanteAsync` para detectar la constraint específica.
  2. Agregar un test `[Fact]` puro al detector.
  3. Sumar ~6-8 LoC al cambio.

→ **Recomiendo Opción B** — es consistente con el patrón ya existente en `VacanteServicioComandos`, no introduce nueva arquitectura, y alinea el test con el comportamiento esperado por spec. El cambio se documenta como **delta al design** (no en el proposal original) en el `apply-progress.md`.

### Limpieza (espejo del helper existente)

```csharp
private static async Task LimpiarOcupacionesAsync(
    SgvDbContext context,
    PuestoEntity puesto,
    CargoEntity cargo,
    UnidadOrganizativaEntity unidad,
    params object[] extras)
{
    context.ChangeTracker.Clear();
    // Idéntico patrón de topological DELETE que LimpiarVacantesAsync.
    // Tablas: Ocupaciones → HistorialEstadosVacante → Vacantes →
    //         PersonasOcupaciones (si existe) → Puestos → Cargos → UnidadesOrganizativas.
}
```

(Implementación completa en fase de tasks — sigue la plantilla de `VacantesConcurrenciaTests.LimpiarVacantesAsync:315-369` ajustando los nombres de tabla.)

## D-5: Eliminación de `MotivoObligatorio` — diseño

**Cambio de una línea + XML doc**. En `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs`:

```csharp
// ANTES (línea 29)
public const string MotivoObligatorio = nameof(MotivoObligatorio);

// DESPUÉS (eliminar)
```

No hay XML doc precedente (la línea está desnuda entre `EstadoTerminalInmutable` y `ObservacionesMuyLargas`). Solo se elimina la línea 29. LoC Δ: −1.

Verificado por grep: cero referencias en `src/` ni `tests/` (la única ocurrencia es la declaración misma). Cero impacto downstream.

## Triviales aislados

### T-1 — Reemplazar literales por constantes en `Index.cshtml.cs:48`

```csharp
// ANTES (línea 48)
public bool CanMutate => User.IsInRole("Administrador") || User.IsInRole("GestorVacantes");

// DESPUÉS
using SGV.Contracts.Seguridad;
// ...
public bool CanMutate => User.IsInRole(RolesSgv.Administrador) || User.IsInRole(RolesSgv.GestorVacantes);
```

`using SGV.Contracts.Seguridad;` ya está importado en `Create.cshtml.cs:5` y `Edit.cshtml.cs:5`; verificar que `Index.cshtml.cs` lo agregue. LoC Δ: 0 (1:1 swap).

### T-2 — Guard de Edit sobre vacante terminal

```csharp
// src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs OnGetAsync (líneas 51-67)
// ANTES
var current = await LoadCurrentAsync(id, cancellationToken);
if (current is null) return Page();
PopulateInput(current);
await LoadStatesAsync(cancellationToken);
return Page();

// DESPUÉS
var current = await LoadCurrentAsync(id, cancellationToken);
if (current is null) return Page();

// Guard contra vacante terminal: redirigir a Details sin poblar el form.
// VacanteDetailViewModel.EsCerrada ya está vigente en VacanteDetailDto.
var viewModel = VacanteDetailViewModel.FromDto(current);
if (viewModel.EsCerrada)
{
    return RedirectToPage("/Organizacion/Vacantes/Details", new { id });
}

PopulateInput(current);
await LoadStatesAsync(cancellationToken);
return Page();
```

`VacanteDetailViewModel.FromDto(current)` ya existe y ya setea `EsCerrada` desde `VacanteDetailDto.EstadoVacanteId` → catálogo de estados. No requiere cambios al view-model.

### T-3 — Pre-popular `FechaApertura` con hoy

```csharp
// src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs OnGetAsync (después de línea 90)
// ANTES (líneas 85-96 — solo el final)
ReturnUrl = NormalizeReturn(returnUrl);
await LoadPuestosAsync(cancellationToken);
return Page();

// DESPUÉS
ReturnUrl = NormalizeReturn(returnUrl);

// T-3: pre-popular FechaApertura con la fecha del día (issue: UX friction
// en Create). El usuario puede sobrescribir el valor antes de enviar.
Input.FechaApertura = DateTime.Today;

await LoadPuestosAsync(cancellationToken);
return Page();
```

`DateTime.Today` retorna `DateTime` (no `DateTime?`); el campo `Input.FechaApertura` es `DateTime?` por la validación `[Required]`. La asignación es directa: el `DateTime` se boxing-implícitamente a `DateTime?` (es la misma conversión que usa el resto del módulo).

## Plan de ejecución por tarea

Clusterizado por dependencias y budget de 2 h. Cada task referencia su entrada en `openspec/changes/2026-08-18-vacantes-hardening/tasks.md` (a crear en fase de tasks).

### Cluster A — Identidad + Tests back-compat (D-1)

| # | Task | Esfuerzo | Pre-requisitos |
|---|---|---|---|
| A.1 | Inyectar `IUsuarioActual` en `VacanteServicioComandos` (ambos ctors + campo) | 0.5 h | — |
| A.2 | Reemplazar `usuarioId: null` en `VacanteServicioComandos.CambiarEstadoAsync` + guard `ErrorCategoria.Unauthorized` | 0.5 h | A.1 |
| A.3 | Inyectar `IUsuarioActual` en `OcupacionServicioComandos` + reemplazar `usuarioId: null` en `CrearOcupacionCubriendoVacanteAsync` + guard | 0.5 h | A.1 |
| A.4 | Crear `FakeUsuarioActual` (test stub) + actualizar `CrearServicio` helper + actualizar tests que asumen `ChangedByUserId = null` | 1.0 h | A.1 |

### Cluster B — Eliminación de `ActualizarObservacionesAsync` (D-2)

| # | Task | Esfuerzo | Pre-requisitos |
|---|---|---|---|
| B.1 | Borrar tests `ActualizarObservacionesAsync_*` en `VacanteServicioComandosTests.cs` (4 tests, líneas 819-893) | 0.25 h | — |
| B.2 | Borrar método `ActualizarObservacionesAsync` en `VacanteServicioComandos.cs` (líneas 380-450) | 0.25 h | B.1 |
| B.3 | Borrar signatura en `IVacanteServicioComandos.cs` (líneas 41-49) | 0.05 h | B.2 |
| B.4 | Borrar override en `FakeVacanteServicioComandos.ActualizarObservacionesAsync` (ApiWebApplicationFactory.cs:1341-1358) | 0.05 h | B.3 |

### Cluster C — Split de Input Models + triviales Web (D-3, T-1, T-2, T-3)

| # | Task | Esfuerzo | Pre-requisitos |
|---|---|---|---|
| C.1 | Crear `VacanteCreateInputModel.cs` y `VacanteEditInputModel.cs`; borrar `VacanteInputModel.cs` | 0.5 h | — |
| C.2 | Actualizar `Create.cshtml.cs` (cambiar tipo, quitar `ModelState.Remove`, agregar pre-poblado `FechaApertura`) | 0.25 h | C.1 |
| C.3 | Actualizar `Edit.cshtml.cs` (cambiar tipo, agregar guard `EsCerrada`) | 0.25 h | C.1 |
| C.4 | Actualizar `Index.cshtml.cs` (literales → `RolesSgv.*`) | 0.05 h | — |
| C.5 | Tests de defensa por reflexión (`VacanteCreateInputModel_NoExponeEstadoVacanteId`, `VacanteEditInputModel_EstadoVacanteId_EsRequerido`) | 0.5 h | C.1 |

### Cluster D — Tests de concurrencia Cubrir (D-4)

| # | Task | Esfuerzo | Pre-requisitos |
|---|---|---|---|
| D.1 | Refactorizar `OcupacionServicioComandos.CrearOcupacionCubriendoVacanteAsync` catch para mapear constraint `IX_Ocupaciones_VacanteIdUnique` → `OcupacionErrorCodigo.VacanteYaCubierta` | 0.5 h | A.3 |
| D.2 | Crear `VacantesCubrirConcurrencyTests.cs` con escenario 1 (TOCTOU) + escenario 2 (atomicidad) + `LimpiarOcupacionesAsync` | 1.5 h | D.1 |

### Cluster E — Dead code (D-5)

| # | Task | Esfuerzo | Pre-requisitos |
|---|---|---|---|
| E.1 | Eliminar `MotivoObligatorio` de `VacanteErrorCodigo.cs:29` | 0.05 h | — |

### Cluster F — Validación final

| # | Task | Esfuerzo | Pre-requisitos |
|---|---|---|---|
| F.1 | `dotnet build SGV.slnx` sin warnings nuevos | 0.25 h | A.x, B.x, C.x, D.x, E.x |
| F.2 | `dotnet test SGV.slnx` 100% verde; suite focal verde contra MySQL si disponible | 1.0 h | F.1 |

**Total estimado**: ~7.5 h de trabajo concentrado, repartido en los 6 clusters. Por debajo del review budget de 400 LoC.

### Orden de ejecución recomendado

1. **Cluster A** primero (D-1 toca la signatura de los servicios; B y D dependen de la inyección).
2. **Cluster B** segundo (D-2 limpia el servicio antes de D-4 para que el código esté más legible cuando escribimos los tests de carrera).
3. **Cluster E** en cualquier momento (es trivial; puede ir al final del cluster C).
4. **Cluster C** tercero (los triviales son independientes pero conviene agruparlos).
5. **Cluster D** cuarto (necesita A.3 para tener `IUsuarioActual` en `OcupacionServicioComandos`).
6. **Cluster F** al final.

## Riesgos de implementación

1. **R-1 — Tests pre-existentes asumen `ChangedByUserId = null`** (probabilidad: media). Los tests de `VacanteServicioComandosTests` y `OcupacionServicioComandosTests` que verifican `Historial[0].ChangedByUserId` después de `CambiarEstadoAsync` hoy asumen `null`. Tras D-1 el valor será el `UserId` del `FakeUsuarioActual` (`"test-user-id"`). Si la fase de tasks no actualiza cada uno, el build pasa pero los tests fallan. Mitigación: en A.4, hacer `grep "ChangedByUserId" tests/SGV.Tests/Aplicacion/Vacantes/ tests/SGV.Tests/Aplicacion/Ocupaciones/ -rn` y actualizar sistemáticamente.

2. **R-2 — Refactor del catch en `CrearOcupacionCubriendoVacanteAsync` (D-4) introduce cambio no documentado en proposal** (probabilidad: alta; impacto: bajo). El spec escenario 2 pide rechazo con `OcupacionErrorCodigo.VacanteYaCubierta`, pero el catch actual mapea `DbUpdateException` a `DatosInvalidos`. El refactor necesario en D.4.D.1 (discriminar constraint `IX_Ocupaciones_VacanteIdUnique`) NO está en el proposal. Mitigación: documentar el delta en `apply-progress.md` como hallazgo espontáneo; pedir confirmación al usuario en fase de tasks si hay duda sobre el código de error exacto.

3. **R-3 — `EsCerrada` flag podría no estar expuesto en el catálogo de estados** (probabilidad: baja). El guard de Edit T-2 depende de `VacanteDetailViewModel.EsCerrada`. El codebase YA tiene ese flag (exploration:23 confirma `EsCerrada property disponible`). Pero el guard requiere que `VacanteDetailDto.EstadoVacanteId` se mapee contra el catálogo de estados — si el detalle DTO sólo trae el ID sin el nombre ni `EsTerminal`, el view-model cae en `false` por default y el guard nunca dispara. Mitigación: verificar en fase de tasks que `VacanteDetailViewModel.FromDto` ya consulta el catálogo; si no lo hace, agregar la consulta.

4. **R-4 — `CambiarEstadoVacanteRequest` ya está wire-stable, pero `VacanteServicioComandos.CrearAsync` recibe `CrearVacanteRequest` con `EstadoVacanteId: null` desde el Create HTML** (probabilidad: baja). El test `[MySqlFact]` escenario 2 de D-4 ejercita dos coberturas concurrentes; el path `CrearVacanteRequest.EstadoVacanteId = null` ya está manejado por la rama de `VacanteServicioComandos.CrearAsync:147-160` que resuelve "Abierta" del catálogo. No requiere cambio, pero la fase de tasks debe confirmar que los tests existentes que asumen estado explícito siguen funcionando.

5. **R-5 — `ApiWebApplicationFactory.FakeVacanteServicioComandos` se usa en otros tests no-Vacantes** (probabilidad: muy baja). El grep muestra que el `Fake` está referenciado únicamente en `VacantesControllerTests.cs` (10 ocurrencias) y en sí mismo. Si algún test de integración de otro módulo lo consume vía `services.RemoveService<IVacanteServicioComandos>()` + `AddSingleton<>(new FakeVacanteServicioComandos())`, la eliminación de `ActualizarObservacionesAsync` rompe su compilación. Mitigación: en B.4, hacer `grep -rn "FakeVacanteServicioComandos" tests/` y validar que el conjunto de consumidores sea únicamente `VacantesControllerTests` y el seam `ApiWebApplicationFactory`.

## Compatibilidad

- **Sin migraciones de BD**. `HistorialEstadoVacante.ChangedByUserId` ya existe como `string?` (nullable). Cero DDL.
- **Sin wire-type break**. `CambiarEstadoVacanteRequest`, `CrearVacanteRequest`, `VacanteDetailDto`, `VacanteDto`, `HistorialEstadoVacanteDto` no cambian. El campo `ChangedByUserId` ya está en el wire (siempre estuvo, sólo que valía `null`).
- **Sin cambios en JWT claims**. El claim `NameIdentifier` ya se emite; `UsuarioActualHttpContext` ya lo lee.
- **Sin cambios en `AuditoriaSaveChangesInterceptor`**. La tabla `Auditorias` sigue funcionando intacta; este cambio sólo mejora la calidad del campo `ChangedByUserId` en `HistorialEstadoVacante`, no toca `Auditorias`.
- **Sin breaking change en tests de otros módulos**. La inyección de `IUsuarioActual` es additive en los dos servicios; el resto del grafo de dependencias no se toca.
- **Forward-compatible con `OpenSpec/specs/vacante-management/spec.md`**: el delta de spec escenario "Trazabilidad de usuario en HistorialEstadoVacante" (líneas 169-192) ya está escrito y exige lo que D-1 entrega.

## Métricas de éxito (heredadas del proposal)

- [ ] `dotnet build SGV.slnx` sin errores ni warnings nuevos.
- [ ] `dotnet test SGV.slnx` pasa 100%.
- [ ] `grep -r "MotivoObligatorio" src/ tests/` retorna 0 resultados.
- [ ] `grep -r "ActualizarObservacionesAsync" src/ tests/` retorna 0 resultados.
- [ ] `grep -r "IsInRole(\"Administrador\")" src/SGV.Web` retorna 0 resultados (sólo `RolesSgv.*`).
- [ ] `grep -r "ModelState.Remove" src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` retorna 0 resultados.
- [ ] `grep -r "usuarioId: null" src/SGV.Aplicacion/Vacantes src/SGV.Aplicacion/Ocupaciones` retorna 0 resultados en `CambiarEstado(...)`.
- [ ] Los 2 nuevos `[MySqlFact]` corren y pasan contra MySQL real (o se skipe-an limpio si no hay MySQL).
- [ ] `Create.cshtml.cs.OnGet` setea `Input.FechaApertura = DateTime.Today`.
- [ ] `Edit.cshtml.cs.OnGet` redirige a Details cuando `current.EsCerrada == true`.
