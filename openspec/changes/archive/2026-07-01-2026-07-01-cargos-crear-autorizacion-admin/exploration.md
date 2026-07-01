## Exploration: Autorización faltante en CargosController

### Current State
`CargosController` expone hoy TODOS sus endpoints sin `[Authorize]`, incluyendo lecturas, mutaciones y el subrecurso `/skills`. `Program.cs` solo registra `UseAuthentication()`, `UseAuthorization()` y `app.MapControllers();`, sin `RequireAuthorization()` global. El único patrón de autorización ya aplicado en la API está en `UsuariosController`, que usa `[Authorize(Roles = RolesSgv.Administrador)]` a nivel controller. En tests API no se emiten JWT reales: `ApiWebApplicationFactory` reemplaza auth por un scheme fake (`Test`) y hoy solo ofrece `FakeAuthenticationDefaults.AdminHeader`, o sea existe helper para autenticado-admin pero NO para autenticado sin rol admin. Además, el rol canon del código es `RolesSgv.Administrador` / string `"Administrador"`; no aparece un rol `"Admin"` en el repositorio.

### Affected Areas
- `src/SGV.Api/Controllers/CargosController.cs` — requiere aplicar autorización diferenciada: lecturas autenticadas, mutaciones solo administrador.
- `src/SGV.Api/Controllers/UsuariosController.cs` — referencia de patrón existente con `[Authorize(Roles = RolesSgv.Administrador)]`.
- `src/SGV.Api/Program.cs` — fue verificado; ya tiene `AddAuthorization()` y no necesita policy custom para este cambio.
- `src/SGV.Aplicacion/Seguridad/RolesSgv.cs` — define el catálogo real de roles (`Administrador`, `GestorVacantes`, `Consultor`); conviene reutilizar constante y no string literal nuevo.
- `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` — confirma que los roles sembrados incluyen `RolesSgv.Administrador`.
- `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` — hoy solo modela autenticación fake admin; necesita soporte para usuario autenticado sin rol admin para cubrir 403.
- `tests/SGV.Tests/Api/CargosControllerTests.cs` — hoy asume acceso anónimo y hasta verifica que el controller no tenga `[Authorize]`; deberá invertirse ese comportamiento y cubrir 401/403/2xx.
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` — mismo impacto para `GET /skills` autenticado y `PUT`/`DELETE` admin-only.
- `tests/SGV.Tests/Api/UsuariosControllerTests.cs` — muestra el patrón actual de pruebas protegidas: 401 sin credenciales + header fake admin.

### Approaches
1. **Autorización explícita por acción** — agregar `[Authorize]` en cada GET y `[Authorize(Roles = RolesSgv.Administrador)]` en cada POST/PUT/PATCH/DELETE.
   - Pros: máxima explicitud por endpoint; fácil de leer en Swagger/código; no mezcla permisos implícitos.
   - Cons: repetición alta; más superficie para olvidarse un action nuevo o subrecurso futuro.
   - Effort: Medium
2. **Baseline autenticado a nivel controller + override admin en mutaciones** — poner `[Authorize]` en `CargosController` y agregar `[Authorize(Roles = RolesSgv.Administrador)]` solo en `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill` y `DeleteSkill`.
   - Pros: modela exactamente la regla confirmada; reduce duplicación; protege automáticamente nuevos GET salvo que alguien agregue `[AllowAnonymous]`; consistente con el uso controller-level ya visto en `UsuariosController`.
   - Cons: requiere recordar que la seguridad base viene del controller; las mutaciones siguen necesitando anotación puntual.
   - Effort: Low

### Recommendation
Recomiendo **Baseline autenticado a nivel controller + override admin en mutaciones**. Es el approach más chico, consistente y difícil de romper: `CargosController` pasa a requerir autenticación por defecto y las operaciones que cambian estado elevan a `RolesSgv.Administrador`. Además evita inventar policies o tocar `Program.cs`, porque `AddAuthorization()` ya existe y el repo ya usa atributos directos con `RolesSgv`.

### Risks
- **Desalineación de naming de rol**: la issue habla de `Admin`, pero el código real usa `RolesSgv.Administrador` / `"Administrador"`. Si alguien implementa `"Admin"` literal, va a romper autorización aunque el endpoint quede anotado.
- **Hueco de cobertura 403**: el test harness actual solo conoce un usuario admin. Sin extender `FakeAuthenticationHandler`/headers para un autenticado no-admin, no se puede probar correctamente el criterio anónimo→401 / autenticado-sin-rol→403 / admin→2xx.

### Ready for Proposal
Yes — ya está claro el alcance real: el change puede proponerse como hardening de `CargosController` usando `[Authorize]` a nivel controller, `[Authorize(Roles = RolesSgv.Administrador)]` en mutaciones y ampliación del harness de tests fake auth para cubrir 401/403/2xx sin introducir policies nuevas ni tocar otros controllers fuera de scope.

```yaml
status: success
executive_summary: Se confirmó que `CargosController` está completamente expuesto y que el patrón real del repo para auth usa atributos `[Authorize]` con `RolesSgv.Administrador`, no policies custom. También se confirmó que los tests API no emiten JWT reales: usan auth fake y hoy falta un principal autenticado no-admin para cubrir 403.
artifacts:
  - openspec/changes/2026-07-01-cargos-crear-autorizacion-admin/exploration.md
next_recommended: propose
risks: []
skill_resolution: paths-injected
```
