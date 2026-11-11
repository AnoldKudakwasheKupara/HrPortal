(function () {
    "use strict";

    if (!window.bootstrap || !window.bootstrap.Tab) {
        return;
    }

    function show(target) {
        var button = document.querySelector("[data-bs-toggle='tab'][data-bs-target='" + target + "']");

        if (button) {
            window.bootstrap.Tab.getOrCreateInstance(button).show();
        }
    }

    if (location.hash) {
        show(location.hash);
    }

    document.querySelectorAll("[data-bs-toggle='tab']").forEach(function (button) {
        button.addEventListener("shown.bs.tab", function () {
            history.replaceState(null, "", button.getAttribute("data-bs-target"));
        });
    });

    document.querySelectorAll("[data-show-tab]").forEach(function (link) {
        link.addEventListener("click", function () {
            show(link.getAttribute("data-show-tab"));
            window.scrollTo({ top: 0, behavior: "smooth" });
        });
    });
})();
