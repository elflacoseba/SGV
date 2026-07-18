# Proposal: Corregir combo único de Roles en Crear Usuario + localizar errores de Identity

## Intent

El formulario de **Crear Usuario** (`/seguridad/usuarios/crear`) presenta dos bugs reportados en #170:

1. **Bug 1 — Roles como checkboxes múltiples**: el campo `Roles` se renderiza con checkboxes (líneas 167-184 de `_Form.cshtml`), lo que permite seleccionar varios roles al crear. El dominio espera asignación 1:1 usuario↔rol. En Edit la situación es diferente: la UI vigente (multi-checkbox) refleja el contrato backend (`ActualizarUsuarioRequest` con `Roles: string[]`) y el comportamiento multi-rol existe y se valida; el bug 1 se limita al alta.
2. **Bug 2 — Errores de Identity en inglés**: las violaciones de la política de contraseña configurada en `Program.cs:112-118` (`PasswordTooShort`, `PasswordRequiresDigit`, etc.) llegan al cliente tal cual los emite `IdentityErrorDescriber`, en inglés. `ToIdentityFailure` (`UsuarioIdentityGateway.cs:437-463`) sólo cubre `DuplicateUserName`, `DuplicateEmail`, `InvalidEmail` e `InvalidUserName`. El resto cae a un fallback genérico en inglés.

Ambos bugs degradan la UX del flujo de alta (que es la puerta de entrada al módulo de seguridad) y mezclan idiomas en el formulario.

## Scope

### In Scope

- **Bug 1 — UI Crear**: renderizar un `<select>` único en el alta, conservando checkboxes en Edit.
- **Bug 2 — Localización**: mapear los códigos de `IdentityError` conocidos (política de contraseña + duplicados + formato) a mensajes en español dentro de `ToIdentityFailure`.
- Tests unitarios de `ToIdentityFailure` sin MySQL.
- Actualización del test web de alta que asume multi-rol.

### Out of Scope (Non-goals)

- Cambios en `CrearUsuarioRequest`, `ActualizarUsuarioRequest` ni en `UsuarioServicioComandos`.
- Cambios en el formulario Edit (sigue con checkboxes y multi-rol).
- Creación de un `IdentityErrorDescriber` global ni tocar `Program.cs`.
- Cambios en `IdentityOptions.Password`, `LockoutOptions` u otra configuración de Identity.
- Cambios de taxonomía HTTP→`ErrorCategoria` (cubierto por `commandresult-error-taxonomy`).
- CRUD de roles, lockout, hard-delete (cubiertos por otros changes).

## Capabilities

### Modified

- `usuario-web-crear-editar`: el alta MUST renderizar un `<select>` único para `Roles` (con placeholder `-- Seleccione un rol --`) y MUST validar selección obligatoria. Edit MUST conservar checkboxes.
- `identity-user-role-management`: los errores `IdentityResult.Failed` traducidos por `ToIdentityFailure` para los códigos de política de contraseña (`PasswordTooShort`, `PasswordRequiresNonAlphanumeric`, `PasswordRequiresDigit`, `PasswordRequiresLower`, `PasswordRequiresUpper`, `PasswordRequiresUniqueChars`) y los de unicidad/formato ya cubiertos MUST llegar al cliente en español. Códigos no reconocidos MUST caer a un fallback en español.

## Approach

**Bug 1 — flag en `IUsuarioForm`**:

1. Agregar `bool RenderSingleRoleSelect { get; }` a `IUsuarioForm` (siguiendo el patrón de `IsEdit`/`EsAccionSobreSiMismo`).
2. `Create.cshtml.cs`: implementar `=> true`. `Edit.cshtml.cs`: implementar `=> false`.
3. En `_Form.cshtml`, dentro del bloque `data-usuario-roles-section`, bifurcar el inner loop (líneas 169-181): si `RenderSingleRoleSelect`, renderizar `<select asp-for="Input.Roles" asp-items="Model.RolesCatalogo"><option value="">-- Seleccione un rol --</option></select>`. Si no, mantener los checkboxes actuales.
4. `UsuarioFormHelpers.cs.RolesKey = "Input.Roles"` ya es compatible con ambos.

**Bug 2 — map en `ToIdentityFailure`**:

