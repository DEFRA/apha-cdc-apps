// "Set For All Sections" on the All Revision dates tab: changing a status or next review date in
// that area copies it into the matching control of every section row, as the legacy page's
// onXxxAllChanged() handlers did. Nothing is saved until the user presses Save.
(function () {
    'use strict';

    function copyToAllSections(source) {
        var field = source.getAttribute('data-revision-setall');
        var targets = document.querySelectorAll('[data-revision-field="' + field + '"]');

        for (var index = 0; index < targets.length; index++) {
            if (!targets[index].disabled) {
                targets[index].value = source.value;
            }
        }
    }

    var controls = document.querySelectorAll('[data-revision-setall]');

    for (var index = 0; index < controls.length; index++) {
        controls[index].addEventListener('change', function (event) {
            copyToAllSections(event.currentTarget);
        });
    }
})();
