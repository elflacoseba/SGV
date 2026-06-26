/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Datatables Child Rows
 */

document.addEventListener("DOMContentLoaded", () => {
    const tableElement = document.getElementById("column-search-data")
    if (tableElement) {
        new DataTable(tableElement, {
            language: {
                paginate: {
                    first: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M11 7l-5 5l5 5" /><path d="M17 7l-5 5l5 5" /></svg>',
                    previous:
                        '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M15 6l-6 6l6 6" /></svg>',
                    next: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M9 6l6 6l-6 6" /></svg>',
                    last: '<svg  xmlns="http://www.w3.org/2000/svg"  width="24"  height="24"  viewBox="0 0 24 24"  fill="none"  stroke="currentColor"  stroke-width="2"  stroke-linecap="round"  stroke-linejoin="round"><path stroke="none" d="M0 0h24v24H0z" fill="none"/><path d="M7 7l5 5l-5 5" /><path d="M13 7l5 5l-5 5" /></svg>',
                },
            },
            initComplete: function () {
                const api = this.api()

                // Disable sorting clicks on #xx th elements
                const columnHeaderElements = document.querySelectorAll("#column-search-inputs th")
                if (columnHeaderElements.length > 0) {
                    columnHeaderElements.forEach((th) => {
                        th.addEventListener("click", function (e) {
                            e.stopPropagation() // Prevent sorting trigger
                        })
                    })
                } else {
                    console.error("Datatables Column Search: Column header elements not found.")
                }

                // Handle input filtering for columns
                const columnSearchInputs = document.querySelectorAll("#column-search-inputs th input")
                if (columnSearchInputs.length > 0) {
                    columnSearchInputs.forEach((input, index) => {
                        input.addEventListener("click", function (e) {
                            e.stopPropagation() // Prevent input click from bubbling up too
                        })

                        input.addEventListener("keyup", function () {
                            if (api.column(index).search() !== this.value) {
                                api.column(index).search(this.value).draw()
                            }
                        })
                    })
                } else {
                    console.error("Datatables Column Search: Column search input elements not found.")
                }
            },
        })
    }
})
