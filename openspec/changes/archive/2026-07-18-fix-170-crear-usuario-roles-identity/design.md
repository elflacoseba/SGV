# Design: `2026-07-18-fix-170-crear-usuario-roles-identity`

## Technical Approach

Dos arreglos puntuales sin cambio de contrato ni de migración: (a) flag `bool RenderSingleRoleSelect` en `IUsuarioForm` bifurca el inner loop de `data-usuario-roles-section` en `_Form.cshtml` entre `<select>` único (Create) y checkboxes vigentes (Edit); (b) mapa `Dictionary<string, Func<IdentityError, string>>` estático privado dentro de `ToIdentityFailure` traduce los `IdentityError.Code` alcanzables por la política de `IdentityOptions.Password` vigente a mensajes en español, conservando `Categoria = Validation` y `Code = "IdentityError"`. Cobertura por código vía `[Theory]` + `[InlineData]` puros del gateway (sin MySQL, sin `WebApplicationFactory`).

## Architecture Decisions

### Decision: Bifurcación UI por flag en `IUsuarioForm` (vs. duplicar el partial)

| Opción | Tradeoff | Decisión |
|---|---|---|
| Flag en `IUsuarioForm` + condicional en `_Form.cshtml` | Mínimo cambio; reutiliza label, validación y `<span asp-validation-for>`; consistente con `IsEdit`/`EsAccionSobreSiMismo` | **Adoptada** |
| Partial separado `_FormCreate.cshtml`/`_FormEdit.cshtml` | Aísla cada modo | Descartada — duplica PersonaDisplay, auto-edición, password |
| Variable local en `CreateModel` vía `ViewData` | Rompe el contrato `IUsuarioForm` vigente que ya comparten Create/Edit | Descartada |

### Decision: `<select>` con `asp-for` + iteración manual de `RolesCatalogo`

| Opción | Tradeoff | Decisión |
|---|---|---|
| `asp-for="Input.Roles"` + `@foreach rol` con `<option selected="@(...)">` | Portable; no depende del comportamiento de auto-selección del tag helper para `string[]`; coincide exactamente con `UsuarioFormKeys.RolesKey = "Input.Roles"` | **Adoptada** |
| `asp-items="@Model.Input.RolesCatalogo"` como `SelectList` | Más declarativo pero pierde el control fino de `selected` por `<option>` (auto-binding no es nativo para `string[]`) | Descartada — riesgo de selección silenciosa |

### Decision: Mensaje de validación `[Required]` en `UsuarioInputModel.Roles`

| Opción | Tradeoff | Decisión |
|---|---|---|
| Cambiar `ErrorMessage` del `[Required]` a `"Debe seleccionar un rol."` | Toca un archivo; mismo alcance válido para Create/Edit | **Adoptada** |
| Mantener el mensaje actual y sobreescribir vía `ModelState.AddModelError` en `CreateModel.OnPost` | Doble fuente de verdad | Descartada |

### Decision: `Dictionary<string, Func<IdentityError, string>>` (vs. diccionario plano)

| Opción | Tradeoff | Decisión |
|---|---|---|
| `Dictionary<string, Func<IdentityError, string>>` (lookup + formateo con `Metadata`) | Permite interpolar `RequiredLength`/`RequiredUniqueChars` desde `error.Metadata` | **Adoptada** |
| `Dictionary<string, string>` plano | Rompe `PasswordTooShort` y `PasswordRequiresUniqueChars` | Descartada |

**Justificación de `Func`**: el `IdentityErrorDescriber` estándar setea `error.Metadata["RequiredLength"]` para `PasswordTooShort` y `error.Metadata["RequiredUniqueChars"]` para `PasswordRequiresUniqueChars`. La `Func<IdentityError,string>` lee el `Metadata` y compone la cadena; los códigos de mensaje fijo se modelan como lambdas que ignoran el parámetro.

### Decision: Forma de `UsuarioError` al localizar

`Type = UsuarioErrorType.Validation`, `Code = "IdentityError"`, `Categoria = ErrorCategoria.Validation`. **No** se introduce granularidad por sub-código (`PasswordTooShort` no se separa) — refinamiento futuro fuera de scope.

## Data Flow

```
GET /seguridad/usuarios/crear (admin) ──► CreateModel.OnGet ──► _Form.cshtml
                                                                          │
                                          RenderSingleRoleSelect == true  │
                                          <select asp-for="Input.Roles">  │
                                            <option value="">…</option>   │
                                            @foreach(rol) { selected }    │
                                          </select>                       │
                                                                          │
POST ─► ModelState.IsValid ?                                              │
            │ sí                                                         │
            ▼                                                            │
        IUsuarioApiClient.CreateAsync                                     │
            │ IdentityError PasswordTooShort                             │
            ▼                                                            │
        SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.CrearAsync   │
            │ ToIdentityFailure(map["PasswordTooShort"])                 │
            ▼                                                            │
        UsuarioError(Type=Validation, Code="IdentityError", msg es,       │
                     Categoria=Validation)                               │
            ▼                                                            │
        CreateModel.OnPost → PostResultMapper.TryMap →                    │
        ModelState["Input.Password"] = msg en español → re-render         │
```

