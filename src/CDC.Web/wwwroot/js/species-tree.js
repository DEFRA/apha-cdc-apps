// Placeholder tree UI behaviour for ViewSpeciesData (toggle-all + selection display only).
// No data loading - operates purely on the static/mock markup rendered server-side.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var toggle = document.getElementById('species-tree-toggle');
        var toggleText = toggle ? toggle.querySelector('.app-species-tree__toggle-text') : null;
        var tree = document.getElementById('species-tree');
        var selectionText = document.getElementById('species-selection');
        var selectionValue = document.getElementById('species-selection-value');
        var removeButton = document.getElementById('species-tree-remove-selection');

        if (toggle && tree && toggleText) {
            toggle.addEventListener('click', function () {
                var isExpanded = toggle.getAttribute('aria-expanded') === 'true';
                var nestedLists = tree.querySelectorAll('.app-species-tree__item .app-species-tree__list');

                nestedLists.forEach(function (list) {
                    list.hidden = isExpanded;
                });

                toggle.setAttribute('aria-expanded', String(!isExpanded));
                toggleText.textContent = isExpanded ? 'Expand all' : 'Collapse all';
            });
        }

        if (tree && selectionText && selectionValue) {
            tree.addEventListener('change', function (event) {
                if (event.target.type !== 'checkbox') {
                    return;
                }

                var checked = tree.querySelectorAll('input[type="checkbox"]:checked');

                if (checked.length === 0) {
                    selectionText.hidden = true;
                    return;
                }

                var lastChecked = checked[checked.length - 1];
                selectionValue.textContent = lastChecked.parentElement.textContent.trim();
                selectionText.hidden = false;
            });
        }

        if (removeButton && tree && selectionText) {
            removeButton.addEventListener('click', function () {
                tree.querySelectorAll('input[type="checkbox"]:checked').forEach(function (checkbox) {
                    checkbox.checked = false;
                });
                selectionText.hidden = true;
            });
        }
    });
})();
