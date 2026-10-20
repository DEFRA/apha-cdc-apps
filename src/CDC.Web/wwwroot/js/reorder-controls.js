// Maintain species data, reorder mode: each Move up/down posts and redirects, which would
// drop keyboard focus. This remembers which arrow was pressed and refocuses it after the
// reload (or the other arrow if that one is now disabled at the end of the list), so the
// same key can be pressed repeatedly. It also restores the page scroll position and scrolls
// the moved species into view in the tree panel, so the reload does not jump back to the top.
(function () {
    'use strict';

    const STORAGE_KEY = 'app-reorder-last-arrow';
    const SCROLL_KEY = 'app-reorder-page-scroll';
    const $controls = document.querySelector('[data-app-reorder-controls]');

    if (!$controls) {
        sessionStorage.removeItem(STORAGE_KEY);
        sessionStorage.removeItem(SCROLL_KEY);

        return;
    }

    const $arrows = Array.from($controls.querySelectorAll('.app-reorder-arrow'));

    function showError(message) {
        let $error = document.getElementById('species-select-error');

        if (!$error) {
            $error = document.createElement('p');
            $error.id = 'species-select-error';
            $error.className = 'govuk-error-message';
            $controls.closest('form').appendChild($error);
        }

        $error.textContent = '';

        const $hidden = document.createElement('span');

        $hidden.className = 'govuk-visually-hidden';
        $hidden.textContent = 'Error:';
        $error.append($hidden, ' ' + message);
    }

    // Saves the move in the background and reorders the tree in place, so the page does not reload.
    // Without fetch support, or if the request fails to send, the normal form post still applies.
    async function moveInPlace($arrow) {
        const $radio = document.querySelector('.app-tree input[type="radio"]:checked');
        const $item = $radio && $radio.closest('.app-tree__item');

        if (!$item) {
            return false;
        }

        const movingUp = $arrow.id === 'reorder-move-up';
        const $neighbour = movingUp ? $item.previousElementSibling : $item.nextElementSibling;

        if (!$neighbour || !$neighbour.matches('.app-tree__item')) {
            return false;
        }

        const form = $arrow.form;
        const response = await fetch($arrow.formAction, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'fetch' },
            credentials: 'same-origin'
        });

        if (!response.ok && response.status !== 409) {
            return false;
        }

        const result = await response.json();

        if (!result.success) {
            showError(result.message);

            return true;
        }

        const $error = document.getElementById('species-select-error');

        if ($error) {
            $error.remove();
        }

        if (movingUp) {
            $neighbour.before($item);
        } else {
            $neighbour.after($item);
        }

        updateArrows($radio);
        $radio.scrollIntoView({ block: 'nearest' });

        if ($arrow.disabled) {
            const $other = $arrows.find(($a) => !$a.disabled);

            if ($other) {
                $other.focus({ preventScroll: true });
            }
        }

        return true;
    }

    $arrows.forEach(($arrow) => {
        $arrow.addEventListener('click', (event) => {
            if (typeof fetch === 'function' && typeof FormData === 'function') {
                event.preventDefault();

                if ($arrow.dataset.busy) {
                    return;
                }

                $arrow.dataset.busy = 'true';
                moveInPlace($arrow)
                    .then((handled) => {
                        if (!handled) {
                            $arrow.form.requestSubmit($arrow);
                        }
                    })
                    .catch(() => $arrow.form.requestSubmit($arrow))
                    .finally(() => delete $arrow.dataset.busy);

                return;
            }

            sessionStorage.setItem(STORAGE_KEY, $arrow.id);
            sessionStorage.setItem(SCROLL_KEY, String(window.scrollY));
        });
    });

    // The arrows are rendered for the species selected when the page loaded; recompute them
    // when another species is picked, otherwise they stay disabled and nothing can be moved.
    const $moveUp = document.getElementById('reorder-move-up');
    const $moveDown = document.getElementById('reorder-move-down');

    function updateArrows($radio) {
        const $item = $radio.closest('.app-tree__item');
        const $siblings = $item ? Array.from($item.parentElement.children).filter(($li) => $li.matches('.app-tree__item')) : [];
        const index = $siblings.indexOf($item);

        $moveUp.disabled = index <= 0;
        $moveDown.disabled = index < 0 || index >= $siblings.length - 1;
    }

    document.querySelector('.app-tree').addEventListener('change', (event) => {
        if (event.target.matches('input[type="radio"]') && $moveUp && $moveDown) {
            updateArrows(event.target);
        }
    });

    const initiallyChecked = document.querySelector('.app-tree input[type="radio"]:checked');

    if (initiallyChecked && $moveUp && $moveDown) {
        updateArrows(initiallyChecked);
    }

    const lastId = sessionStorage.getItem(STORAGE_KEY);
    const $last = $arrows.find(($arrow) => $arrow.id === lastId);

    if (!$last) {
        return;
    }

    const savedScroll = Number(sessionStorage.getItem(SCROLL_KEY));

    // Deferred so tree-view.js (also on DOMContentLoaded, registered first) has expanded the tree.
    function restoreScroll() {
        // Keep the moved species visible in the tree's own scroll area after the reload.
        const $panel = document.querySelector('.app-tree__panel');
        const $selected = $panel && $panel.querySelector('.app-tree input:checked');
        const $row = $selected && $selected.closest('.app-tree__row');

        if ($row) {
            const offset = $row.getBoundingClientRect().top - $panel.getBoundingClientRect().top;

            $panel.scrollTop += offset - ($panel.clientHeight - $row.offsetHeight) / 2;
        }

        if (savedScroll > 0) {
            window.scrollTo(0, savedScroll);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', restoreScroll);
    } else {
        restoreScroll();
    }

    const $target = $last.disabled ? $arrows.find(($arrow) => !$arrow.disabled) : $last;

    // preventScroll so refocusing the arrow does not move the page.
    if ($target) {
        $target.focus({ preventScroll: true });
    }
})();
