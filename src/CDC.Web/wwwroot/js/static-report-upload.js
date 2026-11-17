(function () {
    "use strict";

    var form = document.querySelector("[data-upload-form]");
    if (!form) {
        return;
    }

    var input = form.querySelector("input[type=file]");
    var marker = form.querySelector("[data-file-required]");

    // Mirrors the legacy RequiredFieldValidator: an asterisk, with no postback when no file is chosen.
    form.addEventListener("submit", function (event) {
        var missing = !input.files || input.files.length === 0;
        marker.hidden = !missing;
        if (missing) {
            event.preventDefault();
        }
    });

    input.addEventListener("change", function () {
        marker.hidden = true;
    });
})();
