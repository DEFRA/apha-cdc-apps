// The two "Search for" checkboxes are mutually exclusive, mirroring the legacy Search.aspx radio
// group: selecting one clears the other, and the selected one cannot be cleared on its own.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var $modes = document.querySelectorAll('[data-app-search-mode]');

        if ($modes.length < 2) {
            return;
        }

        Array.prototype.forEach.call($modes, function ($mode) {
            $mode.addEventListener('change', function () {
                if (!$mode.checked) {
                    $mode.checked = true;
                    return;
                }

                Array.prototype.forEach.call($modes, function ($other) {
                    if ($other !== $mode) {
                        $other.checked = false;
                    }
                });
            });
        });
    });
})();
