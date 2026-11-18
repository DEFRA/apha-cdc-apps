// Keeps the user where they were when the page refreshes itself after a confirmed change of answer (the hidden refresh button), as the legacy page did.
(function () {
    'use strict';

    var KEY = 'app:refresh-scroll';

    function path() {
        return window.location.pathname;
    }

    var button = document.querySelector('[data-relevance-refresh]');

    if (button) {
        button.addEventListener('click', function () {
            try {
                sessionStorage.setItem(KEY, JSON.stringify({ path: path(), top: window.scrollY }));
            } catch {
                // The browser refuses the storage, so the page that answers opens at the top.
            }
        });
    }

    var saved = null;

    try {
        saved = JSON.parse(sessionStorage.getItem(KEY));
        sessionStorage.removeItem(KEY);
    } catch {
        saved = null;
    }

    if (!saved || saved.path !== path() || !Number.isFinite(saved.top)) {
        return;
    }

    // A page that answers with an error stays at the top, where the error summary is.
    function restore() {
        if (!document.querySelector('.govuk-error-summary:not([hidden])')) {
            window.scrollTo(0, saved.top);
        }
    }

    // The accordion and the rich text editors change the page's height as they start, so the position is put back after them.
    if (document.readyState === 'complete') {
        restore();
    } else {
        window.addEventListener('load', restore);
    }
})();
