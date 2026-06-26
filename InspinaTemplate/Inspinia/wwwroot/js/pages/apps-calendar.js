/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Apps Calendar
 */

class CalendarSchedule {
    constructor() {
        this.body = document.body
        const eventModal = document.getElementById("event-modal")

        if (!eventModal) {
            console.error("Calendar: Event modal element not found")
            return
        }

        try {
            this.modal = new bootstrap.Modal(eventModal, { backdrop: "static" })
            this.calendar = document.getElementById("calendar")
            this.formEvent = document.getElementById("forms-event")
            this.btnNewEvent = document.querySelectorAll(".btn-new-event")
            this.btnDeleteEvent = document.getElementById("btn-delete-event")
            this.btnSaveEvent = document.getElementById("btn-save-event")
            this.modalTitle = document.getElementById("modal-title")
            this.calendarObj = null
            this.selectedEvent = null
            this.newEventData = null

            // Validate required elements
            const requiredElements = {
                "calendar": this.calendar,
                "formEvent": this.formEvent,
                "btnDeleteEvent": this.btnDeleteEvent,
                "modalTitle": this.modalTitle,
            }

            for (const [name, element] of Object.entries(requiredElements)) {
                if (!element) {
                    throw new Error(`Required element not found: ${name}`)
                }
            }

            if (this.btnNewEvent.length === 0) {
                console.warn("No new event buttons found with class .btn-new-event")
            }
        } catch (error) {
            console.error("Calendar initialization failed:", error.message)
            // Show user-friendly error message
            const errorContainer = document.createElement("div")
            errorContainer.className = "alert alert-danger m-3"
            errorContainer.textContent = "Failed to initialize calendar. Please refresh the page or contact support."
            document.body.prepend(errorContainer)
            throw error // Re-throw to prevent further execution
        }
    }

    onEventClick(info) {
        this.formEvent?.reset()
        this.formEvent.classList.remove("was-validated")
        this.newEventData = null
        this.btnDeleteEvent.style.display = "block"
        this.modalTitle.text = "Edit Event"
        this.modal.show()
        this.selectedEvent = info.event
        document.getElementById("event-title").value = this.selectedEvent.title
        const categoryInput = document.getElementById("event-category")
        if (categoryInput) {
            const { classNames } = this.selectedEvent
            categoryInput.value = Array.isArray(classNames) ? classNames.join(" ") : classNames || ""
        }
    }

    onSelect(info) {
        this.formEvent?.reset()
        this.formEvent?.classList.remove("was-validated")
        this.selectedEvent = null
        this.newEventData = info
        this.btnDeleteEvent.style.display = "none"
        this.modalTitle.text = "Add New Event"
        this.modal.show()
        this.calendarObj.unselect()
    }

