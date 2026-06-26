/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Apps Chat
 */

class Chat {
    constructor({
        sidebarSelector = "#chat-sidebar",
        sidebarContactAttribute = "data-chat-id",
        sidebarContactSelector = `[${sidebarContactAttribute}]`,
        searchSelector = "[data-chat-search]",
        contentSelector = "[data-chat]",
        inputSelector = "[data-chat-input]",
        sendSelector = "[data-send]",
        errorSelector = "[data-error]",
        usernameSelector = "[data-chat-username]",
        data = [],
    } = {}) {
        this.sidebar = document.querySelector(sidebarSelector)
        this.sidebarContactAttribute = sidebarContactAttribute
        this.sidebarContactSelector = sidebarContactSelector
        this.searchInput = document.querySelector(searchSelector)
        this.chatContent = document.querySelector(contentSelector)
        this.chatInput = document.querySelector(inputSelector)
        this.sendButton = document.querySelector(sendSelector)
        this.errorElement = document.querySelector(errorSelector)
        this.usernameElement = document.querySelector(usernameSelector)
        this.activeChatId = null
        this.chatData = Array.isArray(data) ? data : []

        // Check for required elements
        this.requiredElements = [
            { element: this.chatContent, name: "Chat content" },
            { element: this.chatInput, name: "Chat input" },
            { element: this.sendButton, name: "Send button" },
        ]

        this.init()
    }

    showError(message, duration = 3000) {
        if (!this.errorElement) {
            console.error("Error element not found:", message)
            return
        }

        this.errorElement.textContent = message
        this.errorElement.classList.remove("d-none")
        this.errorElement.classList.add("d-block")

        if (this.errorTimeout) clearTimeout(this.errorTimeout)
        this.errorTimeout = setTimeout(() => {
            this.errorElement.classList.remove("d-block")
            this.errorElement.classList.add("d-none")
        }, duration)
    }

    checkRequiredElements() {
        const missingElements = this.requiredElements.filter((item) => !item.element).map((item) => item.name)

        if (missingElements.length > 0) {
            const errorMsg = `Missing required elements: ${missingElements.join(", ")}. Please check your HTML structure.`
            this.showError(errorMsg, 10000) // Show for 10 seconds
            console.error(errorMsg)
            return false
        }
        return true
    }

    init() {
        // Check for required elements before proceeding
        if (!this.checkRequiredElements()) {
            return
        }

        // Initialize with first chat if available
        const defaultChat = this.chatData[0]
        this.setupSearch()

        // Sidebar click handler
        if (this.sidebar) {
            this.sidebar.addEventListener("click", (e) => {
                const link = e.target.closest(this.sidebarContactSelector)
                if (link && link.dataset.chatId) {
                    this.switchChat(link.dataset.chatId, link)
                }
            })

            if (defaultChat) {
                const defaultEl = this.sidebar.querySelector(`[${this.sidebarContactAttribute}="${defaultChat.id}"]`)
                if (defaultEl) {
                    this.switchChat(defaultChat.id, defaultEl)
                } else {
                    console.warn(`Default chat element with ID ${defaultChat.id} not found in sidebar`)
                }
            }
        }

        // Send button click handler
        this.sendButton.addEventListener("click", () => this.sendMessage())

        // Input handlers
        this.chatInput.addEventListener("keypress", (e) => {
            if (e.key === "Enter") {
                e.preventDefault()
                this.sendMessage()
            }
        })

        this.chatInput.addEventListener("input", () => {
            const text = this.chatInput.value.trim()
            this.sendButton.disabled = !text

            if (this.errorElement && text) {
                this.errorElement.classList.remove("d-block")
                this.errorElement.classList.add("d-none")
                if (this.errorTimeout) clearTimeout(this.errorTimeout)
            }
        })
    }

    getChatById(id) {
        return this.chatData.find((c) => c.id === id)
    }

