function wirePuestoDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-puesto-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-puesto-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Eliminar puesto?',
                text: 'Se eliminará el puesto ' + (button.getAttribute('data-puesto-item-name') || '') + ' (' + (button.getAttribute('data-puesto-item-code') || '') + ').',
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

function wirePuestoReactivateConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-puesto-reactivate-form]').forEach(function (form) {
        var button = form.querySelector('[data-puesto-reactivate-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Reactivar puesto?',
                text: 'Se reactivará el puesto ' + (button.getAttribute('data-puesto-item-name') || '') + ' (' + (button.getAttribute('data-puesto-item-code') || '') + ').',
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
    window.wirePuestoDeleteConfirmation = wirePuestoDeleteConfirmation;
    window.wirePuestoReactivateConfirmation = wirePuestoReactivateConfirmation;

    if (window.document && window.Swal) {
        wirePuestoDeleteConfirmation(window.document, window.Swal);
        wirePuestoReactivateConfirmation(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wirePuestoDeleteConfirmation, wirePuestoReactivateConfirmation };
}
