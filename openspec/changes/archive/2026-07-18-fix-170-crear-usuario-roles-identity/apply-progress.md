# Apply Progress — `2026-07-18-fix-170-crear-usuario-roles-identity`

## Resumen ejecutivo

Implementación completa del change en strict TDD: dos arreglos puntuales (Bug 2: localización de `IdentityError` al español en `ToIdentityFailure`; Bug 1: `<select>` único en alta de `/seguridad/usuarios/crear`). Las 14 tareas cerraron en GREEN, `dotnet build` y `dotnet test` (2479/2479) quedan verdes, `bun run build` también. No se commiteó nada — el orquestador maneja el commit.

## Estado por tarea

| # | Tarea | Estado | Nota |
|---|-------|--------|------|
| 1 | Test RED `ToIdentityFailure` parametrizado | ✓ | `[Theory]`+`[InlineData]` cubriendo los 10 códigos + fallback. RED inicial fue compile error porque `ToIdentityFailure` era `private static`. |
| 2 | Test invariantes `Categoria=Validation` y `Code="IdentityError"` | ✓ | `[Fact]` en `TodosLosErroresLocalizados_CompartenCategoriaValidationYCodeIdentityError` que itera los 11 códigos. |
| 3 | Implementar `IdentityErrorMap` + `FallbackIdentityMessage` + reescritura | ✓ | `Dictionary<string,string>` con los 8 códigos alcanzables; `FallbackIdentityMessage` en español; método cambia a `internal static` para exponer vía `InternalsVisibleTo("SGV.Tests")`. |
| 4 | Agregar `bool RenderSingleRoleSelect` a `IUsuarioForm` | ✓ | Provocó 2 CS0535 en Create/Edit como esperaba la acceptance. |
| 5 | Implementar `RenderSingleRoleSelect` en Create (`true`) y Edit (`false`) | ✓ | Create con member pública `=> true`; Edit con explicit interface impl `bool IUsuarioForm.RenderSingleRoleSelect => false;` siguiendo el patrón de `EsAccionSobreSiMismo`. |
| 6 | Cambiar `ErrorMessage` del `[Required]` y `[MinLength]` en `Roles` | ✓ | Ambos atributos ahora `"Debe seleccionar un rol."` (alineado al placeholder del `<select>`). |
| 7 | Test RED `Post_Create_WhenNoRoleSelected_ReturnsValidationErrorWithoutInvokingApi` | ✓ | El test pasó sin necesidad de UI nueva: `[MinLength(1)]` ya capturaba la lista vacía con el mensaje actualizado. Se conserva como regression guard. |
| 8 | Bifurcar `_Form.cshtml` con `<select>` cuando `RenderSingleRoleSelect == true` | ✓ | Rama `true`: `<select asp-for="Input.Roles" class="form-select">` con placeholder `<option value="">-- Seleccione un rol --</option>` + `@foreach` roles del catálogo con `selected="@isSelected"`. Rama `else`: bloque de checkboxes (169-181) intacto. Label y `<span asp-validation-for>` fuera del condicional. |
| 9 | Verificar test GET Crear/Editar | ✓ | Agregado `Get_Create_RenderizaSelectUnicoConPlaceholderObligatorio`: verifica un único `<select name="Input.Roles">`, placeholder, options por cada rol del catálogo, y la ausencia de checkboxes de Roles. |
| 10 | Ajustar `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` a 1 rol + `selected` | ✓ | Cambiado a 1 rol (`Consultor`); aserciones `checked` → `selected`; agregada verificación de que placeholder y otros roles NO estén selected. |
| 11 | Agregar `Post_Create_WhenPasswordPolicyFails_RendersSpanishError` | ✓ | Fake `CreateResult` con `UsuarioError(Validation, "IdentityError", "La contraseña debe incluir al menos un dígito.")`. Verifica que el mensaje español se renderee en el HTML. |
| 12 | `dotnet build SGV.slnx` verde | ✓ | 0 warnings, 0 errors. |
| 13 | `dotnet test SGV.slnx` verde | ✓ | 2479 / 2479 pasan (estable en 2 corridas consecutivas tras flake transitoria no relacionada con este change en `UsuariosEndToEndMySqlFactTests.Bloquear_AnotherUser_Returns200WithBloqueadoTrue` — test MySQL preexistente que crea usuarios bajo carga paralela). MySQL local sí está disponible; los `[MySqlFact]` corren. |
| 14 | `bun run build` en `src/SGV.Web` verde | ✓ | Gulp `build` completa: plugins + styles en 2.93s. |