    switchChat(chatId, clickedEl = null) {
        if (!chatId) {
            console.warn("switchChat called without chatId")
            return
        }

        const chat = this.getChatById(chatId)
        if (!chat) {
            this.showError(`Chat with ID ${chatId} not found`)
            console.warn(`Chat with ID ${chatId} not found in chatData`)
            return
        }

        try {
            this.activeChatId = chatId
            this.renderMessages(chat.messages)

            // Highlight sidebar
            if (this.sidebar) {
                const sidebarItems = this.sidebar.querySelectorAll(this.sidebarContactSelector)
                if (sidebarItems.length === 0) {
                    console.warn("No sidebar items found with selector:", this.sidebarContactSelector)
                } else {
                    sidebarItems.forEach((el) => el.classList.remove("active"))
                }
            }

            if (clickedEl) {
                clickedEl.classList.add("active")
            }

            // Focus input if available
            if (this.chatInput) {
                this.chatInput.focus()
            }

            // Update username if element exists
            if (this.usernameElement && chat.contact?.name) {
                this.usernameElement.textContent = chat.contact.name
            }

            // Scroll to bottom with SimpleBar support
            setTimeout(() => {
                if (!this.chatContent) return

                try {
                    const simpleBar = window.SimpleBar?.instances?.get(this.chatContent)
                    const scrollEl = simpleBar ? simpleBar.getScrollElement() : this.chatContent
                    scrollEl.scrollTop = scrollEl.scrollHeight
                } catch (error) {
                    console.error("Error while scrolling chat:", error)
                }
            }, 50)
        } catch (error) {
            console.error("Error in switchChat:", error)
            this.showError("An error occurred while switching chats")
        }
    }

    renderMessages(messages) {
        if (!messages) {
            console.warn("No messages provided to render")
            return
        }

        if (!this.chatContent) {
            console.error("Chat content element not found")
            return
        }

        try {
            const simpleBar = window.SimpleBar?.instances?.get(this.chatContent)
            const scrollContent = simpleBar ? simpleBar.getContentElement() : this.chatContent

            if (!scrollContent) {
                console.error("Could not find scroll content element")
                return
            }

            // Sanitize messages to prevent XSS
            const sanitize = (str) => {
                if (typeof str !== "string") return ""
                const div = document.createElement("div")
                div.textContent = str
                return div.innerHTML
            }

            scrollContent.innerHTML = messages
                .filter((msg) => msg && typeof msg === "object" && "text" in msg)
                .map((msg) => {
                    const isSelf = msg.from === "me"
                    const alignment = isSelf ? "text-end justify-content-end" : ""
                    const bubbleClass = isSelf ? "bg-info-subtle" : "bg-warning-subtle"
                    const avatar = msg.avatar || "/images/users/default-avatar.png"
                    const text = sanitize(msg.text)
                    const time = sanitize(msg.time || "")

                    return `
                    <div class="d-flex align-items-start gap-2 my-3 chat-item ${alignment}">
                    ${!isSelf ? `<img src="${avatar}" class="avatar-md rounded-circle" alt="User">` : ""}
                        <div>
                            <div class="chat-message py-2 px-3 ${bubbleClass} rounded">${text}</div>
                            <div class="text-muted fs-xs mt-1">
                                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path stroke="none" d="M0 0h24v24H0z" fill="none"/>
                                    <path d="M3 12a9 9 0 1 0 18 0a9 9 0 0 0 -18 0" />
                                    <path d="M12 7v5l3 3" />
                                </svg> ${time}
                            </div>
                        </div>
                    ${isSelf ? `<img src="${avatar}" class="avatar-md rounded-circle" alt="User">` : ""}
                        </div>`
                })
                .join("")

            // Update scroll position
            if (simpleBar) {
                try {
                    simpleBar.recalculate()
                    const scrollEl = simpleBar.getScrollElement()
                    if (scrollEl) {
                        scrollEl.scrollTop = scrollEl.scrollHeight
                    }
                } catch (error) {
                    console.error("Error updating SimpleBar scroll:", error)
                    this.chatContent.scrollTop = this.chatContent.scrollHeight
                }
            } else if (this.chatContent) {
                this.chatContent.scrollTop = this.chatContent.scrollHeight
            }
        } catch (error) {
            console.error("Error rendering messages:", error)
            this.showError("Failed to load messages. Please refresh the page.")
        }
    }