## File Changes

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` | Modificar | Agregar `bool RenderSingleRoleSelect { get; }` con XML doc |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` | Modificar | `bool RenderSingleRoleSelect => true;` |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` | Modificar | `bool RenderSingleRoleSelect => false;` |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Modificar | Bifurcar `data-usuario-roles-section` (167-184) según `@Model.RenderSingleRoleSelect` |
| `src/SGV.Web/Integration/Usuarios/UsuarioInputModel.cs` | Modificar | `ErrorMessage` de `[Required]` y `[MinLength]` en `Roles` → `"Debe seleccionar un rol."` |
| `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` | Modificar | Reemplazar `ToIdentityFailure` (437-463) por versión con `IdentityErrorMap` estático privado |
| `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` | Crear | Suite `[Theory]` + `[InlineData]` (sin MySQL) |
| `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` | Modificar | Ajustar test que envía 2 roles + agregar dos tests (`Post_Create_WhenNoRoleSelected_*` y `Post_Create_WhenPasswordPolicyFails_*`) |

> **Nota de path**: el proposal dice `tests/SGV.Tests/Infraestructura/Seguridad/...`, pero esa carpeta no existe en el repo; los tests puros de `Seguridad/` viven en `tests/SGV.Tests/Seguridad/` (p.ej. `JwtOptionsTests.cs`). Este design usa ese path por consistencia con el resto del suite de seguridad no-MySQL.

## Interfaces / Contracts

### `IUsuarioForm` (nueva propiedad)

```csharp
/// <summary>
/// <c>true</c> cuando el campo Roles debe renderearse como
/// <c>&lt;select&gt;</c> único (alta en Create); <c>false</c> cuando
/// conserva checkboxes multi-rol (Edit).
/// </summary>
bool RenderSingleRoleSelect { get; }
```

### `_Form.cshtml` — bloque a sustituir (167-184)

```razor
<div class="col-12" data-usuario-roles-section>
    <label class="form-label">Roles</label>
    @if (Model.RenderSingleRoleSelect)
    {
        <select asp-for="Input.Roles" class="form-select"
                disabled="@(Model.EsAccionSobreSiMismo ? "disabled" : null)">
            <option value="">-- Seleccione un rol --</option>
            @foreach (var rol in Model.Input.RolesCatalogo)
            {
                var isSelected = Model.Input.Roles?.Contains(rol, StringComparer.Ordinal) ?? false;
                <option value="@rol" selected="@isSelected">@rol</option>
            }
        </select>
    }
    else
    {
        @* Rama else: bloque actual de checkboxes 169-181 intacto *@
    }
    <span asp-validation-for="Input.Roles" class="text-danger"></span>
</div>
```

### `ToIdentityFailure` — pseudocódigo

```csharp
private static readonly Dictionary<string, Func<IdentityError, string>> IdentityErrorMap =
    new(StringComparer.Ordinal)
    {
        ["PasswordTooShort"] = err =>
            $"La contraseña debe tener al menos {err.Metadata["RequiredLength"]} caracteres.",
        ["PasswordRequiresNonAlphanumeric"] = _ =>
            "La contraseña debe incluir al menos un carácter no alfanumérico.",
        ["PasswordRequiresDigit"] = _ =>
            "La contraseña debe incluir al menos un dígito.",
        ["PasswordRequiresLower"] = _ =>
            "La contraseña debe incluir al menos una letra minúscula.",
        ["PasswordRequiresUpper"] = _ =>
            "La contraseña debe incluir al menos una letra mayúscula.",
        ["PasswordRequiresUniqueChars"] = err =>
            $"La contraseña debe incluir al menos {err.Metadata["RequiredUniqueChars"]} caracteres únicos.",
        ["DuplicateUserName"] = _ => "El nombre de usuario ya está en uso.",
        ["DuplicateEmail"]    = _ => "El email ya está en uso.",
        ["InvalidEmail"]      = _ => "El email no tiene un formato válido.",
        ["InvalidUserName"]   = _ =>
            "El nombre de usuario sólo admite letras, números, punto, guión bajo y guión medio.",
    };

private const string FallbackIdentityMessage =
    "No se pudo completar la operación de identidad. Verifique los datos ingresados.";

