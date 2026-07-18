# Tasks: Corregir combo único de Roles en Crear Usuario + localizar errores de Identity

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~370 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | single PR |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Bug 2 + Bug 1 + tests | PR 1 | base = main; fits budget (~370 lines) |

---

## Phase 1: Bug 2 — IdentityError localization via TDD RED→GREEN

### TASK-1: Test RED `ToIdentityFailure` parametrizado por `IdentityError.Code`

**Phase**: 1
**Type**: test
**Spec ref**: `identity-user-role-management` — escenarios `PasswordTooShort` a `InvalidUserName` + fallback
**Files**: `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` (crear)
**Depends on**: none

**Description**: Escribir suite `[Theory]` + `[InlineData]` que ejercita cada `IdentityError.Code` alcanzable (`PasswordTooShort`, `PasswordRequiresNonAlphanumeric`, `PasswordRequiresDigit`, `PasswordRequiresLower`, `PasswordRequiresUpper`, `PasswordRequiresUniqueChars`, `DuplicateUserName`, `DuplicateEmail`, `InvalidEmail`, `InvalidUserName`, `ConcurrencyFailure` como fallback). Cada caso espera mensaje en español pero la implementación actual devuelve `error.Description` en inglés → test ROJO. `PasswordTooShort` y `PasswordRequiresUniqueChars` setean `Metadata["RequiredLength"]` y `Metadata["RequiredUniqueChars"]`. Sin MySQL, usa `IdentityResult.Failed(IdentityError[])` directo.
**Acceptance**: `dotnet test --filter "UsuarioIdentityGatewayToIdentityFailure"` falla con al menos un test rojo.

---

### TASK-2: Test invariantes estructurales de `ToIdentityFailure`

**Phase**: 1
**Type**: test
**Spec ref**: `identity-user-role-management` — escenario "Todos los errores localizados comparten Categoria=Validation y Code=IdentityError"
**Files**: `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` (agregar)
**Depends on**: TASK-1

**Description**: Agregar `[Fact]` que itera sobre todos los códigos cubiertos y verifica que cada `UsuarioError` producido tenga `Categoria == ErrorCategoria.Validation` y `Code == "IdentityError"`. Test ROJO porque hoy la implementación existente retorna códigos diferentes (`"UserNameDuplicado"`, `"EmailDuplicado"`).
**Acceptance**: El nuevo `[Fact]` falla al correr contra la implementación actual.

---

### TASK-3: Implementar `IdentityErrorMap` + `FallbackIdentityMessage` + reescritura de `ToIdentityFailure`

**Phase**: 1
**Type**: code
**Spec ref**: `identity-user-role-management` — todos los escenarios
**Files**: `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` (modificar)
**Depends on**: TASK-2

**Description**: Agregar `private static readonly Dictionary<string, Func<IdentityError, string>> IdentityErrorMap` con los 9 códigos password+formato, la constante `FallbackIdentityMessage` en español, y reemplazar el cuerpo de `ToIdentityFailure`. Conservar la rama `DuplicateUserName`/`DuplicateEmail` como `Conflict` (no cambia categoría). El resto itera `result.Errors`, traduce vía `IdentityErrorMap.TryGetValue` con fallback, y retorna `Failure(Validation, "IdentityError", joined, Validation)`. Usar `TryGetValue` en `Metadata` para `PasswordTooShort`/`PasswordRequiresUniqueChars` (mitigación `KeyNotFoundException`). Ambos tests de TASK-1 y TASK-2 pasan a VERDE.
**Acceptance**: `dotnet test --filter "UsuarioIdentityGatewayToIdentityFailure"` todos GREEN.

---

## Phase 2: Bug 1 — Contrato `IUsuarioForm` + modelo

### TASK-4: Agregar `bool RenderSingleRoleSelect` a `IUsuarioForm`

**Phase**: 2
**Type**: code
**Spec ref**: `usuario-web-crear-editar` — REQ-UCE-11
**Files**: `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` (modificar)
**Depends on**: TASK-3

**Description**: Agregar propiedad `bool RenderSingleRoleSelect { get; }` con XML doc indicando que `true` renderiza `<select>` único en alta; `false` conserva checkboxes multi-rol en Edit. NO implementar en los PageModels todavía.
**Acceptance**: Compilación exitosa con errores CS0535 en CreateModel/EditModel por propiedad no implementada.