    sendMessage() {
        if (!this.chatInput) {
            console.error("Chat input element not found")
            return
        }

        if (!this.activeChatId) {
            this.showError("No active chat selected")
            return
        }

        const text = this.chatInput.value.trim()

        // Clear previous timeout
        if (this.errorTimeout) clearTimeout(this.errorTimeout)

        // Validate message
        if (!text) {
            this.showError("Message cannot be empty")
            return
        }

        try {
            const chat = this.getChatById(this.activeChatId)
            if (!chat) {
                this.showError("Chat not found")
                console.error(`Chat with ID ${this.activeChatId} not found`)
                return
            }

            // Ensure messages array exists
            if (!Array.isArray(chat.messages)) {
                chat.messages = []
            }

            const now = new Date()
            const time = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }).toLowerCase()

            const msg = {
                from: "me",
                text,
                time,
                avatar: "/images/users/user-2.jpg",
            }

            // Add message to chat
            chat.messages.push(msg)

            // Clear input and disable send button
            this.chatInput.value = ""
            if (this.sendButton) {
                this.sendButton.disabled = true
            }

            // Render messages
            this.renderMessages(chat.messages)

            // Simulate response if needed
            this.simulateIncomingMessage(chat.id)
        } catch (error) {
            console.error("Error sending message:", error)
            this.showError("Failed to send message. Please try again.")
        }
    }

    setupSearch() {
        if (this.searchInput) {
            this.searchInput.addEventListener("keyup", (e) => {
                const query = e.target.value.toLowerCase()

                const list = document.querySelector(".list-group")
                const items = list.querySelectorAll(".list-group-item")

                items.forEach((item) => {
                    const fields = item.querySelectorAll("[data-chat-search-field]")
                    const match = [...fields].some((el) => el.textContent.toLowerCase().includes(query))
                    item.style.setProperty("display", match ? "" : "none", "important")
                })
            })
        }
    }

    simulateIncomingMessage(chatId) {
        const chat = this.getChatById(chatId)
        if (!chat) return

        const responses = ["Can't chat, calls only", "😑😑😑", "👍", "Thanks!", "Talk soon.", "No worries 😄"]

        const now = new Date()
        const time = now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }).toLowerCase()

        const reply = {
            from: chat.contact.name,
            text: responses[Math.floor(Math.random() * responses.length)],
            time,
            avatar: chat.contact.avatar,
        }

        setTimeout(
            () => {
                chat.messages.push(reply)
                if (this.activeChatId === chatId) {
                    this.renderMessages(chat.messages)
                }
            },
            Math.random() * 2000 + 1000
        )
    }
}

