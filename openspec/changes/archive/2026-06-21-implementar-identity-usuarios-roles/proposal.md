# Proposal: Implementar Identity para Usuarios y Roles

## Intent

Activar ASP.NET Core Identity para administrar usuarios SGV y roles sin contaminar el Dominio. Cada usuario DEBE asociarse a una `Persona` existente; no habrá cuentas standalone.

## Scope

### In Scope
- Activar Identity en API/Infraestructura con auth y Swagger seguro.
- Crear usuarios vinculados obligatoriamente a una `Persona` existente.
- Asignar uno de tres roles fijos: `Administrador`, `GestorVacantes`, `Consultor`.
- Exponer casos de uso para usuarios y roles.

### Out of Scope
- Catálogo dinámico de roles administrado por usuarios.
- Proveedores externos, password reset, email confirmation o MFA.
- Autorizar masivamente endpoints existentes de lectura pública.

### Non-goals
- No convertir Identity en agregado de Dominio.
- No exponer entidades Identity/Persistencia en contratos API.
- No crear usuarios sin `Persona`.

## Capabilities

### New Capabilities
- `identity-user-role-management`: usuarios vinculados a Personas, auth y roles fijos.

### Modified Capabilities
- `persona-management`: vínculo Persona-usuario y reglas de ciclo de vida.
- `sgv-database`: agrega vínculo obligatorio `AspNetUsers` → `Personas` y ajusta seed de roles fijos.
- `sgv-persistence-architecture`: permite personalización Identity en Infraestructura manteniendo Dominio limpio.
- `sgv-readonly-api`: preserva lecturas anónimas y documenta endpoints protegidos.

## Approach

Usar Identity como preocupación de API/Infraestructura. Aplicación define casos de uso; Infraestructura adapta `UserManager`, `RoleManager` y EF/Pomelo. Para la regla de Persona obligatoria, agregar un tipo Identity propio de Infraestructura con `PersonaId` requerido y FK restrictiva. Sembrar solo tres roles fijos con IDs determinísticos.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Api/Program.cs` | Modified | Identity, middleware, Swagger security. |
| `src/SGV.Api/Controllers` | New/Modified | Endpoints protegidos. |
| `src/SGV.Aplicacion/Seguridad` | New/Modified | Contratos y usuario autenticado. |
| `src/SGV.Infraestructura/Persistencia` | Modified | `PersonaId`, seed y migración MySQL. |
| `tests/SGV.Tests` | Modified | Cobertura por capa. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Migración por `PersonaId` obligatorio | Medium | Fail-loud/backfill explícito y pruebas. |
| Romper lecturas anónimas existentes | Medium | No aplicar `[Authorize]` global; specs lo preservan. |
| Drift de roles sembrados | Medium | Constantes determinísticas y pruebas de seed. |

## Rollback Plan

Revertir endpoints, registros Identity/auth y adaptadores. Revertir la migración de `PersonaId`/seed o aplicar script inverso antes de usar datos productivos nuevos.

## Dependencies

- `Personas` existentes para crear usuarios.
- Tablas `AspNet*` ya generadas por migración inicial.

## Success Criteria

- [ ] No se puede crear usuario sin `Persona` existente.
- [ ] Solo existen/asignan `Administrador`, `GestorVacantes`, `Consultor`.
- [ ] Dominio sigue sin depender de Identity.
- [ ] Lecturas públicas existentes continúan anónimas.