---

### TASK-5: Implementar `RenderSingleRoleSelect` en CreateModel (`true`) y EditModel (`false`)

**Phase**: 2
**Type**: code
**Spec ref**: `usuario-web-crear-editar` — REQ-UCE-11
**Files**:
- `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` (modificar)
- `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` (modificar)
**Depends on**: TASK-4

**Description**: En `Create.cshtml.cs` agregar `public bool RenderSingleRoleSelect => true;`. En `Edit.cshtml.cs` agregar `bool IUsuarioForm.RenderSingleRoleSelect => false;` (explicit interface impl para mantener consistencia con `EsAccionSobreSiMismo`). Sin cambio de UI todavía.
**Acceptance**: `dotnet build` verde. Todos los tests de CreatePageTests existentes verdes.

---

### TASK-6: Cambiar `ErrorMessage` del `[Required]` en `UsuarioInputModel.Roles`

**Phase**: 2
**Type**: code
**Spec ref**: `usuario-web-crear-editar` — REQ-UCE-11 POST sin rol
**Files**: `src/SGV.Web/Integration/Usuarios/UsuarioInputModel.cs` (modificar)
**Depends on**: TASK-5

**Description**: Cambiar `ErrorMessage` de `[Required(ErrorMessage = "Debe asignar al menos un rol.")]` y de `[MinLength(1, ErrorMessage = "Debe asignar al menos un rol.")]` a `"Debe seleccionar un rol."`. Ambos atributos se alinean al mismo mensaje del spec.
**Acceptance**: `dotnet build` verde.

---

## Phase 3: Bug 1 — UI TDD RED→GREEN

### TASK-7: Test RED `Post_Create_WhenNoRoleSelected_ReturnsValidationErrorWithoutInvokingApi`

**Phase**: 3
**Type**: test
**Spec ref**: `usuario-web-crear-editar` — escenario "POST alta sin rol es rechazado antes de invocar la API"
**Files**: `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` (modificar — agregar test)
**Depends on**: TASK-6

**Description**: Agregar test que envía POST sin `Input.Roles`. Verificar `ModelState` inválido con mensaje `Debe seleccionar un rol.` ligado a `Input.Roles`. Verificar que `FakeUsuarioApiClient.CreateCalls.Count == 0` (no se invocó la API). Test ROJO porque hoy el `[Required]` sí valida pero el binder de checkboxes vacíos no se comporta igual que el de `<select>` vacío.
**Acceptance**: `dotnet test --filter "Post_Create_WhenNoRoleSelected"` falla.

---

### TASK-8: Bifurcar `_Form.cshtml` en `data-usuario-roles-section`

**Phase**: 3
**Type**: code
**Spec ref**: `usuario-web-crear-editar` — REQ-UCE-11
**Files**: `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` (modificar, líneas 167-184)
**Depends on**: TASK-7

**Description**: Dentro del bloque `<div data-usuario-roles-section>`, condicionar el inner loop con `@if (Model.RenderSingleRoleSelect)`. Rama `true`: `<select asp-for="Input.Roles" class="form-select">` con placeholder `<option value="">-- Seleccione un rol --</option>` + `@foreach` roles del catálogo con `selected="@isSelected"`. Rama `else`: bloque actual de checkboxes (íntegro, líneas 169-181). Label y `<span asp-validation-for>` fuera del condicional. Test de TASK-7 pasa a VERDE.
**Acceptance**: `dotnet test --filter "Post_Create_WhenNoRoleSelected"` verde.

---

### TASK-9: Verificar test GET Crear/Editar y ajustar si es necesario

**Phase**: 3
**Type**: test
**Spec ref**: `usuario-web-crear-editar` — escenario "GET Crear renderiza `<select>` único" y "GET Editar conserva checkboxes"
**Files**: `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` (verificar/agregar assertions)
**Depends on**: TASK-8

**Description**: Verificar que el test existente de `GET /seguridad/usuarios/crear` (si existe) o un test nuevo verifica que el HTML contiene `<select name="Input.Roles">` y `<option value="">-- Seleccione un rol --</option>` cuando `RenderSingleRoleSelect == true`. No debe romperse GET /editar que sigue con checkboxes. Ajustar cualquier test existente que asuma checkboxes en Create.
**Acceptance**: `dotnet test SGV.slnx` verde.

