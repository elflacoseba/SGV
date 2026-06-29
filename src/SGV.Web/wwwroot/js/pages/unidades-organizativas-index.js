function wireUnidadOrganizativaDeleteConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-uo-delete-form]').forEach(function (form) {
        var button = form.querySelector('[data-uo-delete-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Eliminar unidad organizativa?',
                text: 'Se eliminará la unidad organizativa ' + (button.getAttribute('data-uo-item-name') || '') + ' (' + (button.getAttribute('data-uo-item-code') || '') + ').',
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

function wireUnidadOrganizativaReactivateConfirmation(root, swal) {
    if (!root || !swal || typeof swal.fire !== 'function') {
        return;
    }

    root.querySelectorAll('[data-uo-reactivate-form]').forEach(function (form) {
        var button = form.querySelector('[data-uo-reactivate-button]');
        if (!button) {
            return;
        }

        button.addEventListener('click', function (event) {
            event.preventDefault();

            swal.fire({
                title: '¿Reactivar unidad organizativa?',
                text: 'Se reactivará la unidad organizativa ' + (button.getAttribute('data-uo-item-name') || '') + ' (' + (button.getAttribute('data-uo-item-code') || '') + ').',
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
    window.wireUnidadOrganizativaDeleteConfirmation = wireUnidadOrganizativaDeleteConfirmation;
    window.wireUnidadOrganizativaReactivateConfirmation = wireUnidadOrganizativaReactivateConfirmation;

    if (window.document && window.Swal) {
        wireUnidadOrganizativaDeleteConfirmation(window.document, window.Swal);
        wireUnidadOrganizativaReactivateConfirmation(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wireUnidadOrganizativaDeleteConfirmation };
}
