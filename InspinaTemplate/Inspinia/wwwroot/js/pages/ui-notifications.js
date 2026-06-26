/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): UI Notifications
 */

const toastTrigger = document.querySelector("#liveToastBtn")
const toastLiveExample = document.querySelector("#liveToast")

if (!toastTrigger || !toastLiveExample) {
    console.error("UI Notifications: Elements not found.")
}

const toastBootstrap = bootstrap.Toast.getOrCreateInstance(toastLiveExample)
toastTrigger.addEventListener("click", () => {
    toastBootstrap.show()
})