    init() {
        // Check if initialization was successful
        if (!this.calendar) {
            console.error("Calendar: Cannot initialize - required elements missing")
            return
        }

        /*  Initialize the calendar  */
        const today = new Date()
        const self = this
        const externalEventContainerEl = document.getElementById("external-events")

        if (!externalEventContainerEl) {
            console.warn("External events container not found - drag and drop functionality will be limited")
        }

        new FullCalendar.Draggable(externalEventContainerEl, {
            itemSelector: ".external-event",
            eventData: function (eventEl) {
                return {
                    title: eventEl.innerText,
                    classNames: eventEl.getAttribute("data-class"),
                }
            },
        })

        const defaultEvents = [
            {
                title: "Interview - Backend Engineer",
                start: today,
                end: today,
                className: "bg-primary-subtle text-primary border-start border-3 border-primary",
            },
            {
                title: "Design Sprint Planning",
                start: new Date(Date.now() + 16000000),
                end: new Date(Date.now() + 20000000),
                className: "bg-info-subtle text-info border-start border-3 border-info",
            },
            {
                title: "Project Kick-off Meeting",
                start: new Date(Date.now() + 400000000),
                end: new Date(Date.now() + 440000000),
                className: "bg-secondary-subtle text-secondary border-start border-3 border-secondary",
            },
            {
                title: "UI/UX Design Review",
                start: new Date(Date.now() + 80000000),
                end: new Date(Date.now() + 180000000),
                className: "bg-warning-subtle text-warning border-start border-3 border-warning",
            },
            {
                title: "Code Review - Frontend",
                start: new Date(Date.now() + 200000000),
                end: new Date(Date.now() + 220000000),
                className: "bg-success-subtle text-success border-start border-3 border-success",
            },
            {
                title: "Team Stand-up Meeting",
                start: new Date(Date.now() + 340000000),
                end: new Date(Date.now() + 345000000),
                className: "bg-secondary-subtle text-secondary border-start border-3 border-secondary",
            },
            {
                title: "Client Presentation Prep",
                start: new Date(Date.now() + 1200000000),
                end: new Date(Date.now() + 1300000000),
                className: "bg-danger-subtle text-danger border-start border-3 border-danger",
            },
            {
                title: "Backend API Integration",
                start: new Date(Date.now() + 2500000000),
                end: new Date(Date.now() + 2600000000),
                className: "bg-dark-subtle text-dark border-start border-3 border-dark",
            },
        ]

        // cal - init
        self.calendarObj = new FullCalendar.Calendar(self.calendar, {
            plugins: [],
            slotDuration: "00:30:00" /* If we want to split day time each 15minutes */,
            slotMinTime: "07:00:00",
            slotMaxTime: "19:00:00",
            themeSystem: "bootstrap",
            bootstrapFontAwesome: false,
            buttonText: {
                today: "Today",
                month: "Month",
                week: "Week",
                day: "Day",
                list: "List",
                prev: "Prev",
                next: "Next",
            },
            initialView: "dayGridMonth",
            handleWindowResize: true,
            height: window.innerHeight - 240,
            headerToolbar: {
                left: "prev,next today",
                center: "title",
                right: "dayGridMonth,timeGridWeek,timeGridDay,listMonth",
            },
            initialEvents: defaultEvents,
            editable: true,
            droppable: true, // this allows things to be dropped onto the calendar !!!
            // dayMaxEventRows: false, // allow "more" link when too many events
            selectable: true,
            dateClick: function (info) {
                self.onSelect(info)
            },
            eventClick: function (info) {
                self.onEventClick(info)
            },
        })

        try {
            self.calendarObj.render()
        } catch (error) {
            console.error("Failed to render calendar:", error)
            const errorMsg = document.createElement("div")
            errorMsg.className = "alert alert-danger m-3"
            errorMsg.textContent = "Failed to render calendar. Please check the console for details."
            self.calendar.parentNode.insertBefore(errorMsg, self.calendar)
            return
        }

        // on new event button click
        if (self.btnNewEvent.length > 0) {
            self.btnNewEvent.forEach(function (btn) {
                btn.addEventListener("click", function (e) {
                    self.onSelect({
                        date: new Date(),
                        allDay: true,
                    })
                })
            })
        }

        // save event
        if (!self.formEvent) {
            console.error("Form element not found")
            return
        }

        self.formEvent.addEventListener("submit", function (e) {
            e.preventDefault()
            const form = self.formEvent

            // validation
            if (form.checkValidity()) {
                if (self.selectedEvent) {
                    self.selectedEvent.setProp("title", document.getElementById("event-title").value)
                    self.selectedEvent.setProp("classNames", document.getElementById("event-category").value)
                } else {
                    const eventData = {
                        title: document.getElementById("event-title").value,
                        start: self.newEventData.date,
                        allDay: self.newEventData.allDay,
                        className: document.getElementById("event-category").value,
                    }
                    self.calendarObj.addEvent(eventData)
                }
                self.modal.hide()
            } else {
                e.stopPropagation()
                form.classList.add("was-validated")
            }
        })

        // delete event
        if (!self.btnDeleteEvent) {
            console.error("Delete button not found")
            return
        }

        self.btnDeleteEvent.addEventListener("click", function (e) {
            if (self.selectedEvent) {
                self.selectedEvent.remove()
                self.selectedEvent = null
                self.modal.hide()
            }
        })
    }
}

document.addEventListener("DOMContentLoaded", function (e) {
    new CalendarSchedule().init()
})
