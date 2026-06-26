/**
 * Template Name: Inspinia - Multipurpose Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Chart Apex Bubble
 */

//
// SIMPLE BUBBLE CHART
//
function generateData(baseval, count, yrange) {
    let i = 0
    const series = []
    while (i < count) {
        const x = Math.floor(Math.random() * (750 - 1 + 1)) + 1
        const y = Math.floor(Math.random() * (yrange.max - yrange.min + 1)) + yrange.min
        const z = Math.floor(Math.random() * (75 - 15 + 1)) + 15

        series.push([x, y, z])
        baseval += 86400000
        i++
    }
    return series
}

new CustomApexChart({
    selector: "#simple-bubble",
    options: () => ({
        chart: {
            height: 350,
            type: "bubble",
            toolbar: {
                show: false,
            },
        },
        dataLabels: {
            enabled: false,
        },
        series: [
            {
                name: "Bubble 1",
                data: generateData(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
            {
                name: "Bubble 2",
                data: generateData(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
            {
                name: "Bubble 3",
                data: generateData(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
        ],
        fill: {
            opacity: 0.8,
            gradient: {
                enabled: false,
            },
        },
        colors: [theme("chart-primary"), theme("chart-secondary"), theme("chart-gamma")],
        xaxis: {
            tickAmount: 12,
            type: "category",
        },
        yaxis: {
            max: 70,
        },
        grid: {
            borderColor: [theme("chart-order-color")],
            padding: {
                top: -20,
                right: 0,
                bottom: -5,
                left: 10,
            },
        },
        legend: {
            offsetY: 7,
        },
    }),
})

//
// 3D BUBBLE CHART
//
function generateData1(baseval1, count, yrange) {
    let i = 0
    const series = []
    while (i < count) {
        //const x =Math.floor(Math.random() * (750 - 1 + 1)) + 1;
        const y = Math.floor(Math.random() * (yrange.max - yrange.min + 1)) + yrange.min
        const z = Math.floor(Math.random() * (75 - 15 + 1)) + 15

        series.push([baseval1, y, z])
        baseval1 += 86400000
        i++
    }
    return series
}

new CustomApexChart({
    selector: "#threed-bubble",
    options: () => ({
        chart: {
            height: 350,
            type: "bubble",
            toolbar: { show: false },
        },
        dataLabels: {
            enabled: false,
        },
        series: [
            {
                name: "Product 1",
                data: generateData1(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
            {
                name: "Product 2",
                data: generateData1(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
            {
                name: "Product 3",
                data: generateData1(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
            {
                name: "Product 4",
                data: generateData1(new Date("11 Feb 2017 GMT").getTime(), 10, {
                    min: 10,
                    max: 60,
                }),
            },
        ],
        fill: {
            type: "gradient",
        },
        colors: [theme("chart-delta"), theme("chart-beta"), theme("chart-zeta"), theme("chart-alpha")],
        xaxis: {
            tickAmount: 12,
            type: "datetime",

            labels: {
                rotate: 0,
            },
        },
        yaxis: {
            max: 70,
        },
        legend: {
            offsetY: 10,
        },
        grid: {
            borderColor: [theme("chart-order-color")],
            padding: {
                top: -20,
                right: 0,
                bottom: 0,
                left: 10,
            },
        },
    }),
})

new CustomApexChart({
    selector: "#three-bubble",
    options: () => ({
        series: [
            {
                name: "Social Campaigns",
                data: [[150, 300, 35]],
            },
            {
                name: "Email Newsletter",
                data: [[250, 350, 35]],
            },
            {
                name: "TV Campaign",
                data: [[350, 450, 30]],
            },
            {
                name: "Google Ads",
                data: [[450, 250, 25]],
            },
            {
                name: "Courses",
                data: [[500, 500, 30]],
            },
            {
                name: "Radio",
                data: [[600, 250, 28]],
            },
        ],
        chart: {
            fontFamily: "inherit",
            type: "bubble",
            height: 350,
            toolbar: { show: false },
        },
        plotOptions: {
            bubble: {
                minBubbleRadius: 5,
                maxBubbleRadius: 35,
            },
        },
        legend: { show: false },
        dataLabels: { enabled: false },
        fill: { opacity: 0.8 },
        xaxis: {
            type: "numeric",
            tickAmount: 7,
            min: 0,
            max: 700,
        },
        yaxis: {
            tickAmount: 7,
            min: 0,
            max: 700,
        },
        tooltip: {
            y: {
                formatter: function (val) {
                    return "$" + val + "K"
                },
            },
            z: { title: "Impressions: " },
        },
        colors: [theme("chart-primary"), theme("chart-secondary"), theme("chart-delta"), theme("chart-alpha"), theme("chart-zeta"), theme("chart-beta")],
        grid: {
            borderColor: [theme("chart-order-color")],
            padding: {
                top: -20, // You can use negative or positive values here
                right: 0,
                bottom: 0,
                left: 5,
            },
        },
    }),
})
