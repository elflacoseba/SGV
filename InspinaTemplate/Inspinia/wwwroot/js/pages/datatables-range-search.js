/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Datatables Range Search
 */

document.addEventListener("DOMContentLoaded", function () {
    const tableElement = document.getElementById("range-search-data")

    if (tableElement) {
        const table = new DataTable(tableElement, {
            dom: "<'d-md-flex justify-content-between align-items-center my-2'<'filter-range me-2'>f>" + "rt" + "<'d-md-flex justify-content-between align-items-center mt-2'ip>",
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

        const filterContainer = document.querySelector(".filter-range")

        if (filterContainer) {
            // Add custom range filter inputs
            filterContainer.innerHTML = `
            <div class="d-flex align-items-center gap-2 my-2">
                <label class="fw-semibold">Price: </label>
                <input type="text" class="form-control form-control-sm" placeholder="Min" id="min">
                <input type="text" class="form-control form-control-sm" placeholder="Max" id="max">
            </div>`

            const minInput = document.getElementById("min")
            const maxInput = document.getElementById("max")

            if (!minInput || !maxInput) {
                console.error("Range filter elements not found.")
                return
            }

            // Add range-based filtering logic
            table.search.fixed("range", function (searchStr, data) {
                const min = parseFloat(minInput.value) || NaN
                const max = parseFloat(maxInput.value) || NaN

                // Get and normalize price from column index 2
                const priceStr = (data[2] || "").replace(/[^0-9.]/g, "")
                const price = parseFloat(priceStr) || 0

                return (isNaN(min) && isNaN(max)) || (isNaN(min) && price <= max) || (min <= price && isNaN(max)) || (min <= price && price <= max)
            })

            // Attach input listeners to trigger table redraw
            minInput.addEventListener("input", () => table.draw())
            maxInput.addEventListener("input", () => table.draw())
        }
    }
})
