/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Apps Email
 */

document.addEventListener("DOMContentLoaded", function () {
    const selectAllCheckbox = document.getElementById("select-all-email")
    const emailItemCheckboxes = document.querySelectorAll(".email-item-check")

    if (!selectAllCheckbox || emailItemCheckboxes.length === 0) {
        console.error("Apps Email: Required elements not found.")
        return
    }

    selectAllCheckbox.addEventListener("change", function () {
        emailItemCheckboxes.forEach((checkbox) => {
            checkbox.checked = this.checked
        })
    })
})
