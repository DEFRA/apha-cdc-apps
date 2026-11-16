// Asks the user to confirm a change to an answer that decides whether other questions apply, as the legacy page did: such an answer is
// marked with the numbers of the questions it may affect (data-relevance-affects), and changing it opens the confirmation dialog.
//
// OK keeps the change. For an answer in the panel that edits a species' answers, question-species.js then saves the section when the
// panel's OK is pressed; for any other answer the section is saved straight away, through the page's hidden refresh button, so that the
// questions the change makes irrelevant are cleared and hidden. Cancel (or Escape) puts the answer back as it was.
(function () {
    'use strict';

    var dialog = document.getElementById('relevance-confirm-dialog');

    if (!dialog || typeof dialog.showModal !== 'function') {
        return;
    }

    var questions = dialog.querySelector('[data-relevance-questions]');
    var okButton = dialog.querySelector('[data-relevance-ok]');

    // A control that decides whether other questions apply names them; one that names none is left alone.
    var AFFECTING = '[data-relevance-affects]:not([data-relevance-affects=""])';

    // The radio of each group that was checked when the page opened or when the last change was confirmed.
    var heldByRadioGroup = {};

    var pending = null;
    var confirmed = false;

    function isRadio(control) {
        return control.type === 'radio';
    }

    // What a drop-down held when the page opened, or was last confirmed, which a change that is not confirmed puts back, as the legacy
    // lastSelectedIndex did. It is kept in the drop-down's data-relevance-held attribute so that the panel that edits a species' answers,
    // which fills its drop-downs with each species' answers in turn, can say what a drop-down now holds (question-species.js).
    function holdAnswers() {
        document.querySelectorAll('input[type="radio"]' + AFFECTING).forEach(function (radio) {
            if (!(radio.name in heldByRadioGroup)) {
                heldByRadioGroup[radio.name] = null;
            }
            if (radio.checked) {
                heldByRadioGroup[radio.name] = radio;
            }
        });

        document.querySelectorAll('select' + AFFECTING).forEach(function (select) {
            select.dataset.relevanceHeld = select.value;
        });
    }

    function hold(control) {
        if (isRadio(control)) {
            heldByRadioGroup[control.name] = control;
        } else {
            control.dataset.relevanceHeld = control.value;
        }
    }

    function putBack(control) {
        if (isRadio(control)) {
            var held = heldByRadioGroup[control.name];

            if (held) {
                held.checked = true;
            } else {
                control.checked = false;
            }
        } else {
            control.value = control.dataset.relevanceHeld || '';
        }
    }

    function requestRefresh() {
        var button = document.querySelector('[data-relevance-refresh]');

        if (button) {
            button.click();
        }
    }

    document.addEventListener('change', function (event) {
        var control = event.target;

        if (pending || !control.matches?.(AFFECTING)) {
            return;
        }

        pending = control;
        confirmed = false;
        questions.textContent = control.dataset.relevanceAffects;
        dialog.showModal();
    });

    okButton.addEventListener('click', function () {
        confirmed = true;
        dialog.close();
    });

    // Whichever way the dialog closes, OK, Cancel or Escape, says what the user decided.
    dialog.addEventListener('close', function () {
        var control = pending;

        pending = null;

        if (!control) {
            return;
        }

        if (confirmed) {
            hold(control);
            control.dispatchEvent(new CustomEvent('app:relevance-confirmed', { bubbles: true }));

            if (!control.closest('[data-species-panel]')) {
                requestRefresh();
            }
        } else {
            putBack(control);
            control.dispatchEvent(new CustomEvent('app:relevance-cancelled', { bubbles: true }));
        }

        confirmed = false;
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', holdAnswers);
    } else {
        holdAnswers();
    }
})();
