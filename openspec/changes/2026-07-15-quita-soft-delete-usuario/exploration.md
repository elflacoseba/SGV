## Exploration: Quita soft-delete de usuarios

### Current State

El módulo Usuarios está implementado con soft-delete completo:

- **Entidad**: `SgvIdentityUser` tiene `IsDeleted` (bool, default false) como campo custom.
- **EF Config**: Columnas generadas STORED `ActiveUserNameUnique` y `ActivePersonaIdUnique` que devuelven NULL cuando `IsDeleted=1`, con índices únicos sobre ellas para permitir soft-delete sin violar unicidad de UserName/PersonaId.
- **Gateway** (`UsuarioIdentityGateway`): `DesactivarAsync` setea `IsDeleted=true`, `ReactivarAsync` setea `IsDeleted=false`. `QueryAsync` filtra por `where query.Segmento == Activas ? !user.IsDeleted : user.IsDeleted`.
- **Login** (`AuthServicio.LoginAsync`): chequea `user.IsDeleted` manualmente y retorna null si es true (401).
- **Creación** (`CrearAsync`): verifica que no exista otro usuario activo con mismo PersonaId (`AnyAsync(u => u.PersonaId == request.PersonaId && !u.IsDeleted)`).
- **API**: `DELETE /api/v1/usuarios/{id}` → `DesactivarAsync` (soft-delete). `PATCH /api/v1/usuarios/{id}/reactivar` → `ReactivarAsync`.
- **Web**: Vista segmentada `activas|eliminadas` con botones Eliminar (soft-delete) y Reactivar.
- **Contracts**: `UsuarioSegmentoListado` enum con `Activas=0, Eliminadas=1`.
- **Tests**: 3 tests de login con soft-delete (`SoftDeletedUserLoginTests`), tests de gateway con `isDeleted` parameter, tests de comandos con `DesactivarCalled`/`ReactivarCalled`.
- **Specs**: `identity-user-role-management/spec.md` y `usuario-web-listado-detalle-baja/spec.md` referencian "baja lógica" y `IsDeleted`.

### Affected Areas

