(function () {
    "use strict";

    // Per-species questions: a row's Edit link opens its editor, and OK can copy the answer to other species.
    var tables = document.querySelectorAll("[data-module='app-species-table']");

    var controlsOf = function (container) {
        return Array.prototype.slice.call(container.querySelectorAll("select[name], input[name], textarea[name]"));
    };

    var plainText = function (value) {
        return new DOMParser().parseFromString(value, "text/html").body.textContent.trim();
    };

    var textOf = function (container, name) {
        var controls = controlsOf(container).filter(function (control) { return control.name === name; });

        if (controls.length === 0) {
            return null;
        }

        var control = controls[0];

        if (control.tagName === "SELECT") {
            return control.value ? control.options[control.selectedIndex].text : "Not answered";
        }

        if (control.type === "checkbox") {
            var checked = controls.filter(function (item) { return item.checked; }).map(function (item) {
                var label = container.querySelector("label[for='" + item.id + "']");
                return label ? label.textContent.trim() : item.value;
            });
            return checked.length ? checked.join(", ") : "Not answered";
        }

        return plainText(control.value) || "Not answered";
    };

    var copyValue = function (control, targetName, form) {
        var targets = Array.prototype.slice.call(form.querySelectorAll("[name='" + targetName + "']"));

        if (control.type === "checkbox") {
            targets.forEach(function (target) { target.checked = target.value === control.value && control.checked; });
            return;
        }

        if (targets.length > 0) {
            targets[0].value = control.value;
        }
    };

    var refreshCells = function (table, key) {
        table.querySelectorAll("[data-species-row='" + key + "'] [data-species-cell]").forEach(function (cell) {
            var text = textOf(table, cell.getAttribute("data-species-cell"));

            if (text !== null) {
                // The legacy grid showed only the first 50 characters of an answer.
                cell.textContent = text.length > 50 ? text.substring(0, 50) : text;
            }
        });
    };

    tables.forEach(function (table) {
        var form = table.closest("form");
        var snapshots = {};

        var close = function (key) {
            var editor = table.querySelector("[data-species-editor='" + key + "']");
            var button = table.querySelector("[data-species-edit='" + key + "']");

            editor.hidden = true;
            button.setAttribute("aria-expanded", "false");
            button.focus();
        };

        table.addEventListener("click", function (event) {
            var edit = event.target.closest("[data-species-edit]");
            var editorRow = event.target.closest("[data-species-editor]");

            if (edit) {
                var key = edit.getAttribute("data-species-edit");
                var editor = table.querySelector("[data-species-editor='" + key + "']");

                snapshots[key] = controlsOf(editor).map(function (control) {
                    return { control: control, value: control.value, checked: control.checked };
                });
                editor.hidden = false;
                edit.setAttribute("aria-expanded", "true");
                return;
            }

            if (!editorRow) {
                return;
            }

            var editorKey = editorRow.getAttribute("data-species-editor");

            if (event.target.closest("[data-species-cancel]")) {
                (snapshots[editorKey] || []).forEach(function (saved) {
                    saved.control.value = saved.value;
                    saved.control.checked = saved.checked;
                });
                editorRow.querySelectorAll("[data-species-apply], [data-species-apply-all]").forEach(function (box) { box.checked = false; });
                close(editorKey);
                return;
            }

            if (event.target.closest("[data-species-ok]")) {
                var targetKeys = Array.prototype.slice.call(editorRow.querySelectorAll("[data-species-apply]:checked"))
                    .map(function (box) { return box.getAttribute("data-species-apply"); });

                controlsOf(editorRow).forEach(function (control) {
                    if (control.hasAttribute("data-species-apply") || control.hasAttribute("data-species-apply-all")) {
                        return;
                    }

                    targetKeys.forEach(function (otherKey) {
                        copyValue(control, control.name.replace("_" + editorKey, "_" + otherKey), form);
                    });
                });

                [editorKey].concat(targetKeys).forEach(function (key) { refreshCells(table, key); });
                editorRow.querySelectorAll("[data-species-apply], [data-species-apply-all]").forEach(function (box) { box.checked = false; });
                close(editorKey);
            }
        });

        table.addEventListener("change", function (event) {
            var editorRow = event.target.closest("[data-species-editor]");

            if (!editorRow) {
                return;
            }

            var boxes = Array.prototype.slice.call(editorRow.querySelectorAll("[data-species-apply]"));

            if (event.target.hasAttribute("data-species-apply-all")) {
                boxes.forEach(function (box) { box.checked = event.target.checked; });
            } else if (event.target.hasAttribute("data-species-apply")) {
                editorRow.querySelector("[data-species-apply-all]").checked = boxes.every(function (box) { return box.checked; });
            }
        });
    });
})();
