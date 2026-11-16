// The table of a repeating question on the Questions tab, and the panel that adds, edits and deletes its rows. It replaces the legacy
// ProfileRowListPanel: "Add" opens a blank row in the panel below the table, "Edit" opens a row's answers in it, "OK" applies them and
// "Cancel" drops them, and "Delete" takes a row out of the table.
//
// Each row's answers are held in hidden inputs of the row, and the row lists itself in a hidden input, so the rows, the order they are in and their
// answers are what the form posts: they reach the server with Save, Validate or a change of tab as any other answer does. A row the user adds is
// copied from the question's template, under an identifier of its own that the server adds it under. The panel's own controls have no names and
// are never posted. Only an answer the user changed in the panel is written back; the others keep the value the row held.
(function () {
    'use strict';

    var fields = window.appAnswerFields;

    // The identifier the template names its row for, which a new row has swapped for its own.
    var BLANK_ID = '00000000-0000-0000-0000-000000000000';

    function newId() {
        if (typeof window.crypto.randomUUID === 'function') {
            return window.crypto.randomUUID();
        }

        // Browsers that are not on a secure origin have no randomUUID, but have getRandomValues.
        var bytes = window.crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 0x0f) | 0x40;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;

        var hex = Array.prototype.map.call(bytes, function (byte) {
            return byte.toString(16).padStart(2, '0');
        }).join('');

        return [hex.slice(0, 8), hex.slice(8, 12), hex.slice(12, 16), hex.slice(16, 20), hex.slice(20)].join('-');
    }

    function valueInput(row, fieldId) {
        return row.querySelector('[data-row-value="' + fieldId + '"]');
    }

    function storedValue(row, fieldId) {
        var input = valueInput(row, fieldId);

        return input ? input.value : '';
    }

    function store(row, fieldId, value, summary) {
        var input = valueInput(row, fieldId);
        var cell = row.querySelector('[data-row-summary="' + fieldId + '"]');

        if (input) {
            input.value = value;
        }
        if (cell) {
            cell.textContent = summary;
        }
    }

    // Tells assistive technology whether the panel is open for the button that opens it.
    function setExpanded(button, expanded) {
        if (button) {
            button.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        }
    }

    function initialise(root) {
        var panel = root.querySelector('[data-row-panel]');
        var table = root.querySelector('[data-row-table]');
        var body = root.querySelector('[data-row-body]');

        if (!panel || !table || !body) {
            return;
        }

        var canEdit = root.dataset.rowCanEdit === 'true';
        var emptyMessage = root.querySelector('[data-row-empty]');
        var addButton = root.querySelector('[data-row-add]');
        var template = root.querySelector('template[data-row-template]');
        var status = root.querySelector('[data-row-status]');
        var current = null;

        function rows() {
            return Array.prototype.slice.call(body.querySelectorAll('tr[data-row]'));
        }

        function wrappers() {
            return Array.prototype.slice.call(panel.querySelectorAll('[data-answer-field]'));
        }

        // The table shows when there is a row to show, and the rows are numbered as they are in it, which their links name them by.
        function showRows() {
            var all = rows();

            table.hidden = all.length === 0;
            emptyMessage.hidden = all.length > 0;

            all.forEach(function (row, index) {
                row.querySelectorAll('[data-row-number]').forEach(function (number) {
                    number.textContent = String(index + 1);
                });
            });
        }

        function say(message) {
            if (status) {
                status.textContent = message;
            }
        }

        // A copy of the template's row, whose inputs name the blank identifier, under an identifier of its own.
        function makeRow() {
            var id = newId();
            var row = template.content.firstElementChild.cloneNode(true);

            [row].concat(Array.prototype.slice.call(row.querySelectorAll('*'))).forEach(function (element) {
                Array.prototype.slice.call(element.attributes).forEach(function (attribute) {
                    if (attribute.value.includes(BLANK_ID)) {
                        element.setAttribute(attribute.name, attribute.value.replaceAll(BLANK_ID, id));
                    }
                });
            });

            return row;
        }

        // Writes the panel's answers to the row being edited, which joins the table when it is a new one. An answer the user did not change is
        // written as it was held, so that it is neither lost nor altered.
        function apply() {
            if (!current || !canEdit) {
                return;
            }

            var edited = current;

            wrappers().filter(function (wrapper) {
                return fields.isEditable(wrapper);
            }).forEach(function (wrapper) {
                var fieldId = fields.fieldId(wrapper);
                var value = edited.changed[fieldId] ? fields.read(wrapper) : storedValue(edited.row, fieldId);

                store(edited.row, fieldId, value, fields.summaryText(wrapper, value));
            });

            if (!edited.row.isConnected) {
                body.appendChild(edited.row);
                showRows();
                say('Answer added.');
            }
        }

        function close() {
            var button = current?.button;

            panel.hidden = true;
            current = null;
            setExpanded(button, false);

            if (button) {
                button.focus();
            }
        }

        // As the legacy Edit and Add links did, opening a row first keeps the changes made to the one that is open.
        function open(row, button) {
            apply();

            if (current) {
                setExpanded(current.button, false);
            }

            current = { row: row, button: button, changed: {} };
            setExpanded(button, true);

            wrappers().forEach(function (wrapper) {
                fields.write(wrapper, storedValue(row, fields.fieldId(wrapper)));
            });

            panel.hidden = false;
            panel.focus();
        }

        function accept() {
            if (!current) {
                return;
            }

            apply();

            // Focus goes to the row's own Edit link, which a new row has only now.
            var row = current.row;

            close();

            var edit = row.isConnected ? row.querySelector('[data-row-edit]') : null;

            if (edit) {
                edit.focus();
            }
        }

        function remove(row) {
            if (current && current.row === row) {
                setExpanded(current.button, false);
                panel.hidden = true;
                current = null;
            } else {
                apply();
            }

            row.remove();
            showRows();
            say('Answer deleted.');

            if (addButton) {
                addButton.focus();
            }
        }

        function noteChange(event) {
            var wrapper = event.target.closest('[data-answer-field]');

            if (wrapper && current) {
                current.changed[fields.fieldId(wrapper)] = true;
            }
        }

        root.addEventListener('click', function (event) {
            var edit = event.target.closest('[data-row-edit]');
            var remover = event.target.closest('[data-row-delete]');

            if (edit && root.contains(edit)) {
                open(edit.closest('tr[data-row]'), edit);
            } else if (remover && root.contains(remover)) {
                remove(remover.closest('tr[data-row]'));
            } else if (event.target.closest('[data-row-add]')) {
                open(makeRow(), addButton);
            } else if (event.target.closest('[data-row-ok]')) {
                accept();
            } else if (event.target.closest('[data-row-cancel]')) {
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

        // As the legacy Save did, an open panel's answers are kept when the section is saved, validated or left.
        var form = root.closest('form');
        if (form) {
            form.addEventListener('submit', apply);
        }
    }

    function initialiseAll() {
        document.querySelectorAll('[data-row-question]').forEach(initialise);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseAll);
    } else {
        initialiseAll();
    }
})();
