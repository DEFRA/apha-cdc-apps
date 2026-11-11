// PagedRegion: following a pagination link inside an element marked [data-app-paged-region] (which needs an id too)
// - or a link marked [data-app-region-link], or submitting a GET form marked [data-app-region-form], inside one -
// loads that page in the background and replaces only the region, instead of reloading the whole page - the modern
// equivalent of the legacy Profiles.Web UpdatePanel partial postbacks behind the paged lists. The page title and the
// address follow the page shown, so a refresh or a bookmark still lands on it, and anything that goes wrong falls back
// to following the link as the browser would.
// window.appPaging.settle() is shared with the search results, which load their pages their own way.
(function () {
    'use strict';

    // The block variant moves between sections of a profile, which changes more than the region, so it is left alone.
    // Other links (sort headers) and GET forms (table and page size choices) opt in with a data attribute.
    const pageLinkSelector = '.govuk-pagination:not(.govuk-pagination--block) a.govuk-pagination__link, a[data-app-region-link]';
    const requestTokens = new WeakMap();
    let $status = null;
    let $lastChanged = null;

    document.addEventListener('click', function (event) {
        const $link = event.target instanceof Element ? event.target.closest(pageLinkSelector) : null;
        const $region = $link ? $link.closest('[data-app-paged-region]') : null;

        if (!$region?.id || !canFollowInPlace(event, $link)) {
            return;
        }

        event.preventDefault();
        void loadPage($region, $link.href, $link);
    });

    // Remembers the control whose change submitted the form, as requestSubmit() gives the submit event no submitter.
    document.addEventListener('change', function (event) {
        $lastChanged = event.target instanceof Element ? event.target : null;
    });

    document.addEventListener('submit', function (event) {
        const $form = event.target;
        const $region = $form instanceof HTMLFormElement && $form.hasAttribute('data-app-region-form')
            ? $form.closest('[data-app-paged-region]')
            : null;

        if (!$region?.id || event.defaultPrevented || ($form.method || 'get').toLowerCase() !== 'get'
            || typeof window.fetch !== 'function' || typeof window.DOMParser !== 'function') {
            return;
        }

        const url = new URL($form.action || window.location.href, window.location.href);

        if (url.origin !== window.location.origin) {
            return;
        }

        url.search = new URLSearchParams(new FormData($form)).toString();
        url.hash = '';

        const $origin = event.submitter ?? ($lastChanged?.form === $form ? $lastChanged : $form);

        event.preventDefault();
        void loadPage($region, url.href, $origin);
    });

    // The status has to be in the page before it is first written to for assistive technology to announce the change.
    if (document.body && document.querySelector('[data-app-paged-region], #search-results-container')) {
        ensureStatus();
    }

    window.appPaging = { settle };

    // A modified click, or a link that opens somewhere else, can only be followed by the browser.
    function canFollowInPlace(event, $link) {
        return !event.defaultPrevented
            && event.button === 0
            && !(event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
            && ($link.target === '' || $link.target === '_self')
            && $link.origin === window.location.origin
            && typeof window.fetch === 'function'
            && typeof window.DOMParser === 'function';
    }

    async function loadPage($region, url, $origin) {
        // Each click gets its own token; if a later click starts before this one finishes, this response is dropped,
        // so the region only ever shows the page asked for last - as the UpdatePanel did with its latest postback.
        const token = (requestTokens.get($region) ?? 0) + 1;
        requestTokens.set($region, token);
        $region.setAttribute('aria-busy', 'true');

        let page = null;

        try {
            page = await fetchRegion(url, $region.id);
        } catch {
            // Dealt with below, with every other reason the page could not be fetched.
        }

        if (requestTokens.get($region) !== token) {
            return;
        }

        if (!page) {
            window.location.assign(url);
            return;
        }

        $region.innerHTML = page.html;
        $region.removeAttribute('aria-busy');

        if (page.title) {
            document.title = page.title;
        }

        window.history.replaceState(window.history.state, '', url);
        $region.dispatchEvent(new CustomEvent('app:region-updated', { bubbles: true }));
        settle($region, $origin);
    }

    // Takes the region out of the whole page the server sends back, so the pages need no fragment of their own.
    // Returns null when the response is not the page asked for: an error, a redirect, or a page without the region.
    async function fetchRegion(url, id) {
        const response = await fetch(url);

        if (!response.ok || response.redirected) {
            return null;
        }

        const page = new DOMParser().parseFromString(await response.text(), 'text/html');
        const $fresh = page.getElementById(id);

        return $fresh ? { html: $fresh.innerHTML, title: page.title } : null;
    }

    // The control used has gone with the old page, so keyboard focus goes to its counterpart on the new one, and the
    // change is announced to a screen reader user, who cannot see the list change.
    function settle($container, $followed) {
        const $control = findControl($container, $followed);

        if ($control) {
            $control.focus();
        }

        // A section opened by the link is announced by its heading taking focus, so the page is not described too.
        const openedSection = $control && $control.id && $control.id === $followed.getAttribute?.('data-app-region-focus');
        announce(describeSort($control) || (openedSection ? '' : describePage($container)));
    }

    // A link naming a [data-app-region-focus] target (the section it opens) sends focus there. A control with an id
    // (a sort header, a select) goes to the one with the same id. Otherwise Previous and Next stay with the same kind of
    // link; a page number, or a kind no longer there (Next on the last page), goes to the current page.
    function findControl($container, $followed) {
        const focusId = $followed.getAttribute?.('data-app-region-focus');
        const $target = focusId ? document.getElementById(focusId) : null;

        if ($target && $container.contains($target)) {
            return $target;
        }

        const $same = $followed.id ? document.getElementById($followed.id) : null;

        if ($same && $container.contains($same)) {
            return $same;
        }

        let kind = null;

        if ($followed.closest('.govuk-pagination__prev')) {
            kind = '.govuk-pagination__prev';
        } else if ($followed.closest('.govuk-pagination__next')) {
            kind = '.govuk-pagination__next';
        }

        return (kind ? $container.querySelector(`${kind} .govuk-pagination__link`) : null)
            ?? $container.querySelector('.govuk-pagination__item--current .govuk-pagination__link');
    }

    function describeSort($control) {
        const sort = $control?.closest('th[aria-sort]')?.getAttribute('aria-sort');

        return sort === 'ascending' || sort === 'descending' ? `Sorted by ${$control.textContent.trim()}, ${sort}` : '';
    }

    function describePage($container) {
        const $current = $container.querySelector('.govuk-pagination__item--current .govuk-pagination__link');
        const $pages = $container.querySelectorAll('.govuk-pagination__item:not(.govuk-pagination__item--ellipsis) .govuk-pagination__link');
        const $last = $pages[$pages.length - 1];

        return $current && $last ? `Page ${$current.textContent.trim()} of ${$last.textContent.trim()}` : '';
    }

    function announce(message) {
        if (message) {
            ensureStatus().textContent = message;
        }
    }

    function ensureStatus() {
        if (!$status?.isConnected) {
            $status = document.createElement('div');
            $status.className = 'govuk-visually-hidden';
            $status.setAttribute('role', 'status');
            document.body.appendChild($status);
        }

        return $status;
    }
})();
