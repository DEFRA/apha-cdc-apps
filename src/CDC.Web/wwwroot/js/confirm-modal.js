(function () {
    "use strict";

    // Greyed-out confirmation dialog (e.g. "Create new draft version"), backed by a native
    // <dialog> element: showModal()/close() give focus-trapping, Escape-to-close, and
    // top-layer stacking for free, matching the legacy apha.confirmPrompt/ConfirmDialog
    // behaviour exactly - Escape or the Cancel button close the dialog with no action, and
    // there is no backdrop-click-to-close (native <dialog> does not do this by default).

    document.addEventListener("click", function (event) {
        var trigger = event.target.closest("[data-confirm-target]");
        if (trigger) {
            var modal = document.querySelector(trigger.getAttribute("data-confirm-target"));
            if (modal) {
                event.preventDefault();
                modal.showModal();
            }
            return;
        }

        var closeTrigger = event.target.closest("[data-confirm-close]");
        if (closeTrigger) {
            var openModal = closeTrigger.closest(".app-modal");
            if (openModal) {
                event.preventDefault();
                openModal.close();
            }
        }
    });

    // A dialog the server wants open when the page loads, for example one whose form failed
    // validation and has to show its error.
    document.querySelectorAll("dialog.app-modal[data-open-on-load='true']").forEach(function (modal) {
        if (typeof modal.showModal === "function" && !modal.open) {
            modal.showModal();
        }
    });

    // A long-running submit (regenerating a report) shows the progress dialog its button names, as the legacy
    // LongTaskUI popup did, until the response replaces the page. Escape does not dismiss it.
    function keepOpen(event) {
        event.preventDefault();
    }

    document.addEventListener("submit", function (event) {
        var selector = event.submitter?.dataset.progressDialog;
        var progress = selector ? document.querySelector(selector) : null;

        if (progress && !event.defaultPrevented && typeof progress.showModal === "function") {
            progress.addEventListener("cancel", keepOpen);
            progress.showModal();
        }
    });

    // The back/forward cache restores a page left through a dialog's form with that dialog open and the old content.
    var leftWithDialogOpen = "data-left-with-dialog-open";

    window.addEventListener("pagehide", function () {
        var openModals = document.querySelectorAll("dialog.app-modal[open]");
        if (openModals.length === 0) {
            return;
        }

        openModals.forEach(function (modal) {
            modal.close();
        });
        document.documentElement.setAttribute(leftWithDialogOpen, "");
    });

    window.addEventListener("pageshow", function (event) {
        if (event.persisted && document.documentElement.hasAttribute(leftWithDialogOpen)) {
            window.location.reload();
        }
    });
})();
