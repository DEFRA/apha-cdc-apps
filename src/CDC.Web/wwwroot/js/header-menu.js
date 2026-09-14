// Toggles the header "Menu" navigation panel on both desktop and mobile.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var toggle = document.getElementById('app-menu-toggle');
        var panel = document.getElementById('app-menu-panel');

        if (!toggle || !panel) {
            return;
        }

        toggle.addEventListener('click', function () {
            var isExpanded = toggle.getAttribute('aria-expanded') === 'true';

            toggle.setAttribute('aria-expanded', String(!isExpanded));
            panel.hidden = isExpanded;
        });
    });
})();
