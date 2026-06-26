/**
 * Template Name: Inspinia - Admin & Dashboard Template
 * By (Author): WebAppLayers
 * Module/App (File Name): Video Player Js
 */

// standard HTML5 players

// standard HTML5 players
try {
    new Plyr("#player1")
    new Plyr("#player3")
} catch (err) {
    console.error("Plyr: Error initializing player element", err)
}

// YouTube (no extra library required—Plyr handles the provider)
try {
    new Plyr("#yt1", {
        youtube: { modestbranding: 1, rel: 0, playsinline: 1 },
    })
} catch (err) {
    console.error("Plyr: Error initializing YouTube player element", err)
}

// Vimeo (no extra library required—Plyr handles the provider)
try {
    new Plyr("#vimeo1", {
        vimeo: { byline: false, portrait: false, title: false },
    })
} catch (err) {
    console.error("Plyr: Error initializing Vimeo player element", err)
}

// audio
document.addEventListener("DOMContentLoaded", function () {
    try {
        window.Plyr && new Plyr("#player-audio")
    } catch (err) {
        console.error("Plyr: Error initializing audio player element", err)
    }
})
