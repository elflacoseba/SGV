# Especificación de Gestión Web de Persona-Habilidades

## Purpose

Flujo Razor `SGV.Web/Pages/Personas/Habilidades.cshtml` para que un `Administrador` liste, asigne, modifique el nivel y quite habilidades de una `Persona` con feedback PRG, paridad con `cargo-skill-ui-tabla-editable` y bloqueando gestión sobre personas inactivas. Los wire-types `PersonaSkill*` migran a `SGV.Contracts.Personas.*` preservando JSON; los errores PersonaSkill se unifican bajo `ErrorCategoria` (ver delta `commandresult-error-taxonomy`).

## Requirements

### Requirement: Acceso restringido a Administrador

La página `/personas/{id:guid}/habilidades` y sus handlers MUST exigir autenticación y rol `Administrador` (lectura y escritura).

#### Scenario: Sin rol o anónimo

- **DADO** un usuario sin rol `Administrador` o anónimo
- **CUANDO** intenta abrir la página o ejecutar handlers
- **ENTONCES** MUST impedir el acceso (no-admin) o redirigir al sign-in (anónimo).

### Requirement: Listado, asignación y baja de habilidades

La página MUST consultar el backend al abrirse e hidratar la grilla con las asociaciones activas. La fila MUST permitir `PUT /api/v1/personas/{personaId}/skills/{skillId}` con `{ "nivelId": <Guid> }` y `DELETE /api/v1/personas/{personaId}/skills/{skillId}`. Tras éxito, MUST volver por PRG con `TempData`.

#### Scenario: Listar y reasignar

- **DADO** una Persona activa
- **CUANDO** un `Administrador` abre la página y envía `PUT` para un par `personaId/skillId`
- **ENTONCES** la grilla MUST mostrar una fila por cada asociación (o estado vacío legible), la API MUST persistir el vínculo y la página MUST mostrar la fila tras el redirect.
- **Y** un segundo `PUT` para el mismo par MUST actualizar el nivel sin duplicar la asociación.

#### Scenario: Quitar habilidad

- **DADO** una asociación activa visible
- **CUANDO** el `Administrador` confirma la baja
- **ENTONCES** la página MUST invocar `DELETE` y MUST mostrar mensaje de éxito tras el redirect.

### Requirement: Bloqueo cuando la persona está inactiva

Si la `Persona` está inactiva, la página MUST impedir listar, asignar, modificar o quitar habilidades. El comportamiento UI concreto (redirigir o deshabilitar) lo decide `sdd-design`, pero ninguna escritura MUST llegar al backend.

#### Scenario: Persona inactiva bloquea UI y backend

- **DADO** una Persona con `Activa == false`
- **CUANDO** un `Administrador` abre la página o una mutación llega al backend
- **ENTONCES** la página MUST NO permitir ejecutar asignaciones, modificaciones ni bajas y MUST mostrar mensaje legible de bloqueo.
- **Y** la API MUST responder `404 Not Found` y MUST NOT persistir cambios.

### Requirement: Cliente tipado expone los tres métodos

`IPersonaApiClient` MUST exponer `GetSkillsAsync`, `UpsertSkillAsync` y `DeleteSkillAsync`, delegando la rama no exitosa en `CommandResultMapper`/`DeleteResultMapper` comunes.

#### Scenario: Fake registra invocaciones sin HTTP

- **DADO** un `FakePersonaApiClient` con seed de `PersonaSkillDetailDto`
- **CUANDO** el `PageModel` invoca los tres métodos
- **ENTONCES** cada uno MUST incrementar su contador y el test MUST NOT emitir HTTP.

### Requirement: Manejo de errores recuperables y feedback PRG

La página MUST traducir errores `4xx/5xx` o fallas de transporte a mensajes legibles en español sin exponer stack traces. El feedback MUST fluir por `TempData`.

#### Scenario: Error del backend al cargar o guardar

- **DADO** que la API responde `4xx`, `5xx` o falla el transporte
- **CUANDO** la página intenta cargar o persistir cambios
- **ENTONCES** la UI MUST mostrar un mensaje accionable sin exponer stack traces.

### Requirement: Descubribilidad desde el detalle de Persona

`Pages/Personas/Details.cshtml` MUST exponer un botón `Habilidades` con ícono `ti ti-stars me-1` y `href` hacia `/personas/{id:guid}/habilidades`. Cuando la persona no sea consultable como activa, el botón MUST NOT renderizarse.

#### Scenario: Detalle existente y no consultable

- **DADO** un `Administrador` en el detalle de una Persona
- **CUANDO** la página se renderiza
- **ENTONCES** la barra inferior MUST contener el botón `Habilidades` con `href` al `id` mostrado si la persona está activa, y MUST NOT renderizarlo si no es consultable como activa.

> **Nota**: la spec archivada `persona-skill-query-contract` promete `skillId`/`nivelId` planos en el JSON, pero el wire actual solo expone los anidados `skill` y `nivel`. `sdd-design` debe confirmar el contrato antes de archivar.
