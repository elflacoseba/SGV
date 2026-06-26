/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Plugins Loading Button
 */

function findElements(selector) {
    const elements = document.querySelectorAll(selector)
    if (elements.length === 0) {
        console.error(`Plugins Loading Button: Elements not found for selector "${selector}".`)
    }
    return elements
}

// Bind basic Ladda buttons
function bindBasicButtons(selector, timeout = 2000) {
    const buttons = findElements(selector)
    buttons.forEach((btn) => Ladda.bind(btn, { timeout }))
}

// Bind progress simulation buttons
function bindProgressButtons(selector) {
    const buttons = findElements(selector)
    buttons.forEach((btn) => {
        Ladda.bind(btn, {
            callback: function (instance) {
                let progress = 0
                const interval = setInterval(() => {
                    progress = Math.min(progress + Math.random() * 0.1, 1)
                    instance.setProgress(progress)

                    if (progress === 1) {
                        instance.stop()
                        clearInterval(interval)
                    }
                }, 200)
            },
        })
    })
}

// Bind manually controlled Ladda button
function bindManualButton(selector, stopDelay = 12000) {
    const btn = findElements(selector)
    if (btn.length > 0) {
        const instance = Ladda.create(btn[0])

        btn[0].addEventListener("click", () => {
            instance.start()

            setTimeout(() => {
                instance.stop()
            }, stopDelay)
        })
    }
}

bindBasicButtons(".ladda-button:not(.progress-demo .ladda-button):not(.ladda-button-demo)")
bindProgressButtons(".progress-demo .ladda-button")
bindManualButton(".ladda-button-demo")
