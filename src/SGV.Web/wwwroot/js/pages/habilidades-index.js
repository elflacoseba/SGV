function wireHabilidadDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-habilidad-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-habilidad-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Eliminar habilidad?',
                text: 'Se eliminará la habilidad ' + (button.getAttribute('data-habilidad-item-name') || '') + ' (' + (button.getAttribute('data-habilidad-item-code') || '') + ').',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Sí, eliminar',
                cancelButtonText: 'Cancelar',
                reverseButtons: true
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

function wireHabilidadReactivateConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-habilidad-reactivate-form]').forEach(function (form) {
        var button = form.querySelector('[data-habilidad-reactivate-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Reactivar habilidad?',
                text: 'Se reactivará la habilidad ' + (button.getAttribute('data-habilidad-item-name') || '') + ' (' + (button.getAttribute('data-habilidad-item-code') || '') + ').',
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Sí, reactivar',
                cancelButtonText: 'Cancelar',
                reverseButtons: true
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

if (typeof window !== 'undefined') {
    window.wireHabilidadDeleteConfirmation = wireHabilidadDeleteConfirmation;
    window.wireHabilidadReactivateConfirmation = wireHabilidadReactivateConfirmation;

    if (window.document && window.Swal) {
        wireHabilidadDeleteConfirmation(window.document, window.Swal);
        wireHabilidadReactivateConfirmation(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wireHabilidadDeleteConfirmation, wireHabilidadReactivateConfirmation };
}