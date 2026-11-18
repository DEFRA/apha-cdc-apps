// Shows a field only while the list answer it depends on is the one it names, as the legacy page did; the server marks the field's container.
(function () {
    'use strict';

    var dependents = Array.prototype.slice.call(document.querySelectorAll('[data-visible-when-field]'));

    if (dependents.length === 0) {
        return;
    }

    function update() {
        dependents.forEach(function (dependent) {
            var control = document.getElementById(dependent.dataset.visibleWhenField);

            // Where the answer cannot be changed there is no drop-down, and the field stays as the server showed it.
            if (control && control.tagName === 'SELECT') {
                dependent.hidden = control.value !== dependent.dataset.visibleWhenValue;
            }
        });
    }

    document.addEventListener('change', function (event) {
        if (event.target.tagName === 'SELECT') {
            update();
        }
    });

    // A change to the answer that the user does not confirm is put back.
    document.addEventListener('app:relevance-cancelled', update);

    update();
})();
