(function () {
    "use strict";

    var key = "portal-theme";
    var root = document.documentElement;

    function read() {
        try {
            return window.localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    }

    function save(theme) {
        try {
            window.localStorage.setItem(key, theme);
        } catch (e) {
            return;
        }
    }

    function apply(theme) {
        var dark = theme === "dark";

        root.setAttribute("data-theme", dark ? "dark" : "light");
        root.setAttribute("data-bs-theme", dark ? "dark" : "light");
    }

    function isDark() {
        return root.getAttribute("data-theme") === "dark";
    }

    apply(read());

    document.addEventListener("DOMContentLoaded", function () {
        var button = document.getElementById("themeToggle");

        if (!button) {
            return;
        }

        function describe() {
            var label = isDark() ? "Switch to light theme" : "Switch to dark theme";

            button.setAttribute("aria-label", label);
            button.setAttribute("title", label);
        }

        describe();

        button.addEventListener("click", function () {
            var next = isDark() ? "light" : "dark";

            apply(next);
            save(next);
            describe();
        });
    });
}());
