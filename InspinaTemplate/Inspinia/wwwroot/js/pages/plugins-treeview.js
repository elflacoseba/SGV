/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Plugins Treeview
 */

class TreeView {
    constructor() {
        this.defaultTypes = {
            default: { icon: "treeview-folder-filled fs-lg text-warning" },
            file: { icon: "treeview-article fs-lg" },
        }
    }

    init() {
        this.initTree("#jstree-1", { types: this.defaultTypes })
        this.initTree("#jstree-2", { types: this.defaultTypes })

        this.initSelectableLinks("#jstree-1")
        this.initSelectableLinks("#jstree-2")

        this.initTree("#jstree-3", {
            plugins: ["wholerow", "checkbox", "types"],
            types: this.defaultTypes,
            core: {
                themes: { responsive: false },
                data: [
                    {
                        text: "Sample Nodes with Checkboxes",
                        children: [
                            { text: "Item initially selected", state: { selected: true } },
                            {
                                text: "Custom feedback icon",
                                icon: "treeview-icon-database fs-lg text-danger",
                            },
                            {
                                text: "Folder opened by default",
                                icon: "treeview-folder-open fs-lg text-default",
                                state: { opened: true },
                                children: ["Subfolder item", "Another subfolder item"],
                            },
                            {
                                text: "Document with a warning icon",
                                icon: "treeview-file fs-lg text-warning",
                            },
                            {
                                text: "Node with disabled state",
                                icon: "treeview-file",
                                state: { disabled: true },
                            },
                            { text: "New added item", icon: "treeview-file" },
                            { text: "Icon with a download symbol", icon: "treeview-file" },
                        ],
                    },
                    {
                        text: "Additional Category",
                        children: [
                            { text: "Category item 1", icon: "treeview-file" },
                            { text: "Category item 2", icon: "treeview-file" },
                            { text: "Category item 3", icon: "treeview-file" },
                        ],
                    },
                    "And wholerow selection",
                ],
            },
        })

        this.initTree("#jstree-4", {
            plugins: ["contextmenu", "state", "types"],
            types: this.defaultTypes,
            core: {
                check_callback: true,
                themes: { responsive: false },
                data: this.getStaticData(),
            },
            state: { key: "demo2" },
        })

        this.initTree("#jstree-5", {
            plugins: ["dnd", "state", "types"],
            types: {
                default: { icon: "treeview-folder-filled text-success fs-lg" },
                file: { icon: "treeview-file text-primary fs-lg" },
            },
            core: {
                check_callback: true,
                themes: { responsive: false },
                data: this.getTree5Data(),
            },
            state: { key: "demo2" },
        })

        this.initTree("#jstree-6", {
            plugins: ["dnd", "state", "types"],
            types: {
                default: { icon: "treeview-folder-filled text-primary fs-lg" },
                file: { icon: "treeview-article" },
            },
            core: {
                check_callback: true,
                themes: { responsive: false },
                data: {
                    url: (node) => (node.id === "#" ? "/data/treeview-data.json" : "/data/treeview-data.json"),
                    data: (node) => ({ id: node.id }),
                },
            },
            state: { key: "demo3" },
        })
    }

    initTree(selector, options) {
        const el = document.querySelector(selector)
        if (el) {
            $(el).jstree({
                core: {
                    themes: { responsive: false },
                    ...(options.core || {}),
                },
                plugins: options.plugins || ["types"],
                types: options.types || this.defaultTypes,
                ...(options.state && { state: options.state }),
            })
        }
    }

    initSelectableLinks(selector) {
        const el = document.querySelector(selector)
        if (!el) return

        $(el).on("select_node.jstree", function (e, data) {
            const anchor = document.querySelector(`#${data.selected} a`)
            if (!anchor) return

            const href = anchor.getAttribute("href")
            const target = anchor.getAttribute("target")

            if (href && href !== "#" && href !== "javascript:;") {
                if (target === "_blank") {
                    window.open(href, "_blank")
                } else {
                    window.location.href = href
                }
                return false
            }
        })
    }

