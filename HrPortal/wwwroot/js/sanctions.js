(function () {
    var form = document.getElementById("sanctions-form");
    var panel = document.getElementById("sanctions-progress");
    if (!form || !panel) return;

    var token = form.querySelector("input[name='__RequestVerificationToken']").value;
    var stepsList = panel.querySelector("[data-steps]");
    var previousField = form.querySelector("[data-previous-file]");
    var formError = form.querySelector("[data-form-error]");

    form.querySelectorAll("input[name='mode']").forEach(function (radio) {
        radio.addEventListener("change", function () {
            previousField.hidden = form.querySelector("input[name='mode']:checked").value !== "two";
        });
    });

    function wait(ms) {
        return new Promise(function (resolve) { setTimeout(resolve, ms); });
    }

    function post(url, data) {
        var body = data instanceof FormData ? data : new FormData();
        if (!(data instanceof FormData)) {
            Object.keys(data).forEach(function (key) {
                if (data[key] !== null && data[key] !== undefined) body.append(key, data[key]);
            });
        }
        body.append("__RequestVerificationToken", token);

        return fetch(url, {
            method: "POST",
            body: body,
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        }).then(function (response) {
            return response.json().catch(function () { return {}; }).then(function (json) {
                if (!response.ok) throw new Error(json.error || "The portal could not finish this step.");
                return json;
            });
        });
    }

    function step(title) {
        var item = document.createElement("li");
        item.className = "sanctions-step is-running";
        item.innerHTML =
            '<span class="sanctions-step-icon"><i class="fas fa-circle-notch fa-spin" aria-hidden="true"></i></span>' +
            '<div class="sanctions-step-body"><strong></strong><ul class="sanctions-step-detail"></ul></div>';
        item.querySelector("strong").textContent = title;
        stepsList.appendChild(item);
        item.scrollIntoView({ behavior: "smooth", block: "nearest" });

        function finish(state, icon, lines) {
            item.className = "sanctions-step " + state;
            item.querySelector(".sanctions-step-icon").innerHTML = '<i class="fas ' + icon + '" aria-hidden="true"></i>';
            var detail = item.querySelector(".sanctions-step-detail");
            (lines || []).forEach(function (line) {
                var li = document.createElement("li");
                li.textContent = line;
                detail.appendChild(li);
            });
        }

        return {
            done: function (lines) { finish("is-done", "fa-circle-check", lines); },
            warn: function (lines) { finish("is-warning", "fa-triangle-exclamation", lines); },
            fail: function (message) { finish("is-failed", "fa-circle-xmark", [message]); }
        };
    }

    async function run(stepTitle, work, describe) {
        var s = step(stepTitle);
        try {
            var results = await Promise.all([work(), wait(700)]);
            var outcome = describe(results[0]);
            if (outcome.warn) s.warn(outcome.lines); else s.done(outcome.lines);
            return results[0];
        } catch (error) {
            s.fail(error.message);
            throw error;
        }
    }

    function list(label, names, empty) {
        if (!names.length) return [empty];
        var shown = names.slice(0, 10);
        var lines = [label + ":"].concat(shown.map(function (n) { return "  " + n; }));
        if (names.length > shown.length) lines.push("  and " + (names.length - shown.length) + " more");
        return lines;
    }

    function readLines(doc) {
        var lines = [doc.label];
        if (doc.alreadyUploaded) lines.push(doc.alreadyUploaded);
        lines.push(doc.kind + (doc.listName ? " of the " + doc.listName : "") + ", " + doc.pages + " page" + (doc.pages === 1 ? "" : "s"));
        lines.push(doc.entries + " names found (" + doc.individuals + " people, " + doc.entities + " organisations)");
        if (doc.marked) lines.push(doc.marked + " of them with underlined or struck-through amendments");
        return lines.concat(doc.warnings || []);
    }

    form.addEventListener("submit", async function (event) {
        event.preventDefault();
        formError.classList.add("d-none");

        var mode = form.querySelector("input[name='mode']:checked").value;
        var newFile = document.getElementById("new-file").files[0];
        var previousFile = document.getElementById("previous-file").files[0];

        if (!newFile || (mode === "two" && !previousFile)) {
            formError.textContent = mode === "two"
                ? "Choose both documents: the previous one and the new one."
                : "Choose the new document.";
            formError.classList.remove("d-none");
            return;
        }

        form.querySelectorAll("input, button").forEach(function (el) { el.disabled = true; });
        panel.hidden = false;

        try {
            var previous = null;

            if (mode === "two") {
                var prevData = new FormData();
                prevData.append("file", previousFile);
                previous = await run("Reading the previous document", function () {
                    return post(panel.dataset.uploadUrl, prevData);
                }, function (doc) { return { lines: readLines(doc), warn: (doc.warnings || []).length > 0 }; });
            }

            var newData = new FormData();
            newData.append("file", newFile);
            var current = await run("Reading the new document", function () {
                return post(panel.dataset.uploadUrl, newData);
            }, function (doc) { return { lines: readLines(doc), warn: (doc.warnings || []).length > 0 }; });

            var comparison = await run(
                mode === "last" ? "Finding the earlier version and comparing name by name" : "Comparing the two documents name by name",
                function () {
                    return post(panel.dataset.compareUrl, {
                        newDocumentId: current.id,
                        previousDocumentId: previous ? previous.id : null
                    });
                }, function (c) {
                    var lines = [];
                    if (c.previous) {
                        lines.push("Compared with " + c.previous.label + " (" + c.previous.entries + " names)");
                    } else if (!c.isNotice) {
                        return { lines: ["No earlier full list of this list has been uploaded, so this document becomes the starting point."].concat(c.notes) };
                    } else {
                        lines.push("Each name compared with its latest earlier version uploaded");
                    }
                    lines = lines
                        .concat(list(c.added.length + " added", c.added, "No names were added."))
                        .concat(list(c.removed.length + " removed", c.removed, "No names were removed."))
                        .concat(list(c.changed.length + " amended", c.changed, "No names were amended."));
                    if (!c.isNotice) lines.push(c.unchanged + " unchanged.");
                    return { lines: lines.concat(c.notes), warn: c.inconclusive };
                });

            await run("Checking for discrepancies", function () { return Promise.resolve(comparison); }, function (c) {
                return c.discrepancies.length
                    ? { lines: c.discrepancies, warn: true }
                    : { lines: ["No discrepancies found."] };
            });

            function people(count, noun, group, extra) {
                var lines = [count + " " + noun + " checked."].concat(extra || []);
                if (group.setAside) lines.push(group.setAside + " similar name" + (group.setAside === 1 ? "" : "s") + " set aside because the date of birth differs.");
                if (!group.matches.length) return { lines: lines.concat(["Nobody matches a name on the new document."]) };
                return {
                    lines: lines.concat(list(group.matches.length + " possible match" + (group.matches.length === 1 ? "" : "es"), group.matches, "")),
                    warn: true
                };
            }

            var screening = await run("Checking staff", function () {
                return post(panel.dataset.screenUrl, { reviewId: comparison.reviewId });
            }, function (s) { return people(s.staff, "active staff", s.staffResult); });

            await run("Checking vendors", function () { return Promise.resolve(screening); }, function (s) {
                return people(s.vendors, "active vendors and their contact people", s.vendorResult);
            });

            await run("Checking clients", function () { return Promise.resolve(screening); }, function (s) {
                if (!s.hasClientList) return { lines: ["No client list has been uploaded, so no clients were checked."], warn: true };
                return people(s.clients, "clients", s.clientResult);
            });

            var findings = panel.querySelector("[data-findings]");
            screening.findings.forEach(function (sentence) {
                var li = document.createElement("li");
                li.textContent = sentence;
                findings.appendChild(li);
            });

            panel.querySelector("[data-result-link]").href =
                panel.dataset.resultUrl.replace(/\/0$/, "/" + comparison.reviewId);
            panel.querySelector("[data-summary]").hidden = false;
            panel.querySelector("[data-summary]").scrollIntoView({ behavior: "smooth", block: "nearest" });
        } catch (error) {
            var retry = document.createElement("button");
            retry.type = "button";
            retry.className = "btn btn-outline-secondary mt-3";
            retry.textContent = "Start again";
            retry.addEventListener("click", function () { location.reload(); });
            stepsList.after(retry);
        }
    });
})();
