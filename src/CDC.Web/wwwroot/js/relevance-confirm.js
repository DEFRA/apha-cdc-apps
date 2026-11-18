(function () {
    "use strict";

    // Shows a field only while its controlling field in the same question has the answer that makes it visible.
    var updateVisibility = function () {
        document.querySelectorAll("[data-visible-when-control]").forEach(function (wrapper) {
            var control = document.getElementById(wrapper.dataset.visibleWhenControl);

            if (control) {
                wrapper.hidden = control.value !== wrapper.dataset.visibleWhenValue;
            }
        });
    };

    updateVisibility();

    document.addEventListener("change", function (event) {
        if (!event.target.hasAttribute("data-affects-relevance-of")) {
            updateVisibility();
        }
    });

    // Asks for confirmation before a change to a field that decides whether other questions are relevant, as the legacy page did.
    var selects = document.querySelectorAll("select[data-affects-relevance-of]");

    if (selects.length === 0 || typeof HTMLDialogElement === "undefined") {
        return;
    }

    var dialog = document.createElement("dialog");
    dialog.className = "app-modal";
    dialog.setAttribute("aria-labelledby", "relevance-confirm-title");
    dialog.innerHTML =
        '<h2 class="govuk-heading-m" id="relevance-confirm-title">Confirm</h2>' +
        '<p class="govuk-body" data-relevance-message></p>' +
        '<p class="govuk-body">Do you want to continue?</p>' +
        '<button type="button" class="govuk-button govuk-!-margin-bottom-0 govuk-!-margin-right-2" data-relevance-ok>OK</button>' +
        '<button type="button" class="govuk-button govuk-button--secondary govuk-!-margin-bottom-0" data-relevance-cancel>Cancel</button>';
    document.body.appendChild(dialog);

    var message = dialog.querySelector("[data-relevance-message]");
    var current = null;

    // Mirrors the server's relevance test: an unanswered controlling field makes a question irrelevant.
    var isRelevant = function (section, control) {
        var answer = control.value;
        var flag = section.dataset.irrelevanceFlag === "true";
        var valueId = section.dataset.irrelevanceValue;

        if (!answer) {
            return false;
        }

        if (valueId) {
            return flag ? answer === valueId : answer !== valueId;
        }

        return (answer === "true") !== flag;
    };

    var updateRelevance = function () {
        document.querySelectorAll("[data-irrelevance-field]").forEach(function (section) {
            var control = document.getElementById(section.dataset.irrelevanceField);

            if (control) {
                section.hidden = !isRelevant(section, control);
            }
        });
    };

    dialog.querySelector("[data-relevance-ok]").addEventListener("click", function () {
        current.select.dataset.lastValue = current.select.value;
        current = null;
        dialog.close();
        updateRelevance();
        updateVisibility();
    });

    var cancel = function () {
        if (current) {
            current.select.value = current.select.dataset.lastValue;
            current = null;
        }
        if (dialog.open) {
            dialog.close();
        }
    };

    dialog.querySelector("[data-relevance-cancel]").addEventListener("click", cancel);
    dialog.addEventListener("cancel", cancel);

    selects.forEach(function (select) {
        select.dataset.lastValue = select.value;

        select.addEventListener("change", function () {
            current = { select: select };
            message.textContent =
                "Changing the answer to this question may affect the relevancy of other parts of this section, " +
                "any questions that become irrelevant will be cleared and hidden. Questions that may be affected are: " +
                select.dataset.affectsRelevanceOf + ".";
            dialog.showModal();
        });
    });
})();
