/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Datatables Add Rows
 */

document.addEventListener("DOMContentLoaded", () => {
    let counter = 1

    const tableElement = document.getElementById("add-rows-data")
    if (tableElement) {
        const table = new DataTable("#add-rows-data", {
            dom: "<'d-md-flex justify-content-between align-items-center my-2'<'btn-toolbar'<'addRowBtn me-3'>><'dropdown'B>f>" + "<'row'<'col-sm-12'tr>>" + "<'d-md-flex justify-content-between align-items-center mt-2'ip>",
            language: {
                paginate: {
                    first: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M11 7l-5 5l5 5" /><path d="M17 7l-5 5l5 5" /></svg>',
                    previous:
                        '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M15 6l-6 6l6 6" /></svg>',
                    next: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M9 6l6 6l-6 6" /></svg>',
                    last: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M7 7l5 5l-5 5" /><path d="M13 7l5 5l-5 5" /></svg>',
                },
            },
            columns: [{ title: "Company" }, { title: "Symbol" }, { title: "Price" }, { title: "Change" }, { title: "Volume" }, { title: "Market Cap" }, { title: "Rating" }, { title: "Status" }],
        })

        // Add a custom button (after table renders)
        const btnContainer = document.querySelector(".addRowBtn")
        if (btnContainer) {
            const btn = document.createElement("button")
            btn.id = "addRow"
            btn.className = "btn btn-primary btn-sm"
            btn.textContent = "Add Row"
            btnContainer.appendChild(btn)

            btn.addEventListener("click", () => {
                table.row
                    .add([
                        `New Company ${counter}`,
                        `SYM${counter}`,
                        `$${(Math.random() * 1000 + 1000).toFixed(2)}`,
                        `${Math.random() > 0.5 ? "+" : "-"}${(Math.random() * 2).toFixed(2)}%`,
                        Math.floor(Math.random() * 1000000).toLocaleString(),
                        `$${(Math.random() * 100).toFixed(2)}B`,
                        `${(Math.random() * 5).toFixed(1)} ★`,
                        `<span class="badge badge-label badge-soft-${Math.random() > 0.5 ? "success" : "danger"}">${Math.random() > 0.5 ? "Bullish" : "Bearish"}</span>`,
                    ])
                    .draw(false)
                counter++
            })
        }
    }
})
