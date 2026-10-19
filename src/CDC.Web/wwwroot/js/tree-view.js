// TreeView: reusable single- or multi-select tree picker.
//
// Enhances the markup rendered by Views/Shared/_TreeView.cshtml. Without JavaScript the
// markup is still a usable set of nested radios/checkboxes, so nothing is lost.
//
// Single-select trees use native radio inputs sharing one "name", so the browser itself
// enforces that only one node - parent, child or grandchild - can ever be selected. Multi-select
// trees (Model.AllowMultipleSelection) use checkboxes instead, so any number can be selected.
// Semantics otherwise come from native buttons - no ARIA tree role - and the only ARIA used is
// aria-expanded plus an aria-controls that points at a real element id.
(function () {
    'use strict';

    const ITEM = '.app-tree__item';
    const GROUP = ':scope > ul.app-tree__group';
    const ROW = ':scope > .app-tree__row';
    const TOGGLE = '.app-tree__toggle';
    // :is() keeps the list scoped when this is appended to a ROW selector; a bare comma would let
    // the second input match anywhere in the item, so a node with no input of its own (a
    // non-selectable branch) would resolve to its first descendant's checkbox.
    const INPUT = ':is(input[type="radio"], input[type="checkbox"])';
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
            this.multiple = $root.dataset.appTreeMultiple === TRUE;
            this.cascade = this.multiple && $root.dataset.appTreeCascade === TRUE;
            this.hierarchyExclusive = $root.dataset.appTreeHierarchyExclusive !== 'false';

            const $form = $root.closest('form');
            this.$status = $form ? $form.querySelector('.app-tree__status') : null;
            this.$toggleAll = $form ? $form.querySelector('.app-tree__toggle-all') : null;
            this.$announcement = $form ? $form.querySelector('[data-app-tree-announcement]') : null;

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
            return item.querySelector(`${ROW} ${INPUT}`);
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

        ancestors(item) {
            const result = [];
            let current = this.parent(item);

            while (current) {
                result.push(current);
                current = this.parent(current);
            }

            return result;
        }

        descendants(item) {
            const result = [];
            const stack = [...this.children(item)];

            while (stack.length > 0) {
                const current = stack.pop();
                result.push(current);
                stack.push(...this.children(current));
            }

            return result;
        }

        siblings(item) {
            return Array.from(item.parentElement.children).filter((element) => element.matches(ITEM));
        }

        // The category label shown to screen readers on a branch's toggle, and to everyone in
        // the "Selected: ..." status once a node is picked.
        labelFor(item) {
            const label = item.querySelector(`${ROW} label, ${ROW} .app-tree__branch-label`);
            const input = this.radio(item);

            return label ? label.textContent.trim() : (input ? input.value : '');
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

            // Set on the <li>, not just its row, so the guide-line connectors drawn on the
            // <li> itself (see site.css) can read the same depth without relying on inheritance
            // from a sibling element.
            item.style.setProperty('--app-tree-depth', this.depth(item));

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
                    const input = this.radio(item);

                    if (input) {
                        input.checked = false;
                    }
                });

                this.updateSelectedStatus();

                return;
            }

            if (action === 'include-guidance' || action === 'exclude-guidance') {
                this.setGuidance(action === 'include-guidance');
            }
        }

        // Cascading trees only. Guidance nodes are marked data-app-tree-kind="guidance"; including or
        // excluding guidance only changes the ones under a ticked parent, so guidance of a section
        // that is not part of the selection is never ticked by it. An ancestor stays ticked only
        // while one of its children is, as when a node is ticked or unticked by hand.
        setGuidance(checked) {
            this.items
                .filter((item) => item.dataset.appTreeKind === 'guidance')
                .forEach((item) => {
                    const parent = this.parent(item);

                    if (parent && this.radio(parent).checked) {
                        this.radio(item).checked = checked;
                        this.syncAncestors(item, checked);
                    }
                });

            this.announce(checked ? 'Included all guidance questions' : 'Excluded all guidance questions');
            this.updateSelectedStatus();
        }

        announce(message) {
            if (this.$announcement) {
                this.$announcement.textContent = message;
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
            const input = event.target;

            if (!(input instanceof HTMLInputElement) || (input.type !== 'radio' && input.type !== 'checkbox')) {
                return;
            }

            if (this.multiple && this.cascade) {
                this.cascadeSelection(input.closest(ITEM), input.checked);
            } else if (this.multiple && input.checked) {
                this.enforceHierarchyExclusivity(input.closest(ITEM));
            }

            this.updateSelectedStatus();
        }

        // Cascading multi-select trees (Model.CascadeSelection): ticking a node ticks everything
        // under it, unticking it clears everything under it, and an ancestor is ticked whenever one
        // of its children is - so ticking a single question ticks its section, and unticking the
        // last ticked question of a section unticks the section.
        cascadeSelection(item, checked) {
            if (!item) {
                return;
            }

            this.descendants(item).forEach((descendant) => {
                this.radio(descendant).checked = checked;
            });

            this.syncAncestors(item, checked);
        }

        // Walks up from the nearest ancestor, so each one sees its children's final state.
        syncAncestors(item, checked) {
            this.ancestors(item).forEach((ancestor) => {
                const anyChildChecked = this.children(ancestor).some((child) => this.radio(child).checked);

                this.radio(ancestor).checked = checked || anyChildChecked;
            });
        }

        // Multi-select trees only (Model.AllowMultipleSelection): checking a node means "this
        // whole branch", which would be redundant/contradictory alongside an ancestor (the
        // broader group it belongs to) or a descendant (a more specific part of it) also being
        // checked, so both are cleared.
        enforceHierarchyExclusivity(item) {
            if (!item) {
                return;
            }

            [...this.ancestors(item), ...this.descendants(item)].forEach((relative) => {
                const input = this.radio(relative);

                if (input) {
                    input.checked = false;
                }
            });
        }

        // When the tree defines an empty-selection label (data-app-tree-empty-label), the status
        // stays visible at all times, falling back to that label once nothing is selected.
        // Otherwise the status disappears entirely (rather than showing empty text), including
        // the moment "Remove selection" is clicked. Multiple selected items (checkbox mode) are
        // joined with a comma; single-select (radio) mode only ever has one.
        updateSelectedStatus() {
            if (!this.$status) {
                return;
            }

            const selectedItems = this.items.filter((item) => this.radio(item)?.checked);
            const labels = this.cascade ? this.cascadeLabels() : selectedItems.map((item) => this.labelFor(item));
            const emptyLabel = this.$root.dataset.appTreeEmptyLabel;

            if (emptyLabel) {
                this.$status.hidden = false;
                this.$status.textContent = `Currently selected: ${labels.length > 0 ? labels.join(', ') : emptyLabel}`;

                return;
            }

            this.$status.hidden = labels.length === 0;
            this.$status.textContent = labels.length > 0 ? `Selected: ${labels.join(', ')}` : '';
        }

        refreshAll() {
            this.updateSelectedStatus();
            this.refreshToggleAllControl();
        }

        // Cascading trees tick every descendant of a ticked node, so listing every ticked node would
        // repeat the whole tree. A branch whose children are all ticked stands for them, the root
        // (depth 0) never stands for itself, and a branch is only descended into when some of its
        // children are ticked - as the legacy page's "Selected:" text did.
        cascadeLabels() {
            const labels = [];

            this.items
                .filter((item) => !this.parent(item))
                .forEach((root) => this.collectCascadeLabels(root, labels));

            return labels;
        }

        collectCascadeLabels(item, labels) {
            const children = this.children(item);
            const checked = this.radio(item)?.checked ?? false;

            if (children.length === 0) {
                if (checked) {
                    labels.push(this.labelFor(item));
                }

                return;
            }

            const anyChecked = children.some((child) => this.radio(child).checked);
            const allChecked = children.every((child) => this.radio(child).checked);
            const isRoot = !this.parent(item);

            if (!isRoot && (allChecked || (!anyChecked && checked))) {
                labels.push(this.labelFor(item));
            } else if (anyChecked) {
                children.forEach((child) => this.collectCascadeLabels(child, labels));
            }
        }

        /* ---------- keyboard ---------- */

        // Disabled inputs cannot take focus, so the arrow keys skip them rather than stopping on one.
        visibleItems() {
            return this.items.filter((item) => !item.parentElement.closest('[hidden]') && !this.radio(item).disabled);
        }

        focusItem(item) {
            const target = item ? (this.radio(item) ?? this.toggle(item)) : null;

            if (target) {
                target.focus();
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
