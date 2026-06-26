/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Plugins Clipboard
 */

const elements = document.querySelectorAll("[data-clipboard-target]")

if (elements && elements.length > 0) {
    new ClipboardJS(elements)
}
