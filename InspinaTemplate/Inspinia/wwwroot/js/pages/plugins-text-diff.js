/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Plugins Text Diff
 */

document.addEventListener("DOMContentLoaded", () => {
    const originalInput = document.querySelector(".diff-original")
    const changedInput = document.querySelector(".diff-changed")
    const diffOutput = document.querySelector(".diff-output")

    if (!originalInput) {
        console.error("Plugins Text Diff: .diff-original input not found.")
        return
    }
    if (!changedInput) {
        console.error("Plugins Text Diff: .diff-changed input not found.")
        return
    }
    if (!diffOutput) {
        console.error("Plugins Text Diff: .diff-output element not found.")
        return
    }

    new (class TextDiff {
        constructor() {
            this.bindEvents()
            this.render()
        }

        bindEvents() {
            originalInput.addEventListener("input", () => this.render())
            changedInput.addEventListener("input", () => this.render())
        }

        render() {
            const originalText = originalInput.value
            const changedText = changedInput.value
            const diff = Diff.diffWords(originalText, changedText)

            const fragment = document.createDocumentFragment()
            diff.forEach((part) => {
                let el
                if (part.added) {
                    el = document.createElement("ins")
                } else if (part.removed) {
                    el = document.createElement("del")
                } else {
                    el = document.createElement("span")
                }
                el.textContent = part.value
                fragment.appendChild(el)
            })

            diffOutput.innerHTML = ""
            diffOutput.appendChild(fragment)
        }
    })()
})
