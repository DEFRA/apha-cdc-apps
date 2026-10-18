// Compare profile versions: keeps the "Selected versions" list in step with the ticked versions in
// the tree, limits the selection to two (a third replaces the second, as the legacy page did) and
// records which version was ticked first, because the earlier version must come first in the report.
// Without JavaScript the form still posts the ticked versions and "Remove selection" is a submit.
(function () {
    'use strict';

    const MAX_SELECTED = 2;
    const ITEM = '.app-tree__item';
    const LABEL = ':scope > .app-tree__row .app-tree__branch-label, :scope > .app-tree__row label';

    class CompareVersions {
        constructor($form) {
            this.$form = $form;
            this.$inputs = Array.from($form.querySelectorAll('[data-module="app-tree-view"] input[type="checkbox"]'));
            this.$list = $form.querySelector('[data-compare-list]');
            this.$empty = $form.querySelector('[data-compare-empty]');
            this.$notice = $form.querySelector('[data-compare-notice]');
            this.$first = $form.querySelector('[data-compare-first]');
            this.$second = $form.querySelector('[data-compare-second]');
            this.order = this.initialOrder();

            $form.addEventListener('change', (event) => this.onChange(event));

            const $remove = $form.querySelector('[data-compare-remove]');

            if ($remove) {
                $remove.addEventListener('click', (event) => {
                    event.preventDefault();
                    this.clear();
                });
            }

            this.render('');
        }

        initialOrder() {
            const checked = this.$inputs.filter((input) => input.checked);
            const posted = [this.$first?.value, this.$second?.value].filter(Boolean);
            const ordered = posted
                .map((value) => checked.find((input) => input.value === value))
                .filter(Boolean);

            return [...ordered, ...checked.filter((input) => !ordered.includes(input))].slice(0, MAX_SELECTED);
        }

        onChange(event) {
            const input = event.target;

            if (!this.$inputs.includes(input)) {
                return;
            }

            let notice = '';

            if (input.checked) {
                this.order.push(input);

                if (this.order.length > MAX_SELECTED) {
                    const [replaced] = this.order.splice(1, 1);
                    replaced.checked = false;
                    notice = `You can only compare two versions. ${this.fullLabel(replaced)} was replaced by ${this.fullLabel(input)}.`;
                }
            } else {
                this.order = this.order.filter((item) => item !== input);
            }

            this.render(notice);
        }

        clear() {
            this.$inputs.forEach((input) => {
                input.checked = false;
            });
            this.order = [];
            this.render('Selection cleared.');
        }

        // "Profile - Scenario - Version", matching the label the server renders.
        fullLabel(input) {
            const parts = [];
            let item = input.closest(ITEM);

            while (item) {
                const label = item.querySelector(LABEL);

                if (label) {
                    parts.unshift(label.textContent.trim());
                }

                item = item.parentElement.closest(ITEM);
            }

            return parts.join(' - ');
        }

        render(notice) {
            this.$list.replaceChildren(...this.order.map((input) => {
                const $item = document.createElement('li');
                $item.textContent = this.fullLabel(input);

                return $item;
            }));

            this.$empty.hidden = this.order.length > 0;
            this.$notice.textContent = notice;
            this.$notice.hidden = notice === '';
            this.$first.value = this.order[0]?.value ?? '';
            this.$second.value = this.order[1]?.value ?? '';
        }
    }

    window.CompareVersions = CompareVersions;

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-module="app-compare-versions"]').forEach(function ($form) {
            $form.compareVersions = new CompareVersions($form);
        });
    });
})();
