// Sidebar drawer, matching the IT Service Management System.
//
// The sidebar is a fixed rail on desktop and slides in over an
// overlay on small screens. The markup calls these by name from
// onclick attributes, so they have to be global.

function toggleSidebar() {
    "use strict";

    var sidebar = document.getElementById("sidebar");
    var overlay = document.getElementById("sidebarOverlay");

    if (!sidebar || !overlay) {
        return;
    }

    sidebar.classList.toggle("active");

    if (sidebar.classList.contains("active")) {
        overlay.classList.add("active");
        document.body.style.overflow = "hidden";
    } else {
        overlay.classList.remove("active");
        document.body.style.overflow = "";
    }
}

function closeSidebar() {
    "use strict";

    var sidebar = document.getElementById("sidebar");
    var overlay = document.getElementById("sidebarOverlay");

    if (!sidebar || !overlay) {
        return;
    }

    sidebar.classList.remove("active");
    overlay.classList.remove("active");
    document.body.style.overflow = "";
}

// Following a link inside the drawer should close it, otherwise
// the new page loads behind an overlay on a phone.
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var nav = document.getElementById("sidebarNav");

        if (!nav) {
            return;
        }

        nav.addEventListener("click", function (event) {
            if (event.target.closest(".nav-item")) {
                closeSidebar();
            }
        });
    });
})();
