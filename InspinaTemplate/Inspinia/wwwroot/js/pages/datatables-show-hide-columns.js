/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Datatables Show Hide Columns
 */

document.addEventListener("DOMContentLoaded", function () {
    const tableElement = document.getElementById("show-hide-column")
    if (tableElement) {
        const table = new DataTable(tableElement, {
            responsive: true,
            dom: "<'d-md-flex justify-content-between align-items-center mt-2 mb-3'<'columnToggleWrapper'B>f>" + "rt" + "<'d-md-flex justify-content-between align-items-center mt-2'lp>",
            language: {
                paginate: {
                    first: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M11 7l-5 5l5 5" /><path d="M17 7l-5 5l5 5" /></svg>',
                    previous:
                        '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M15 6l-6 6l6 6" /></svg>',
                    next: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M9 6l6 6l-6 6" /></svg>',
                    last: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M7 7l5 5l-5 5" /><path d="M13 7l5 5l-5 5" /></svg>',
                },
            },
        })

        // Define columns and labels
        const columnLabels = ["Company", "Symbol", "Price", "Change", "Volume", "Market Cap", "Rating", "Status"]

        // Create dropdown with toggles dynamically
        const columnToggleWrapper = document.querySelector(".columnToggleWrapper")

        if (!columnToggleWrapper) {
            console.error("Datatables Show Hide Columns: Column toggle wrapper not found.")
            return
        }

        const dropdown = document.createElement("div")
        dropdown.className = "dropdown"

        dropdown.innerHTML = `
        <button class="btn btn-sm btn-secondary" type="button" data-bs-toggle="dropdown" data-bs-auto-close="outside">
            Show/Hide Columns
        </button>
        <ul class="dropdown-menu" id="columnToggleMenu">
            ${columnLabels
                .map(
                    (label, index) => `
                <li class="dropdown-item">
                    <div class="form-check">
                        <input class="form-check-input form-check-input-light fs-14 mt-0 toggle-vis" 
                               type="checkbox" data-column="${index}" id="colToggle${index}" checked>
                        <label class="form-check-label fw-medium" for="colToggle${index}">
                            ${label}
                        </label>
                    </div>
                </li>
            `
                )
                .join("")}
        </ul>
    `

        columnToggleWrapper.appendChild(dropdown)

        // Handle visibility toggle
        const columnToggleMenu = document.getElementById("columnToggleMenu")
        if (!columnToggleMenu) {
            console.error("Datatables Show Hide Columns: Column toggle menu not found.")
            return
        }

        columnToggleMenu.addEventListener("change", function (e) {
            if (e.target.classList.contains("toggle-vis")) {
                const colIndex = parseInt(e.target.dataset.column, 10)
                const column = table.column(colIndex)
                if (column) {
                    column.visible(e.target.checked)
                } else {
                    console.error("Datatables Show Hide Columns: Column not found.")
                }
            }
        })
    }
})
