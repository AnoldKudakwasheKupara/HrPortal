(function () {
    "use strict";

    var INK = "#14213d";
    var MAX_LENGTH = 400000;

    function inkBounds(canvas) {
        var context = canvas.getContext("2d");
        var pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
        var left = canvas.width, top = canvas.height, right = -1, bottom = -1;

        for (var y = 0; y < canvas.height; y++) {
            for (var x = 0; x < canvas.width; x++) {
                if (pixels[(y * canvas.width + x) * 4 + 3] > 20) {
                    if (x < left) { left = x; }
                    if (x > right) { right = x; }
                    if (y < top) { top = y; }
                    if (y > bottom) { bottom = y; }
                }
            }
        }

        return right < 0 ? null : { x: left, y: top, w: right - left + 1, h: bottom - top + 1 };
    }

    function dataOf(canvas) {
        var ink = inkBounds(canvas);

        if (!ink || (ink.w < 12 && ink.h < 12)) { return ""; }

        var pad = 6;
        var out = document.createElement("canvas");
        out.width = Math.max(40, ink.w + pad * 2);
        out.height = Math.max(24, ink.h + pad * 2);
        out.getContext("2d").drawImage(canvas, ink.x, ink.y, ink.w, ink.h, pad, pad, ink.w, ink.h);

        return out.toDataURL("image/png");
    }

    function clearPaper(canvas) {
        var context = canvas.getContext("2d");
        var image = context.getImageData(0, 0, canvas.width, canvas.height);
        var pixels = image.data;

        for (var i = 0; i < pixels.length; i += 4) {
            var light = (pixels[i] * 299 + pixels[i + 1] * 587 + pixels[i + 2] * 114) / 1000;
            var strength = pixels[i + 3] / 255 * Math.max(0, Math.min(1, (175 - light) / 70));

            pixels[i] = 0x14;
            pixels[i + 1] = 0x21;
            pixels[i + 2] = 0x3d;
            pixels[i + 3] = Math.round(strength * 255);
        }

        context.putImageData(image, 0, 0);
    }

    function setUpPad(pad) {
        var canvas = pad.querySelector("canvas");
        var context = canvas.getContext("2d");
        var field = pad.querySelector("input[type=hidden]");
        var error = pad.querySelector("[data-sign-error]");
        var last = null;

        function say(text) {
            error.textContent = text || "";
            error.hidden = !text;
        }

        function at(event) {
            var box = canvas.getBoundingClientRect();

            return {
                x: (event.clientX - box.left) * canvas.width / box.width,
                y: (event.clientY - box.top) * canvas.height / box.height
            };
        }

        function stroke(from, to) {
            context.strokeStyle = INK;
            context.lineWidth = 3.2;
            context.lineCap = "round";
            context.lineJoin = "round";
            context.beginPath();
            context.moveTo(from.x, from.y);
            context.lineTo(to.x, to.y);
            context.stroke();
        }

        canvas.addEventListener("pointerdown", function (event) {
            event.preventDefault();
            try { canvas.setPointerCapture(event.pointerId); } catch (ignored) { last = null; }
            last = at(event);
            stroke(last, last);
            say("");
        });

        canvas.addEventListener("pointermove", function (event) {
            if (!last) { return; }
            var next = at(event);
            stroke(last, next);
            last = next;
        });

        ["pointerup", "pointercancel"].forEach(function (name) {
            canvas.addEventListener(name, function () { last = null; });
        });

        pad.querySelector("[data-sign-clear]").addEventListener("click", function () {
            context.clearRect(0, 0, canvas.width, canvas.height);
            say("");
        });

        pad.querySelector("[data-sign-choose]").addEventListener("click", function () {
            var picker = document.createElement("input");
            picker.type = "file";
            picker.accept = "image/png,image/jpeg";

            picker.addEventListener("change", function () {
                if (!picker.files.length) { return; }

                var reader = new FileReader();

                reader.onload = function () {
                    var picture = new Image();

                    picture.onload = function () {
                        var scale = Math.min(canvas.width / picture.width, canvas.height / picture.height);
                        var w = picture.width * scale;
                        var h = picture.height * scale;

                        context.clearRect(0, 0, canvas.width, canvas.height);
                        context.drawImage(picture, (canvas.width - w) / 2, (canvas.height - h) / 2, w, h);
                        clearPaper(canvas);
                        say(inkBounds(canvas) ? "" : "No signature could be seen in that picture. Use dark ink on white paper.");
                    };

                    picture.onerror = function () { say("That picture could not be read. Use a PNG or JPEG."); };
                    picture.src = reader.result;
                };

                reader.readAsDataURL(picker.files[0]);
            });

            picker.click();
        });

        return function () {
            field.value = dataOf(canvas);

            if (field.value.length > MAX_LENGTH) {
                say("That signature is too detailed. Draw it in the box instead.");
                return false;
            }

            if (!field.value && pad.getAttribute("data-required") === "true") {
                say("Sign in the box first.");
                return false;
            }

            return true;
        };
    }

    document.querySelectorAll("form").forEach(function (form) {
        var pads = Array.prototype.map.call(form.querySelectorAll("[data-sign-pad]"), setUpPad);

        if (!pads.length) { return; }

        form.addEventListener("submit", function (event) {
            var ready = pads.map(function (check) { return check(); });

            if (ready.indexOf(false) >= 0) {
                event.preventDefault();
                event.stopImmediatePropagation();
                var first = form.querySelector("[data-sign-error]:not([hidden])");
                if (first) { first.scrollIntoView({ block: "center" }); }
            }
        }, true);
    });

    document.querySelectorAll("[data-rows]").forEach(function (group) {
        var rows = Array.prototype.slice.call(group.querySelectorAll("[data-row]"));
        var add = group.querySelector("[data-add-row]");

        function filled(row) {
            return Array.prototype.some.call(row.querySelectorAll("input:not([type=hidden])"), function (input) { return input.value; });
        }

        function refresh() {
            add.hidden = !rows.some(function (row) { return row.hidden; });
        }

        rows.forEach(function (row, index) { row.hidden = index > 0 && !filled(row); });

        add.hidden = false;
        add.addEventListener("click", function () {
            var next = rows.filter(function (row) { return row.hidden; })[0];

            if (next) {
                next.hidden = false;
                next.querySelector("input:not([type=hidden]), select").focus();
            }

            refresh();
        });

        rows.forEach(function (row) {
            var remove = row.querySelector("[data-clear-row]");

            remove.hidden = false;
            remove.addEventListener("click", function () {
                row.querySelectorAll("input:not([type=hidden])").forEach(function (input) { input.value = ""; });
                row.querySelectorAll("select").forEach(function (select) { select.selectedIndex = 0; });
                row.hidden = rows.filter(function (r) { return !r.hidden; }).length > 1;
                refresh();
            });
        });

        refresh();
    });
}());