const chatData = [
    {
        id: "chat1",
        contact: {
            name: "Ava Thompson",
            avatar: "/images/users/user-4.jpg",
        },
        messages: [
            {
                from: "Ava Thompson",
                text: "Hey! Are you available for a quick call? 📞",
                time: "08:55 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Sure, give me 5 minutes. Just wrapping something up.",
                time: "08:57 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "Perfect. Let me know when you're ready 👍",
                time: "08:58 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Ready now. Calling you!",
                time: "09:00 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "Thanks for your time earlier!",
                time: "09:45 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Of course! It was a productive discussion.",
                time: "09:46 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "I’ll send over the updated files by noon.",
                time: "09:50 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Great, I’ll review them once they arrive.",
                time: "09:52 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "Just sent them via Drive. Let me know if you have issues accessing.",
                time: "12:03 pm",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Got them. Everything looks good so far!",
                time: "12:10 pm",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "Awesome 😊 Looking forward to your feedback!",
                time: "12:12 pm",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Will get back to you after lunch 🍴",
                time: "12:13 pm",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "No rush, enjoy your lunch! 😄",
                time: "12:14 pm",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Thanks! Talk soon.",
                time: "12:15 pm",
                avatar: "/images/users/user-2.jpg",
            },
        ],
    },
    {
        id: "chat2",
        contact: {
            name: "Noah Smith",
            avatar: "/images/users/user-5.jpg",
        },
        messages: [
            {
                from: "Noah Smith",
                text: "Hey, quick question—did you check the latest design mockups?",
                time: "10:05 am",
                avatar: "/images/users/user-5.jpg",
            },
            {
                from: "me",
                text: "Not yet, just logging in now. Want me to prioritize that?",
                time: "10:06 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Noah Smith",
                text: "Yes please. I need your feedback before the client review at noon.",
                time: "10:07 am",
                avatar: "/images/users/user-5.jpg",
            },
            {
                from: "me",
                text: "Got it. I’ll go through them and send notes in a bit.",
                time: "10:08 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Noah Smith",
                text: "Thanks a ton!",
                time: "10:08 am",
                avatar: "/images/users/user-5.jpg",
            },
            {
                from: "me",
                text: "First impression: very clean. Minor spacing issues though.",
                time: "10:20 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Noah Smith",
                text: "Noted. Fixing those now.",
                time: "10:21 am",
                avatar: "/images/users/user-5.jpg",
            },
            {
                from: "me",
                text: "Sent detailed feedback via email too 📬",
                time: "10:25 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Noah Smith",
                text: "Got it. Appreciate the quick turnaround!",
                time: "10:26 am",
                avatar: "/images/users/user-5.jpg",
            },
        ],
    },
    {
        id: "chat3",
        contact: {
            name: "Liam Turner",
            avatar: "/images/users/user-7.jpg",
        },
        messages: [
            {
                from: "Liam Turner",
                text: "Morning! Did you update the backend endpoints yet?",
                time: "09:15 am",
                avatar: "/images/users/user-7.jpg",
            },
            {
                from: "me",
                text: "Morning! Just pushed the changes to dev branch.",
                time: "09:16 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Liam Turner",
                text: "Awesome, I’ll pull and test on my side.",
                time: "09:17 am",
                avatar: "/images/users/user-7.jpg",
            },
            {
                from: "me",
                text: "Let me know if anything breaks ⚠️",
                time: "09:18 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Liam Turner",
                text: "Looks good so far. Just one CORS error.",
                time: "09:20 am",
                avatar: "/images/users/user-7.jpg",
            },
            {
                from: "me",
                text: "Ah, forgot the whitelist entry. Fixing now.",
                time: "09:21 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Liam Turner",
                text: "Reloaded… and it's working. All green ✅",
                time: "09:23 am",
                avatar: "/images/users/user-7.jpg",
            },
            {
                from: "me",
                text: "Nice! That wraps our side for this sprint then?",
                time: "09:24 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Liam Turner",
                text: "Yep. Good work 💪",
                time: "09:25 am",
                avatar: "/images/users/user-7.jpg",
            },
        ],
    },
    {
        id: "chat4",
        contact: {
            name: "Emma Wilson",
            avatar: "/images/users/user-4.jpg",
        },
        messages: [
            {
                from: "Ava Thompson",
                text: "Hey! Are you available for a quick call? 📞",
                time: "08:55 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Sure, give me 5 minutes. Just wrapping something up.",
                time: "08:57 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "Perfect. Let me know when you're ready 👍",
                time: "08:58 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Ready now. Calling you!",
                time: "09:00 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "Thanks for your time earlier!",
                time: "09:45 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Of course! It was a productive discussion.",
                time: "09:46 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Ava Thompson",
                text: "I’ll send over the updated files by noon.",
                time: "09:50 am",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Great, I’ll review them once they arrive.",
                time: "09:52 am",
                avatar: "/images/users/user-2.jpg",
            },
        ],
    },
    {
        id: "chat5",
        contact: {
            name: "Olivia Martinez",
            avatar: "/images/users/user-8.jpg",
        },
        messages: [
            {
                from: "Olivia Martinez",
                text: "Thanks a ton!",
                time: "10:08 am",
                avatar: "/images/users/user-8.jpg",
            },
            {
                from: "me",
                text: "First impression: very clean. Minor spacing issues though.",
                time: "10:20 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Olivia Martinez",
                text: "Noted. Fixing those now.",
                time: "10:21 am",
                avatar: "/images/users/user-8.jpg",
            },
            {
                from: "me",
                text: "Sent detailed feedback via email too 📬",
                time: "10:25 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Olivia Martinez",
                text: "Got it. Appreciate the quick turnaround!",
                time: "10:26 am",
                avatar: "/images/users/user-8.jpg",
            },
        ],
    },
    {
        id: "chat6",
        contact: {
            name: "William Davis",
            avatar: "/images/users/user-7.jpg",
        },
        messages: [
            {
                from: "William Davis",
                text: "Looks good so far. Just one CORS error.",
                time: "09:20 am",
                avatar: "/images/users/user-7.jpg",
            },
            {
                from: "me",
                text: "Ah, forgot the whitelist entry. Fixing now.",
                time: "09:21 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "William Davis",
                text: "Reloaded… and it's working. All green ✅",
                time: "09:23 am",
                avatar: "/images/users/user-7.jpg",
            },
            {
                from: "me",
                text: "Nice! That wraps our side for this sprint then?",
                time: "09:24 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "William Davis",
                text: "Yep. Good work 💪",
                time: "09:25 am",
                avatar: "/images/users/user-7.jpg",
            },
        ],
    },
    {
        id: "chat7",
        contact: {
            name: "Sophia Moore",
            avatar: "/images/users/user-10.jpg",
        },
        messages: [
            {
                from: "me",
                text: "Of course! It was a productive discussion.",
                time: "09:46 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Sophia Moore",
                text: "I’ll send over the updated files by noon.",
                time: "09:50 am",
                avatar: "/images/users/user-10.jpg",
            },
            {
                from: "me",
                text: "Great, I’ll review them once they arrive.",
                time: "09:52 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Sophia Moore",
                text: "Just sent them via Drive. Let me know if you have issues accessing.",
                time: "12:03 pm",
                avatar: "/images/users/user-10.jpg",
            },
            {
                from: "me",
                text: "Got them. Everything looks good so far!",
                time: "12:10 pm",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Sophia Moore",
                text: "Awesome 😊 Looking forward to your feedback!",
                time: "12:12 pm",
                avatar: "/images/users/user-10.jpg",
            },
            {
                from: "me",
                text: "Will get back to you after lunch 🍴",
                time: "12:13 pm",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Sophia Moore",
                text: "No rush, enjoy your lunch! 😄",
                time: "12:14 pm",
                avatar: "/images/users/user-10.jpg",
            },
            {
                from: "me",
                text: "Thanks! Talk soon.",
                time: "12:15 pm",
                avatar: "/images/users/user-2.jpg",
            },
        ],
    },
    {
        id: "chat8",
        contact: {
            name: "Jackson Lee",
            avatar: "/images/users/user-2.jpg",
        },
        messages: [
            {
                from: "Jackson Lee",
                text: "Thanks a ton!",
                time: "10:08 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "me",
                text: "First impression: very clean. Minor spacing issues though.",
                time: "10:20 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Jackson Lee",
                text: "Noted. Fixing those now.",
                time: "10:21 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "me",
                text: "Sent detailed feedback via email too 📬",
                time: "10:25 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Jackson Lee",
                text: "Got it. Appreciate the quick turnaround!",
                time: "10:26 am",
                avatar: "/images/users/user-2.jpg",
            },
        ],
    },
    {
        id: "chat9",
        contact: {
            name: "Chloe Anderson",
            avatar: "/images/users/user-3.jpg",
        },
        messages: [
            {
                from: "Chloe Anderson",
                text: "Looks good so far. Just one CORS error.",
                time: "09:20 am",
                avatar: "/images/users/user-3.jpg",
            },
            {
                from: "me",
                text: "Ah, forgot the whitelist entry. Fixing now.",
                time: "09:21 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Chloe Anderson",
                text: "Reloaded… and it's working. All green ✅",
                time: "09:23 am",
                avatar: "/images/users/user-3.jpg",
            },
            {
                from: "me",
                text: "Nice! That wraps our side for this sprint then?",
                time: "09:24 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Chloe Anderson",
                text: "Yep. Good work 💪",
                time: "09:25 am",
                avatar: "/images/users/user-3.jpg",
            },
        ],
    },
    {
        id: "chat10",
        contact: {
            name: "Lucas Wright",
            avatar: "/images/users/user-4.jpg",
        },
        messages: [
            {
                from: "me",
                text: "Great, I’ll review them once they arrive.",
                time: "09:52 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Lucas Wright",
                text: "Just sent them via Drive. Let me know if you have issues accessing.",
                time: "12:03 pm",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Got them. Everything looks good so far!",
                time: "12:10 pm",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Lucas Wright",
                text: "Awesome 😊 Looking forward to your feedback!",
                time: "12:12 pm",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Will get back to you after lunch 🍴",
                time: "12:13 pm",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Lucas Wright",
                text: "No rush, enjoy your lunch! 😄",
                time: "12:14 pm",
                avatar: "/images/users/user-4.jpg",
            },
            {
                from: "me",
                text: "Thanks! Talk soon.",
                time: "12:15 pm",
                avatar: "/images/users/user-2.jpg",
            },
        ],
    },
    {
        id: "chat11",
        contact: {
            name: "Mia Scott",
            avatar: "/images/users/user-6.jpg",
        },
        messages: [
            {
                from: "me",
                text: "First impression: very clean. Minor spacing issues though.",
                time: "10:20 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Mia Scott",
                text: "Noted. Fixing those now.",
                time: "10:21 am",
                avatar: "/images/users/user-6.jpg",
            },
            {
                from: "me",
                text: "Sent detailed feedback via email too 📬",
                time: "10:25 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Mia Scott",
                text: "Got it. Appreciate the quick turnaround!",
                time: "10:26 am",
                avatar: "/images/users/user-6.jpg",
            },
        ],
    },
    {
        id: "chat12",
        contact: {
            name: "Benjamin Clark",
            avatar: "/images/users/user-9.jpg",
        },
        messages: [
            {
                from: "Benjamin Clark",
                text: "Looks good so far. Just one CORS error.",
                time: "09:20 am",
                avatar: "/images/users/user-9.jpg",
            },
            {
                from: "me",
                text: "Ah, forgot the whitelist entry. Fixing now.",
                time: "09:21 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Benjamin Clark",
                text: "Reloaded… and it's working. All green ✅",
                time: "09:23 am",
                avatar: "/images/users/user-9.jpg",
            },
            {
                from: "me",
                text: "Nice! That wraps our side for this sprint then?",
                time: "09:24 am",
                avatar: "/images/users/user-2.jpg",
            },
            {
                from: "Benjamin Clark",
                text: "Yep. Good work 💪",
                time: "09:25 am",
                avatar: "/images/users/user-9.jpg",
            },
        ],
    },
]

// Example usage
document.addEventListener("DOMContentLoaded", () => {
    try {
        new Chat({
            data: chatData,
        })
    } catch (error) {
        console.error("Failed to initialize chat:", error)
    }
})
