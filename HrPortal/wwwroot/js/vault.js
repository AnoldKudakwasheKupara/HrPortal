(function () {
    var alphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%&*+-=?@_";

    function generate(length) {
        var out = "";
        var limit = 256 - (256 % alphabet.length);
        var bytes = new Uint8Array(1);
        while (out.length < length) {
            crypto.getRandomValues(bytes);
            if (bytes[0] < limit) out += alphabet[bytes[0] % alphabet.length];
        }
        return out;
    }

    document.addEventListener("click", function (event) {
        var gen = event.target.closest("[data-vault-generate]");
        var toggle = event.target.closest("[data-vault-show-input]");
        if (!gen && !toggle) return;

        var input = event.target.closest(".input-group")
            .querySelector("[data-vault-password-input]");

        if (gen) {
            input.value = generate(20);
            input.type = "text";
            toggle = input.parentElement.querySelector("[data-vault-show-input]");
            toggle.textContent = "Hide";
            return;
        }

        var showing = input.type === "text";
        input.type = showing ? "password" : "text";
        toggle.textContent = showing ? "Show" : "Hide";
    });

    var form = document.getElementById("vault-reveal-form");
    if (!form) return;

    var token = form.querySelector("input[name='__RequestVerificationToken']").value;
    var hideAfter = 30000;
    var masked = "••••••••••";

    function fetchSecret(id) {
        var body = new URLSearchParams();
        body.append("__RequestVerificationToken", token);

        return fetch(form.action + "/" + encodeURIComponent(id), {
            method: "POST",
            body: body,
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        }).then(function (response) {
            if (response.status === 401 || response.redirected) {
                location.reload();
                throw new Error("locked");
            }
            if (response.status === 409) {
                return response.text().then(function (message) {
                    alert(message);
                    throw new Error("locked");
                });
            }
            if (!response.ok) throw new Error("failed");
            return response.json();
        });
    }

    function hide(row) {
        row.querySelector("[data-vault-password]").textContent = masked;
        var notes = row.querySelector("[data-vault-notes]");
        notes.textContent = "";
        notes.hidden = true;
        row.querySelector("[data-vault-reveal]").textContent = "Reveal";
        clearTimeout(row._vaultTimer);
        row._vaultTimer = null;
    }

    function show(row, secret) {
        row.querySelector("[data-vault-password]").textContent = secret.password;
        var notes = row.querySelector("[data-vault-notes]");
        if (secret.notes) {
            notes.textContent = secret.notes;
            notes.hidden = false;
        }
        row.querySelector("[data-vault-reveal]").textContent = "Hide";
        clearTimeout(row._vaultTimer);
        row._vaultTimer = setTimeout(function () { hide(row); }, hideAfter);
    }

    function flash(button, text) {
        var before = button.textContent;
        button.textContent = text;
        setTimeout(function () { button.textContent = before; }, 1500);
    }

    document.addEventListener("click", function (event) {
        var reveal = event.target.closest("[data-vault-reveal]");
        var copy = event.target.closest("[data-vault-copy]");
        var copyText = event.target.closest("[data-vault-copy-text]");

        if (copyText) {
            navigator.clipboard.writeText(copyText.dataset.vaultCopyText).then(function () {
                copyText.title = "Copied";
            });
            return;
        }

        if (!reveal && !copy) return;

        var row = event.target.closest("[data-vault-entry]");
        var id = row.dataset.vaultEntry;

        if (reveal && row._vaultTimer) {
            hide(row);
            return;
        }

        fetchSecret(id).then(function (secret) {
            if (reveal) {
                show(row, secret);
            } else {
                navigator.clipboard.writeText(secret.password).then(function () {
                    flash(copy, "Copied");
                });
            }
        }).catch(function (error) {
            if (error.message !== "locked") {
                flash(reveal || copy, "Could not load");
            }
        });
    });
})();
