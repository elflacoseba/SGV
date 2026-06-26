/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Plugins Idle Timer
 */

class IdleDetector {
    constructor(options = {}) {
        this.idleLimit = options.idleLimit || 5
        this.idleTime = 0
        this.wasIdle = false

        this.alertSelector = options.alertSelector || ".idle-alert"
        this.toastIdleId = options.toastIdleId || "liveToast"
        this.toastReturnId = options.toastReturnId || "backToast"

        this.events = options.events || ["mousemove", "keydown", "scroll", "click"]
        this.interval = null
        this.timeout = null

        this.init()
    }

    init() {
        // Attach activity listeners
        this.events.forEach((evt) => {
            window.addEventListener(evt, () => this.resetTimer())
        })

        // Start idle timer
        this.interval = setInterval(() => {
            this.idleTime++
            if (this.idleTime === this.idleLimit) {
                this.setIdleState()
            }
        }, 1000)
    }

    resetTimer() {
        this.idleTime = 0

        const alert = document.querySelector(this.alertSelector)
        if (alert && !alert.classList.contains("d-none")) {
            alert.classList.add("d-none")
        }

        if (this.wasIdle) {
            this.showToast(this.toastReturnId)
            this.wasIdle = false
        }
    }

    setIdleState() {
        this.wasIdle = true

        const alert = document.querySelector(this.alertSelector)
        if (alert) {
            alert.classList.remove("d-none")
        }

        this.showToast(this.toastIdleId)
    }

    showToast(toastId) {
        const toastEl = document.getElementById(toastId)
        if (toastEl && typeof bootstrap !== "undefined" && bootstrap.Toast) {
            this.timeout = setTimeout(() => {
                new bootstrap.Toast(toastEl).show()
            }, 500)
        }
    }

    cleanup() {
        clearInterval(this.interval)
        clearTimeout(this.timeout)
    }
}

document.addEventListener("DOMContentLoaded", () => {
    const idleDetector = new IdleDetector()
    window.addEventListener("beforeunload", () => {
        idleDetector.cleanup()
    })
})
