// Form behaviour the pages declare with data attributes, in place of inline onchange and onsubmit handlers, which the
// Content-Security-Policy does not let run:
// - a control marked [data-app-submit-on-change] submits its form when its value changes, first setting the field whose id
//   its [data-app-reset-page] names back to 1, so a new page size starts from the first page;
// - a form marked [data-app-confirm] asks that question first, and is not submitted unless the user agrees.
// Both listen on the document, so they also cover controls a page loads in afterwards (paged lists and search results).
(function () {
    'use strict';

    document.addEventListener('change', function (event) {
        const $control = event.target instanceof Element ? event.target.closest('[data-app-submit-on-change]') : null;
        const $form = $control?.form;

        if (!$form) {
            return;
        }

        const pageFieldId = $control.dataset.appResetPage;
        const $page = pageFieldId ? document.getElementById(pageFieldId) : null;

        if ($page) {
            $page.value = '1';
        }

        $form.requestSubmit();
    });

    // In the capture phase, so a submit the user cancels is already prevented when the other submit listeners see it.
    document.addEventListener('submit', function (event) {
        const $form = event.target;
        const question = $form instanceof HTMLFormElement ? $form.dataset.appConfirm : null;

        if (question && !window.confirm(question)) {
            event.preventDefault();
        }
    }, true);
})();
