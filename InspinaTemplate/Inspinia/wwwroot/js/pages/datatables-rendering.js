/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Datatables Rendering
 */

document.addEventListener("DOMContentLoaded", function () {
    const tableElement = document.getElementById("datatable-rendering")

    if (tableElement) {
        const cityToCountryCode = {
            Gujarat: "in",
            Tokyo: "jp",
            "San Francisco": "us",
            "New York": "us",
            London: "gb",
            Sydney: "au",
            Argentina: "ar",
        }

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
            ajax: "/data/datatables-rendering.json",
            columns: [
                {
                    data: "name",
                },
                {
                    data: "position",
                    render: function (data, type) {
                        if (type === "display") {
                            let link = "https://datatables.net"

                            if (data[0] < "H") {
                                link = "https://cloudtables.com"
                            } else if (data[0] < "S") {
                                link = "https://editor.datatables.net"
                            }

                            return '<a href="' + link + '">' + data + "</a>"
                        }

                        return data
                    },
                },
                {
                    data: "office",
                    className: "f32", // used by world-flags-sprite library
                    render: function (data, type) {
                        if (type === "display") {
                            const flagMap = {
                                Argentina: "ar",
                                Gujarat: "in",
                                Germany: "de",
                                London: "gb",
                                "New York": "us",
                                "San Francisco": "us",
                                Sydney: "au",
                                Tokyo: "jp",
                            }

                            const countryCode = flagMap[data] || ""
                            return `<span class="flag ${countryCode}">
                                        <img class="avatar-xs rounded me-2" src="/images/flags/${countryCode}.svg" alt="${data}" />
                                    </span> ${data}`
                        }

                        return data
                    },
                },
                {
                    data: "extn",
                    render: function (data, type, row, meta) {
                        return type === "display"
                            ? `<div class="progress" role="progressbar" aria-label="Basic example" aria-valuenow="${data}" aria-valuemin="0" aria-valuemax="9999" style="height:8px">
                                  <div class="progress-bar" style="width: ${(data / 9999) * 100}%"></div>
                            </div>`
                            : data
                    },
                },
                {
                    data: "start_date",
                },
                {
                    data: "salary",
                    render: function (data, type) {
                        var number = DataTable.render.number(",", ".", 2, "$").display(data)

                        if (type === "display") {
                            let color = "green"
                            if (data < 250000) {
                                color = "red"
                            } else if (data < 500000) {
                                color = "orange"
                            }

                            return `<span style="color:${color}">${number}</span>`
                        }

                        return number
                    },
                },
            ],
        })
    }
})