- `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs` — eliminar `IsDeleted` property
- `src/SGV.Infraestructura/Persistencia/Configuraciones/SgvIdentityUserConfiguracion.cs` — eliminar config de IsDeleted, ActiveUserNameUnique y ActivePersonaIdUnique
- `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` — cambiar `DesactivarAsync`/`ReactivarAsync` a usar Lockout; cambiar `QueryAsync` filtro; cambiar `CrearAsync` chequeo de unicidad
- `src/SGV.Infraestructura/Seguridad/AuthServicio.cs` — cambiar chequeo `user.IsDeleted` por `userManager.IsLockedOutAsync(user)`
- `src/SGV.Api/Controllers/UsuariosController.cs` — posiblemente añadir endpoint Bloquear/Desbloquear si se separa de DELETE físico
- `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` — renombrar `UsuarioSegmentoListado.Eliminadas` → `Bloqueadas`
- `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml.cs` — cambiar lógica de segmento, nombres de vistas, handlers
- `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` — actualizar textos y labels de "eliminados" a "bloqueados"
- `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs` — actualizar lógica de segmento
- `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` — actualizar textos
- `src/SGV.Web/Integration/Usuarios/IUsuarioApiClient.cs` — actualizar comentarios
- `src/SGV.Web/Integration/Usuarios/UsuarioApiClient.cs` — actualizar `BuildQueryUri` (status parameter)
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260715145121_AddSoftDeleteToAspNetUsers.cs` — nueva migración para dropear IsDeleted y columnas generadas
- `openspec/specs/identity-user-role-management/spec.md` — actualizar req de baja lógica
- `openspec/specs/usuario-web-listado-detalle-baja/spec.md` — actualizar reqs
- `tests/SGV.Tests/Seguridad/SoftDeletedUserLoginTests.cs` — cambiar para usar Lockout en vez de IsDeleted
- `tests/SGV.Tests/Persistencia/UsuarioIdentityGatewayTests.cs` — cambiar tests que usan `isDeleted` parameter
- `tests/SGV.Tests/Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` — cambiar tests de fake gateway
- `tests/SGV.Tests/Web/Usuario/*` — tests que referencian Eliminadas/Reactivar
- `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` — se regenera con nueva migración

### Approaches

#### Approach 1: Lockout reemplaza a IsDeleted (sin delete físico)

**Descripción**: `IsDeleted` se elimina por completo. El DELETE endpoint **no borra físicamente** la fila, sino que setea `LockoutEnd = DateTimeOffset.MaxValue` mediante `UserManager.SetLockoutEndAsync`. El PATCH reactivar limpia el lockout. Todo el filtrado por segmento usa `LockoutEnabled && LockoutEnd >= UtcNow` como reemplazo de `IsDeleted`. Las columnas generadas `ActiveUserNameUnique` y `ActivePersonaIdUnique` se dropean porque ya no hay soft-delete que evite duplicates — la unicidad normal de Identity y el FK 1:1 con Personas bastan. No hay delete físico expuesto.

- Pros:
  - Migración directa desde el estado actual: DELETE → Lockout, REACTIVATE → Unlock, mínimos cambios semánticos
  - La fila del usuario permanece en DB, preservando la auditoría histórica y permitsiendo listar "bloqueados"
  - No se pierde la capacidad de "reactivar" un usuario (solo cambia el mecanismo: de IsDeleted a Lockout)
  - `UserManager.SetLockoutEndAsync` y `UserManager.IsLockedOutAsync` ya existen en Identity, no requieren código nuevo
  - Las columnas generadas se dropean, simplificando el schema
  - La unicidad de PersonaId se maneja con el FK 1:1 normal (sin generated columns)
  - Menos cambios en web layer (solo renombrar "eliminadas" → "bloqueadas")

- Cons:
  - **NO cumple con "delete físico"** explícitamente solicitado por el usuario
  - `CheckPasswordAsync` no chequea lockout automáticamente — hay que agregar `IsLockedOutAsync` manual en `AuthServicio.LoginAsync`
  - La auditoría de baja lógica registra "BajaLogica" pero ahora es "Bloqueo" — hay que actualizar el código de acción
  - Los tests existentes de soft-delete hay que reescribirlos para lockout

- Effort: Medium (cambio mecánico, bien acotado)

#### Approach 2: DELETE físico + Lockout para estado "inactivo/bloqueado"

**Descripción**: Se introducen DOS operaciones separadas: (a) **Bloquear/Desbloquear** usando Lockout de Identity (reemplaza al soft-delete como mecanismo de estado), y (b) **Eliminar** como DELETE físico real de la fila (`UserManager.DeleteAsync`). El segmento `Eliminadas` desaparece y se reemplaza por `Bloqueadas` (usuarios con `LockoutEnabled=true` y `LockoutEnd` futuro). Los usuarios eliminados físicamente no aparecen en ningún listado. La unicidad de PersonaId y UserName se resuelve sin columnas generadas porque el delete físico elimina la fila competidora, y el lockout no introduce duplicados (el usuario bloqueado sigue siendo el mismo registro).

- Pros:
  - **Cumple exactamente con lo solicitado**: delete físico real + Lockout como filtro activo/inactivo
  - La fila eliminada desaparece por completo, limpiando datos sensibles (credenciales, hashes)
  - No hay ambigüedad semántica: Bloquear = no puede loguearse, Eliminar = ya no existe
  - Unicidad natural: el delete físico libera PersonaId/UserName inmediatamente, lockout no causa duplicados
  - Se dropean columnas generadas y el campo IsDeleted, schema más limpio

- Cons:
  - **Pérdida de datos al eliminar**: la auditoría histórica referenciando el UserId se vuelve huérfana (FK hacia una fila que ya no existe)
  - **Impacto masivo en la web layer**: hay que rediseñar la UI para separar Bloquear de Eliminar (dos acciones distintas)
  - Las specs existentes de "baja lógica" requieren reescritura completa
  - Los tests de reactivación pierden sentido (un usuario eliminado físicamente no se reactiva)
  - El `LastDeletedId` en TempData para "undo" (reactivación rápida) ya no funciona
  - `UsuarioServicioComandos.DesactivarAsync` con chequeo `AutoBaja` (no puedes eliminarte a vos mismo) se mantiene, pero `ReactivarAsync` desaparece
  - La vista `Details` para usuarios eliminados físicamente no puede mostrar nada (404)
  - Las columnas generadas no son el único problema de unicidad: sin soft-delete, `CrearAsync` ya no necesita verificar `!user.IsDeleted` en el `AnyAsync`
  - Esfuerzo alto porque toca todas las capas incluyendo la experiencia de usuario web

- Effort: High (cambio profundo en todas las capas, incluyendo UI/UX)

### Recommendation

**Approach 1** (Lockout reemplaza a IsDeleted, sin delete físico) es el enfoque recomendado por las siguientes razones:

1. **El soft-delete actual es funcionalmente un "bloqueo"** — el usuario no puede loguearse pero su registro persiste. Lockout hace exactamente eso con la mecanismo nativo de Identity.
2. **No hay pérdida de datos**: la auditoría histórica referenciando el UserId se preserva porque la fila sigue existiendo.
3. **Migración directa**: `IsDeleted=true` → `LockoutEnd=MaxValue`, `IsDeleted=false` → `LockoutEnd=null`. El cambio es casi 1:1.
4. **Las columnas generadas se dropean**: el schema se limpia porque lockout no introduce duplicados (la fila sigue siendo la misma).
5. **La experiencia web cambia mínimamente**: "Eliminadas" → "Bloqueadas", "Reactivar" → "Desbloquear".
6. **El delete físico debería ser un change separado** con análisis de impacto en retención de datos, auditoría, y UX.

Si el usuario insiste en delete físico, recomiendo hacerlo en dos cambios separados: (1) este change (lockout como reemplazo de IsDeleted), y (2) un change futuro que agregue DELETE físico real como operación adicional (no como reemplazo).

### Riesgos

- **CheckPasswordAsync no chequea lockout automáticamente**. SGV usa `UserManager.CheckPasswordAsync` directamente (no `SignInManager.PasswordSignInAsync`). El chequeo `IsLockedOutAsync` debe agregarse MANUALMENTE en `AuthServicio.LoginAsync` después de encontrar al usuario y antes de validar password. Si se omite, un usuario "bloqueado" podría seguir logueándose.
- **Identidad entre Lockout y bloqueo administrativo**: Identity usa LockoutEnd también para bloqueo por intentos fallidos. Si `LockoutOnFailure` está configurado en IdentityOptions, un usuario podría quedar bloqueado temporalmente por muchos intentos y confundirse con un bloqueo administrativo. Hay que revisar la configuración de `IdentityOptions.Lockout` en el startup.
- **`SetLockoutEndAsync` puede fallar** si el usuario ya tiene lockout por intentos fallidos y se solapa con el lockout administrativo. No hay conflicto real porque `SetLockoutEndAsync` siempre sobreescribe, pero la semántica debe documentarse.
- **Las specs existentes** (`identity-user-role-management/spec.md`, `usuario-web-listado-detalle-baja/spec.md`) referencian "baja lógica" y requieren actualizaciones como delta specs. Son specs del core del módulo usuarios.
- **Tests de integración con MySQL**: `SoftDeletedUserLoginTests` usa `JwtRealWebApplicationFactory` con MySQL real. Los tests que setean `IsDeleted=true` deben cambiarse a `SetLockoutEndAsync(user, DateTimeOffset.MaxValue)` y requieren `UserManager` en vez de modificar directamente la propiedad.
- **FakeUsuarioIdentityGateway en tests unitarios**: el `DesactivarCalled`/`ReactivarCalled` debe cambiar a `BloquearCalled`/`DesbloquearCalled` o similar.
- **Auditoría**: `UsuarioServicioComandos.DesactivarAsync` registra "BajaLogica". Debe cambiar a "Bloqueo". `ReactivarAsync` registra "Reactivacion". Debe cambiar a "Desbloqueo".

### Ready for Proposal

**Yes**. El alcance está claro, el mecanismo de Identity Lockout está bien entendido, y el enfoque recomendado es viable. El próximo paso es `sdd-propose` para formalizar el alcance, los no-objetivos y el plan de rollback.

**Qué decirle al usuario**: El análisis muestra que reemplazar `IsDeleted` por `LockoutEnd` es un cambio mecánico bien acotado que toca ~15 archivos en todas las capas. El delete físico (eliminar la fila de la DB) tiene implicaciones profundas en auditoría, UX y manejo de datos históricos — recomiendo hacerlo como change separado si es necesario. Este change se enfoca en: (1) eliminar `IsDeleted` y columnas generadas, (2) usar `LockoutEnd` como marcador de inactividad, (3) renombrar segmento "eliminadas" → "bloqueadas", (4) actualizar todas las capas incluyendo specs y tests.
