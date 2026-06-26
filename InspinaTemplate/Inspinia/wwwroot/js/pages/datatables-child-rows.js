/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Datatables Child Rows
 */

// Formatting function for row details - modify as you need
function format(d) {
    // `d` is the original data object for the row
    return (
        '<div class="row align-items-center">' +
        '<div class="col-md-4">' +
        '<h5 class="fs-base mb-1">Rating:</h5>' +
        "<div>" +
        d.rating +
        "</div>" +
        "</div>" +
        '<div class="col-md-4">' +
        '<h5 class="fs-base mb-1">Status:</h5>' +
        `<span class="badge badge-label ${d.status === "Bullish" ? "badge-soft-success" : "badge-soft-danger"}">${d.status}</span>` +
        "</div>" +
        '<div class="col-md-4">' +
        '<h5 class="fs-base mb-1">Extra info:</h5>' +
        "<div>And any further details here (images etc)...</div>" +
        "</div>" +
        "</div>"
    )
}

document.addEventListener("DOMContentLoaded", () => {
    const tableElement = document.getElementById("child-rows-data")
    if (tableElement) {
        const table = new DataTable(tableElement, {
            ajax: "/data/datatables.json", // path to your JSON file
            columns: [
                {
                    className: "dt-control dt-child-rows-btn",
                    orderable: false,
                    data: null,
                    defaultContent:
                        '<svg  xmlns="http://www.w3.org/2000/svg"  width="22"  height="22"  viewBox="0 0 24 24"  fill="currentColor"  class="text-primary"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M4.929 4.929a10 10 0 1 1 14.141 14.141a10 10 0 0 1 -14.14 -14.14zm8.071 4.071a1 1 0 1 0 -2 0v2h-2a1 1 0 1 0 0 2h2v2a1 1 0 1 0 2 0v-2h2a1 1 0 1 0 0 -2h-2v-2z" /></svg>',
                },
                { data: "company" },
                { data: "symbol" },
                { data: "price" },
                { data: "change" },
                { data: "volume" },
                { data: "market_cap" },
            ],
            order: [[1, "asc"]],
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

        // Add event listener for opening and closing details
        table.on("click", "td.dt-control", function (e) {
            let tr = e.target.closest("tr")
            let row = table.row(tr)

            if (row.child.isShown()) {
                // This row is already open - close it
                row.child.hide()
            } else {
                // Open this row
                row.child(format(row.data())).show()
            }
        })
    }
})
