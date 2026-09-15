// Toggles the header "Menu" navigation panel on both desktop and mobile.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var toggle = document.getElementById('app-menu-toggle');
        var panel = document.getElementById('app-menu-panel');

        if (!toggle || !panel) {
            return;
        }

        function closeMenu() {
            toggle.setAttribute('aria-expanded', 'false');
            panel.hidden = true;
        }

        toggle.addEventListener('click', function (event) {
            var isExpanded = toggle.getAttribute('aria-expanded') === 'true';

            // Stop this click reaching the document listener below, which would immediately re-close the panel.
            event.stopPropagation();
            toggle.setAttribute('aria-expanded', String(!isExpanded));
            panel.hidden = isExpanded;
        });

        // Close the panel on any click outside the toggle button and the panel itself.
        document.addEventListener('click', function (event) {
            if (!panel.hidden && !panel.contains(event.target) && event.target !== toggle) {
                closeMenu();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && !panel.hidden) {
                closeMenu();
                toggle.focus();
            }
        });
    });
})();
