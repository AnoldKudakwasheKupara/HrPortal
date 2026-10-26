// Moves the underline in the front page's navigation to whichever
// section is currently in view. Purely decorative: without it the
// links still jump to their sections.
(function () {
    'use strict';

    var nav = document.querySelector('.public-nav');

    if (!nav) {
        return;
    }

    var links = Array.prototype.slice.call(nav.querySelectorAll('a[href^="#"]'));

    var sections = links
        .map(function (link) {
            var target = document.getElementById(link.hash.slice(1));
            return target ? { link: link, target: target } : null;
        })
        .filter(Boolean);

    if (sections.length === 0) {
        return;
    }

    // How much of the section is on screen, in pixels. Ranking by
    // the share of the element visible instead would hand it to
    // whichever section is smallest, since a short one is wholly on
    // screen while a tall one never is.
    function visibleHeight(el) {
        var box = el.getBoundingClientRect();
        var height = window.innerHeight || 0;

        return Math.max(0, Math.min(box.bottom, height) - Math.max(box.top, 0));
    }

    function update() {
        var best = null;
        var bestSeen = 0;

        sections.forEach(function (pair) {
            var seen = visibleHeight(pair.target);

            if (seen > bestSeen) {
                bestSeen = seen;
                best = pair;
            }
        });

        // At the very bottom the last section wins outright, so the
        // underline does not stick on whatever sat above it.
        var atBottom = window.innerHeight + window.scrollY >=
            document.documentElement.scrollHeight - 2;

        if (atBottom) {
            best = sections[sections.length - 1];
        }

        // Every link is cleared, not just the ones with a section on
        // the page. A link whose target is missing would otherwise
        // keep the class it was served with and never let go of it.
        links.forEach(function (link) {
            link.classList.toggle('is-active', !!best && link === best.link);
        });
    }

    // Measuring four elements is cheap enough to do on the event
    // itself. Deferring to requestAnimationFrame would be tidier,
    // but it does not run in a page the browser is not painting,
    // and then the underline simply stops following the page.
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    update();
}());
