# Proposal: Implementar login frontend

## Intent

Habilitar un acceso real en `SGV.Web` con Razor Pages usando el diseño Inspinia de `Auth/SignIn`, consumiendo el login existente de `SGV.Api` y redirigiendo a un dashboard vacío. Hoy el shell web es público y no tiene sesión, logout ni convención para consumir endpoints del API.

## Scope

### In Scope
- Agregar pantalla `/auth/sign-in` con layout auth separado de `_VerticalLayout`.
- Implementar login funcional contra `POST /api/v1/auth/login` y logout desde `SGV.Web`.
- Redirigir login exitoso a un dashboard inicial vacío.
- Hacer que `src/SGV.Web/SGV.Web.csproj` consuma `src/SGV.Api/SGV.Api.csproj` para reutilizar contratos/convenciones del API.
- Definir una centralización de endpoints del API para evitar rutas dispersas en el frontend.

### Non-goals / Out of Scope
- Registro de cuenta, forgot password, reset password y recuperación de credenciales.
- Dashboard funcional, módulos de negocio o refresh token.
- Cambios de reglas de negocio, persistencia o contrato del endpoint de login.

## Capabilities

### New Capabilities
- `sgv-web-authentication`: Login/logout de `SGV.Web`, sesión web, dashboard inicial y consumo centralizado de endpoints de autenticación.

### Modified Capabilities
- `sgv-web-shell`: deja de ser exclusivamente anónimo y pasa a admitir páginas/auth flow sin mezclar el layout shell con el layout de autenticación.

## Approach

Usar el enfoque “Razor Page + login contra API + estado de sesión en SGV.Web”. `SGV.Web` llamará al API mediante un cliente centralizado; las rutas del API deberán vivir en una ubicación única reutilizable desde la referencia a `SGV.Api`, sin duplicar strings en PageModels. El JWT seguirá siendo responsabilidad del backend, pero la experiencia web se expondrá como sesión/cookie del frontend con logout explícito.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/SGV.Web.csproj` | Modified | Referencia a `SGV.Api`. |
| `src/SGV.Web/Pages/Auth/` | New | Login y logout UX. |
| `src/SGV.Web/Pages/Shared/` | Modified | Layout auth y dashboard vacío. |
| `src/SGV.Web/Program.cs`, `appsettings*.json` | Modified | Auth, cliente HTTP y configuración del API. |
| `src/SGV.Api/` | Modified | Punto central de rutas/contratos consumibles por web. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Acoplar `SGV.Web` a detalles no deseados de `SGV.Api` | Medium | Reutilizar solo contratos/constantes de integración. |
| Romper el spec actual del shell público | High | Delta explícito sobre `sgv-web-shell`. |
| Manejo inseguro del JWT en UI | Medium | Mantener token fuera de scripts/UI y exponer sesión web controlada. |

## Rollback Plan

Revertir cambios en `SGV.Web` y la centralización de endpoints en `SGV.Api`; el shell vuelve a acceso público sin login. No requiere rollback de base de datos.

## Dependencies

- Endpoint existente `POST /api/v1/auth/login`.
- SDK .NET 10 y pipeline Razor Pages actual.

## Success Criteria

- [ ] Un usuario válido puede iniciar sesión desde `SGV.Web` y llegar a un dashboard vacío.
- [ ] El usuario autenticado puede cerrar sesión sin conservar acceso.
- [ ] Las rutas del API usadas por web quedan centralizadas en un único punto.
- [ ] La propuesta no introduce registro/forgot-password en este corte.
