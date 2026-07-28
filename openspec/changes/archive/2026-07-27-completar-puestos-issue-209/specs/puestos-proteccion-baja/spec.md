# Especificación de protección de baja de puestos

## Propósito

Delta sobre `openspec/specs/puesto-management/spec.md`: completa la baja lógica de puestos con la guarda ya aplicada conceptualmente a Cargos y Unidades Organizativas.

## MODIFIED Requirements

## REQ-PTO-010 — Baja protegida por ocupaciones vigentes

CUANDO un administrador solicita desactivar un puesto, el sistema SHALL consultar primero si existen ocupaciones activas asociadas. Si existen, SHALL devolver un conflicto estable con código `PuestoConOcupacionesActivas`, no SHALL mutar el puesto y la API SHALL responder 409 ProblemDetails. Si no existen, SHALL conservar la baja exitosa 204; si el puesto no existe, SHALL conservar 404. (Previously: la baja de puestos podía desactivar sin comprobar ocupaciones vigentes.)

#### Scenario: Ocupaciones activas bloquean la baja
- GIVEN un puesto activo con al menos una ocupación vigente
- WHEN un administrador solicita `DELETE /api/v1/puestos/{id}`
- THEN responde 409 con código `PuestoConOcupacionesActivas`
- AND el puesto permanece activo.

#### Scenario: Puesto sin ocupaciones se desactiva
- GIVEN un puesto activo sin ocupaciones vigentes
- WHEN un administrador solicita su baja
- THEN responde 204 y el puesto queda inactivo.

#### Scenario: Puesto inexistente
- GIVEN un identificador que no corresponde a un puesto activo
- WHEN un administrador solicita su baja
- THEN responde 404 y no se modifica ningún puesto.

#### Scenario: Usuario sin autorización
- GIVEN un usuario autenticado sin rol Administrador
- WHEN solicita la baja
- THEN responde 403 y no se consulta ni modifica el puesto.

**Referencia:** el patrón de segmentación y metadatos de Cargos se define en `cargo-management`; la taxonomía de errores y el mapeo ProblemDetails se rigen por `commandresult-error-taxonomy` y `web-apiclient-transport-contract`.