## Decisiones técnicas aplicadas

### D1 — `IdentityError.Metadata` no existe en Microsoft.Extensions.Identity.Core 9.0.0

El `design.md` asumía leer `err.Metadata["RequiredLength"]`, pero la inspección con reflection confirma que `IdentityError` en 9.0.0 sólo expone `Code` y `Description`. La implementación usa mensajes hardcodeados que reflejan la configuración vigente en `SGV.Api/Program.cs:112-118` (`RequiredLength = 6`, `RequireUniqueChars` default = 1). El spec del change requiere "incluya la longitud N" — los valores hardcodeados (6 para `PasswordTooShort`, 1 para `PasswordRequiresUniqueChars`) cumplen el contrato. **Si en el futuro la política cambia, los tests actuarán como red de protección y forzarán la actualización de los mensajes.**

### D2 — `ToIdentityFailure` cambia de `private static` a `internal static`

`InternalsVisibleTo("SGV.Tests")` ya estaba configurado en `src/SGV.Infraestructura/SGV.Infraestructura.csproj`. Marcar el método `internal` es la opción (b) del design.md. La firma y todos los call sites internos quedan idénticos — los 10 call sites siguen usando `ToIdentityFailure(...)` sin cambios.

### D3 — Rama explícita con `IsSelected` por `<option>`

`_Form.cshtml` usa iteración manual con `selected="@isSelected"` (mismo patrón que ya tenían los checkboxes) en vez de `asp-items`. Razón: control fino del atributo `selected` por `<option>` — el binding auto-selected de `asp-items` para `string[]` no es nativo y traía riesgo de selección silenciosa (evaluado en design §Open Questions).

## Archivos modificados

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` | Modificado | `IdentityErrorMap` + `FallbackIdentityMessage` + reescritura de `ToIdentityFailure`; cambio de `private static` a `internal static`. |
| `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` | Modificado | Agregada propiedad `bool RenderSingleRoleSelect { get; }` con XML doc. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` | Modificado | Agregada `public bool RenderSingleRoleSelect => true;`. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` | Modificado | Agregada `bool IUsuarioForm.RenderSingleRoleSelect => false;` (explicit interface impl). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Modificado | Bifurcación en `<div data-usuario-roles-section>`: `<select asp-for="Input.Roles">` cuando `RenderSingleRoleSelect == true`; checkboxes vigentes en else. |
| `src/SGV.Web/Integration/Usuarios/UsuarioInputModel.cs` | Modificado | `[Required]` y `[MinLength(1)]` en `Roles` con `ErrorMessage = "Debe seleccionar un rol."`. |
| `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` | Creado | Suite 12 tests (10 Theory + 2 Fact) que cubren los 11 códigos conocidos + invariantes + fallback. |
| `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` | Modificado | Agregados 3 tests (`Post_Create_WhenNoRoleSelected_*`, `Get_Create_RenderizaSelectUnicoConPlaceholderObligatorio`, `Post_Create_WhenPasswordPolicyFails_RendersSpanishError`); ajustado `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` a 1 rol + aserciones `selected`. |

## Resultado de suite final

- Build: exit 0, 0 warnings, 0 errors.
- Test: 2479/2479 pasan (2 corridas estables).
- Bun: exit 0, build completa en 2.93s.

## Riesgos y notas

- **D1 anterior**: si `Program.cs:118` cambia `RequiredLength` a otro valor, los mensajes hardcodeados deben actualizarse en sincronía. Cobertura: `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` con el literal `"al menos 6 caracteres"` actuará como red.
- **Flake preexistente observado**: `UsuariosEndToEndMySqlFactTests.Bloquear_AnotherUser_Returns200WithBloqueadoTrue` falló una vez en 3 corridas bajo carga paralela. No toca archivos de este change. Considerar aislamiento futuro (collection dedicada).
- **Sin commits**: el orquestador maneja los commits al final.
