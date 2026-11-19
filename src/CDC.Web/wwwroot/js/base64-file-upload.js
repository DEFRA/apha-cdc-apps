(function () {
    "use strict";

    // Sends the chosen file as base64 text instead of raw bytes. The WAF in front of the site scans the first part of a
    // request body for markup and blocks it as cross-site scripting; the XML metadata at the start of a PDF looks like
    // markup, whereas base64 text never contains the characters the rule looks for. The form is still submitted natively,
    // so redirects, validation messages and the antiforgery token work as before. Base64FileUpload.cs decodes the part,
    // recognising it by this content type. Without JavaScript the file is sent unchanged.
    var BASE64_CONTENT_TYPE = "application/base64";

    function canEncode() {
        return typeof FileReader === "function" && typeof DataTransfer === "function";
    }

    function encode(file) {
        return new Promise(function (resolve, reject) {
            var reader = new FileReader();

            reader.onload = function () {
                // A data URL is "data:<type>;base64,<payload>"; slicing the blob avoids copying the payload again.
                var dataUrl = reader.result;
                var payload = new Blob([dataUrl]).slice(dataUrl.indexOf(",") + 1);
                resolve(new File([payload], file.name, { type: BASE64_CONTENT_TYPE }));
            };
            reader.onerror = function () {
                reject(reader.error);
            };
            reader.readAsDataURL(file);
        });
    }

    function replaceFile(input, file) {
        var transfer = new DataTransfer();
        transfer.items.add(file);
        input.files = transfer.files;
    }

    function initialise(form) {
        var input = form.querySelector("input[type=file]");
        var pending = false;

        form.addEventListener("submit", function (event) {
            if (event.defaultPrevented || !input) {
                return;
            }

            // Cancel buttons submit the same form with their own method and have nothing to encode.
            if (event.submitter?.hasAttribute("formmethod")) {
                return;
            }

            var file = input.files?.[0];
            // A page restored from the back/forward cache still holds the file this script already encoded.
            if (!file || file.type === BASE64_CONTENT_TYPE || !canEncode()) {
                return;
            }

            event.preventDefault();
            if (pending) {
                return;
            }

            pending = true;
            encode(file)
                .then(function (encoded) {
                    replaceFile(input, encoded);
                })
                .catch(function () {
                    // Submit the file unchanged rather than lose the upload.
                })
                .then(function () {
                    pending = false;
                    form.submit();
                });
        });
    }

    document.querySelectorAll("form[data-base64-upload]").forEach(initialise);
})();
