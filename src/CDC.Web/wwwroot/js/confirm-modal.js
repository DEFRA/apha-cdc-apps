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
})();
