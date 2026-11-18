// Shows a field only while the list answer it depends on is the one it names, as the legacy page did; the server marks the field's container.
// A change to that answer also saves the section and shows the page again, through the page's hidden refresh button, as the legacy page did.
(function () {
    'use strict';

    var dependents = Array.prototype.slice.call(document.querySelectorAll('[data-visible-when-field]'));

    if (dependents.length === 0) {
        return;
    }

    // An answer that also decides whether other questions apply is saved once the user confirms it (relevance-confirm.js).
    var CONFIRMED_CHANGE = '[data-relevance-affects]:not([data-relevance-affects=""])';

    // A keyboard user stepping through the options changes the answer with each key press, so the page waits until they stop.
    var REFRESH_DELAY = 800;

    var timer = null;

    function update() {
        dependents.forEach(function (dependent) {
            var control = document.getElementById(dependent.dataset.visibleWhenField);

            // Where the answer cannot be changed there is no drop-down, and the field stays as the server showed it.
            if (control && control.tagName === 'SELECT') {
                dependent.hidden = control.value !== dependent.dataset.visibleWhenValue;
            }
        });
    }

    function decidesAField(control) {
        return dependents.some(function (dependent) {
            return dependent.dataset.visibleWhenField === control.id;
        });
    }

    function refresh() {
        var button = document.querySelector('[data-relevance-refresh]');

        if (button) {
            button.click();
        }
    }

    document.addEventListener('change', function (event) {
        var control = event.target;

        if (control.tagName !== 'SELECT') {
            return;
        }

        update();

        if (decidesAField(control) && !control.matches(CONFIRMED_CHANGE)) {
            clearTimeout(timer);
            timer = setTimeout(refresh, REFRESH_DELAY);
        }
    });

    // A change to the answer that the user does not confirm is put back.
    document.addEventListener('app:relevance-cancelled', update);

    update();
})();
