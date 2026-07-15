function wirePersonaDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-persona-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-persona-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            var name = button.getAttribute('data-persona-item-name') || '';
            var legajo = button.getAttribute('data-persona-item-legajo') || '';

            swal.fire({
                title: '¿Eliminar persona?',
                text: 'Se eliminará la persona ' + name + (legajo ? ' (legajo ' + legajo + ')' : '') + '.',
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

function wirePersonaReactivateConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-persona-reactivate-form]').forEach(function (form) {
        var button = form.querySelector('[data-persona-reactivate-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            var name = button.getAttribute('data-persona-item-name') || '';
            var legajo = button.getAttribute('data-persona-item-legajo') || '';

            swal.fire({
                title: '¿Reactivar persona?',
                text: 'Se reactivará la persona ' + name + (legajo ? ' (legajo ' + legajo + ')' : '') + '.',
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
    window.wirePersonaDeleteConfirmation = wirePersonaDeleteConfirmation;
    window.wirePersonaReactivateConfirmation = wirePersonaReactivateConfirmation;

    if (window.document && window.Swal) {
        wirePersonaDeleteConfirmation(window.document, window.Swal);
        wirePersonaReactivateConfirmation(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wirePersonaDeleteConfirmation, wirePersonaReactivateConfirmation };
}