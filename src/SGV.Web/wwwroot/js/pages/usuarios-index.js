// PR 2 — `2026-07-17-fix-popups-usuarios-riesgos`.
// Espejo estructural de cargos-index.js y puestos-index.js. Reemplaza
// los modales Bootstrap (#confirm-bloquear-modal / #confirm-desbloquear-modal
// / #confirm-delete-modal) por SweetAlert2 manteniendo el contrato de
// data-attributes (data-usuario-*-form + data-usuario-*-button) y
// disparando el submit solo cuando result.isConfirmed === true.
//
// Escenario (REQ-UCB-01, REQ-UCB-02, REQ-ULD-05):
//   * El botón data-usuario-*-button tiene type="button" (no submit).
//   * Al hacer click, preventDefault() evita el submit nativo.
//   * swal.fire(...) muestra la alerta con foco en Cancelar (focusCancel).
//   * Si el usuario confirma → form.requestSubmit(button) para preservar
//     antiforgery + PRG + TempData feedback.
//   * Si cancela o descarta (Esc, backdrop) → no se envía el form.

function wireUsuarioBloquearConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-usuario-bloquear-form]').forEach(function (form) {
        var button = form.querySelector('[data-usuario-bloquear-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: 'Bloquear usuario',
                text: 'Esta acción afecta este usuario. ¿Desea continuar?',
                icon: 'warning',
                showCancelButton: true,
                showCloseButton: false,
                confirmButtonText: 'Bloquear',
                cancelButtonText: 'Cancelar',
                reverseButtons: true,
                focusCancel: true,
                allowEscapeKey: true,
                allowOutsideClick: true,
                customClass: {
                    confirmButton: 'btn btn-secondary',
                    cancelButton: 'btn btn-light'
                }
            }).then(function (result) {
                if (result.isConfirmed) {
                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit(button);
                        return;
                    }

                    form.submit();
                }
            });
        });
    });
}

function wireUsuarioDesbloquearConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-usuario-desbloquear-form]').forEach(function (form) {
        var button = form.querySelector('[data-usuario-desbloquear-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: 'Desbloquear usuario',
                text: 'Esta acción afecta este usuario. ¿Desea continuar?',
                icon: 'warning',
                showCancelButton: true,
                showCloseButton: false,
                confirmButtonText: 'Desbloquear',
                cancelButtonText: 'Cancelar',
                reverseButtons: true,
                focusCancel: true,
                allowEscapeKey: true,
                allowOutsideClick: true,
                customClass: {
                    confirmButton: 'btn btn-success',
                    cancelButton: 'btn btn-light'
                }
            }).then(function (result) {
                if (result.isConfirmed) {
                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit(button);
                        return;
                    }

                    form.submit();
                }
            });
        });
    });
}

function wireUsuarioDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-usuario-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-usuario-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: 'Eliminar usuario',
                text: 'Esta acción eliminará este usuario de forma permanente. No se puede deshacer.',
                icon: 'warning',
                showCancelButton: true,
                showCloseButton: false,
                confirmButtonText: 'Eliminar definitivamente',
                cancelButtonText: 'Cancelar',
                reverseButtons: true,
                focusCancel: true,
                allowEscapeKey: true,
                allowOutsideClick: true,
                customClass: {
                    confirmButton: 'btn btn-danger',
                    cancelButton: 'btn btn-light'
                }
            }).then(function (result) {
                if (result.isConfirmed) {
                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit(button);
                        return;
                    }

                    form.submit();
                }
            });
        });
    });
}

// Helper agregado: invoca las 3 funciones sobre el mismo root. Cada una
// hace early-return si no encuentra los selectores esperados, así que es
// seguro llamarlo sobre Index (que tiene los 3 forms) o Details (que tiene
// sólo Bloquear o Desbloquear + Eliminar según el estado del usuario).
function wireUsuarioActions(root, swal) {
    wireUsuarioBloquearConfirmation(root, swal);
    wireUsuarioDesbloquearConfirmation(root, swal);
    wireUsuarioDeleteConfirmation(root, swal);
}

if (typeof window !== 'undefined') {
    window.wireUsuarioBloquearConfirmation = wireUsuarioBloquearConfirmation;
    window.wireUsuarioDesbloquearConfirmation = wireUsuarioDesbloquearConfirmation;
    window.wireUsuarioDeleteConfirmation = wireUsuarioDeleteConfirmation;
    window.wireUsuarioActions = wireUsuarioActions;

    if (window.document && window.Swal) {
        wireUsuarioActions(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        wireUsuarioBloquearConfirmation: wireUsuarioBloquearConfirmation,
        wireUsuarioDesbloquearConfirmation: wireUsuarioDesbloquearConfirmation,
        wireUsuarioDeleteConfirmation: wireUsuarioDeleteConfirmation,
        wireUsuarioActions: wireUsuarioActions
    };
}