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
                text: 'Se eliminará ' + (button.getAttribute('data-uo-item-name') || 'la unidad seleccionada') + '.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Sí, eliminar',
                cancelButtonText: 'Cancelar',
                reverseButtons: true
            }).then(function (result) {
                if (result.isConfirmed) {
                    form.submit();
                }
            });
        });
    });
}

if (typeof window !== 'undefined') {
    window.wireUnidadOrganizativaDeleteConfirmation = wireUnidadOrganizativaDeleteConfirmation;

    if (window.document && window.Swal) {
        wireUnidadOrganizativaDeleteConfirmation(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wireUnidadOrganizativaDeleteConfirmation };
}
