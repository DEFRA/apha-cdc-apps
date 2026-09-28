// SearchFilters: repopulates the search results fragment via a background GET request when a
// [data-app-search-auto-refresh] control changes, instead of a full page reload - the modern
// equivalent of the legacy Profiles.Web Search.aspx UpdatePanel partial postback.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const $form = document.getElementById('search-form');
        const $results = document.getElementById('search-results-container');

        if (!$form || !$results) {
            return;
        }

        document.querySelectorAll('[data-app-search-auto-refresh]').forEach(function ($control) {
            $control.addEventListener('change', function () {
                refreshResults($form, $results);
            });
        });
    });

    async function refreshResults($form, $results) {
        const url = `${$form.action}?${new URLSearchParams(new FormData($form)).toString()}`;

        $results.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                return;
            }

            $results.innerHTML = await response.text();
            window.history.replaceState(null, '', url);

            if (typeof window.initFiltersToggles === 'function') {
                window.initFiltersToggles($results);
            }
        } finally {
            $results.removeAttribute('aria-busy');
        }
    }
})();
