# Exploration — `2026-07-18-fix-170-crear-usuario-roles-identity`

Issue GitHub: #170 — Bugs en Crear Usuario: combo de Rol único + localizar errores de Identity.

## Contexto

El change apunta a dos bugs en el formulario de creación de usuarios (`/seguridad/usuarios/crear`):

1. **Bug 1** — El campo "Roles" se renderiza como checkboxes múltiples en `_Form.cshtml:167-184`. El usuario puede marcar varios roles a la vez, pero el dominio espera asignación 1:1 usuario↔rol. **Scope acordado**: solo UI del Create. Edit mantiene checkboxes. No se tocan `CrearUsuarioRequest`, `ActualizarUsuarioRequest` ni `UsuarioServicioComandos`. Estrategia propuesta: agregar `bool RenderSingleRoleSelect` a `IUsuarioForm` (Create devuelve `true`, Edit devuelve `false`) y condicionar el bloque en `_Form.cshtml`.

2. **Bug 2** — Los errores de política de contraseña de Identity llegan al cliente en inglés desde `ToIdentityFailure` (`UsuarioIdentityGateway.cs:437-463`). **Scope acordado**: mapeo de códigos Identity conocidos a español dentro de `ToIdentityFailure`. No se crea un `IdentityErrorDescriber` global ni se toca `Program.cs`.

## Verificación de scope Bug 1 (Roles)

### Archivos leídos y veredicto

| Archivo | Estado | Observación |
|---|---|---|
| `_Form.cshtml` (líneas 167-184) | Aislable | El bloque `<div class="col-12" data-usuario-roles-section>` es autocontenido. El flag puede condicionar solo el inner loop (líneas 169-181), dejando intacto el label y el validation span. |
| `Create.cshtml` | OK | Llama `_Form` sin cambios necesarios. |
| `Create.cshtml.cs` | OK | Implementa `IUsuarioForm` con `IsEdit => false`. Agregar `RenderSingleRoleSelect => true` es directo. |
| `Edit.cshtml` | OK | Misma llamada que Create. |
| `Edit.cshtml.cs` | OK | `IsEdit => true`. Agregar `RenderSingleRoleSelect => false` es directo. |
| `IUsuarioForm.cs` | OK | Ya vive `IsEdit` y `EsAccionSobreSiMismo` con implementaciones separadas. El nuevo flag sigue el mismo patrón. |
| `UsuarioInputModel.cs` | OK | `RolesCatalogo` y `FilterByCatalog` no requieren modificación. |
| `UsuarioFormHelpers.cs` | OK | `UsuarioFormKeys.RolesKey = "Input.Roles"` es compatible con `<select>` y con checkboxes. |

### Test afectado

`CreatePageTests.cs:301-336` envía 2 roles y verifica `checked`. Con dropdown único debe modificarse.

## Verificación de scope Bug 2 (Identity)

**Política vigente** (`Program.cs:112-118`):

```csharp
options.Password.RequireDigit = true;
options.Password.RequireLowercase = true;
options.Password.RequireUppercase = true;
options.Password.RequireNonAlphanumeric = true;
options.Password.RequiredLength = 6;
```

### Códigos alcanzables con la política actual

| Código | Alcanzable | Origen |
|---|---|---|
| `PasswordTooShort` | Sí | `RequiredLength = 6` |
| `PasswordRequiresNonAlphanumeric` | Sí | `RequireNonAlphanumeric = true` |
| `PasswordRequiresDigit` | Sí | `RequireDigit = true` |
| `PasswordRequiresLower` | Sí | `RequireLowercase = true` |
| `PasswordRequiresUpper` | Sí | `RequireUppercase = true` |
| `PasswordRequiresUniqueChars` | Sí | Default `RequireUniqueChars = 1` |
| `DuplicateUserName` | Sí | Ya está cubierto |
| `DuplicateEmail` | Sí | Ya está cubierto |
| `InvalidEmail` | Sí | Validación de Identity |
| `InvalidUserName` | Sí | Validación de Identity |
| `UserAlreadyHasPassword` | No | Solo con `AddPasswordAsync` |
| `UserLockoutNotEnabled` | No | Solo si `LockoutEnabled = false` |

## Call sites de `ToIdentityFailure`

10 call sites total. El map de códigos password aplica principalmente al #1 (`CrearAsync:70`). Los demás pueden emitir `DuplicateUserName/Email`, `InvalidUserName/Email`, o errores genéricos.

| # | Método | Línea | Fuente |
|---|---|---|---|
| 1 | `CrearAsync` | 70 | `UserManager.CreateAsync` |
| 2 | `CrearAsync` | 77 | `UserManager.AddToRolesAsync` |
| 3 | `ActualizarAsync` | 139 | `UserManager.UpdateAsync` |
| 4 | `BloquearAsync` | 175 | `SetLockoutEndDateAsync` |
| 5 | `BloquearAsync` | 182 | `UserManager.UpdateAsync` |
| 6 | `DesbloquearAsync` | 208 | `SetLockoutEndDateAsync` |
| 7 | `DesbloquearAsync` | 219 | `UserManager.UpdateAsync` |
| 8 | `EliminarAsync` | 244 | `UserManager.DeleteAsync` |
| 9 | `ReplaceRolesAsync` | 380 | `RemoveFromRolesAsync` |
| 10 | `ReplaceRolesAsync` | 390 | `AddToRolesAsync` |

## Tests existentes a tocar/crear

**Requieren cambios**:

- `tests/SGV.Tests/Web/Usuario/CreatePageTests.cs:301-336` — `Post_Create_Con409_PreservaFormYMuestraErrorEnPersonaId`. Envía 2 roles y verifica `checked`. Cambiar a 1 rol y verificar `selected`.

**Recomendados (crear)**:

- Tests unitarios de `ToIdentityFailure` con `IdentityResult.Failed(...)`, ejecutables sin MySQL. Carpeta sugerida: `tests/SGV.Tests/Infraestructura/Seguridad/UsuarioIdentityGatewayToIdentityFailureTests.cs`.

## Riesgos y mitigaciones

| Riesgo | Nivel | Mitigación |
|---|---|---|
| Tests de Create multi-role se rompen | Medio | Modificar test para 1 rol + aserciones `selected`. |
| Sin tests de `ToIdentityFailure` | Medio | Crear tests unitarios con `IdentityResult.Failed`. |
| `RenderSingleRoleSelect` en `IUsuarioForm` | Bajo | Cambio controlado, consistente con patrón existente. |
| Cambios abiertos en develop | Bajo | Working tree limpio, sin branches divergentes. |

## Recomendación para la fase `propose`

El cambio es viable y está bien delimitado. Pasar a **propose** con el alcance actual, incluyendo explícitamente la creación de tests unitarios para `ToIdentityFailure` como tarea adicional.
