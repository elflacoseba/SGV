# Verify Report — `2026-07-18-fix-170-crear-usuario-roles-identity`

## Resumen

| Categoría | Conteo |
|-----------|--------|
| CRITICAL | 0 |
| WARNING | 1 |
| SUGGESTION | 2 |

**Verdict**: APTO CON ADVERTENCIAS

## Cobertura por spec

### Spec `usuario-web-crear-editar` — REQ-UCE-11 (Selector único de rol en alta)
| Escenario | Test que lo cubre | Estado |
|-----------|-------------------|--------|
| GET Crear renderiza `<select>` único con placeholder obligatorio | `SGV.Tests.Web.Usuario.CreatePageTests.Get_Create_RenderizaSelectUnicoConPlaceholderObligatorio` | ✅ PASS |
| GET Editar conserva checkboxes multi-rol | `SGV.Tests.Web.Usuario.EditPageTests.Get_Edit_WhenAdminEditsAnotherUser_DoesNotShowAutoEdicionSelf` (verifica presencia de checkboxes; la rama `<select>`/else queda implícita) | ✅ PASS (coverage parcial — ver WARNING-1) |
| POST alta sin rol es rechazado antes de invocar la API | `SGV.Tests.Web.Usuario.CreatePageTests.Post_Create_WhenNoRoleSelected_ReturnsValidationErrorWithoutInvokingApi` | ✅ PASS |
| POST alta con un rol envía un único elemento a la API | `SGV.Tests.Web.Usuario.CreatePageTests.Post_Create_WhenSuccessful_RedirectsToDetailsWithFeedback` (1 rol `Consultor` → API recibe exactamente 1) | ✅ PASS |
| Tras 400/409 el rol seleccionado se preserva en el `<select>` | `SGV.Tests.Web.Usuario.CreatePageTests.Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` (asserts `<option value="Consultor" … selected>` + placeholder/otros NO selected) | ✅ PASS |

### Spec `identity-user-role-management` — Localización de `IdentityError`
| Escenario | Test que lo cubre | Estado |
|-----------|-------------------|--------|
| `PasswordTooShort` informa la longitud requerida | `SGV.Tests.Seguridad.UsuarioIdentityGatewayToIdentityFailureTests.ToIdentityFailure_LocalizaCodigoConocido(code: "PasswordTooShort", expectedFragment: "al menos 6 caracteres")` | ✅ PASS |
| `PasswordRequiresNonAlphanumeric` | `…(code: "PasswordRequiresNonAlphanumeric", expectedFragment: "al menos un carácter no alfanumérico")` | ✅ PASS |
| `PasswordRequiresDigit` | `…(code: "PasswordRequiresDigit", expectedFragment: "al menos un dígito")` | ✅ PASS |
| `PasswordRequiresLower` | `…(code: "PasswordRequiresLower", expectedFragment: "al menos una letra minúscula")` | ✅ PASS |
| `PasswordRequiresUpper` | `…(code: "PasswordRequiresUpper", expectedFragment: "al menos una letra mayúscula")` | ✅ PASS |
| `PasswordRequiresUniqueChars` informa los caracteres únicos requeridos | `…(code: "PasswordRequiresUniqueChars", expectedFragment: "al menos 1 carácter único")` | ✅ PASS |
| `DuplicateUserName` localizado | `…(code: "DuplicateUserName", expectedFragment: "nombre de usuario ya está en uso")` | ✅ PASS |
| `DuplicateEmail` localizado | `…(code: "DuplicateEmail", expectedFragment: "email ya está en uso")` | ✅ PASS |
| `InvalidEmail` localizado | `…(code: "InvalidEmail", expectedFragment: "email no tiene un formato válido")` | ✅ PASS |
| `InvalidUserName` localizado | `…(code: "InvalidUserName", expectedFragment: "letras, números, punto, guión bajo y guión medio")` | ✅ PASS |
| Código no reconocido cae a fallback en español | `ToIdentityFailure_CodigoNoMapeado_CaeAFallbackEnEspanol` (`ConcurrencyFailure` → `Verifique/no se pudo/operación` regex match + asserts que el inglés NO aparece) | ✅ PASS |
| Todos los errores localizados comparten `Categoria = Validation` y `Code = "IdentityError"` | `ToIdentityFailure_TodosLosErroresLocalizados_CompartenCategoriaValidationYCodeIdentityError` (los 11 códigos cubiertos) | ✅ PASS |
| Pipeline integral web→gateway→error español en `Input.Password` | `SGV.Tests.Web.Usuario.CreatePageTests.Post_Create_WhenPasswordPolicyFails_RendersSpanishError` | ✅ PASS |

## Build / Test / Bun

