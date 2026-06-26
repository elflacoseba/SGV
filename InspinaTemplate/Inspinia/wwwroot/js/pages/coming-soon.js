/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Coming Soon
 */

class Countdown {
    initCountDown() {
        const daysElement = document.getElementById("days")
        const hoursElement = document.getElementById("hours")
        const minutesElement = document.getElementById("minutes")
        const secondsElement = document.getElementById("seconds")
        const endElement = document.getElementById("end")

        if (!daysElement || !hoursElement || !minutesElement || !secondsElement) {
            console.error("Countdown elements not found.")
            return
        }

        // The data/time we want to countdown to
        const eventCountDown = new Date("Sep 27, 2028 12:00:01").getTime()

        // Run myfunc every second
        const count = setInterval(function () {
            const now = new Date().getTime()
            const timeleft = eventCountDown - now

            // Calculating the days, hours, minutes and seconds left
            const days = Math.floor(timeleft / (1000 * 60 * 60 * 24))
            const hours = Math.floor((timeleft % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60))
            const minutes = Math.floor((timeleft % (1000 * 60 * 60)) / (1000 * 60))
            const seconds = Math.floor((timeleft % (1000 * 60)) / 1000)

            // Result is output to the specific element
            daysElement.innerHTML = days
            hoursElement.innerHTML = hours
            minutesElement.innerHTML = minutes
            secondsElement.innerHTML = seconds

            // Display the message when countdown is over
            if (timeleft < 0) {
                clearInterval(count)
                daysElement.innerHTML = ""
                hoursElement.innerHTML = ""
                minutesElement.innerHTML = ""
                secondsElement.innerHTML = ""

                endElement.innerHTML = "00:00:00:00"
            }
        }, 1000)
    }

    init() {
        this.initCountDown()
    }
}

document.addEventListener("DOMContentLoaded", () => {
    new Countdown().init()
})