private static UsuarioCommandResult ToIdentityFailure(IdentityResult result)
{
    var errors = result.Errors.ToArray();

    if (errors.Any(e => string.Equals(e.Code, "DuplicateUserName", StringComparison.Ordinal)))
        return Failure(UsuarioErrorType.Conflict, "UserNameDuplicado",
            "El nombre de usuario ya está en uso.", ErrorCategoria.Conflict);

    if (errors.Any(e => string.Equals(e.Code, "DuplicateEmail", StringComparison.Ordinal)))
        return Failure(UsuarioErrorType.Conflict, "EmailDuplicado",
            "El email ya está en uso.", ErrorCategoria.Conflict);

    var mapped = string.Join(" ", errors.Select(e =>
        IdentityErrorMap.TryGetValue(e.Code, out var translate)
            ? translate(e)
            : FallbackIdentityMessage));

    return Failure(UsuarioErrorType.Validation, "IdentityError", mapped, ErrorCategoria.Validation);
}
```

**Justificación del `Join`**: cuando Identity emite varios errores simultáneos (p.ej. contraseña corta + sin dígito), preservamos granularidad en español. Si ninguno matchea, todos los segmentos son `FallbackIdentityMessage` (cumpliendo MUST NOT inglés).

## Testing Strategy

| Capa | Qué cubre | Cómo |
|---|---|---|
| Unit (gateway) | Los 10 códigos cubiertos (`PasswordTooShort` con `Metadata["RequiredLength"]=N`, `PasswordRequiresUniqueChars` con `Metadata["RequiredUniqueChars"]=N`, 5× password de texto fijo, `DuplicateUserName/Email/InvalidEmail/InvalidUserName`) + fallback (`"ConcurrencyFailure"`) + invariantes `Categoria=Validation` y `Code="IdentityError"` | `[Theory]` + `[InlineData(code, metadata, expected)]` en `UsuarioIdentityGatewayToIdentityFailureTests.cs`. Sin MySQL. Reusa `IdentityResult.Failed(IdentityError[])` directo. |
| Integration (web) | GET Crear renderea `<select>` único con placeholder; POST sin rol agrega `Debe seleccionar un rol.` y NO llama al API (`CreateCalls.Count == 0`); POST con Password débil renderea el mensaje español | `FakeUsuarioApiClient.CreateResult/CreateCalls` en `CreatePageTests.cs`. `WebApplicationFactory` vigente. |

El test que envía 2 roles se ajusta a 1 rol (p.ej. `"Consultor"`) y cambia `checked` → `selected`.

## Migration / Rollout

Sin migración, sin `appsettings`, sin cambio en `Program.cs`. Mismos secretos JWT, misma DB MySQL. Rollback = un commit revertido: `ToIdentityFailure` y `RenderSingleRoleSelect` se deshacen a la vez; ningún estado intermedio.

## Open Questions

- [ ] Si Identity omitiera `Metadata["RequiredLength"]`/`RequiredUniqueChars`, `err.Metadata["X"]` lanzaría `KeyNotFoundException`. Mitigación propuesta: `err.Metadata.TryGetValue("X", out var v) ? $"…{v}…" : "…"`. Adoptar en implementación.
- [ ] ¿Persistir `Selected` vía `SelectListItem` con `asp-items` o vía iteración con `selected="@(...)"`? Decisión: iteración manual — evita propiedad adicional al `UsuarioInputModel`.

## Implementation Order (TDD)

1. Tests `[Theory]` + `[InlineData]` para los 10 códigos + fallback + invariantes `Categoria/Code` → rojo.
2. `IdentityErrorMap` + `FallbackIdentityMessage` + `ToIdentityFailure` reescrito → verde.
3. Flag en `IUsuarioForm` + `Create => true` + `Edit => false` → compilación verde sin cambio de UI.
4. Test `Post_Create_WhenNoRoleSelected_ReturnsValidationErrorWithoutInvokingApi` (`CreateCalls.Count == 0` + assert `Debe seleccionar un rol.`) → rojo.
5. Bifurcación en `_Form.cshtml` → verde.
6. Test actualizado `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` (1 rol + `selected`) → verde.
7. (Opcional) Test `Post_Create_WhenPasswordPolicyFails_RendersSpanishError` con `CreateResult = Failure(UsuarioError(Validation, "IdentityError", "La contraseña debe…"))`.
8. `dotnet test SGV.slnx` + `bun run build` en `src/SGV.Web`.

## Risks

- **`asp-for` + `<select>` único** — el binder materializa `string[]` de 1 elemento sobre `Input.Roles: string[]`. Sin cambio de modelo, verificado contra `UsuarioInputModel`.
- **Orden de `<option>`** — sigue `RolesCatalogo` (invariante `RolesSgv.Todos`). Ningún test depende de orden alfabético.
- **`Edit` no rompe** — la propiedad nueva en la interfaz obliga a ambas implementaciones; `Edit => false` mantiene los checkboxes vigentes.
- **Working tree** — limpio al inicio; bloques bifurcados conviven con un PR paralelo sobre `_Form.cshtml` sin colisión.
