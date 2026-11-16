// What the panels that edit one answer at a time have in common: the per-species and the repeating questions of the Questions tab each
// show their answers in a table and edit one of them in a panel below it. This reads an answer from the panel's control for a field, writes
// one to it, and works out the text the table shows for it.
//
// A panel has an element marked data-answer-field for each field, with the field's data type in data-field-type and whether the user may
// change it in data-field-editable. Its control, or for a list of choices each of its checkboxes, is marked data-answer-editor. A text
// answer the user may not change is shown formatted in an element marked data-answer-text-view instead.
(function () {
    'use strict';

    // How many characters of a text answer the table shows, as the legacy table did.
    var SUMMARY_LENGTH = 50;

    function plainText(html) {
        var parsed = new DOMParser().parseFromString(html, 'text/html');

        return (parsed.body.textContent || '').trim();
    }

    function labelOf(control) {
        var label = document.querySelector('label[for="' + control.id + '"]');

        return label ? label.textContent.trim() : '';
    }

    function dataType(wrapper) {
        return wrapper.dataset.fieldType;
    }

    function booleanText(value) {
        if (value === 'true') {
            return 'Yes';
        }

        return value === 'false' ? 'No' : '';
    }

    // The text the table shows for an answer, which the server works out in the same way for the answers it renders.
    function summaryText(wrapper, value) {
        if (value === '') {
            return '';
        }

        switch (dataType(wrapper)) {
            case 'Boolean':
                return booleanText(value);
            case 'List':
                var option = wrapper.querySelector('option[value="' + value + '"]');
                return option ? option.textContent : '';
            case 'MultiValueList':
                var chosen = new Set(value.split(','));
                return Array.prototype.filter.call(wrapper.querySelectorAll('input[type="checkbox"]'), function (box) {
                    return chosen.has(box.value);
                }).map(labelOf).join(', ');
            case 'Decimal':
                return Number.isFinite(Number(value)) ? Number(value).toFixed(2) : value;
            case 'Date':
                return value;
            default:
                return plainText(value).slice(0, SUMMARY_LENGTH);
        }
    }

    function readControl(wrapper) {
        if (dataType(wrapper) === 'MultiValueList') {
            return Array.prototype.filter.call(wrapper.querySelectorAll('input[type="checkbox"]'), function (box) {
                return box.checked;
            }).map(function (box) {
                return box.value;
            }).join(',');
        }

        var control = wrapper.querySelector('[data-answer-editor]');

        return control ? control.value : '';
    }

    function writeControl(wrapper, value) {
        if (dataType(wrapper) === 'MultiValueList') {
            var chosen = new Set(value === '' ? [] : value.split(','));
            wrapper.querySelectorAll('input[type="checkbox"]').forEach(function (box) {
                box.checked = chosen.has(box.value);
            });
            return;
        }

        var control = wrapper.querySelector('[data-answer-editor]');

        if (control) {
            control.value = value;

            // A drop-down that decides whether other questions apply is put back to this answer when a change to it is not confirmed.
            if ('relevanceAffects' in control.dataset) {
                control.dataset.relevanceHeld = control.value;
            }

            // A rich text field shows an editor in place of its text area, which has to be filled as well.
            var editor = document.getElementById(control.id + '-editor');
            if (editor && window.appRichText) {
                window.appRichText.fill(editor, value);
            }
            return;
        }

        var view = wrapper.querySelector('[data-answer-text-view]');

        if (view && window.appRichText) {
            window.appRichText.fill(view, value);
        } else if (view) {
            view.textContent = plainText(value);
        }
    }

    window.appAnswerFields = {
        fieldId: function (wrapper) {
            return wrapper.dataset.answerField;
        },
        isEditable: function (wrapper) {
            return wrapper.dataset.fieldEditable === 'true';
        },
        summaryText: summaryText,
        read: readControl,
        write: writeControl
    };
})();
