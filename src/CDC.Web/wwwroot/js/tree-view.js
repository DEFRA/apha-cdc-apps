// TreeView: reusable nested-checkbox tree picker.
//
// Enhances the markup rendered by Views/Shared/_TreeView.cshtml. Without JavaScript the
// markup is still a usable set of nested checkboxes, so nothing is lost.
//
// Semantics come from native checkboxes and native buttons - no ARIA tree role - and the only
// ARIA used is aria-expanded plus an aria-controls that points at a real element id.
(function () {
    'use strict';

    var ITEM = '.app-tree__item';
    var GROUP = ':scope > ul.app-tree__group';

    function TreeView($root) {
        if (!($root instanceof HTMLElement)) {
            return;
        }

        this.$root = $root;
        this.name = $root.dataset.appTreeName || 'item';
        this.namePlural = $root.dataset.appTreeNamePlural || this.name + 's';
        this.idPrefix = $root.id || this.name;

        var $form = $root.closest('form');
        this.$status = $form ? $form.querySelector('.app-tree__status') : null;

        this.items = Array.prototype.slice.call($root.querySelectorAll(ITEM));
        this.items.forEach(this.setupItem, this);

        $root.addEventListener('click', this.onClick.bind(this));
        $root.addEventListener('change', this.onChange.bind(this));
        $root.addEventListener('keydown', this.onKeydown.bind(this));

        if ($form) {
            $form.querySelectorAll('[data-app-tree-action]').forEach(function ($button) {
                $button.addEventListener('click', function () {
                    this.runAction($button.dataset.appTreeAction);
                }.bind(this));
            }, this);
        }

        this.refreshAll();
    }

    /* ---------- structure helpers ---------- */

    TreeView.prototype.group = function (item) {
        return item.querySelector(GROUP);
    };

    TreeView.prototype.checkbox = function (item) {
        return item.querySelector(':scope > .app-tree__row input[type="checkbox"]');
    };

    TreeView.prototype.toggle = function (item) {
        return item.querySelector(':scope > .app-tree__row .app-tree__toggle');
    };

    TreeView.prototype.parent = function (item) {
        return item.parentElement.closest(ITEM);
    };

    TreeView.prototype.children = function (item) {
        var group = this.group(item);
        return group
            ? Array.prototype.slice.call(group.children).filter(function (el) { return el.matches(ITEM); })
            : [];
    };

    TreeView.prototype.siblings = function (item) {
        return Array.prototype.slice.call(item.parentElement.children).filter(function (el) {
            return el.matches(ITEM);
        });
    };

    TreeView.prototype.descendantBoxes = function (item) {
        var group = this.group(item);
        return group ? Array.prototype.slice.call(group.querySelectorAll('input[type="checkbox"]')) : [];
    };

    TreeView.prototype.leafBoxes = function () {
        return this.items
            .filter(function (item) { return !this.group(item); }, this)
            .map(function (item) { return this.checkbox(item); }, this);
    };

    TreeView.prototype.depth = function (item) {
        var depth = 0;
        var current = this.parent(item);

        while (current) {
            depth += 1;
            current = this.parent(current);
        }

        return depth;
    };

    /* ---------- set up ---------- */

    TreeView.prototype.setupItem = function (item, index) {
        var row = item.querySelector(':scope > .app-tree__row');
        var group = this.group(item);
        var checkbox = this.checkbox(item);

        row.style.setProperty('--app-tree-depth', this.depth(item));

        if (!group) {
            var spacer = document.createElement('span');
            spacer.className = 'app-tree__spacer';
            spacer.setAttribute('aria-hidden', 'true');
            row.insertBefore(spacer, row.firstElementChild);
            return;
        }

        // aria-controls must point at a real id, so guarantee one exists.
        group.id = group.id || this.idPrefix + '-group-' + index;

        var label = item.querySelector(':scope > .app-tree__row label');
        var expanded = item.dataset.appTreeExpanded === 'true';

        var toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'app-tree__toggle';
        toggle.setAttribute('aria-expanded', String(expanded));
        toggle.setAttribute('aria-controls', group.id);

        // Accessible name is the category it opens; state comes from aria-expanded.
        var name = document.createElement('span');
        name.className = 'govuk-visually-hidden';
        name.textContent = label ? label.textContent.trim() : checkbox.value;
        toggle.appendChild(name);
        toggle.insertAdjacentHTML('beforeend',
            '<svg class="app-tree__toggle-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 16" ' +
            'fill="none" stroke="currentColor" stroke-width="3" focusable="false" aria-hidden="true">' +
            '<path d="M1.5 1.5 8 8l-6.5 6.5"/></svg>');

        row.insertBefore(toggle, row.firstElementChild);
        group.hidden = !expanded;
    };

    /* ---------- expand and collapse ---------- */

    TreeView.prototype.setExpanded = function (item, expanded) {
        var group = this.group(item);
        var toggle = this.toggle(item);

        if (!group || !toggle) {
            return;
        }

        group.hidden = !expanded;
        toggle.setAttribute('aria-expanded', String(expanded));
        item.dataset.appTreeExpanded = String(expanded);
    };

    TreeView.prototype.isExpanded = function (item) {
        var toggle = this.toggle(item);
        return !!toggle && toggle.getAttribute('aria-expanded') === 'true';
    };

    TreeView.prototype.runAction = function (action) {
        if (action === 'expand-all' || action === 'collapse-all') {
            var expanded = action === 'expand-all';
            this.items.forEach(function (item) { this.setExpanded(item, expanded); }, this);
            return;
        }

        if (action === 'clear') {
            this.items.forEach(function (item) {
                var checkbox = this.checkbox(item);
                checkbox.checked = false;
                checkbox.indeterminate = false;
            }, this);

            this.updateCount();
        }
    };

    /* ---------- selection cascade ---------- */

    TreeView.prototype.onChange = function (event) {
        var checkbox = event.target;

        if (!(checkbox instanceof HTMLInputElement) || checkbox.type !== 'checkbox') {
            return;
        }

        var item = checkbox.closest(ITEM);
        checkbox.indeterminate = false;

        this.descendantBoxes(item).forEach(function (box) {
            box.checked = checkbox.checked;
            box.indeterminate = false;
        });

        var ancestor = this.parent(item);

        while (ancestor) {
            this.refreshAncestorState(ancestor);
            ancestor = this.parent(ancestor);
        }

        this.updateCount();
    };

    TreeView.prototype.refreshAncestorState = function (item) {
        var checkbox = this.checkbox(item);
        var children = this.children(item).map(function (child) { return this.checkbox(child); }, this);
        var checked = children.filter(function (box) { return box.checked; }).length;
        var partial = children.some(function (box) { return box.indeterminate; });

        checkbox.checked = checked === children.length && !partial;
        checkbox.indeterminate = partial || (checked > 0 && checked < children.length);
    };

    TreeView.prototype.refreshAll = function () {
        // Bottom-up so parents see settled child state, which matters when the markup
        // arrives with some checkboxes already checked by the server.
        for (var i = this.items.length - 1; i >= 0; i -= 1) {
            if (this.group(this.items[i])) {
                this.refreshAncestorState(this.items[i]);
            }
        }

        this.updateCount();
    };

    TreeView.prototype.updateCount = function () {
        if (!this.$status) {
            return;
        }

        var total = this.leafBoxes().filter(function (box) { return box.checked; }).length;

        this.$status.textContent = total === 0
            ? 'No ' + this.namePlural + ' selected'
            : total + ' ' + (total === 1 ? this.name : this.namePlural) + ' selected';
    };

    /* ---------- keyboard ---------- */

    TreeView.prototype.visibleItems = function () {
        return this.items.filter(function (item) { return !item.parentElement.closest('[hidden]'); });
    };

    TreeView.prototype.focusItem = function (item) {
        var checkbox = item ? this.checkbox(item) : null;

        if (checkbox) {
            checkbox.focus();
        }
    };

    TreeView.prototype.onClick = function (event) {
        var toggle = event.target.closest('.app-tree__toggle');

        if (!toggle || !this.$root.contains(toggle)) {
            return;
        }

        var item = toggle.closest(ITEM);
        this.setExpanded(item, toggle.getAttribute('aria-expanded') !== 'true');
    };

    TreeView.prototype.onKeydown = function (event) {
        var item = event.target.closest(ITEM);

        if (!item) {
            return;
        }

        var visible = this.visibleItems();
        var index = visible.indexOf(item);
        var handled = true;
        var parent;

        switch (event.key) {
            case 'ArrowDown':
                if (index > -1 && index < visible.length - 1) {
                    this.focusItem(visible[index + 1]);
                }
                break;
            case 'ArrowUp':
                if (index > 0) {
                    this.focusItem(visible[index - 1]);
                }
                break;
            case 'ArrowRight':
                if (!this.group(item)) {
                    break;
                }
                if (this.isExpanded(item)) {
                    this.focusItem(this.children(item)[0]);
                } else {
                    this.setExpanded(item, true);
                }
                break;
            case 'ArrowLeft':
                if (this.group(item) && this.isExpanded(item)) {
                    this.setExpanded(item, false);
                } else {
                    parent = this.parent(item);
                    if (parent) {
                        this.focusItem(parent);
                    }
                }
                break;
            case 'Home':
                this.focusItem(visible[0]);
                break;
            case 'End':
                this.focusItem(visible[visible.length - 1]);
                break;
            case '*':
                this.siblings(item).forEach(function (sibling) { this.setExpanded(sibling, true); }, this);
                break;
            default:
                handled = false;
        }

        if (handled) {
            event.preventDefault();
        }
    };

    window.TreeView = TreeView;

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-module="app-tree-view"]').forEach(function ($tree) {
            new TreeView($tree);
        });
    });
})();
