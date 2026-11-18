// The table of a per-species question on the Questions tab, and the panel that edits one species' answers. It replaces the legacy
// PerSpeciesListPanel: "Edit" opens the answers of that species in the one panel below the table, "OK" applies them (and to any other
// species picked under "Apply this answer also to:"), and "Cancel" drops them.
//
// Each species' answers are held in hidden inputs of the row, which are what the form posts, so an answer reaches the server with Save,
// Validate or a change of tab as any other does. The panel's own controls have no names and are never posted. Only an answer the user
// changed in the panel is written back; the others keep the value the row held, so that an answer the panel cannot show (such as a list
// item that is no longer offered) is never lost by pressing OK.
(function () {
    'use strict';

    var fields = window.appAnswerFields;

    // Set when the user has confirmed a change to an answer that decides whether other questions apply (relevance-confirm.js). The
    // next OK then saves the section, so that the questions that have become irrelevant are hidden, as the legacy page did.
    var refreshNeeded = false;

    function valueInput(row, fieldId) {
        return row.querySelector('[data-species-value="' + fieldId + '"]');
    }

    function storedValue(row, fieldId) {
        var input = valueInput(row, fieldId);

        return input ? input.value : '';
    }

    function store(row, fieldId, value, summary) {
        var input = valueInput(row, fieldId);
        var cell = row.querySelector('[data-species-summary="' + fieldId + '"]');

        if (input) {
            input.value = value;
        }
        if (cell) {
            cell.textContent = summary;
        }
    }

    function requestRefresh() {
        var button = document.querySelector('[data-relevance-refresh]');

        if (button) {
            button.click();
        }
    }

    function initialise(root) {
        var panel = root.querySelector('[data-species-panel]');

        if (!panel) {
            return;
        }

        var canEdit = root.dataset.speciesCanEdit === 'true';
        var heading = panel.querySelector('[data-species-panel-heading]');
        var picker = panel.querySelector('[data-species-picker]');
        var current = null;

        function rowOf(speciesId) {
            return root.querySelector('[data-species-row="' + speciesId + '"]');
        }

        function wrappers() {
            return Array.prototype.slice.call(panel.querySelectorAll('[data-answer-field]'));
        }

        function pickBoxes() {
            return picker ? Array.prototype.slice.call(picker.querySelectorAll('[data-species-pick]')) : [];
        }

        function offeredBoxes() {
            return pickBoxes().filter(function (box) {
                return !box.closest('[data-species-pick-item]').hidden;
            });
        }

        function pickedRows() {
            return pickBoxes().filter(function (box) {
                return box.checked;
            }).map(function (box) {
                return rowOf(box.dataset.speciesPick);
            }).filter(Boolean);
        }

        function showPickedNames() {
            var names = pickBoxes().filter(function (box) {
                return box.checked;
            }).map(function (box) {
                return box.dataset.speciesName;
            });

            picker.querySelector('[data-species-picker-names]').textContent = names.join(', ');
        }

        // A new panel starts with no other species picked and the list closed, and leaves out the species being edited.
        function resetPicker(speciesId) {
            if (!picker) {
                return;
            }

            picker.open = false;
            picker.querySelectorAll('input[type="checkbox"]').forEach(function (box) {
                box.checked = false;
            });
            picker.querySelectorAll('[data-species-pick-item]').forEach(function (item) {
                item.hidden = item.dataset.speciesPickItem === speciesId;
            });
            showPickedNames();
        }

        // Writes the panel's answers to the species being edited and to each other species picked. An answer the user did not change is
        // written as it was held, so that it is neither lost nor altered.
        function apply() {
            if (!current || !canEdit) {
                return;
            }

            var targets = [current.row].concat(pickedRows());

            wrappers().filter(function (wrapper) {
                return fields.isEditable(wrapper);
            }).forEach(function (wrapper) {
                var fieldId = fields.fieldId(wrapper);
                var value = current.changed[fieldId] ? fields.read(wrapper) : storedValue(current.row, fieldId);
                var summary = fields.summaryText(wrapper, value);

                targets.forEach(function (row) {
                    store(row, fieldId, value, summary);
                });
            });
        }

        function close() {
            var button = current?.button;

            panel.hidden = true;
            current = null;

            if (button) {
                button.setAttribute('aria-expanded', 'false');
                button.focus();
            }
        }

        // As the legacy Edit link did, opening another species first keeps the changes made to the one that is open.
        function open(speciesId, button) {
            var row = rowOf(speciesId);

            if (!row) {
                return;
            }

            apply();
            current?.button.setAttribute('aria-expanded', 'false');

            current = { row: row, button: button, changed: {} };
            button.setAttribute('aria-expanded', 'true');
            heading.textContent = (canEdit ? 'Edit' : 'View') + ' answer for ' + (row.dataset.speciesName || '').toLowerCase();

            wrappers().forEach(function (wrapper) {
                fields.write(wrapper, storedValue(row, fields.fieldId(wrapper)));
            });

            resetPicker(speciesId);
            panel.hidden = false;
            panel.focus();
        }

        function accept() {
            if (!current) {
                return;
            }

            apply();
            close();

            if (canEdit && refreshNeeded) {
                refreshNeeded = false;
                requestRefresh();
            }
        }

        function noteChange(event) {
            var wrapper = event.target.closest('[data-answer-field]');

            if (wrapper && current) {
                current.changed[fields.fieldId(wrapper)] = true;
            }
        }

        root.addEventListener('click', function (event) {
            var edit = event.target.closest('[data-species-edit]');

            if (edit && root.contains(edit)) {
                open(edit.dataset.speciesEdit, edit);
            } else if (event.target.closest('[data-species-ok]')) {
                accept();
            } else if (event.target.closest('[data-species-cancel]')) {
                close();
            }
        });

        panel.addEventListener('input', noteChange);
        panel.addEventListener('change', noteChange);

        // Pressing Enter in a text box of the panel applies the answers, rather than submitting the form as Save.
        panel.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && event.target.matches('input[type="text"]')) {
                event.preventDefault();
                accept();
            }
        });

        panel.addEventListener('app:relevance-confirmed', function () {
            refreshNeeded = true;
        });

        if (picker) {
            picker.addEventListener('change', function (event) {
                var all = picker.querySelector('[data-species-pick-all]');
                var offered = offeredBoxes();

                if (event.target === all) {
                    offered.forEach(function (box) {
                        box.checked = all.checked;
                    });
                } else {
                    all.checked = offered.length > 0 && offered.every(function (box) {
                        return box.checked;
                    });
                }

                showPickedNames();
            });
        }

        // As the legacy Save did, an open panel's answers are kept when the section is saved, validated or left.
        var form = root.closest('form');
        if (form) {
            form.addEventListener('submit', apply);
        }
    }

    function initialiseAll() {
        document.querySelectorAll('[data-species-question]').forEach(initialise);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseAll);
    } else {
        initialiseAll();
    }
})();
