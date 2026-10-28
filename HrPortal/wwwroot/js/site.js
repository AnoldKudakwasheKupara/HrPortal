// Sidebar drawer, matching the IT Service Management System.

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

// The sidebar keeps its own scroll position, and every page is a
// fresh load, so without this the menu starts at the top each time.
// On a long list that put the item you were on below the fold, and
// the menu looked as though it had forgotten where you were.
//
// This script sits at the end of the body, so the nav is already
// parsed and the position can be put back before the first paint
// rather than visibly jumping afterwards.
(function () {
    "use strict";

    var KEY = "sidebarScroll";

    var nav = document.getElementById("sidebarNav");

    if (!nav) {
        return;
    }

    // Storage is unavailable in a private window and throws rather
    // than returning nothing, and a menu that will not render is a
    // worse answer than a menu that forgets.
    function remember(value) {
        try {
            window.sessionStorage.setItem(KEY, value);
        } catch (e) {
            // Nothing to do: the menu simply starts at the top.
        }
    }

    function remembered() {
        try {
            return parseInt(window.sessionStorage.getItem(KEY), 10);
        } catch (e) {
            return NaN;
        }
    }

    var saved = remembered();

    if (!isNaN(saved)) {
        nav.scrollTop = saved;
    }

    // Wherever the menu ended up, the page you are on has to be
    // visible on it: arriving from a link elsewhere, or on the
    // first page of a session, there is no position to put back.
    var current = nav.querySelector(".nav-item.active");

    if (current) {
        // Measured as rectangles rather than through offsetTop. The
        // nav is not a positioned element, so offsetTop would be the
        // distance from the page, not from the menu, and the sums
        // below would be out by wherever the menu happens to sit.
        var navBox = nav.getBoundingClientRect();
        var itemBox = current.getBoundingClientRect();

        if (itemBox.top < navBox.top || itemBox.bottom > navBox.bottom) {
            // Centred rather than scrolled just into view, so the
            // items either side of it are visible too. The browser
            // clamps this to the ends of the list on its own.
            nav.scrollTop += (itemBox.top - navBox.top) -
                (nav.clientHeight - itemBox.height) / 2;
        }
    }

    var pending = null;

    nav.addEventListener("scroll", function () {
        // Scrolling fires continuously; writing on every event
        // would put a storage call in the middle of the scroll.
        if (pending !== null) {
            window.clearTimeout(pending);
        }

        pending = window.setTimeout(function () {
            pending = null;
            remember(nav.scrollTop);
        }, 120);
    });

    nav.addEventListener("click", function (event) {
        if (event.target.closest(".nav-item")) {
            // Before the page goes: a click within the debounce
            // window would otherwise navigate away with the old
            // position still stored.
            remember(nav.scrollTop);
            closeSidebar();
        }
    });
})();
