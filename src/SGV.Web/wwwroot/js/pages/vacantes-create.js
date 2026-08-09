/**
 * Vacantes — Nueva Vacante (Pages/Organizacion/Vacantes/Create.cshtml).
 *
 * Issue #265: cuando el POST devuelve 409 PuestoOcupado / 400 Validation,
 * CreateModel.ApplyFailureAsync vuelca el mensaje al alert-danger superior
 * Y al `asp-validation-summary="ModelOnly"`. Al cambiar el Puesto en el
 * SELECT, ese mensaje quedaba visible y confundía al usuario.
 *
 * Este handler limpia ambos lugares al primer `change` del SELECT
 * `Input.PuestoId`. Es estrictamente cosmético — el siguiente submit
 * vuelve a evaluar la regla en el backend. No toca el estado del modelo.
 */
(function () {
    "use strict";

    function clearTopAlert() {
        // Top alert rendered by Create.cshtml:15-18 (`Model.ErrorMessage`).
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

    function init() {
        var puestoSelect = document.getElementById("Input_PuestoId");
        if (!puestoSelect) return;

        puestoSelect.addEventListener("change", function () {
            clearTopAlert();
            clearValidationSummary();
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