    getStaticData() {
        return [
            {
                text: "Parent Node",
                children: [
                    { text: "Initially selected", state: { selected: true } },
                    { text: "Custom Icon", icon: "treeview-icon-database fs-lg text-danger" },
                    {
                        text: "Initially open",
                        icon: "treeview-folder-open fs-lg text-success",
                        state: { opened: true },
                        children: [{ text: "Another node", icon: "treeview-file fs-lg text-warning" }],
                    },
                    { text: "Another Custom Icon", icon: "treeview-file fs-lg text-warning" },
                    { text: "Disabled Node", icon: "treeview-icon-ban", state: { disabled: true } },
                    {
                        text: "Sub Nodes",
                        icon: "treeview-folder-filled fs-lg text-danger",
                        children: [
                            { text: "Item 1", icon: "treeview-file fs-lg text-warning" },
                            { text: "Item 2", icon: "treeview-file fs-lg text-success" },
                            { text: "Item 3", icon: "treeview-file fs-lg text-default" },
                            { text: "Item 4", icon: "treeview-file fs-lg text-danger" },
                            { text: "Item 5", icon: "treeview-file fs-lg text-info" },
                            { text: "Item 6", icon: "treeview-file" },
                            { text: "Item 7", icon: "treeview-file" },
                        ],
                    },
                ],
            },
            {
                text: "Additional Category",
                children: [
                    { text: "Category item 1", icon: "treeview-icon-database" },
                    { text: "Category item 2", icon: "treeview-icon-database" },
                    { text: "Category item 3", icon: "treeview-icon-database" },
                ],
            },
            {
                text: "Miscellaneous Nodes",
                children: [
                    { text: "Node with plus icon", icon: "treeview-icon-database" },
                    { text: "Node with download icon", icon: "treeview-icon-database" },
                    { text: "Node with edit icon", icon: "treeview-icon-database" },
                ],
            },
        ]
    }

    getTree5Data() {
        return [
            {
                text: "Main Category",
                icon: "treeview-folder-filled fs-lg text-warning",
                children: [
                    { text: "Dashboard", icon: "treeview-icon-arrow" },
                    {
                        text: "Reports",
                        icon: "treeview-icon-database",
                        state: { opened: true },
                        children: [
                            { text: "Annual Report", icon: "treeview-icon-arrow" },
                            { text: "Monthly Report", icon: "treeview-icon-arrow" },
                        ],
                    },
                    {
                        text: "User Management",
                        icon: "treeview-icon-database",
                        children: [
                            { text: "Add User", icon: "treeview-icon-arrow" },
                            { text: "User Roles", icon: "treeview-icon-arrow" },
                            { text: "Permissions", icon: "treeview-icon-arrow" },
                        ],
                    },
                    {
                        text: "Settings",
                        icon: "treeview-icon-database",
                        children: [
                            { text: "General", icon: "treeview-icon-arrow" },
                            { text: "Security", icon: "treeview-icon-arrow" },
                            { text: "Notifications", icon: "treeview-icon-arrow" },
                        ],
                    },
                    {
                        text: "Disabled Node",
                        icon: "treeview-icon-ban",
                        state: { disabled: true },
                    },
                ],
            },
            {
                text: "Archives",
                icon: "treeview-folder-filled",
                children: [
                    { text: "2024", icon: "treeview-icon-arrow" },
                    { text: "2023", icon: "treeview-icon-arrow" },
                    { text: "2022", icon: "treeview-icon-arrow" },
                ],
            },
            {
                text: "Media",
                icon: "treeview-folder-filled",
                children: [
                    { text: "Images", icon: "treeview-icon-arrow" },
                    { text: "Videos", icon: "treeview-icon-arrow" },
                    { text: "Audio", icon: "treeview-icon-arrow" },
                ],
            },
        ]
    }
}

document.addEventListener("DOMContentLoaded", () => {
    new TreeView().init()
})
