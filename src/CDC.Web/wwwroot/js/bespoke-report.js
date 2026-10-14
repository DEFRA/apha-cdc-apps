(function () {
    "use strict";

    // "Create bespoke report" page behaviour that is specific to it: the title boxes of the Save
    // template as / Edit title dialogs. The tree itself is the shared tree-view.js, and the
    // dialogs open and close through confirm-modal.js.

    // The legacy page's text boxes only let letters, digits and spaces be typed (the server
    // rejects anything else too).
    var DISALLOWED = /[^A-Za-z0-9 ]/g;

    document.addEventListener("input", function (event) {
        var input = event.target;

        if (input instanceof HTMLInputElement && input.hasAttribute("data-bespoke-title-input")) {
            var cleaned = input.value.replace(DISALLOWED, "");

            if (cleaned !== input.value) {
                input.value = cleaned;
            }
        }
    });

    // Enter in a dialog's title box saves that dialog. Without this the browser would submit the
    // form with its first submit button, which belongs to a different action.
    document.addEventListener("keydown", function (event) {
        var input = event.target;

        if (event.key !== "Enter" || !(input instanceof HTMLInputElement) || !input.hasAttribute("data-bespoke-title-input")) {
            return;
        }

        var dialog = input.closest("dialog");
        var save = dialog ? dialog.querySelector("button[type='submit']") : null;

        event.preventDefault();

        if (save) {
            save.click();
        }
    });

    // Save template as starts from the selected template's title with " Copy" added, and goes
    // back to it if the dialog is cancelled.
    var saveAsButton = document.querySelector("[data-bespoke-save-as]");
    var saveAsInput = document.getElementById("NewTemplateTitle");

    if (saveAsButton && saveAsInput) {
        var originalTitle = saveAsInput.value;

        saveAsButton.addEventListener("click", function () {
            saveAsInput.value = originalTitle === "" ? "" : originalTitle + " Copy";
        });

        var dialog = saveAsInput.closest("dialog");

        if (dialog) {
            dialog.addEventListener("close", function () {
                saveAsInput.value = originalTitle;
            });
        }
    }
})();
