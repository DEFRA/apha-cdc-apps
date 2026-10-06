// Reusable tooltip behaviour for elements marked with data-module="app-tooltip".
// Showing and hiding is handled in CSS (:hover / :focus-within). This script positions the
// tooltip at the bottom-right of the pointer and adds the Escape-to-dismiss mechanism
// required by WCAG 2.2 success criterion 1.4.13.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var DISMISSED_CLASS = 'app-tooltip--dismissed';
        var CURSOR_OFFSET = 10;
        var VIEWPORT_MARGIN = 15;
        var tooltips = document.querySelectorAll('[data-module="app-tooltip"]');

        if (!tooltips.length) {
            return;
        }

        Array.prototype.forEach.call(tooltips, function (tooltip) {
            var content = tooltip.querySelector('.app-tooltip__content');

            function clearPosition() {
                tooltip.style.removeProperty('--app-tooltip-x');
                tooltip.style.removeProperty('--app-tooltip-y');
            }

            function reset() {
                tooltip.classList.remove(DISMISSED_CLASS);
                clearPosition();
            }

            if (content) {
                tooltip.addEventListener('mousemove', function (event) {
                    var anchor = tooltip.getBoundingClientRect();
                    var maxLeft = document.documentElement.clientWidth - VIEWPORT_MARGIN - content.offsetWidth;
                    var left = Math.min(event.clientX + CURSOR_OFFSET, Math.max(VIEWPORT_MARGIN, maxLeft));

                    tooltip.style.setProperty('--app-tooltip-x', (left - anchor.left) + 'px');
                    tooltip.style.setProperty('--app-tooltip-y', (event.clientY + CURSOR_OFFSET - anchor.top) + 'px');
                });
            }

            tooltip.addEventListener('mouseleave', reset);
            tooltip.addEventListener('focusout', reset);
            // Keyboard focus anchors the tooltip under the trigger, not at the last pointer position.
            tooltip.addEventListener('focusin', clearPosition);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key !== 'Escape') {
                return;
            }

            Array.prototype.forEach.call(tooltips, function (tooltip) {
                tooltip.classList.add(DISMISSED_CLASS);
            });
        });
    });
})();
