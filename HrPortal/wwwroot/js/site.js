// Light and dark switching.
//
// The theme itself is applied in a small inline script in the
// layout head, before first paint, so the page never flashes the
// wrong colours. This only wires up the toggle button and keeps
// its icon in step.

(function () {
    "use strict";

    var STORAGE_KEY = "hrportal-theme";

    var root = document.documentElement;
    var button = document.getElementById("themeToggle");
    var icon = document.getElementById("themeToggleIcon");

    if (!button || !icon) {
        return;
    }

    function currentTheme() {
        return root.getAttribute("data-bs-theme") === "dark"
            ? "dark"
            : "light";
    }

    function paintIcon() {
        // Shows the thing you would switch to.
        icon.setAttribute(
            "href",
            currentTheme() === "dark" ? "#i-sun" : "#i-moon");
    }

    button.addEventListener("click", function () {
        var next = currentTheme() === "dark" ? "light" : "dark";

        root.setAttribute("data-bs-theme", next);

        try {
            localStorage.setItem(STORAGE_KEY, next);
        } catch (e) {
            // Storage blocked. The change still applies for this
            // page view, it just will not be remembered.
        }

        paintIcon();
    });

    paintIcon();
})();
