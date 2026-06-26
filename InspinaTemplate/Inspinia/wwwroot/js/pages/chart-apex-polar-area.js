/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Chart Apex Polar Area
 */

//
// BASIC POLAR AREA CHART
//
new CustomApexChart({
    selector: "#basic-polar-area",
    options: () => ({
        series: [30, 45, 28, 22, 18, 12],
        chart: {
            height: 380,
            type: "polarArea",
        },
        stroke: {
            colors: ["#fff"],
        },
        fill: {
            opacity: 0.8,
        },
        labels: ["Marketing", "Research", "Operations", "Sales", "HR", "Support"],
        colors: [theme("chart-primary"), theme("chart-secondary"), theme("chart-delta"), theme("chart-beta"), theme("chart-alpha"), theme("chart-zeta")],
        legend: {
            position: "bottom",
        },
        responsive: [
            {
                breakpoint: 480,
                options: {
                    chart: {
                        width: 200,
                    },
                    legend: {
                        position: "bottom",
                    },
                },
            },
        ],
    }),
})

//
// MONOCHROME POLAR AREA
//
new CustomApexChart({
    selector: "#monochrome-polar-area",
    options: () => ({
        series: [35, 48, 55, 60, 70],
        chart: {
            height: 380,
            type: "polarArea",
        },
        labels: ["Planning", "Design", "Development", "Testing", "Deployment"],
        fill: {
            opacity: 1,
        },
        stroke: {
            width: 1,
        },
        yaxis: {
            show: false,
        },
        legend: {
            position: "bottom",
        },
        plotOptions: {
            polarArea: {
                rings: {
                    strokeWidth: 0,
                },
                spokes: {
                    strokeWidth: 0,
                },
            },
        },
        theme: {
            monochrome: {
                enabled: true,
                shadeTo: "light",
                color: theme("chart-primary"),
                shadeIntensity: 0.6,
            },
        },
    }),
})
