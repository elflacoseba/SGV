# Archive Report — `2026-07-18-fix-170-crear-usuario-roles-identity`

## Resumen

Este change corrige dos bugs del flujo de alta web de usuarios en `/seguridad/usuarios/crear` (GitHub #170):

**Bug 1 — Selector de rol incorrecto en Crear Usuario.** El campo `Roles` se renderizaba como checkboxes múltiples permitiendo seleccionar varios roles, cuando el dominio actual exige asignación 1:1 usuario↔rol en alta. Se reemplazó por un `<select>` único con placeholder obligatorio (`-- Seleccione un rol --`), validación `[Required]`/`[MinLength(1)]` con mensaje específico, y preservación de selección tras errores 400/409. La edición (`/seguridad/usuarios/editar/{id}`) conserva los checkboxes multi-rol sin cambios.

**Bug 2 — Mensajes de IdentityError en inglés.** Los errores de política de contraseña (`PasswordTooShort`, `PasswordRequiresDigit`, etc.), unicidad (`DuplicateUserName`, `DuplicateEmail`) y formato (`InvalidEmail`, `InvalidUserName`) emitidos por ASP.NET Core Identity llegaban al cliente en inglés. Se implementó `IdentityErrorMap` (Dictionary con los 8 códigos alcanzables + fallback genérico) dentro de `UsuarioIdentityGateway.ToIdentityFailure`, que ahora traduce cada error al español antes de devolver el `UsuarioError` con `Categoria = Validation` y `Code = "IdentityError"`.

El cambio se desarrolló en strict TDD con 14 tareas distribuidas en 5 fases: test RED → implementación → test GREEN → ajustes → verificación. Sin migraciones, sin tocar `Program.cs`, `appsettings*` ni la API.

## Issue

- GitHub: [#170](https://github.com/elflacoseba/SGV/issues/170)

## Specs modificados

- `openspec/specs/usuario-web-crear-editar/spec.md` — agregado `REQ-UCE-11 Selector único de rol en alta con selección obligatoria` con 5 escenarios (GET Crear, GET Editar, POST sin rol, POST con un rol, post-400/409 preservación)
- `openspec/specs/identity-user-role-management/spec.md` — agregado requisito `Localización de errores de Identity al español en ToIdentityFailure` con 12 escenarios (9 códigos + fallback + invariantes)

## Archivos modificados

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` | Modificado | Agregado `IdentityErrorMap` (8 códigos) + `FallbackIdentityMessage`; reescritura de `ToIdentityFailure`; cambio de `private static` a `internal static` vía `InternalsVisibleTo`. |
| `src/SGV.Web/Integration/Usuarios/IUsuarioForm.cs` | Modificado | Agregada propiedad `bool RenderSingleRoleSelect { get; }` con XML doc. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` | Modificado | `public bool RenderSingleRoleSelect => true;` |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` | Modificado | `bool IUsuarioForm.RenderSingleRoleSelect => false;` (explicit interface impl). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Modificado | Bifurcación en `<div data-usuario-roles-section>`: `<select>` cuando `RenderSingleRoleSelect == true`; checkboxes multi-rol en else. |
| `src/SGV.Web/Integration/Usuarios/UsuarioInputModel.cs` | Modificado | `[Required]` y `[MinLength(1)]` con `ErrorMessage = "Debe seleccionar un rol."`. |
| `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` | Creado | Suite de 12 tests (10 Theory parametrizados + 1 Fact fallback + 1 Fact invariantes). |
| `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` | Modificado | Agregados 3 tests nuevos (`Post_Create_WhenNoRoleSelected*`, `Get_Create_RenderizaSelectUnico*`, `Post_Create_WhenPasswordPolicyFails*`); ajustado `Post_Create_Con409*` a 1 rol + aserciones `selected`. |

## Tests agregados/modificados

| Tipo | Archivo | Tests |
|------|---------|-------|
| Unit | `tests/SGV.Tests/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` | 12 nuevos: 10x `[Theory]` + 1x `[Fact]` fallback + 1x `[Fact]` invariantes |
| Integration | `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` | 4 ajustados/agregados: `Post_Create_WhenNoRoleSelected_ReturnsValidationErrorWithoutInvokingApi`, `Get_Create_RenderizaSelectUnicoConPlaceholderObligatorio`, `Post_Create_WhenPasswordPolicyFails_RendersSpanishError`, `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` |

## Suite final

- **Build**: `dotnet build SGV.slnx --configuration Release` → exit 0, 0 warnings introducidos (20 preexistentes en `develop`)
- **Test**: `dotnet test SGV.slnx --configuration Release --no-build` → **2479/2479 PASS**, 0 fail, 0 skip
  - `UsuarioIdentityGatewayToIdentityFailureTests`: 12/12 PASS
  - `CreatePageTests` (#170 related): 4/4 PASS
  - `EditPageTests` (regresión checkboxes): sin cambios, todos PASS
  - `[MySqlFact]` suite: corre completa (MySQL local disponible)
- **Bun**: `bun run build` en `src/SGV.Web` → exit 0, Gulp completó plugins+styles en 2.93s

## Stack

- .NET 10 (`net10.0`) con SDK `10.0.300`
- ASP.NET Core Identity (`Microsoft.Extensions.Identity.Core 9.0.0`)
- xUnit 2.9.2 + `coverlet.collector`
- Sin migraciones de EF Core
- Sin cambios en `Program.cs`, `appsettings*` ni la API

## Decisiones técnicas relevantes

### D1 — `IdentityError.Metadata` no existe en `Microsoft.Extensions.Identity.Core 9.0.0`

El `design.md` exploraba usar `err.Metadata["RequiredLength"]` para mensajes dinámicos de `PasswordTooShort` y `PasswordRequiresUniqueChars`. La inspección con reflection confirma que `IdentityError` en esta versión sólo expone `Code` y `Description`. Se usan valores hardcodeados (N=6 para `PasswordTooShort`, N=1 para `PasswordRequiresUniqueChars`) que reflejan la configuración vigente en `SGV.Api/Program.cs:112-118`. Tests unitarios matchean estos literales, por lo que un cambio de política romperá loss tests y forzará la actualización conjunta.

### D2 — `ToIdentityFailure` cambia de `private static` a `internal static`

`InternalsVisibleTo("SGV.Tests")` ya estaba configurado en `SGV.Infraestructura.csproj`. Marcar el método `internal` expone la unidad a tests sin modificar la API pública. Los 10 call sites internos quedan idénticos.

### D3 — Iteración manual con `selected` en lugar de `asp-items`

`_Form.cshtml` usa iteración manual con `selected="@isSelected"` (mismo patrón que los checkboxes existentes) en vez de `asp-items SelectList`. Esto evita el riesgo de auto-binding silencioso de `string[]` que `asp-items` podría introducir con valores de placeholder.

## Riesgos residuales

### WARNING-1 — Cobertura parcial de GET Editar

El spec exige que en Edit MUST NOT existir `<select name="Input.Roles">`, pero los tests de `EditPageTests` verifican solo la presencia de checkboxes (`Assert.NotEmpty` con regex). No hay un `Assert.DoesNotMatch(@"<select[^>]*name=""Input.Roles""`, content) explícito para ese escenario. La rama `else` del condicional ejecuta correctamente (verificado por presencia de checkboxes), pero el invariante simétrico (Create ⇒ no checkboxes) sí está probado. Recomendado cubrir en follow-up.

### SUGGESTION-1 — Hardcodeo de N=6/N=1 para mensajes de password

Los valores de `Password.RequiredLength` (6) y `RequireUniqueChars` (1) están hardcodeados en los mensajes en español. Si la política en `Program.cs` cambia, los mensajes deben actualizarse en sincronía. Los tests literal-matchean estos valores por lo que funcionan como red de protección (el test fallará y se forzará la actualización). Considerar `IdentityErrorDescriber` personalizado si se necesita localización más robusta en el futuro.

### SUGGESTION-2 — Idioma de XML docs en `UsuarioIdentityGateway.cs`

Los `<summary>` de `IdentityErrorMap` y `FallbackIdentityMessage` están documentados en español (coherente con el dominio del proyecto), pero podrían mezclarse con inglés si futuros cambios tocan el archivo sin esta referencia. Considerar mantener una sola fuente de verdad.

## Follow-ups recomendados

1. **Agregar `Assert.DoesNotMatch(@"<select[^>]*name=""Input.Roles""", content)`** en un test de GET Edit (`EditPageTests`) para cerrar el invariante simétrico de WARNING-1.
2. **Evaluar `IdentityErrorDescriber` personalizado** si en el futuro se necesita localización dinámica sin hardcodeo de N=6/N=1.
3. **Aislar `UsuariosEndToEndMySqlFactTests.Bloquear_AnotherUser_Returns200WithBloqueadoTrue`** en una collection dedicada para mitigar la flake observada bajo carga paralela (no relacionada con este change).

## Comandos de validación

```bash
dotnet build SGV.slnx
dotnet test SGV.slnx
cd src/SGV.Web && bun run build
```

## Fecha de archivado

2026-07-18
