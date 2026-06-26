/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): ECommerce Products
 */

const nonLinearSlider = document.getElementById("price-filter")
const priceFilterLow = document.getElementById("price-filter-low")
const priceFilterHigh = document.getElementById("price-filter-high")

if (!nonLinearSlider || !priceFilterLow || !priceFilterHigh) {
    console.error("Price filter elements not found.")
}

noUiSlider.create(nonLinearSlider, {
    connect: true,
    behaviour: "tap",
    start: [1000, 2500],
    range: {
        min: [1],
        max: [9999],
    },
    format: {
        to: function (value) {
            return "$" + Math.round(value)
        },
        from: function (value) {
            return Number(value.replace("$", ""))
        },
    },
})

nonLinearSlider.noUiSlider.on("update", function (values, handle) {
    if (handle === 0) {
        priceFilterLow.innerHTML = values[handle]
    } else {
        priceFilterHigh.innerHTML = values[handle]
    }
})
