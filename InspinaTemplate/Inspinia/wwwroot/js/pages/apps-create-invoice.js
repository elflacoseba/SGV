/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Create Invoice
 */

function previewImage(event) {
    const input = event.target
    const file = input.files[0]
    const preview = document.getElementById("preview")

    if (file && preview) {
        const reader = new FileReader()
        reader.onload = function (e) {
            preview.src = e.target.result
        }
        reader.readAsDataURL(file)
    }
}
