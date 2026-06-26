/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Form Fileupload
 */

class FileUpload {
    constructor() {
        this.init()
    }

    init() {
        if (typeof Dropzone === "undefined") {
            console.warn("Dropzone is not loaded.")
            return
        }

        Dropzone.autoDiscover = false

        const dropzones = document.querySelectorAll('[data-plugin="dropzone"]')
        if (dropzones) {
            dropzones.forEach((dropzoneEl) => {
                const actionUrl = dropzoneEl.getAttribute("action") || "/"
                const previewContainer = dropzoneEl.dataset.previewsContainer
                const uploadPreviewTemplate = dropzoneEl.dataset.uploadPreviewTemplate

                const options = {
                    url: actionUrl,
                    acceptedFiles: "image/*",
                }

                if (previewContainer) {
                    options.previewsContainer = previewContainer
                }

                if (uploadPreviewTemplate) {
                    const template = document.querySelector(uploadPreviewTemplate)
                    if (template) {
                        options.previewTemplate = template.innerHTML
                    }
                }

                try {
                    new Dropzone(dropzoneEl, options)
                } catch (e) {
                    console.error("Dropzone initialization failed:", e)
                }
            })
        }
    }
}

document.addEventListener("DOMContentLoaded", () => {
    new FileUpload()

    if (typeof FilePond !== "undefined") {
        // FilePond Plugins
        try {
            FilePond.registerPlugin(FilePondPluginImagePreview)
        } catch (e) {
            console.warn("FilePond plugins registration failed:", e)
        }

        // multiple-file inputs
        const multiInputs = Array.from(document.querySelectorAll("input.filepond-input-multiple"))
        if (multiInputs.length > 0) {
            multiInputs.forEach((input) => {
                FilePond.create(input)
            })
        } else {
            console.warn("FilePond multiple-file inputs not found.")
        }

        // circle-style FilePond inputs
        const circleInputs = Array.from(document.querySelectorAll("input.filepond-input-circle"))
        if (circleInputs.length > 0) {
            circleInputs.forEach((input) => {
                FilePond.create(input, {
                    imageCropAspectRatio: "1:1",
                    imageResizeTargetWidth: 200,
                    imageResizeTargetHeight: 200,
                    stylePanelLayout: "compact circle",
                    styleLoadIndicatorPosition: "center bottom",
                    styleProgressIndicatorPosition: "right bottom",
                    styleButtonRemoveItemPosition: "left bottom",
                    styleButtonProcessItemPosition: "right bottom",
                    allowImagePreview: true,
                    imagePreviewHeight: 100,
                    labelIdle: `<svg  xmlns="http://www.w3.org/2000/svg"  width="32"  height="32"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round" class="text-muted"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M5 7h1a2 2 0 0 0 2 -2a1 1 0 0 1 1 -1h6a1 1 0 0 1 1 1a2 2 0 0 0 2 2h1a2 2 0 0 1 2 2v9a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-9a2 2 0 0 1 2 -2" /><path d="M9 13a3 3 0 1 0 6 0a3 3 0 0 0 -6 0" /></svg>`,
                })
            })
        } else {
            console.warn("FilePond circle-style inputs not found.")
        }
    } else {
        console.warn("FilePond is not loaded.")
    }
})
