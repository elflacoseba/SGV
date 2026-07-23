function wireSkillManagement(root, swal) {
    if (!root) {
        return;
    }

    root.querySelectorAll('[data-skill-management-row]').forEach(function (row) {
        var updateForm = row.querySelector('[data-skill-update-form]');
        var editButton = row.querySelector('[data-skill-edit-button]');
        var saveButton = row.querySelector('[data-skill-save-button]');
        var deleteButton = row.querySelector('[data-skill-delete-button]');
        var deleteForm = deleteButton ? deleteButton.closest('form') : null;

        if (updateForm && editButton && saveButton) {
            editButton.addEventListener('click', function () {
                updateForm.querySelectorAll('[data-skill-editable]').forEach(function (control) {
                    control.disabled = false;
                });
                editButton.classList.add('d-none');
                saveButton.classList.remove('d-none');

                if (!saveButton.getAttribute('form')) {
                    if (!updateForm.id) {
                        updateForm.id = 'skill-update-' + Math.random().toString(36).slice(2);
                    }
                    saveButton.setAttribute('form', updateForm.id);
                }
            });
        }

        if (!deleteButton || !deleteForm || !swal || typeof swal.fire !== 'function') {
            return;
        }

        deleteButton.addEventListener('click', function (event) {
            event.preventDefault();
            swal.fire({
                title: '¿Quitar habilidad?',
                text: 'Se quitará la habilidad ' + (deleteButton.getAttribute('data-skill-item-name') || '') + '.',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Sí, quitar',
                cancelButtonText: 'Cancelar',
                reverseButtons: true,
                focusCancel: true
            }).then(function (result) {
                if (!result.isConfirmed) {
                    return;
                }

                if (typeof deleteForm.requestSubmit === 'function') {
                    try {
                        deleteForm.requestSubmit(deleteButton);
                        return;
                    } catch (submitterError) {
                        // Defensa: si el submitter no es un submit button
                        // válido (e.g. type="button"), requestSubmit lanza
                        // NotSupportedError por la spec HTML. Caemos al
                        // submit() genérico del form, cuya action por
                        // defecto ya apunta al handler Quitar tras el
                        // refactor de markup (form Quitar vive aparte del
                        // form Actualizar con asp-page-handler="Quitar").
                    }
                }

                deleteForm.submit();
            });
        });
    });
}

if (typeof window !== 'undefined') {
    window.wireSkillManagement = wireSkillManagement;

    if (window.document) {
        wireSkillManagement(window.document, window.Swal);
    }
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports = { wireSkillManagement: wireSkillManagement };
}
