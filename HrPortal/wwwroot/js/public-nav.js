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

        var atBottom = window.innerHeight + window.scrollY >=
            document.documentElement.scrollHeight - 2;

        if (atBottom) {
            best = sections[sections.length - 1];
        }

        links.forEach(function (link) {
            link.classList.toggle('is-active', !!best && link === best.link);
        });
    }

    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    update();
}());
