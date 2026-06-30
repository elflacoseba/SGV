function wireCargoDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-cargo-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-cargo-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Eliminar cargo?',
                text: 'Se eliminará el cargo ' + (button.getAttribute('data-cargo-item-name') || '') + ' (' + (button.getAttribute('data-cargo-item-code') || '') + ').',
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

if (typeof window !== 'undefined') {
    window.wireCargoDeleteConfirmation = wireCargoDeleteConfirmation;

    if (window.document && window.Swal) {
        wireCargoDeleteConfirmation(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wireCargoDeleteConfirmation };
}