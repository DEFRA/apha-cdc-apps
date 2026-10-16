// Profile properties: the affected species selector and the "Reset selections" button.
//
// The page shows the species hierarchy twice, as two multi-select trees rendered by
// Views/Shared/_TreeView.cshtml and driven by tree-view.js: "Profiled species" and "Other affected
// species". This script applies the rules the legacy AffectedSpeciesSelector control enforced across
// the two trees:
//
// - A species is either profiled or "other", never both, so ticking it in one tree disables it in the other.
// - A species group stands in for every species under it, so ticking a group ticks and disables the
//   active species beneath it in its own tree, and disables them in the other tree. Only the group is
//   submitted, as disabled boxes are not.
// - An inactive species cannot be newly picked, but one that is already ticked can be unticked.
// - Unticking a profiled species warns that its data will be removed (when the page is editing a profile).
// - Opening or closing a branch in one tree does the same in the other, so the two stay level.
(function () {
    'use strict';

    const ITEM = '.app-tree__item';
    const ROW_INPUT = ':scope > .app-tree__row input[type="checkbox"]';
    const ROW_LABEL = ':scope > .app-tree__row label';
    const ROW_TOGGLE = ':scope > .app-tree__row .app-tree__toggle';
    const INACTIVE_LABEL = ':scope > .app-tree__row .app-tree__label--inactive';
    const EXPANDED = 'aria-expanded';
    const ANNOUNCEMENT_DELAY_MS = 100;

    // Reads a rendered tree into nodes that know their ancestors. querySelectorAll returns items in
    // document order, so a node's parent has always been read before it.
    function readTree($panel) {
        const $list = $panel.querySelector('ul.app-tree');
        const nodes = [];
        const byItem = new Map();

        $list.querySelectorAll(ITEM).forEach(($item) => {
            const $parentItem = $item.parentElement.closest(ITEM);
            const parent = $parentItem ? byItem.get($parentItem) : null;
            const input = $item.querySelector(ROW_INPUT);
            const node = {
                $item,
                input,
                value: input.value,
                inactive: Boolean($item.querySelector(INACTIVE_LABEL)),
                covered: false,
                ancestors: parent ? [...parent.ancestors, parent.value] : []
            };

            byItem.set($item, node);
            nodes.push(node);
        });

        return { $list, nodes, byValue: new Map(nodes.map((node) => [node.value, node])) };
    }

    // Boxes the user ticked, as opposed to the ones this script ticked for them beneath a ticked group.
    function userChecked(tree) {
        return new Set(tree.nodes.filter((node) => node.input.checked && !node.covered).map((node) => node.value));
    }

    function hasAncestorIn(node, values) {
        return node.ancestors.some((value) => values.has(value));
    }

    function labelOf(node) {
        const $label = node.$item.querySelector(ROW_LABEL);

        return ($label ? $label.textContent : node.value).replace(/\s+/g, ' ').trim();
    }

    // Brings every box in a tree in line with what is ticked in it and in the other tree.
    function applyRules(tree, own, other) {
        tree.nodes.forEach((node) => {
            if (hasAncestorIn(node, own)) {
                node.covered = true;
                node.input.checked = !node.inactive;
                node.input.disabled = true;
            } else if (other.has(node.value) || hasAncestorIn(node, other)) {
                node.covered = false;
                node.input.checked = false;
                node.input.disabled = true;
            } else {
                if (node.covered) {
                    node.input.checked = false;
                }

                node.covered = false;
                node.input.disabled = node.inactive && !node.input.checked;
            }
        });
    }

    class SpeciesSelector {
        constructor($root) {
            this.$root = $root;
            this.profiled = readTree($root.querySelector('[data-species-tree="profiled"]'));
            this.other = readTree($root.querySelector('[data-species-tree="other"]'));
            this.warningEnabled = $root.dataset.speciesWarningEnabled === 'true';
            this.$warning = $root.querySelector('[data-species-warning]');
            this.$announcement = $root.querySelector('[data-species-announcement]');
            this.statuses = {
                profiled: $root.querySelector('[data-species-status="profiled"]'),
                other: $root.querySelector('[data-species-status="other"]')
            };

            this.owners = new Map();
            this.profiled.nodes.forEach((node) => this.owners.set(node.input, { tree: this.profiled, opposite: this.other, node }));
            this.other.nodes.forEach((node) => this.owners.set(node.input, { tree: this.other, opposite: this.profiled, node }));

            $root.addEventListener('change', (event) => this.onChange(event));
            this.keepBranchesLevel(this.profiled, this.other);
            this.keepBranchesLevel(this.other, this.profiled);
            this.refresh();
        }

        onChange(event) {
            const owner = this.owners.get(event.target);

            if (!owner) {
                return;
            }

            if (event.target.checked) {
                // The species now stands in for everything beneath it, in whichever tree those were ticked.
                this.release(owner.tree, owner.node);
                this.release(owner.opposite, owner.node);
            } else if (owner.tree === this.profiled) {
                this.showWarning();
            }

            this.refresh();
        }

        release(tree, node) {
            tree.nodes
                .filter((candidate) => candidate.ancestors.includes(node.value))
                .forEach((candidate) => {
                    candidate.input.checked = false;
                    candidate.covered = false;
                });
        }

        refresh() {
            const profiledChecked = userChecked(this.profiled);
            const otherChecked = userChecked(this.other);

            applyRules(this.profiled, profiledChecked, otherChecked);
            applyRules(this.other, otherChecked, profiledChecked);
            this.updateStatus('profiled', this.profiled);
            this.updateStatus('other', this.other);
        }

        // "Selected: ..." lists the species ticked directly - not those ticked for the user beneath a group.
        updateStatus(kind, tree) {
            const $status = this.statuses[kind];

            if (!$status) {
                return;
            }

            const labels = tree.nodes
                .filter((node) => node.input.checked && !node.input.disabled)
                .map(labelOf)
                .sort((first, second) => first.localeCompare(second));

            $status.hidden = labels.length === 0;
            $status.textContent = labels.length > 0 ? `Selected: ${labels.join(', ')}` : '';
        }

        showWarning() {
            if (this.warningEnabled && this.$warning) {
                this.$warning.hidden = false;
            }
        }

        // Unticks everything in both trees, as the legacy "Reset selections" button did.
        clear() {
            const hadProfiled = this.profiled.nodes.some((node) => node.input.checked && !node.covered);

            [this.profiled, this.other].forEach((tree) => {
                tree.nodes.forEach((node) => {
                    node.input.checked = false;
                    node.covered = false;
                });
            });

            if (hadProfiled) {
                this.showWarning();
            }

            this.refresh();
            this.announce('Selection cleared');
        }

        // Cleared first so the same message is announced again by a second reset.
        announce(message) {
            if (!this.$announcement) {
                return;
            }

            this.$announcement.textContent = '';
            window.setTimeout(() => {
                this.$announcement.textContent = message;
            }, ANNOUNCEMENT_DELAY_MS);
        }

        // Opening or closing a branch in one tree does the same to the matching branch in the other.
        // Watching the toggles' aria-expanded covers the mouse, the keyboard and "Open all" alike.
        keepBranchesLevel(source, target) {
            new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    const $item = mutation.target.closest(ITEM);
                    const counterpart = $item && target.byValue.get($item.querySelector(ROW_INPUT).value);
                    const $toggle = counterpart && counterpart.$item.querySelector(ROW_TOGGLE);
                    const expanded = mutation.target.getAttribute(EXPANDED) === 'true';

                    if ($toggle && target.$list.treeView && ($toggle.getAttribute(EXPANDED) === 'true') !== expanded) {
                        target.$list.treeView.setExpanded(counterpart.$item, expanded);
                    }
                });
            }).observe(source.$list, { attributes: true, attributeFilter: [EXPANDED], subtree: true });
        }
    }

    class ProfileProperties {
        constructor($form) {
            this.$form = $form;

            const $selector = $form.querySelector('[data-module="app-species-selector"]');
            const $reset = $form.querySelector('[data-profile-properties-reset]');
            const $editor = $form.querySelector('[data-app-title-hidden-input]');

            this.selector = $selector ? new SpeciesSelector($selector) : null;

            if ($reset) {
                $reset.addEventListener('click', () => this.reset($reset));
            }

            // The title is a single line, so Enter must not start a new one.
            if ($editor) {
                $editor.addEventListener('keydown', (event) => {
                    if (event.key === 'Enter') {
                        event.preventDefault();
                    }
                });
            }
        }

        reset($button) {
            const $editor = this.$form.querySelector('[data-app-title-hidden-input]');

            if ($editor) {
                $editor.innerHTML = '';
                // Lets the title editor's script copy the empty title to the hidden input and record it for undo.
                $editor.dispatchEvent(new Event('input', { bubbles: true }));
            }

            if (this.selector) {
                this.selector.clear();
            }

            $button.focus();
        }
    }

    window.SpeciesSelector = SpeciesSelector;
    window.ProfileProperties = ProfileProperties;

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-module="app-profile-properties"]').forEach(function ($form) {
            $form.profileProperties = new ProfileProperties($form);
        });
    });
})();
