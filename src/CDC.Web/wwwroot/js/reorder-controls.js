// Maintain species data, reorder mode: each Move up/down posts and redirects, which would
// drop keyboard focus. This remembers which arrow was pressed and refocuses it after the
// reload (or the other arrow if that one is now disabled at the end of the list), so the
// same key can be pressed repeatedly.
(function () {
    'use strict';

    const STORAGE_KEY = 'app-reorder-last-arrow';
    const $controls = document.querySelector('[data-app-reorder-controls]');

    if (!$controls) {
        sessionStorage.removeItem(STORAGE_KEY);

        return;
    }

    const $arrows = Array.from($controls.querySelectorAll('.app-reorder-arrow'));

    $arrows.forEach(($arrow) => {
        $arrow.addEventListener('click', () => sessionStorage.setItem(STORAGE_KEY, $arrow.id));
    });

    const lastId = sessionStorage.getItem(STORAGE_KEY);
    const $last = $arrows.find(($arrow) => $arrow.id === lastId);

    if (!$last) {
        return;
    }

    const $target = $last.disabled ? $arrows.find(($arrow) => !$arrow.disabled) : $last;

    // preventScroll so refocusing the arrow does not move the page.
    if ($target) {
        $target.focus({ preventScroll: true });
    }
})();
