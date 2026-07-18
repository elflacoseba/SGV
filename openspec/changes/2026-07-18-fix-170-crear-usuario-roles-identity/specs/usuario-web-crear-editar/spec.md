# Delta Spec: 2026-07-18-fix-170-crear-usuario-roles-identity

## Purpose

Corrige dos bugs del flujo de alta web de usuarios (`/seguridad/usuarios/crear`):
(1) el campo `Roles` se renderiza como checkboxes múltiples cuando el dominio exige asignación 1:1 usuario↔rol — pasa a renderizarse como un `<select>` único con placeholder obligatorio;
(2) los `IdentityError` emitidos por la política de contraseña y por validaciones de unicidad/formato llegan al cliente en inglés — pasan a localizarse al español antes de salir del gateway.
La edición de usuario no cambia de comportamiento: sigue exponiendo checkboxes multi-rol.

## ADDED Requirements

### Requirement: REQ-UCE-11 Selector único de rol en alta con selección obligatoria

El formulario de alta de usuario (`GET/POST /seguridad/usuarios/crear`) MUST renderizar el campo `Roles` como un único `<select name="Input.Roles">` cuyo primer `<option>` tenga `value=""` y texto `-- Seleccione un rol --` (placeholder obligatorio que `asp-for="Input.Roles"` resuelve como `string`). El POST sin valor en `Input.Roles` MUST ser rechazado por `ModelState` antes de invocar la API, mostrando `Debe seleccionar un rol.` sobre el campo. Tras 400/409 del API, el formulario re-renderizado MUST preservar la selección vigente en el `<select>`. La edición (`/seguridad/usuarios/editar/{id`) MUST seguir renderizando el campo como checkboxes multi-rol sin cambios.

#### Scenario: GET Crear renderiza `<select>` único con placeholder obligatorio

- **DADO** un `Administrador` autenticado y al menos un rol del catálogo fijo disponible
- **CUANDO** solicita `GET /seguridad/usuarios/crear`
- **ENTONCES** MUST existir exactamente un `<select name="Input.Roles">`
- **Y** MUST existir dentro un `<option value="">-- Seleccione un rol --</option>`
- **Y** MUST haber un `<option>` por cada rol del catálogo fijo (`Administrador`, `GestorVacantes`, `Consultor`).

#### Scenario: GET Editar conserva checkboxes multi-rol

- **DADO** un `Administrador` autenticado
- **CUANDO** solicita `GET /seguridad/usuarios/editar/{id}`
- **ENTONCES** MUST existir `<input type="checkbox" name="Input.Roles" value="...">` por cada rol del catálogo fijo
- **Y** MUST NOT existir un `<select name="Input.Roles">`.

#### Scenario: POST alta sin rol es rechazado antes de invocar la API

- **DADO** un `Administrador` enviando el alta con `Input.Roles` ausente o vacío
- **CUANDO** pulsa `Guardar`
- **ENTONCES** `ModelState` MUST ser inválido con el mensaje `Debe seleccionar un rol.` ligado al campo `Input.Roles`
- **Y** MUST NOT invocarse `POST /api/v1/usuarios`.

#### Scenario: POST alta con un rol envía un único elemento a la API

- **DADO** un `Administrador` con el resto del formulario válido y `Input.Roles` con un único rol del catálogo
- **CUANDO** pulsa `Guardar`
- **ENTONCES** la solicitud a `POST /api/v1/usuarios` MUST contener `Roles` con exactamente un elemento
- **Y** MUST NOT contener marcas de checkbox adicionales fuera del binding.

#### Scenario: Tras 400/409 el rol seleccionado se preserva en el `<select>`

- **DADO** un `POST` de alta con un rol seleccionado y datos que producen `400` o `409` del API
- **CUANDO** el formulario se re-renderiza con el error de campo
- **ENTONCES** el `<select name="Input.Roles">` MUST tener el `<option>` del rol seleccionado con atributo `selected`
- **Y** MUST preservarse el resto del formulario (`UserName`, `Email`, `PersonaId`).

## MODIFIED Requirements

No aplica. El requisito vigente `REQ-UCE-07 Catálogo fijo de roles seleccionable` describe el catálogo invariante (`Administrador`, `GestorVacantes`, `Consultor`) y sigue vigente tal cual; el nuevo requisito `REQ-UCE-11` agrega el modo de render `<select>` con selección obligatoria para el alta sin alterar el catálogo ni el contrato de la API. La validación genérica de "al menos un rol" vigente en `REQ-UCE-03` se concreta con el mensaje específico `Debe seleccionar un rol.` que ahora vive en `REQ-UCE-11`.
