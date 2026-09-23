// FiltersToggle: shows/hides the advanced search filters on the Search Disease Profiles page
// without a page reload. Progressive enhancement: without JavaScript the filters stay visible
// and the toggle button is inert, so nothing is lost.
(function () {
    'use strict';

    const LINK = '[data-app-filters-toggle-link]';
    const TEXT = '[data-app-filters-toggle-text]';
    const EXPANDED = 'aria-expanded';
    const TRUE = 'true';

    class FiltersToggle {
        constructor($root) {
            if (!($root instanceof HTMLElement)) {
                return;
            }

            this.$link = $root.querySelector(LINK);
            this.$text = $root.querySelector(TEXT);
            this.$content = this.$link ? document.getElementById(this.$link.getAttribute('aria-controls')) : null;

            if (!this.$link || !this.$text || !this.$content) {
                return;
            }

            this.$link.addEventListener('click', () => this.toggle());
        }

        toggle() {
            const expanded = this.$link.getAttribute(EXPANDED) === TRUE;
            const next = !expanded;

            // Text pairs are configurable via data attributes so this one module drives both the
            // "More filters"/"Hide filters" link and the "Show/Hide filter by affected species" link.
            const expandedText = this.$link.dataset.appFiltersExpandedText || 'Hide filters';
            const collapsedText = this.$link.dataset.appFiltersCollapsedText || 'More filters';

            this.$link.setAttribute(EXPANDED, String(next));
            this.$content.hidden = !next;
            this.$text.textContent = next ? expandedText : collapsedText;
        }
    }

    window.FiltersToggle = FiltersToggle;

    // Exposed so code that swaps in new markup (e.g. the AJAX results refresh on the Search
    // page) can (re-)initialise any [data-module="app-filters-toggle"] roots it just inserted.
    window.initFiltersToggles = function (root) {
        (root || document).querySelectorAll('[data-module="app-filters-toggle"]').forEach(function ($root) {
            if (!$root.filtersToggle) {
                $root.filtersToggle = new FiltersToggle($root);
            }
        });
    };

    document.addEventListener('DOMContentLoaded', function () {
        window.initFiltersToggles();
    });
})();