- Dentro del método, antes del fallback, agregar un `switch` (o `Dictionary<string,string>` estático interno) por `IdentityError.Code` que devuelva el mensaje en español.
- Tabla de mensajes: textos cortos, accionables, alineados con el español neutro ya usado en la UI web.
- Códigos no listados: fallback genérico en español (no inglés) para evitar regresión.

## Affected Areas

| Capa | Archivo | Cambio |
|------|---------|--------|
| Web | `src/SGV.Web/Pages/Seguridad/Usuarios/IUsuarioForm.cs` | Agregar propiedad |
| Web | `src/SGV.Web/Pages/Seguridad/Usuarios/Create.cshtml.cs` | Implementar `=> true` |
| Web | `src/SGV.Web/Pages/Seguridad/Usuarios/Edit.cshtml.cs` | Implementar `=> false` |
| Web | `src/SGV.Web/Pages/Seguridad/Usuarios/_Form.cshtml` | Bifurcar inner loop roles (169-181) |
| Infra | `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` | Extender `ToIdentityFailure` (437-463) |
| Tests | `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` (301-336) | Actualizar aserciones a 1 rol + `selected` |
| Tests | `tests/SGV.Tests/Infraestructura/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` | NUEVO |

## Acceptance Criteria

- [ ] `GET /seguridad/usuarios/crear` renderiza un único `<select name="Input.Roles">` con placeholder obligatorio.
- [ ] `GET /seguridad/usuarios/editar/{id}` conserva los checkboxes multi-rol vigentes.
- [ ] POST de alta sin rol seleccionado MUST ser rechazado con `Debe seleccionar un rol.`.
- [ ] POST de alta con `Password` que viola la política MUST mostrar el código específico en español (no inglés) en el campo `Input.Password`.
- [ ] `ToIdentityFailure` para códigos no mapeados MUST devolver un fallback en español.
- [ ] Ningún contrato de la API cambia; el frontend no agrega campos.
- [ ] `dotnet test SGV.slnx` verde; `bun run build` sin errores.

## Test Strategy

| Tipo | Alcance | Archivo |
|------|---------|---------|
| Unit (nuevo) | `ToIdentityFailure` con `IdentityResult.Failed(new IdentityError { Code = "..." })` para cada código alcanzable con la política actual + duplicados + fallback | `tests/SGV.Tests/Infraestructura/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs` |
| Integration (modificar) | `CreatePageTests.Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId` envía 1 rol y assert `selected`; agregar scenario de validación "sin rol" y scenario de password débil en español | `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs` |

Strict TDD: tests nuevos antes que la implementación de cada bug.

## Risks

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| Tests de Create existentes asumen multi-rol | Medio | Actualizar test a 1 rol + aserción `selected` antes de cambiar `_Form.cshtml` |
| `ToIdentityFailure` sin cobertura unitaria hoy | Medio | Crear suite `[Theory]` con `InlineData` por cada código alcanzable |
| Mensajes en español divergen con el resto de la UI | Bajo | Reutilizar el español neutro ya presente en `Input.*` validation messages |
| `RenderSingleRoleSelect` rompe el contrato `Input.Roles: string[]` | Bajo | `asp-for` resuelve a `string` en el binding; Edit sigue funcionando |
| Cambios abiertos en develop | Bajo | Working tree limpio al iniciar |

## Rollback Plan

Revertir el commit del change. `ToIdentityFailure` con el map nuevo cae al fallback genérico en español (no inglés), así que un rollback del fix Bug 2 deja un comportamiento equivalente al previo en términos de idioma para códigos no listados (regresión controlada). El flag `RenderSingleRoleSelect` se quita junto con la rama `<select>` de `_Form.cshtml`; no afecta Edit. No hay migraciones ni cambios de schema.

## Dependencies

- Ninguna externa. No requiere migraciones ni cambios de `appsettings*.json`.

## References

- Issue: GitHub #170 — *Bugs en Crear Usuario: combo de Rol único + localizar errores de Identity*.
- Issues relacionadas: #125, #168, #169 (citadas en el cuerpo de #170, no releídas en esta fase).
- Specs vigentes: `openspec/specs/usuario-web-crear-editar/spec.md`, `openspec/specs/identity-user-role-management/spec.md`.
- Change predecesor: `2026-07-15-quita-soft-delete-usuario` (cambió el ciclo de baja; este change no lo toca).
- Política de contraseña: `src/SGV.Api/Program.cs:112-118`.
- Map vigente de errores: `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs:437-463`.
