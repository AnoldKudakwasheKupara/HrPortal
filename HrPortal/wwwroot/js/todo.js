(function () {
    "use strict";

    var sort = document.getElementById("todo-sort");

    if (sort) {
        sort.addEventListener("change", function () {
            sort.form.submit();
        });
    }

    var form = document.getElementById("todo-bulk");

    if (!form) {
        return;
    }

    var all = document.getElementById("todo-select-all");
    var bar = document.getElementById("todo-bulk-bar");
    var count = document.getElementById("todo-bulk-count");
    var boxes = Array.prototype.slice.call(
        document.querySelectorAll("input[name='ids'][form='todo-bulk']"));

    function refresh() {
        var ticked = boxes.filter(function (box) { return box.checked; }).length;

        bar.hidden = ticked === 0;
        count.textContent = ticked + (ticked === 1 ? " task selected" : " tasks selected");

        if (all) {
            all.checked = ticked > 0 && ticked === boxes.length;
            all.indeterminate = ticked > 0 && ticked < boxes.length;
        }
    }

    boxes.forEach(function (box) {
        box.addEventListener("change", refresh);
    });

    if (all) {
        all.addEventListener("change", function () {
            boxes.forEach(function (box) { box.checked = all.checked; });
            refresh();
        });
    }

    form.addEventListener("submit", function (event) {
        var action = event.submitter ? event.submitter.value : "";

        if (action === "delete" && !window.confirm("Remove the selected tasks from your list?")) {
            event.preventDefault();
        }
    });

    refresh();
})();
