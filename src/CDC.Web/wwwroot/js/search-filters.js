// SearchFilters: repopulates the search results fragment via a background GET request when a
// [data-app-search-auto-refresh] control changes, instead of a full page reload - the modern
// equivalent of the legacy Profiles.Web Search.aspx UpdatePanel partial postback. The browser
// URL is left untouched, exactly as the legacy async postback left it.
(function () {
    'use strict';

    let requestToken = 0;

    document.addEventListener('DOMContentLoaded', function () {
        const $form = document.getElementById('search-form');
        const $results = document.getElementById('search-results-container');

        if (!$form || !$results) {
            return;
        }

        const $autoRefreshControls = document.querySelectorAll('[data-app-search-auto-refresh]');

        $autoRefreshControls.forEach(function ($control) {
            $control.addEventListener('change', function () {
                refreshResults($form, $results, $autoRefreshControls);
            });
        });
    });

    async function refreshResults($form, $results, $autoRefreshControls) {
        // Each call gets its own token; if a later call starts before this one finishes, its
        // response is discarded below, so a stale (slower) response can never overwrite newer
        // results - the async equivalent of the UpdatePanel only ever showing its latest postback.
        const token = ++requestToken;
        const url = `${$form.action}?${new URLSearchParams(new FormData($form)).toString()}`;

        $results.setAttribute('aria-busy', 'true');
        $autoRefreshControls.forEach(function ($control) {
            $control.disabled = true;
        });

        try {
            const response = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (token !== requestToken) {
                return;
            }

            if (!response.ok) {
                showError($results, 'Unable to retrieve profiles. The service may be temporarily unavailable.');
                return;
            }

            $results.innerHTML = await response.text();

            if (typeof window.initFiltersToggles === 'function') {
                window.initFiltersToggles($results);
            }
        } catch {
            if (token === requestToken) {
                showError($results, 'Unable to retrieve profiles. Check your connection and try again.');
            }
        } finally {
            if (token === requestToken) {
                $results.removeAttribute('aria-busy');
                $autoRefreshControls.forEach(function ($control) {
                    $control.disabled = false;
                });
            }
        }
    }

    // Mirrors the govuk-error-summary markup _SearchResults.cshtml renders for a server-side
    // search failure, so a client-side (network) failure looks the same to the user.
    function showError($results, message) {
        $results.innerHTML = `
            <div class="govuk-error-summary govuk-!-margin-top-4" data-module="govuk-error-summary">
                <div role="alert">
                    <h2 class="govuk-error-summary__title">There is a problem</h2>
                    <div class="govuk-error-summary__body">
                        <p>${message}</p>
                    </div>
                </div>
            </div>`;
    }
})();
