(function () {
    "use strict";

    Array.prototype.forEach.call(
        document.querySelectorAll("form[data-auto-submit]"),
        function (form) {
            form.addEventListener("change", function (event) {
                var field = event.target;

                if (field.matches("select, input[type=date]")) {
                    form.submit();
                }
            });
        });
})();
