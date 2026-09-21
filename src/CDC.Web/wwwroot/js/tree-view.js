// TreeView: reusable single-select tree picker.
//
// Enhances the markup rendered by Views/Shared/_TreeView.cshtml. Without JavaScript the
// markup is still a usable set of nested radios, so nothing is lost.
//
// Selection uses native radio inputs sharing one "name", so the browser itself enforces that
// only one node - parent, child or grandchild - can ever be selected. Semantics otherwise come
// from native buttons - no ARIA tree role - and the only ARIA used is aria-expanded plus an
// aria-controls that points at a real element id.
(function () {
    'use strict';

    const ITEM = '.app-tree__item';
    const GROUP = ':scope > ul.app-tree__group';
    const ROW = ':scope > .app-tree__row';
    const TOGGLE = '.app-tree__toggle';
    const RADIO = 'input[type="radio"]';
    const EXPANDED = 'aria-expanded';
    const TRUE = 'true';

    // A plus is a horizontal bar plus a vertical bar; a minus is the horizontal bar alone.
    // The vertical bar's visibility is driven by [aria-expanded] in CSS, so no JS is needed
    // to redraw the icon when a branch toggles.
    const TOGGLE_ICON =
        '<svg class="app-tree__toggle-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" ' +
        'fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" focusable="false" aria-hidden="true">' +
        '<path d="M2 8h12"/><path class="app-tree__toggle-icon-vertical" d="M8 2v12"/></svg>';

    class TreeView {
        constructor($root) {
            if (!($root instanceof HTMLElement)) {
                return;
            }

            this.$root = $root;
            this.idPrefix = $root.id || 'item';

            const $form = $root.closest('form');
            this.$status = $form ? $form.querySelector('.app-tree__status') : null;
            this.$toggleAll = $form ? $form.querySelector('.app-tree__toggle-all') : null;

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

        radio(item) {
            return item.querySelector(`${ROW} ${RADIO}`);
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

        // The category label shown to screen readers on a branch's toggle, and to everyone in
        // the "Selected: ..." status once a node is picked.
        labelFor(item) {
            const label = item.querySelector(`${ROW} label`);

            return label ? label.textContent.trim() : this.radio(item).value;
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

            const expanded = item.dataset.appTreeExpanded === TRUE;

            const toggle = document.createElement('button');
            toggle.type = 'button';
            toggle.className = 'app-tree__toggle';
            toggle.setAttribute(EXPANDED, String(expanded));
            toggle.setAttribute('aria-controls', group.id);

            // Accessible name is the category it opens; state comes from aria-expanded.
            const name = document.createElement('span');
            name.className = 'govuk-visually-hidden';
            name.textContent = this.labelFor(item);

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
            this.refreshToggleAllControl();
        }

        isExpanded(item) {
            const toggle = this.toggle(item);

            return Boolean(toggle) && toggle.getAttribute(EXPANDED) === TRUE;
        }

        runAction(action) {
            if (action === 'toggle-all') {
                const branches = this.items.filter((item) => this.group(item));
                const expanded = !this.allExpanded(branches);
                branches.forEach((item) => this.setExpanded(item, expanded));

                return;
            }

            if (action === 'clear') {
                this.items.forEach((item) => {
                    this.radio(item).checked = false;
                });

                this.updateSelectedStatus();
            }
        }

        allExpanded(branches) {
            return branches.length > 0 && branches.every((item) => this.isExpanded(item));
        }

        // Keeps the "Open all"/"Close all" control in sync whenever a branch's state changes,
        // whether that came from the control itself, a single node's toggle, or the keyboard.
        refreshToggleAllControl() {
            if (!this.$toggleAll) {
                return;
            }

            const branches = this.items.filter((item) => this.group(item));
            const expanded = this.allExpanded(branches);
            const label = this.$toggleAll.querySelector('.app-tree__toggle-all-text');

            this.$toggleAll.setAttribute(EXPANDED, String(expanded));

            if (label) {
                label.textContent = expanded ? 'Close all' : 'Open all';
            }
        }

        /* ---------- selection ---------- */

        onChange(event) {
            const radio = event.target;

            if (radio instanceof HTMLInputElement && radio.type === 'radio') {
                this.updateSelectedStatus();
            }
        }

        // The status output disappears entirely (rather than showing empty text) once nothing
        // is selected, so it also disappears the moment "Remove selection" is clicked.
        updateSelectedStatus() {
            if (!this.$status) {
                return;
            }

            const selected = this.items.find((item) => this.radio(item).checked);

            this.$status.hidden = !selected;
            this.$status.textContent = selected ? `Selected: ${this.labelFor(selected)}` : '';
        }

        refreshAll() {
            this.updateSelectedStatus();
            this.refreshToggleAllControl();
        }

        /* ---------- keyboard ---------- */

        visibleItems() {
            return this.items.filter((item) => !item.parentElement.closest('[hidden]'));
        }

        focusItem(item) {
            const radio = item ? this.radio(item) : null;

            if (radio) {
                radio.focus();
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

            if (visible.length === 0) {
                return;
            }

            if (key === 'Home') {
                this.focusItem(visible[0]);
            } else if (key === 'End') {
                this.focusItem(visible.at(-1));
            } else if (key === 'ArrowDown' && index > -1 && index < visible.length - 1) {
                this.focusItem(visible[index + 1]);
            } else if (key === 'ArrowUp' && index > 0) {
                this.focusItem(visible[index - 1]);
            } else {
                return;
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
            $tree.treeView = new TreeView($tree);
        });
    });
})();
