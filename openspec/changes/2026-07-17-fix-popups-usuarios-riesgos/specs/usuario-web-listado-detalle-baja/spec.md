# Delta: Eliminación física de usuarios con SweetAlert2

> Reemplaza solo REQ-ULD-05; los demás requisitos canónicos permanecen. Referencias previas: `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:189-202`, `Index.cshtml:254-280` e `Index.cshtml:344-359`.

## MODIFIED Requirements

### Requirement: REQ-ULD-05 Eliminación física confirmada con modal irreversible (MODIFIED)

`?handler=Delete` MUST exigir rol `Administrador` e invocar `DELETE /api/v1/usuarios/{id}` únicamente después de confirmación. `wireUsuarioDeleteConfirmation` en `src/SGV.Web/wwwroot/js/pages/usuarios-index.js:1` MUST abrir `Swal.fire` con título `Eliminar usuario`, texto `Esta acción eliminará este usuario de forma permanente. No se puede deshacer.`, icono `warning`, cancelación visible, botones `Eliminar definitivamente`/`Cancelar` y `reverseButtons: true`; MUST enviar solo cuando `result.isConfirmed === true`. Éxitos y rechazos MUST conservar PRG y feedback.

(Previously: `#confirm-delete-modal` Bootstrap difería el submit.)

#### Scenario: Click abre confirmación irreversible
- **DADO** un administrador ante una fila activa ajena
- **CUANDO** pulsa `Eliminar`
- **ENTONCES** MUST abrirse SweetAlert2 con la advertencia y acciones especificadas.

#### Scenario: Confirmar elimina y redirige
- **DADO** la alerta abierta para un usuario eliminable
- **CUANDO** pulsa `Eliminar definitivamente` y la API responde `204`
- **ENTONCES** MUST emitirse un POST `?handler=Delete`, guardarse `TempData` `El usuario se eliminó correctamente.` y redirigirse a `status=activas`.

#### Scenario: Descartar no elimina
- **DADO** la alerta abierta
- **CUANDO** pulsa `Cancelar`, `Esc` o backdrop
- **ENTONCES** MUST NOT enviarse el form ni invocarse la API.

#### Scenario: La fila propia oculta Eliminar
- **DADO** un administrador autenticado
- **CUANDO** se renderiza su fila
- **ENTONCES** `data-usuario-delete-button` MUST NOT existir.

#### Scenario: La confirmación no expone PII
- **DADO** una fila con username, email, nombres y apellidos
- **CUANDO** abre la alerta
- **ENTONCES** título/texto MUST usar solo `este usuario` y la advertencia general.

#### Scenario: AutoEliminacion conserva feedback
- **DADO** un POST manual sobre el usuario autenticado
- **CUANDO** backend rechaza con `403 AutoEliminacion`
- **ENTONCES** el PRG MUST publicar en `TempData` `No puede eliminar su propio usuario.` sin eliminar datos.
