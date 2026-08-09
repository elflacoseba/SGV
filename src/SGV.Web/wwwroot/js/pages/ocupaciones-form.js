/**
 * Ocupaciones — Create y Edit (Pages/Organizacion/Ocupaciones/Create.cshtml
 * y Edit.cshtml).
 *
 * Compartido por ambas vistas porque reusan el partial `_Form.cshtml`,
 * donde el hidden `Input_PersonaId` tiene el mismo id y se vincula con
 * la persona vía el modal `usuario-persona-buscador.js`.
 *
 * Issue #266: al guardar el form sin una persona seleccionada, el
 * `<span asp-validation-for="Input.PersonaId">` muestra
 * "Debe escoger una persona". Al elegir (o quitar) una persona desde el
 * modal `usuario-persona-buscador.js`, el hidden `Input_PersonaId`
 * dispara `change` (ver `usuario-persona-buscador.js:84,335`) pero el
 * span field-validation y el alert-danger superior quedaban visibles,
 * sugiriendo que la persona nueva seguía inválida.
 *
 * Este handler limpia tres lugares al primer `change` del hidden:
 *   1. El alert-danger superior (`Model.ErrorMessage`).
 *   2. El `asp-validation-summary="ModelOnly"` (errores model-level).
 *   3. El span field-validation de `Input.PersonaId`.
 * Es estrictamente cosmético — el siguiente submit reevalúa las
 * reglas en el backend. No toca el estado del modelo.
 */
(function () {
    "use strict";

    function clearTopAlert() {
        // Top alert rendered by Create.cshtml:16-19 (`Model.ErrorMessage`).
        document
            .querySelectorAll(".alert.alert-danger[role='alert']")
            .forEach(function (el) {
                el.style.display = "none";
            });
    }

    function clearValidationSummary() {
        // El tag helper `asp-validation-summary` aplica la clase
        // `validation-summary-errors` cuando hay errores model-level
        // y `validation-summary-valid` cuando no los hay. Vaciamos
        // contenido y swapeamos la clase para que el CSS propio del
        // template (display:none sobre `.validation-summary-valid`)
        // entre en efecto.
        var summary = document.querySelector(".validation-summary-errors");
        if (!summary) return;
        summary.innerHTML = "";
        summary.classList.remove("validation-summary-errors");
        summary.classList.add("validation-summary-valid");
    }

    function clearPersonaFieldValidation() {
        // El tag helper `asp-validation-for="Input.PersonaId"` renderea
        // un `<span class="text-danger field-validation-..."
        // data-valmsg-for="Input.PersonaId" data-valmsg-replace="true">`.
        // `field-validation-error` se aplica cuando hay error y
        // `field-validation-valid` cuando no. El CSS del template
        // oculta los `field-validation-valid` por default.
        var span = document.querySelector('span[data-valmsg-for="Input.PersonaId"]');
        if (!span) return;
        span.textContent = "";
        span.classList.remove("field-validation-error");
        span.classList.add("field-validation-valid");
    }

    function init() {
        var personaHidden = document.getElementById("Input_PersonaId");
        if (!personaHidden) return;

        personaHidden.addEventListener("change", function () {
            clearTopAlert();
            clearValidationSummary();
            clearPersonaFieldValidation();
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
