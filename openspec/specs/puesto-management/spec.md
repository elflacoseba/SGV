# Especificación de Gestión de Puestos

## Propósito

Contrato durable de la API HTTP de `Puestos`: lectura autenticada del catálogo maestro, mutaciones restringidas al rol `Administrador`, y diferenciación 401/403 entre acceso anónimo y autenticado sin permisos. Esta capacidad se mantiene estable frente a cambios futuros de UI, persistencia o integración; los detalles de UI, validaciones de payload y reglas de unicidad viven en capacidades específicas (`puesto-web-listado-detalle-baja`, `puesto-web-crear-editar` y los delta specs que correspondan).

## Requisitos

### Requisito: Autorización de endpoints de puestos

`PuestosController` DEBE requerir autenticación. `GET /api/v1/puestos` y `GET /api/v1/puestos/{id}` DEBEN permitir el acceso a cualquier usuario autenticado y DEBEN responder `2xx` con el contrato de lectura vigente. `POST /api/v1/puestos`, `PUT /api/v1/puestos/{id}`, `DELETE /api/v1/puestos/{id}` y `PATCH /api/v1/puestos/{id}/reactivar` DEBEN requerir el rol `Administrador`; con payload válido y rol correcto, DEBEN conservar sus contratos `2xx` vigentes.

#### Escenario: Lectura autenticada exitosa

- **DADO** un usuario autenticado
- **CUANDO** solicita `GET /api/v1/puestos` o `GET /api/v1/puestos/{id}`
- **ENTONCES** la API DEBE responder `2xx` con el contrato de lectura vigente.

#### Escenario: Acceso anónimo rechazado

- **DADO** un cliente sin credenciales
- **CUANDO** solicita un `GET` o una mutación de `PuestosController`
- **ENTONCES** la API DEBE responder `401 Unauthorized`.

#### Escenario: Mutación protegida por rol administrador

- **DADO** una solicitud válida de mutación sobre puestos
- **CUANDO** la ejecuta un usuario autenticado sin rol `Administrador`
- **ENTONCES** la API DEBE responder `403 Forbidden`
- **Y**, si la ejecuta un `Administrador`, DEBE responder `2xx` con el contrato vigente.

#### Source

- `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/specs/puesto-management/spec.md:5-26`
- `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/proposal.md:8,30-34,42`
- `openspec/changes/archive/2026-07-08-implementa-edicion-puesto-frontend/design.md:5,11-12,75-78`

#### Verification

- API (anónimo): `GetAll_WithoutCredentials_ReturnsUnauthorized`, `GetById_WithoutCredentials_ReturnsUnauthorized`, `[Theory] Mutation_WithoutCredentials_ReturnsUnauthorized` (POST/PUT/DELETE/PATCH).
- API (no-admin): `Create/Update/Delete/Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden`.
- API (atributo presente): `Controller_HasAuthorizeAttribute`.
- API (admin sigue verde): los tests `2xx` existentes en `PuestosControllerTests` corren con `factory.CreateAdminClient()` y permanecen `PASS`.
