/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): API keys Js
 */

const sparklineConfig = (data, color) => ({
    chart: {
        type: "area",
        height: 60,
        sparkline: { enabled: true },
    },
    stroke: {
        curve: "smooth",
        width: 2,
    },
    series: [{ data }],
    colors: [color],
    tooltip: {
        enabled: false,
    },
})

const dummyNewUsers = [Math.sin(0) * 50 + 500, Math.sin(1) * 60 + 500, Math.sin(2) * 70 + 500, Math.sin(3) * 80 + 500, Math.sin(4) * 90 + 500, Math.sin(5) * 100 + 500, Math.sin(6) * 110 + 500]

const dummyActiveUsers = [89000 + Math.sin(0) * 500, 89200 + Math.sin(1) * 600, 89700 + Math.sin(2) * 700, 90500 + Math.sin(3) * 800, 91000 + Math.sin(4) * 900, 91300 + Math.sin(5) * 1000, 92000 + Math.sin(6) * 1100]

const dummyBlockedUsers = [2600 + Math.sin(0) * 10, 2605 + Math.sin(1) * 12, 2610 + Math.sin(2) * 15, 2608 + Math.sin(3) * 18, 2612 + Math.sin(4) * 20]

const dummyTables = [7000 + Math.sin(0) * 150, 7100 + Math.sin(1) * 160, 7200 + Math.sin(2) * 170, 7400 + Math.sin(3) * 180, 7850 + Math.sin(4) * 200]

new CustomApexChart({
    selector: "#chart-api-calls",
    options: () => sparklineConfig(dummyNewUsers, theme("chart-secondary")),
})

new CustomApexChart({
    selector: "#chart-successful-conversions",
    options: () => sparklineConfig(dummyActiveUsers, theme("chart-beta")),
})

new CustomApexChart({
    selector: "#chart-failed-requests",
    options: () => sparklineConfig(dummyBlockedUsers, theme("chart-delta")),
})

new CustomApexChart({
    selector: "#chart-active-endpoints",
    options: () => sparklineConfig(dummyTables, theme("chart-alpha")),
})

const clipboardElements = document.querySelectorAll("[data-clipboard-target]")
if (clipboardElements) {
    new ClipboardJS("[data-clipboard-target]")
}
