/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Config
 * Version: 5.0.0
 */

;(function () {
    const html = document.documentElement
    const storageKey = "__THEME_CONFIG__"
    const savedConfig = sessionStorage.getItem(storageKey)

    // Default config
    const defaultConfig = {
        "dir": "ltr",
        "skin": "default",
        "theme": "light",
        "width": "fluid",
        "position": "fixed",
        "orientation": "vertical",
        "sidenav-size": "default",
        "sidenav-user": true,
        "topbar-color": "light",
        "sidenav-color": "dark",
    }

    function getSystemTheme() {
        return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
    }

    // Build config from HTML attributes
    const htmlConfig = {
        skin: html.getAttribute("data-skin") || defaultConfig["skin"],
        theme: html.getAttribute("data-bs-theme") === "system" ? getSystemTheme() : html.getAttribute("data-bs-theme") || (defaultConfig["theme"] === "system" ? getSystemTheme() : defaultConfig["theme"]),
        "topbar-color": html.getAttribute("data-topbar-color") || defaultConfig["topbar-color"],
        "sidenav-color": html.getAttribute("data-menu-color") || defaultConfig["sidenav-color"],
        "sidenav-size": html.getAttribute("data-sidenav-size") || defaultConfig["sidenav-size"],
        "sidenav-user": html.hasAttribute("data-sidenav-user") || defaultConfig["sidenav-user"],
        position: html.getAttribute("data-layout-position") || defaultConfig["position"],
        width: html.getAttribute("data-layout-width") || defaultConfig["width"],
        dir: html.getAttribute("dir") || defaultConfig["dir"],
    }

    // Save merged config as defaults globally
    window.defaultConfig = structuredClone(htmlConfig)

    // Load from session if exists
    let config = savedConfig ? JSON.parse(savedConfig) : htmlConfig
    window.config = config

    // Apply layout attributes immediately
    html.setAttribute("data-skin", config["skin"])
    html.setAttribute("data-bs-theme", config["theme"] === "system" ? getSystemTheme() : config["theme"])
    html.setAttribute("data-menu-color", config["sidenav-color"])
    html.setAttribute("data-topbar-color", config["topbar-color"])
    html.setAttribute("data-layout-width", config["width"])
    html.setAttribute("dir", config["dir"])

    if (config["sidenav-size"]) {
        let size = config["sidenav-size"]

        if (window.innerWidth <= 767) {
            size = "offcanvas"
        } else if (window.innerWidth <= 1140 && !["offcanvas"].includes(size)) {
            size = "condensed"
        }

        html.setAttribute("data-sidenav-size", size)

        if (config["sidenav-user"] === true) {
            html.setAttribute("data-sidenav-user", "true")
        } else {
            html.removeAttribute("data-sidenav-user")
        }
    }
})()
