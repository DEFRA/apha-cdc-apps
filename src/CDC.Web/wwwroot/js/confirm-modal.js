(function () {
    "use strict";

    // Greyed-out confirmation dialog (e.g. "Create new draft version"), styled with GOV.UK
    // tokens: GOV.UK Frontend has no official modal component, and Bootstrap is deliberately
    // being phased out of this app, so this is a small hand-rolled, accessible replacement -
    // matching the pattern already used for the custom tree view component.
    //
    // Behaviour mirrors the legacy apha.confirmPrompt/ConfirmDialog exactly: Escape or the
    // Cancel button close the dialog with no action; there is no backdrop-click-to-close.

    function getFocusable(modal) {
        return Array.prototype.slice
            .call(modal.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'))
            .filter(function (element) { return !element.disabled; });
    }

    function onKeydown(event) {
        var modal = document.querySelector(".app-modal--open");
        if (!modal) {
            return;
        }

        if (event.key === "Escape") {
            event.preventDefault();
            closeModal(modal);
            return;
        }

        if (event.key !== "Tab") {
            return;
        }

        var focusable = getFocusable(modal);
        if (focusable.length === 0) {
            return;
        }

        var first = focusable[0];
        var last = focusable[focusable.length - 1];

        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    }

    function openModal(modal, trigger) {
        modal.__previousFocus = trigger;
        modal.classList.add("app-modal--open");
        modal.setAttribute("aria-hidden", "false");

        var focusable = getFocusable(modal);
        if (focusable.length > 0) {
            focusable[0].focus();
        }

        document.addEventListener("keydown", onKeydown);
    }

    function closeModal(modal) {
        modal.classList.remove("app-modal--open");
        modal.setAttribute("aria-hidden", "true");
        document.removeEventListener("keydown", onKeydown);

        if (modal.__previousFocus) {
            modal.__previousFocus.focus();
        }
    }

    document.addEventListener("click", function (event) {
        var trigger = event.target.closest("[data-confirm-target]");
        if (trigger) {
            var modal = document.querySelector(trigger.getAttribute("data-confirm-target"));
            if (modal) {
                event.preventDefault();
                openModal(modal, trigger);
            }
            return;
        }

        var closeTrigger = event.target.closest("[data-confirm-close]");
        if (closeTrigger) {
            var openModal_ = closeTrigger.closest(".app-modal");
            if (openModal_) {
                event.preventDefault();
                closeModal(openModal_);
            }
        }
    });
})();
