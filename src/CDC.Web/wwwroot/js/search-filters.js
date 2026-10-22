// SearchFilters: repopulates the search results fragment via a background GET request when a
// [data-app-search-auto-refresh] control changes or a results page link is followed, instead of a
// full page reload - the modern equivalent of the legacy Profiles.Web Search.aspx UpdatePanel
// partial postback. A filter change leaves the browser URL untouched, exactly as the legacy async
// postback left it; a page link brings it up to date, as following the link used to.
// The letter links submit the form, so they apply the filters as they are now.
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
                void refreshResults($form, $results, $autoRefreshControls);
            });
        });

        // The links are built on the server from the last search, so following one would bring back a keyword cleared
        // since. Every postback of the legacy page sent the filters as they stood, and sending the form does the same.
        const followLink = function (event) {
            followFiltersLink(event, $form, $results, $autoRefreshControls);
        };
        $form.addEventListener('click', followLink);
        $results.addEventListener('click', followLink);
    });

    function followFiltersLink(event, $form, $results, $autoRefreshControls) {
        const $link = event.target.closest('[data-app-search-letter], [data-app-page]');

        // A modified click opens the link elsewhere, which only its own address can do.
        if (!$link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }

        const isLetter = $link.dataset.appSearchLetter !== undefined;
        const $letter = $form.querySelector('[name="SelectedLetter"]');
        const $page = document.getElementById('PageNumber');

        if (!$page || (isLetter && !$letter)) {
            return;
        }

        event.preventDefault();

        if (!isLetter) {
            // Only the results change from one page to the next, so they are fetched in the background, as for a filter.
            $page.value = $link.dataset.appPage;
            void refreshResults($form, $results, $autoRefreshControls, $link);
            return;
        }

        // As in legacy, choosing a letter starts again from the first page.
        $letter.value = $link.dataset.appSearchLetter;
        $page.value = '1';
        $form.requestSubmit();
    }

    async function refreshResults($form, $results, $autoRefreshControls, $pageLink) {
        // Each call gets its own token; if a later call starts before this one finishes, its
        // response is discarded below, so a stale (slower) response can never overwrite newer
        // results - the async equivalent of the UpdatePanel only ever showing its latest postback.
        const token = ++requestToken;
        const url = buildSearchUrl($form);

        $results.setAttribute('aria-busy', 'true');
        $autoRefreshControls.forEach(function ($control) {
            $control.disabled = true;
        });

        try {
            const response = await fetch(url.href, {
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

            if ($pageLink) {
                // The address follows the page shown, as it did when the link loaded a whole page, so a refresh, or
                // coming back from a profile, lands on the same page of the same search.
                window.history.replaceState(window.history.state, '', url.pathname + url.search);

                if (window.appPaging) {
                    window.appPaging.settle($results, $pageLink);
                }
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

    // Not form.action: with no action attribute that is the address of the page, query string and all.
    function buildSearchUrl($form) {
        const url = new URL($form.getAttribute('action') || window.location.pathname, window.location.href);
        url.search = new URLSearchParams(new FormData($form)).toString();

        return url;
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
