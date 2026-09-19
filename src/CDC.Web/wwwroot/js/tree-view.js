// TreeView: reusable nested-checkbox tree picker.
//
// Enhances the markup rendered by Views/Shared/_TreeView.cshtml. Without JavaScript the
// markup is still a usable set of nested checkboxes, so nothing is lost.
//
// Semantics come from native checkboxes and native buttons - no ARIA tree role - and the only
// ARIA used is aria-expanded plus an aria-controls that points at a real element id.
(function () {
    'use strict';

    const ITEM = '.app-tree__item';
    const GROUP = ':scope > ul.app-tree__group';
    const ROW = ':scope > .app-tree__row';
    const TOGGLE = '.app-tree__toggle';
    const CHECKBOX = 'input[type="checkbox"]';
    const EXPANDED = 'aria-expanded';
    const TRUE = 'true';

    const TOGGLE_ICON =
        '<svg class="app-tree__toggle-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 16" ' +
        'fill="none" stroke="currentColor" stroke-width="3" focusable="false" aria-hidden="true">' +
        '<path d="M1.5 1.5 8 8l-6.5 6.5"/></svg>';

    class TreeView {
        constructor($root) {
            if (!($root instanceof HTMLElement)) {
                return;
            }

            this.$root = $root;
            this.name = $root.dataset.appTreeName || 'item';
            this.namePlural = $root.dataset.appTreeNamePlural || `${this.name}s`;
            this.idPrefix = $root.id || this.name;

            const $form = $root.closest('form');
            this.$status = $form ? $form.querySelector('.app-tree__status') : null;

            this.items = Array.from($root.querySelectorAll(ITEM));
            this.items.forEach((item, index) => this.setupItem(item, index));

            $root.addEventListener('click', (event) => this.onClick(event));
            $root.addEventListener('change', (event) => this.onChange(event));
            $root.addEventListener('keydown', (event) => this.onKeydown(event));

            if ($form) {
                $form.querySelectorAll('[data-app-tree-action]').forEach(($button) => {
                    $button.addEventListener('click', () => this.runAction($button.dataset.appTreeAction));
                });
            }

            this.refreshAll();
        }

        /* ---------- structure helpers ---------- */

        group(item) {
            return item.querySelector(GROUP);
        }

        checkbox(item) {
            return item.querySelector(`${ROW} ${CHECKBOX}`);
        }

        toggle(item) {
            return item.querySelector(`${ROW} ${TOGGLE}`);
        }

        parent(item) {
            return item.parentElement.closest(ITEM);
        }

        children(item) {
            const group = this.group(item);

            return group ? Array.from(group.children).filter((element) => element.matches(ITEM)) : [];
        }

        siblings(item) {
            return Array.from(item.parentElement.children).filter((element) => element.matches(ITEM));
        }

        descendantBoxes(item) {
            const group = this.group(item);

            return group ? Array.from(group.querySelectorAll(CHECKBOX)) : [];
        }

        leafBoxes() {
            return this.items
                .filter((item) => !this.group(item))
                .map((item) => this.checkbox(item));
        }

        depth(item) {
            let depth = 0;
            let current = this.parent(item);

            while (current) {
                depth += 1;
                current = this.parent(current);
            }

            return depth;
        }

        /* ---------- set up ---------- */

        setupItem(item, index) {
            const row = item.querySelector(ROW);
            const group = this.group(item);

            row.style.setProperty('--app-tree-depth', this.depth(item));

            if (group) {
                row.insertBefore(this.createToggle(item, group, index), row.firstElementChild);
                group.hidden = item.dataset.appTreeExpanded !== TRUE;
            } else {
                row.insertBefore(TreeView.createSpacer(), row.firstElementChild);
            }
        }

        createToggle(item, group, index) {
            // aria-controls must point at a real id, so guarantee one exists.
            group.id = group.id || `${this.idPrefix}-group-${index}`;

            const label = item.querySelector(`${ROW} label`);
            const expanded = item.dataset.appTreeExpanded === TRUE;

            const toggle = document.createElement('button');
            toggle.type = 'button';
            toggle.className = 'app-tree__toggle';
            toggle.setAttribute(EXPANDED, String(expanded));
            toggle.setAttribute('aria-controls', group.id);

            // Accessible name is the category it opens; state comes from aria-expanded.
            const name = document.createElement('span');
            name.className = 'govuk-visually-hidden';
            name.textContent = label ? label.textContent.trim() : this.checkbox(item).value;

            toggle.appendChild(name);
            toggle.insertAdjacentHTML('beforeend', TOGGLE_ICON);

            return toggle;
        }

        static createSpacer() {
            const spacer = document.createElement('span');
            spacer.className = 'app-tree__spacer';
            spacer.setAttribute('aria-hidden', TRUE);

            return spacer;
        }

        /* ---------- expand and collapse ---------- */

        setExpanded(item, expanded) {
            const group = this.group(item);
            const toggle = this.toggle(item);

            if (!group || !toggle) {
                return;
            }

            group.hidden = !expanded;
            toggle.setAttribute(EXPANDED, String(expanded));
            item.dataset.appTreeExpanded = String(expanded);
        }

        isExpanded(item) {
            const toggle = this.toggle(item);

            return Boolean(toggle) && toggle.getAttribute(EXPANDED) === TRUE;
        }

        runAction(action) {
            if (action === 'expand-all' || action === 'collapse-all') {
                const expanded = action === 'expand-all';
                this.items.forEach((item) => this.setExpanded(item, expanded));

                return;
            }

            if (action === 'clear') {
                this.items.forEach((item) => {
                    const checkbox = this.checkbox(item);
                    checkbox.checked = false;
                    checkbox.indeterminate = false;
                });

                this.updateCount();
            }
        }

        /* ---------- selection cascade ---------- */

        onChange(event) {
            const checkbox = event.target;

            if (!(checkbox instanceof HTMLInputElement) || checkbox.type !== 'checkbox') {
                return;
            }

            const item = checkbox.closest(ITEM);
            checkbox.indeterminate = false;

            this.descendantBoxes(item).forEach((box) => {
                box.checked = checkbox.checked;
                box.indeterminate = false;
            });

            let ancestor = this.parent(item);

            while (ancestor) {
                this.refreshAncestorState(ancestor);
                ancestor = this.parent(ancestor);
            }

            this.updateCount();
        }

        refreshAncestorState(item) {
            const checkbox = this.checkbox(item);
            const children = this.children(item).map((child) => this.checkbox(child));
            const checked = children.filter((box) => box.checked).length;
            const partial = children.some((box) => box.indeterminate);

            checkbox.checked = checked === children.length && !partial;
            checkbox.indeterminate = partial || (checked > 0 && checked < children.length);
        }

        refreshAll() {
            // Bottom-up so parents see settled child state, which matters when the markup
            // arrives with some checkboxes already checked by the server.
            for (let index = this.items.length - 1; index >= 0; index -= 1) {
                if (this.group(this.items[index])) {
                    this.refreshAncestorState(this.items[index]);
                }
            }

            this.updateCount();
        }

        updateCount() {
            if (!this.$status) {
                return;
            }

            const total = this.leafBoxes().filter((box) => box.checked).length;

            if (total === 0) {
                this.$status.textContent = `No ${this.namePlural} selected`;

                return;
            }

            const noun = total === 1 ? this.name : this.namePlural;
            this.$status.textContent = `${total} ${noun} selected`;
        }

        /* ---------- keyboard ---------- */

        visibleItems() {
            return this.items.filter((item) => !item.parentElement.closest('[hidden]'));
        }

        focusItem(item) {
            const checkbox = item ? this.checkbox(item) : null;

            if (checkbox) {
                checkbox.focus();
            }
        }

        onClick(event) {
            const toggle = event.target.closest(TOGGLE);

            if (!toggle || !this.$root.contains(toggle)) {
                return;
            }

            const item = toggle.closest(ITEM);
            this.setExpanded(item, toggle.getAttribute(EXPANDED) !== TRUE);
        }

        onKeydown(event) {
            const item = event.target.closest(ITEM);

            if (item && this.handleKey(event.key, item)) {
                event.preventDefault();
            }
        }

        // Split out of onKeydown, and split again into the four movement helpers below, to
        // keep every function within the project's complexity budget.
        handleKey(key, item) {
            switch (key) {
                case 'ArrowDown':
                case 'ArrowUp':
                case 'Home':
                case 'End':
                    this.moveVertically(key, item);

                    return true;
                case 'ArrowRight':
                    this.openOrDescend(item);

                    return true;
                case 'ArrowLeft':
                    this.closeOrAscend(item);

                    return true;
                case '*':
                    this.siblings(item).forEach((sibling) => this.setExpanded(sibling, true));

                    return true;
                default:
                    return false;
            }
        }

        moveVertically(key, item) {
            const visible = this.visibleItems();
            const index = visible.indexOf(item);

            if (key === 'Home') {
                this.focusItem(visible[0]);
            } else if (key === 'End') {
                this.focusItem(visible[visible.length - 1]);
            } else if (key === 'ArrowDown' && index > -1 && index < visible.length - 1) {
                this.focusItem(visible[index + 1]);
            } else if (key === 'ArrowUp' && index > 0) {
                this.focusItem(visible[index - 1]);
            }
        }

        openOrDescend(item) {
            if (!this.group(item)) {
                return;
            }

            if (this.isExpanded(item)) {
                this.focusItem(this.children(item)[0]);
            } else {
                this.setExpanded(item, true);
            }
        }

        closeOrAscend(item) {
            if (this.group(item) && this.isExpanded(item)) {
                this.setExpanded(item, false);

                return;
            }

            const parent = this.parent(item);

            if (parent) {
                this.focusItem(parent);
            }
        }
    }

    window.TreeView = TreeView;

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-module="app-tree-view"]').forEach(function ($tree) {
            new TreeView($tree);
        });
    });
})();