- **Build (`dotnet build SGV.slnx --configuration Release`)**: exit code **0**, 0 errores.
  - Warnings únicos: 20 (todos preexistentes en `develop` — diff contra `develop` sin el change = `0` introducidos). Pertenecen a `ErrorCategoriaMappers.cs` (CS8524 exhaustividad enum), `Index.cshtml.cs` (CS8602/CS8604 nullable), tests `CommandResultMapperTests`, `BloquearDesbloquearEliminarGatewayTests` (EF1002 SQL injection en interpolated), `SgvIdentityUserConfiguracionTests` (xUnit2029) y otros ApiClient con `switch` no exhaustivos. **Ningún warning introducido por este change**.
- **Test (`dotnet test SGV.slnx --configuration Release --no-build`)**: exit code **0**.
  - **Total: 2479** · Passed: **2479** · Failed: **0** · Skipped: **0** · Errors: 0.
  - Cobertura específica del change:
    - `UsuarioIdentityGatewayToIdentityFailureTests`: 12/12 PASS (10 Theory + 1 Fact fallback + 1 Fact invariantes).
    - `CreatePageTests` (escenarios #170): 4/4 PASS — `Get_Create_RenderizaSelectUnicoConPlaceholderObligatorio`, `Post_Create_WhenNoRoleSelected_ReturnsValidationErrorWithoutInvokingApi`, `Post_Create_WhenPasswordPolicyFails_RendersSpanishError`, `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId`.
    - `EditPageTests` (regresión checkboxes): `Get_Edit_WhenAdminEditsAnotherUser_DoesNotShowAutoEdicionSelf` y `Get_Edit_WhenAdminEditsSelf_RendersAlertAndDisabledRoleCheckboxes` siguen pasando.
  - MySQL local disponible: `[MySqlFact]` corre en lugar de skipearse.
- **Bun build (`bun run build` en `src/SGV.Web`)**: exit code **0**, Gulp completó `plugins` + `styles` en 3.62 s.

## Correctness (Static Evidence)

| Requirement / Decisión | Status | Notas |
|------------------------|--------|-------|
| `ToIdentityFailure` ahora es `internal static` (TASK-3) | ✅ | `InternalsVisibleTo("SGV.Tests")` ya estaba configurado en `SGV.Infraestructura.csproj`. Firma idéntica para todos los call sites internos. |
| `IdentityErrorMap` cubre los 8 códigos password+formato esperados | ✅ | `PasswordTooShort`, `PasswordRequiresNonAlphanumeric`, `PasswordRequiresDigit`, `PasswordRequiresLower`, `PasswordRequiresUpper`, `PasswordRequiresUniqueChars`, `InvalidEmail`, `InvalidUserName`. Mensajes en español neutro. |
| `FallbackIdentityMessage` en español para códigos no reconocidos | ✅ | Coincide con el escenario del spec. |
| Invariante `Categoria = Validation` y `Code = "IdentityError"` para códigos cubiertos | ✅ | Cubierto por `[Fact] ToIdentityFailure_TodosLosErroresLocalizados_…`. `DuplicateUserName`/`DuplicateEmail` mantienen `Conflict` por contrato vigente. |
| `IUsuarioForm.RenderSingleRoleSelect` declarado con XML doc | ✅ | `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` líneas 62-70. |
| `CreateModel.RenderSingleRoleSelect => true` | ✅ | `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs:70`. |
| `EditModel.IUsuarioForm.RenderSingleRoleSelect => false` (explicit impl) | ✅ | `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs:138`, sigue patrón de `EsAccionSobreSiMismo`. |
| `_Form.cshtml` bifurca con `@if (Model.RenderSingleRoleSelect)` | ✅ | Líneas 167-204: rama `true` con `<select asp-for="Input.Roles">` + placeholder + `@foreach` con `selected="@isSelected"`; rama `else` con checkboxes multi-rol intactos. |
| `[Required(ErrorMessage = "Debe seleccionar un rol.")]` y `[MinLength(1, ErrorMessage = "Debe seleccionar un rol.")]` | ✅ | `src/SGV.Web/Integration/Usuarios/UsuarioInputModel.cs` líneas 79-80. |
| GET Edit sigue mostrando checkboxes | ✅ | Tests `EditPageTests` siguen pasando. |

## Coherence (Design)

| Decisión de design | ¿Seguida? | Notas |
|--------------------|-----------|-------|
| Flag en `IUsuarioForm` + condicional en `_Form.cshtml` | ✅ Sí | Adoptada y verificada estáticamente. |
| Mensaje único en `[Required]`+`[MinLength]` para Create/Edit | ✅ Sí | Ambos atributos usan `"Debe seleccionar un rol."`. |
| Iteración manual con `selected="@isSelected"` (no `asp-items`) | ✅ Sí | Evita auto-binding silencioso de `string[]` con `asp-items` SelectList. |
| Sin migración, sin tocar `Program.cs`, sin tocar `appsettings*` | ✅ Sí | Apply progress confirma y el diff contra `develop` no toca esos paths. |

### Desviaciones del design (conscientes, justificadas)

1. **`IdentityError.Metadata` no existe en `Microsoft.Extensions.Identity.Core 9.0.0`** (D1 de apply-progress). El design exploraba `err.Metadata["RequiredLength"]`, pero la reflexión del tipo confirma que `IdentityError` en esta versión sólo expone `Code` y `Description`. La implementación usa mensajes hardcodeados (`6 caracteres` para `PasswordTooShort`, `1 carácter único` para `PasswordRequiresUniqueChars`) que reflejan la configuración vigente en `src/SGV.Api/Program.cs:112-118`. **Cumple el spec hoy**; documentado como red futura si la política cambia. Ver SUGGESTION-1.

2. **`ToIdentityFailure` cambió de `private static` a `internal static`** (D2 de apply-progress). Justificado por `InternalsVisibleTo("SGV.Tests")` para hacer los tests unitarios accesibles sin exponer el método en la API pública. Decisión aceptable para cobertura TDD.

## Findings

### CRITICAL
*(ninguno)*

### WARNING
- **WARNING-1 — Cobertura parcial del escenario "GET Editar conserva checkboxes multi-rol"** del spec `REQ-UCE-11`. El escenario exige MUST NOT existir `<select name="Input.Roles">` en Edit, pero los tests de `EditPageTests` verifican solo la presencia de checkboxes (`Assert.NotEmpty` con regex sobre `<input[^>]*name="Input.Roles"[^>]*>` en `Get_Edit_WhenAdminEditsAnotherUser_DoesNotShowAutoEdicionSelf`). La rama `else` del condicional está ejecutándose (los checkboxes se renderizan), pero **no hay un `Assert.DoesNotMatch` explícito que diga "no hay `<select name=Input.Roles>` en Edit"**, que es el MUST del escenario. La inversa sí está probada para Create (`Get_Create_RenderizaSelectUnicoConPlaceholderObligatorio` valida `DoesNotMatch(@"<input\b[^>]*type=""checkbox""[^>]*name=""Input\.Roles"")`). Considerar agregar `Assert.DoesNotMatch(@"<select[^>]*name=""Input.Roles""", content)` en un test de GET Edit para cerrar el invariante simétrico. **No bloquea** porque el comportamiento runtime es correcto (rama `else` ejecuta) y la inversa (Create ⇒ no checkboxes) sí está probada.

### SUGGESTION
- **SUGGESTION-1 — Hardcodear N=6 / N=1 en `PasswordTooShort`/`PasswordRequiresUniqueChars`** crea acoplamiento latente entre el mensaje en español y la configuración vigente en `SGV.Api/Program.cs:112-118`. Si esa política cambia (p.ej. a `RequiredLength = 8`), el mensaje "al menos 6 caracteres" será incorrecto. El test `ToIdentityFailure_LocalizaCodigoConocido` literal-matchea el "6", por lo que cambiar la política romperá el test y forzará la actualización conjunta — pero el mensaje seguirá en español neutro. Opciones futuras: (a) extraer el valor desde el código en runtime vía `IdentityErrorDescriber` español custom; (b) usar un parámetro `string.Format` con `RequiredLength` real si una futura versión de `IdentityError` expone metadata. Hoy es aceptable; documentar como red.
- **SUGGESTION-2 — Estandarizar idioma de DOCUMENT XML en `SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs`** dentro de los `<summary>` de `IdentityErrorMap`/`FallbackIdentityMessage`. La doc pública está en español en este archivo, pero podría mezclarse si futuros cambios vuelven a tocar los mensajes. Considerar comentarios extraídos a constantes para mantener una sola fuente de verdad.

## Conclusión

**APTO CON ADVERTENCIAS.**

- **Build**: exit 0, 0 warnings introducidos por este change (los 20 warnings únicos son preexistentes en `develop`).
- **Tests**: 2479/2479 PASS, 0 fallan, 0 skippean. Los 16 tests nuevos/ajustados del change (12 unit-tests del gateway + 4 tests del Create de web) pasan y cubren la matriz de compliance al 100% de los escenarios del spec, **salvo el gap menor documentado en WARNING-1** (presencia-only en Edit, no ausencia-explícita de `<select>`).
- **Regresiones**: ninguna. Los tests preexistentes que enviaban roles siguen verdes.
- **Bun**: exit 0.
- **Static review**: `ToIdentityFailure` correctamente `internal static`, `IdentityErrorMap` con los códigos requeridos, `FallbackIdentityMessage` en español, `_Form.cshtml` bifurca como en el design, mensajes de validación alineados, `RenderSingleRoleSelect` implementado en ambos PageModels.

El warning es informacional: la rama `else` ejecuta correctamente (verificado por la presencia de checkboxes en Edit), pero la spec exige un MUST NOT explícito sobre `<select>` que ningún test covertea con `DoesNotMatch`. Recomendado agregar ese assert en un próximo refinamiento; **no bloquea el archivado**.

El change cumple los criterios de aceptación del proposal (Bug 1 + Bug 2) y la matriz de compliance del spec. **Proceder con `archive`.**
