/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): UI Alerts
 */

const alertPlaceholder = document.getElementById("liveAlertPlaceholder")
const alertTrigger = document.getElementById("liveAlertBtn")

if (!alertPlaceholder || !alertTrigger) {
    console.error("UI Alerts: Elements not found.")
}

const appendAlert = (message, type) => {
    const wrapper = document.createElement("div")
    wrapper.innerHTML = [`<div class="alert alert-${type} alert-dismissible" role="alert">`, `   <div>${message}</div>`, '   <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>', "</div>"].join("")

    alertPlaceholder.append(wrapper)
}

alertTrigger.addEventListener("click", () => {
    appendAlert("Nice, you triggered this alert message!", "success")
})
