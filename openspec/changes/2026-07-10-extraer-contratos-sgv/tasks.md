# Tasks: Extraer `SGV.Contracts` (issue #100)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 800-1200 (≈60 archivos; 1-3 líneas) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 Auth · PR2 Organizacion · PR3 Habilidades · PR4 Seguridad+cleanup (4 PRs) |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | PR | Notes |
|------|------|----|-------|
| 1 | Crear `SGV.Contracts` + `AuthApiRoutes` | PR1 | Valida el patrón |
| 2 | Wire-types de `Organizacion` | PR2 | Quita ≈22 de 38 imports Web; abre `Aplicacion → Contracts` |
| 3 | Wire-types de `Habilidades` | PR3 | Depende de Organizacion |
| 4 | `RolesSgv` + `Usuario*` + romper `Web → Api` + docs | PR4 | Cierra la cadena |

## Phase 1 — PR1: Auth

- [x] 1.1 Crear `src/SGV.Contracts/SGV.Contracts.csproj` (classlib `net10.0`).
- [x] 1.2 Insertar el `.csproj` en `SGV.slnx` arriba de Api/Web.
- [x] 1.3 Mover `AuthApiRoutes.cs` a `src/SGV.Contracts/Auth/` (namespace `SGV.Contracts.Auth`).
- [x] 1.4 Sumar referencia a `SGV.Contracts` en `SGV.Api.csproj`.
- [x] 1.5 Eliminar `src/SGV.Api/Contracts/`.
- [x] 1.6 Cambiar `using` en 4 archivos.
- [x] 1.7 Verificación: build + test verdes; `grep "using SGV.Api.Contracts"` → 0.

## Phase 2 — PR2: Organizacion

- [x] 2.1 Crear `SGV.Contracts/Organizacion/Consultas/Dtos/*.cs` (13 records/enums).
- [x] 2.2 Crear `SGV.Contracts/Organizacion/Comandos/*.cs` (~17 Requests/Results/Errors).
- [x] 2.3 Sumar referencia a `SGV.Contracts` en `SGV.Aplicacion.csproj`.
- [x] 2.4 Reescribir `using` en `Aplicacion/Organizacion/**/*Servicio*.cs`.
- [x] 2.5 Cambiar `using` en 5 controllers.
- [x] 2.6 Cambiar `using` en ≈22 archivos Web (`Pages/Organizacion` + `Integration/Organizacion`).
- [x] 2.7 Cambiar `using` en `ApiWebApplicationFactory` + 6 controller tests.
- [x] 2.8 Verificación: build + test verdes; `grep "using SGV.Aplicacion.Organizacion" src/SGV.Web` → 0.

## Phase 3 — PR3: Habilidades

- [ ] 3.1 Crear `SGV.Contracts/Habilidades/Consultas/Dtos/*.cs` (7 records/enums).
- [ ] 3.2 Crear `SGV.Contracts/Habilidades/Comandos/*.cs` (`Habilidad*` + `CargoSkill*`).
- [ ] 3.3 Reescribir `using` en `SGV.Aplicacion/Habilidades/**`.
- [ ] 3.4 Cambiar `using` en 4 controllers.
- [ ] 3.5 Cambiar `using` en ≈6 archivos Web.
- [ ] 3.6 Cambiar `using` en 6 controller tests.
- [ ] 3.7 Verificación: build + test verdes.

## Phase 4 — PR4: Seguridad + cleanup

- [ ] 4.1 Crear `SGV.Contracts/Seguridad/RolesSgv.cs`.
- [ ] 4.2 Crear `SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` (8 tipos wire de login/usuario).
- [ ] 4.3 Reescribir `using` en `UsuarioServicioComandos.cs`, `RolServicioConsulta.cs`, `DatosSemilla.cs`.
- [ ] 4.4 Cambiar `using` en `UsuariosController.cs`.
- [ ] 4.5 Cambiar `using` en `SignIn.cshtml.cs`, `AuthApiClient.cs`, ≈6 `Pages/.../Cargos/*.cshtml.cs`.
- [ ] 4.6 Cambiar `using` en 7 tests.
- [ ] 4.7 En `SGV.Web.csproj`: `<ProjectReference SGV.Api` → `<ProjectReference SGV.Contracts`.
- [ ] 4.8 Confirmar `SGV.Api.csproj` y `SGV.Aplicacion.csproj` ya referencian `SGV.Contracts`.
- [ ] 4.9 Actualizar `AGENTS.md`: añadir `src/SGV.Contracts/`; nuevo grafo.
- [ ] 4.10 Actualizar `docs/decisiones-implementacion.md` línea 83: namespace → `SGV.Contracts.Organizacion`.
- [ ] 4.11 Verificación: build + test verdes; greps → 0; `dotnet list reference` → `Web → Contracts` y `Web ⟂ Api`.
