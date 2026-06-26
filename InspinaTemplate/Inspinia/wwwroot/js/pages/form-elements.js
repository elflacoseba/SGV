/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Form Elements
 */

document.addEventListener("DOMContentLoaded", function () {
    const checkbox = document.getElementById("checkIndeterminate")
    if (!checkbox) {
        console.error("Form Elements: Checkbox element not found.")
    }
    checkbox.indeterminate = true
})

const passwordInput = document.getElementById("password")
if (!passwordInput) {
    console.error("Form Elements: Password input element not found.")
}

const toggleBtn = document.querySelector(".password-eye")
if (!toggleBtn) {
    console.error("Form Elements: Toggle button element not found.")
}

toggleBtn.addEventListener("click", () => {
    const icons = Array.from(toggleBtn.getElementsByClassName("eye-icon"))
    if (icons.length !== 2) {
        console.error("Form Elements: Icons not found in toggle button.")
    }

    const isPassword = passwordInput.type === "password"
    passwordInput.type = isPassword ? "text" : "password"

    icons.forEach((icon) => {
        icon.classList.toggle("d-none")
        icon.classList.toggle("d-block")
    })
})
