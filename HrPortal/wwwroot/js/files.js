(function () {
    "use strict";

    var body = document.body;
    var maxBytes = parseInt(body.getAttribute("data-upload-max") || "0", 10);
    var defaultTypes = (body.getAttribute("data-upload-types") || "")
        .split(",")
        .map(function (t) { return t.trim().toLowerCase(); })
        .filter(Boolean);

    var words = {
        ".pdf": "PDF", ".doc": "Word", ".docx": "Word", ".xls": "Excel", ".xlsx": "Excel",
        ".ppt": "PowerPoint", ".pptx": "PowerPoint", ".txt": "text", ".csv": "CSV",
        ".png": "pictures", ".jpg": "pictures", ".jpeg": "pictures", "application/pdf": "PDF"
    };

    function size(bytes) {
        if (bytes < 1024) { return bytes + " B"; }
        if (bytes < 1048576) { return Math.round(bytes / 1024) + " KB"; }
        return (bytes / 1048576).toFixed(1).replace(/\.0$/, "") + " MB";
    }

    function extOf(name) {
        var dot = name.lastIndexOf(".");
        return dot < 0 ? "" : name.slice(dot).toLowerCase();
    }

    function iconFor(name) {
        var ext = extOf(name);
        if (ext === ".pdf") { return "fa-file-pdf"; }
        if (ext === ".doc" || ext === ".docx") { return "fa-file-word"; }
        if (ext === ".xls" || ext === ".xlsx") { return "fa-file-excel"; }
        if (ext === ".csv") { return "fa-file-csv"; }
        if (ext === ".ppt" || ext === ".pptx") { return "fa-file-powerpoint"; }
        if (ext === ".png" || ext === ".jpg" || ext === ".jpeg") { return "fa-file-image"; }
        if (ext === ".txt") { return "fa-file-lines"; }
        if (ext === ".zip") { return "fa-file-zipper"; }
        return "fa-file";
    }

    function typesOf(input) {
        var accept = input.getAttribute("accept");
        if (!accept) { return defaultTypes; }
        return accept.split(",").map(function (t) { return t.trim().toLowerCase(); }).filter(Boolean);
    }

    function describe(types) {
        var seen = [];
        types.forEach(function (t) {
            var word = words[t] || t.replace(/^\./, "").toUpperCase();
            if (seen.indexOf(word) < 0) { seen.push(word); }
        });
        if (seen.length < 2) { return seen.join(""); }
        return seen.slice(0, -1).join(", ") + " or " + seen[seen.length - 1];
    }

    function accepts(file, types) {
        if (!types.length) { return true; }
        var ext = extOf(file.name);
        return types.some(function (t) {
            if (t.charAt(0) === ".") { return t === ext; }
            if (t.slice(-2) === "/*") { return (file.type || "").indexOf(t.slice(0, -1)) === 0; }
            return t === (file.type || "").toLowerCase();
        });
    }

    function problemWith(file, types) {
        if (file.size === 0) {
            return file.name + " is empty.";
        }
        if (maxBytes && file.size > maxBytes) {
            return file.name + " is " + size(file.size) + ". Files must be " + size(maxBytes) + " or smaller.";
        }
        if (!accepts(file, types)) {
            return file.name + " cannot be uploaded here. Use " + describe(types) + ".";
        }
        return null;
    }

    function make(tag, className, text) {
        var el = document.createElement(tag);
        if (className) { el.className = className; }
        if (text) { el.textContent = text; }
        return el;
    }

    function icon(name) {
        var i = make("i", "fas " + name);
        i.setAttribute("aria-hidden", "true");
        return i;
    }

    function enhance(input) {
        if (input.getAttribute("data-upload") === "plain" || input.closest(".drop-zone")) {
            return;
        }

        var types = typesOf(input);
        var many = input.multiple;
        var zone = make("div", "drop-zone");

        if (input.classList.contains("form-control-sm")) { zone.classList.add("is-compact"); }
        ["flex-grow-1", "w-auto", "w-100"].forEach(function (c) {
            if (input.classList.contains(c)) { zone.classList.add(c); }
        });

        input.parentNode.insertBefore(zone, input);
        zone.appendChild(input);
        input.classList.add("drop-zone-input");

        var prompt = make("div", "drop-zone-prompt");
        prompt.appendChild(icon("fa-cloud-arrow-up"));
        var text = make("span", "drop-zone-text");
        text.appendChild(make("strong", null, many ? "Drop files here" : "Drop a file here"));
        text.appendChild(document.createTextNode(" or "));
        text.appendChild(make("span", "drop-zone-link", many ? "choose them" : "choose one"));
        var hint = [];
        if (types.length) { hint.push(describe(types)); }
        if (maxBytes) { hint.push("up to " + size(maxBytes)); }
        if (hint.length) { text.appendChild(make("small", null, hint.join(", "))); }
        prompt.appendChild(text);

        var list = make("ul", "drop-zone-files");
        var error = make("div", "drop-zone-error");
        error.setAttribute("role", "alert");
        var progress = make("div", "drop-zone-progress");
        progress.appendChild(make("div", "drop-zone-bar"));
        var status = make("div", "drop-zone-status");
        status.setAttribute("aria-live", "polite");

        zone.appendChild(prompt);
        zone.appendChild(list);
        zone.appendChild(error);
        zone.appendChild(progress);
        zone.appendChild(status);

        function render() {
            list.innerHTML = "";
            var files = Array.prototype.slice.call(input.files || []);
            var problems = [];

            files.forEach(function (file) {
                var item = make("li", "drop-zone-file");
                if ((file.type || "").indexOf("image/") === 0 && file.size <= 8388608 && window.FileReader) {
                    var img = make("img", "drop-zone-thumb");
                    img.alt = "";
                    var reader = new FileReader();
                    reader.onload = function () { img.src = reader.result; };
                    reader.readAsDataURL(file);
                    item.appendChild(img);
                } else {
                    var badge = make("span", "drop-zone-type");
                    badge.appendChild(icon(iconFor(file.name)));
                    item.appendChild(badge);
                }
                var meta = make("span", "drop-zone-meta");
                meta.appendChild(make("span", "drop-zone-name", file.name));
                meta.appendChild(make("small", null, size(file.size)));
                item.appendChild(meta);
                list.appendChild(item);

                var problem = problemWith(file, types);
                if (problem) { problems.push(problem); }
            });

            if (files.length) {
                var remove = make("button", "drop-zone-remove");
                remove.type = "button";
                remove.appendChild(icon("fa-xmark"));
                remove.appendChild(document.createTextNode(files.length > 1 ? " Remove all" : " Remove"));
                remove.addEventListener("click", function () {
                    input.value = "";
                    render();
                    input.focus();
                });
                var last = list.lastChild;
                last.appendChild(remove);
            }

            zone.classList.toggle("has-file", files.length > 0);
            zone.classList.toggle("is-invalid", problems.length > 0);
            error.textContent = problems.join(" ");
            input.setCustomValidity(problems.join(" "));
        }

        input.addEventListener("change", render);

        var depth = 0;
        zone.addEventListener("dragenter", function (e) {
            if (!e.dataTransfer || Array.prototype.indexOf.call(e.dataTransfer.types || [], "Files") < 0) { return; }
            depth++;
            zone.classList.add("is-over");
        });
        zone.addEventListener("dragleave", function () {
            depth = Math.max(0, depth - 1);
            if (!depth) { zone.classList.remove("is-over"); }
        });
        zone.addEventListener("dragover", function (e) {
            if (e.dataTransfer) { e.preventDefault(); e.dataTransfer.dropEffect = "copy"; }
        });
        zone.addEventListener("drop", function (e) {
            depth = 0;
            zone.classList.remove("is-over");
            if (!e.dataTransfer || !e.dataTransfer.files.length) { return; }
            e.preventDefault();
            try {
                var dt = new DataTransfer();
                Array.prototype.slice.call(e.dataTransfer.files, 0, many ? undefined : 1)
                    .forEach(function (f) { dt.items.add(f); });
                input.files = dt.files;
            } catch (err) {
                input.files = e.dataTransfer.files;
            }
            input.dispatchEvent(new Event("change", { bubbles: true }));
        });

        zone.uploadProgress = function (percent, label) {
            zone.classList.add("is-uploading");
            progress.firstChild.style.width = percent + "%";
            status.textContent = label;
        };

        zone.uploadDone = function (message) {
            zone.classList.remove("is-uploading");
            progress.firstChild.style.width = "0";
            status.textContent = "";
            if (message) {
                error.textContent = message;
                zone.classList.add("is-invalid");
            }
        };

        render();
    }

    function enhanceAll(root) {
        Array.prototype.forEach.call(
            (root || document).querySelectorAll("input[type=file]"),
            enhance);
    }

    function filenameFrom(header, fallback) {
        if (!header) { return fallback; }
        var star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(header);
        if (star) {
            try { return decodeURIComponent(star[1].trim().replace(/^"|"$/g, "")); } catch (e) { }
        }
        var plain = /filename="?([^";]+)"?/i.exec(header);
        return plain ? plain[1].trim() : fallback;
    }

    function save(blob, name) {
        var url = URL.createObjectURL(blob);
        var a = document.createElement("a");
        a.href = url;
        a.download = name;
        a.setAttribute("data-download", "off");
        a.style.display = "none";
        document.body.appendChild(a);
        a.click();
        window.setTimeout(function () {
            URL.revokeObjectURL(url);
            a.remove();
        }, 1500);
    }

    var toasts = null;

    function toast(message, kind) {
        if (!toasts) {
            toasts = make("div", "file-toasts");
            toasts.setAttribute("aria-live", "polite");
            document.body.appendChild(toasts);
        }
        var item = make("div", "file-toast" + (kind ? " is-" + kind : ""));
        item.appendChild(icon(kind === "error" ? "fa-circle-exclamation" : "fa-circle-check"));
        item.appendChild(make("span", null, message));
        toasts.appendChild(item);
        window.setTimeout(function () { item.classList.add("is-leaving"); }, 4200);
        window.setTimeout(function () { item.remove(); }, 4700);
    }

    function submitButtonsOf(form, submitter) {
        if (submitter) { return [submitter]; }
        var first = form.querySelector("button[type=submit], input[type=submit], button:not([type])");
        return first ? [first] : [];
    }

    document.addEventListener("submit", function (event) {
        var form = event.target;

        if (!(form instanceof HTMLFormElement) || event.defaultPrevented) { return; }
        if (form.getAttribute("data-upload") === "plain") { return; }
        if ((form.getAttribute("method") || "get").toLowerCase() !== "post") { return; }
        if ((form.enctype || "").toLowerCase() !== "multipart/form-data") { return; }
        if (form.target && form.target !== "_self") { return; }
        if (!window.FormData || !window.XMLHttpRequest || !window.Blob) { return; }

        var inputs = Array.prototype.slice.call(form.querySelectorAll("input[type=file]"));
        var withFiles = inputs.filter(function (i) { return i.files && i.files.length; });
        if (!withFiles.length) { return; }

        event.preventDefault();

        var data = new FormData(form);
        var submitter = event.submitter;
        if (submitter && submitter.name) { data.append(submitter.name, submitter.value); }

        var zones = withFiles
            .map(function (i) { return i.closest(".drop-zone"); })
            .filter(function (z) { return z && z.uploadProgress; });

        var buttons = submitButtonsOf(form, submitter);
        buttons.forEach(function (b) {
            b.classList.add("is-submitting");
            b.setAttribute("aria-busy", "true");
            b.disabled = true;
        });

        function release() {
            buttons.forEach(function (b) {
                b.classList.remove("is-submitting");
                b.removeAttribute("aria-busy");
                b.disabled = false;
            });
        }

        function fail(message) {
            release();
            zones.forEach(function (z) { z.uploadDone(message); });
            if (!zones.length) { toast(message, "error"); }
        }

        zones.forEach(function (z) { z.uploadProgress(0, "Starting upload…"); });

        var xhr = new XMLHttpRequest();
        xhr.open("POST", form.action || window.location.href);
        xhr.responseType = "blob";

        xhr.upload.onprogress = function (e) {
            if (!e.lengthComputable) { return; }
            var percent = Math.round(e.loaded / e.total * 100);
            zones.forEach(function (z) {
                z.uploadProgress(percent, percent < 100
                    ? "Uploading… " + percent + "% of " + size(e.total)
                    : "Uploaded. Saving…");
            });
        };

        xhr.onload = function () {
            var type = (xhr.getResponseHeader("Content-Type") || "").toLowerCase();

            if (xhr.status === 413) {
                fail("That file is too large to upload. Files must be " + size(maxBytes) + " or smaller.");
                return;
            }

            if (type.indexOf("text/html") === 0) {
                xhr.response.text().then(function (html) {
                    if (xhr.responseURL) {
                        try { window.history.replaceState(null, "", xhr.responseURL); } catch (e) { }
                    }
                    document.open();
                    document.write(html);
                    document.close();
                });
                return;
            }

            if (xhr.status >= 400) {
                fail("The upload did not go through. Please try again.");
                return;
            }

            release();
            zones.forEach(function (z) { z.uploadDone(); });
            var name = filenameFrom(xhr.getResponseHeader("Content-Disposition"), "download");
            save(xhr.response, name);
            toast("Downloaded " + name);
        };

        xhr.onerror = function () {
            fail("The upload did not reach the portal. Check your connection and try again.");
        };

        xhr.send(data);
    });

    var downloadPath = /\/(Form|List|Pdf|Report|Download|Template|Statement|Attachment|Export[A-Za-z]*|Csv)(\/|$)/i;

    function isDownload(link) {
        if (link.getAttribute("data-download") === "off") { return false; }
        if (link.protocol !== "http:" && link.protocol !== "https:") { return false; }
        if (link.target && link.target !== "_self") { return false; }
        if (link.origin !== window.location.origin) { return false; }
        if (/^\/Backup/i.test(link.pathname)) { return false; }
        return link.hasAttribute("download") || link.getAttribute("data-download") === "on" || downloadPath.test(link.pathname);
    }

    document.addEventListener("click", function (event) {
        var link = event.target.closest ? event.target.closest("a[href]") : null;

        if (!link || event.defaultPrevented || event.button !== 0 ||
            event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }

        if (!isDownload(link) || !window.fetch || !window.Blob) { return; }
        if (link.classList.contains("is-downloading")) { event.preventDefault(); return; }

        event.preventDefault();

        var spinner = make("span", "spinner-border spinner-border-sm file-spinner");
        spinner.setAttribute("aria-hidden", "true");
        link.classList.add("is-downloading");
        link.setAttribute("aria-busy", "true");
        link.insertBefore(spinner, link.firstChild);

        function finish() {
            link.classList.remove("is-downloading");
            link.removeAttribute("aria-busy");
            spinner.remove();
        }

        fetch(link.href, { credentials: "same-origin" })
            .then(function (res) {
                var type = (res.headers.get("Content-Type") || "").toLowerCase();
                if (!res.ok || type.indexOf("text/html") === 0) {
                    finish();
                    window.location.href = link.href;
                    return null;
                }
                var fallback = (link.textContent || "download").trim() || "download";
                var name = filenameFrom(res.headers.get("Content-Disposition"), fallback);
                return res.blob().then(function (blob) {
                    save(blob, name);
                    finish();
                    toast("Downloaded " + name);
                });
            })
            .catch(function () {
                finish();
                toast("The download did not work. Check your connection and try again.", "error");
            });
    });

    enhanceAll(document);
}());