---

## Phase 4: Ajuste de tests existentes

### TASK-10: Ajustar `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` a 1 rol + `selected`

**Phase**: 4
**Type**: test
**Spec ref**: `usuario-web-crear-editar` — escenario "Tras 400/409 el rol seleccionado se preserva"
**Files**: `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` (modificar, líneas 301-336)
**Depends on**: TASK-8

**Description**: El test envía 2 roles (`Administrador`, `Consultor`) y verifica `checked` en ambos checkboxes. Cambiar a 1 rol (p.ej. `"Consultor"`). Las aserciones de `checked` en `<input name="Input.Roles">` pasan a verificar `selected` en `<option value="Consultor" selected>` dentro del `<select>`. Eliminar la aserción del segundo rol.
**Acceptance**: `dotnet test --filter "Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId"` verde.

---

### TASK-11: Agregar test `Post_Create_WhenPasswordPolicyFails_RendersSpanishError`

**Phase**: 4
**Type**: test
**Spec ref**: `identity-user-role-management` + `usuario-web-crear-editar` — escenario POST password débil
**Files**: `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` (modificar — agregar test)
**Depends on**: TASK-8

**Description**: Agregar test que configura `FakeUsuarioApiClient.CreateResult` para retornar un `Failure(UsuarioError(Validation, "IdentityError", "La contraseña debe incluir al menos un dígito."))`. Hacer POST con contraseña débil. Verificar que el HTML re-renderizado contiene el mensaje en español ligado a `Input.Password`. Este test valida el pipeline completo `ToIdentityFailure` → `PostResultMapper` → `ModelState["Input.Password"]` → HTML.
**Acceptance**: `dotnet test --filter "Post_Create_WhenPasswordPolicyFails"` verde.

---

## Phase 5: Verify — build + tests + frontend

### TASK-12: `dotnet build SGV.slnx` verde

**Phase**: 5
**Type**: code
**Spec ref**: —
**Files**: `SGV.slnx`
**Depends on**: TASK-11

**Description**: Compilar toda la solución y verificar que no hay errores de compilación, incluyendo los cambios en `IUsuarioForm`, `Create.cshtml.cs`, `Edit.cshtml.cs`, `_Form.cshtml`, `UsuarioInputModel.cs`, `UsuarioIdentityGateway.cs`, y los archivos de test.
**Acceptance**: `dotnet build` exit code 0.

---

### TASK-13: `dotnet test SGV.slnx` verde

**Phase**: 5
**Type**: test
**Spec ref**: —
**Files**: `SGV.slnx`
**Depends on**: TASK-12

**Description**: Ejecutar toda la suite de tests. Verificar que los nuevos tests de `UsuarioIdentityGatewayToIdentityFailureTests`, los tests ajustados de `CreatePageTests`, y todos los tests existentes pasan. Tests `[MySqlFact]` se skipean si no hay MySQL local (no bloqueante).
**Acceptance**: `dotnet test` exit code 0 (MySQL tests skipped ok).

---

### TASK-14: `bun run build` en `src/SGV.Web` sin errores

**Phase**: 5
**Type**: code
**Spec ref**: —
**Files**: `src/SGV.Web/`
**Depends on**: TASK-13

**Description**: Ejecutar el pipeline de assets frontend (Inspinia/Gulp) dentro de `src/SGV.Web` para verificar que el bundle se genera correctamente. No debería haber errores porque el cambio es solo Razor + C#, sin tocar JS/CSS.
**Acceptance**: `bun run build` exit code 0.

---

## Notas de implementación

- `err.Metadata["RequiredLength"]` debe protegerse con `TryGetValue` para evitar `KeyNotFoundException` (ver design §Open Questions).
- El binder de `<select>` con `asp-for` materializa `string[]` de 1 elemento — sin cambio de modelo.
- `_Form.cshtml` bifurca dentro del bloque `<div data-usuario-roles-section>` (líneas 167-184), label y `<span asp-validation-for>` fuera del condicional.
- Tests unitarios de `ToIdentityFailure` no requieren MySQL — `IdentityResult.Failed(IdentityError[])` directo.
- El test de TASK-11 verifica el pipeline integral web→gateway→error español; requiere `WebApplicationFactory` y `FakeUsuarioApiClient`.
