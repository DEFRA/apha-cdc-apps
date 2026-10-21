// Single-line rich text for species names, replicating the legacy TinyMCE single-line editor
// (undo redo | italic). The <input> stays in the page as the submitted field and the
// no-JavaScript fallback; with JavaScript a labelled contenteditable textbox replaces it.
(function () {
    'use strict';

    function serialise(node) {
        var html = '';
        node.childNodes.forEach(function (child) {
            if (child.nodeType === Node.TEXT_NODE) {
                html += child.nodeValue.replace(/[\r\n]+/g, '');
            } else if (child.nodeType === Node.ELEMENT_NODE) {
                var inner = serialise(child);
                var tag = child.tagName;
                html += (tag === 'EM' || tag === 'I') ? '<em>' + inner + '</em>' : inner;
            }
        });
        return html;
    }

    function toEditorHtml(value) {
        // DOMParser builds an inert document, so only <em>/<i> are kept and every other tag is
        // dropped without relying on regex stripping. Text is re-added as text nodes.
        var source = new DOMParser().parseFromString(value, 'text/html').body;
        var container = document.createElement('div');

        function copy(node, target) {
            node.childNodes.forEach(function (child) {
                if (child.nodeType === Node.TEXT_NODE) {
                    if (child.nodeValue) {
                        target.appendChild(document.createTextNode(child.nodeValue));
                    }
                } else if (child.nodeType === Node.ELEMENT_NODE) {
                    var isEmphasis = child.tagName === 'EM' || child.tagName === 'I';
                    copy(child, isEmphasis && target === container
                        ? container.appendChild(document.createElement('em'))
                        : target);
                }
            });
        }

        copy(source, container);
        return container.innerHTML;
    }

    var MAX_LENGTH = 50;
    var UNDO_PAUSE_MS = 600;

    // Undo levels are taken at word boundaries, pauses and formatting changes, as TinyMCE does,
    // rather than per keystroke.
    function createHistory(editor, onChange) {
        var states = [editor.innerHTML];
        var index = 0;
        var timer = null;

        function commit() {
            clearTimeout(timer);
            timer = null;
            var html = editor.innerHTML;
            if (html !== states[index]) {
                states.length = index + 1;
                states.push(html);
                index++;
            }
        }

        function restore() {
            editor.innerHTML = states[index];
            var range = document.createRange();
            range.selectNodeContents(editor);
            range.collapse(false);
            var selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            onChange();
        }

        return {
            commit: commit,
            record: function (event) {
                var data = event && event.data;
                if (event && event.inputType === 'insertText' && data && data !== ' ') {
                    clearTimeout(timer);
                    timer = setTimeout(commit, UNDO_PAUSE_MS);
                } else {
                    commit();
                }
            },
            canUndo: function () { return index > 0 || editor.innerHTML !== states[index]; },
            canRedo: function () { return index < states.length - 1 && editor.innerHTML === states[index]; },
            undo: function () {
                commit();
                if (index > 0) { index--; restore(); }
            },
            redo: function () {
                if (this.canRedo()) { index++; restore(); }
            }
        };
    }

    function createToolbar(editor, label, history) {
        var toolbar = document.createElement('div');
        toolbar.className = 'app-richtext-toolbar';
        toolbar.setAttribute('role', 'toolbar');
        toolbar.setAttribute('aria-label', label + ' formatting');
        toolbar.history = history;

        [
            { command: 'undo', text: '\u21B6', name: 'Undo', extra: '' },
            { command: 'redo', text: '\u21B7', name: 'Redo', extra: '' },
            { divider: true },
            { command: 'italic', text: 'I', name: 'Italic', extra: ' app-richtext-toolbar__button--italic' }
        ].forEach(function (item) {
            if (item.divider) {
                var divider = document.createElement('span');
                divider.className = 'app-richtext-toolbar__divider';
                divider.setAttribute('aria-hidden', 'true');
                toolbar.appendChild(divider);
                return;
            }

            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'app-richtext-toolbar__button app-richtext-toolbar__button--icon' + item.extra;
            button.setAttribute('aria-label', item.name);
            button.title = item.name;
            button.textContent = item.text;
            button.dataset.command = item.command;
            if (item.command === 'italic') {
                button.setAttribute('aria-pressed', 'false');
            }
            button.addEventListener('mousedown', function (event) { event.preventDefault(); });
            button.addEventListener('click', function () {
                editor.focus();
                if (item.command === 'undo') {
                    history.undo();
                } else if (item.command === 'redo') {
                    history.redo();
                } else {
                    history.commit();
                    document.execCommand(item.command);
                    editor.dispatchEvent(new Event('input', { bubbles: true }));
                }
                updateState(toolbar);
            });
            toolbar.appendChild(button);
        });

        return toolbar;
    }

    // Mirrors the legacy toolbar: italic shows when it applies at the caret; undo/redo grey out when unavailable.
    function updateState(toolbar) {
        toolbar.querySelectorAll('button').forEach(function (button) {
            var command = button.dataset.command;
            if (command === 'italic') {
                var on = document.queryCommandState('italic');
                button.setAttribute('aria-pressed', on ? 'true' : 'false');
                button.classList.toggle('app-richtext-toolbar__button--active', on);
            } else {
                button.disabled = command === 'undo' ? !toolbar.history.canUndo() : !toolbar.history.canRedo();
            }
        });
    }

    function initialise(input) {
        var label = document.querySelector('label[for="' + input.id + '"]');
        var labelText = label ? label.textContent.trim().replace(/:$/, '') : 'Name';

        var editor = document.createElement('div');
        editor.id = input.id + '-editor';
        editor.className = 'govuk-input app-richtext-editor app-richtext-editor--single-line';
        editor.contentEditable = 'true';
        editor.setAttribute('role', 'textbox');
        editor.setAttribute('aria-multiline', 'false');
        editor.spellcheck = true;

        if (label) {
            label.id = label.id || input.id + '-label';
            label.setAttribute('for', editor.id);
            editor.setAttribute('aria-labelledby', label.id);
        }
        var describedBy = input.getAttribute('aria-describedby');
        if (describedBy) {
            editor.setAttribute('aria-describedby', describedBy);
        }
        if (input.classList.contains('govuk-input--error')) {
            editor.classList.add('govuk-input--error');
        }

        editor.innerHTML = toEditorHtml(input.value);
        input.hidden = true;
        input.parentNode.insertBefore(editor, input);
        var toolbar;
        var history = createHistory(editor, function () {
            sync();
            updateState(toolbar);
        });
        toolbar = createToolbar(editor, labelText, history);
        input.parentNode.insertBefore(toolbar, editor);
        updateState(toolbar);

        ['keyup', 'mouseup', 'focus', 'input'].forEach(function (name) {
            editor.addEventListener(name, function () { updateState(toolbar); });
        });
        document.addEventListener('selectionchange', function () {
            if (document.activeElement === editor) {
                updateState(toolbar);
            }
        });

        function sync() {
            input.value = serialise(editor).trim();
        }

        editor.addEventListener('input', function (event) {
            sync();
            history.record(event);
        });
        editor.addEventListener('keydown', function (event) {
            var modified = event.ctrlKey || event.metaKey;
            var key = event.key.toLowerCase();

            if (modified && (key === 'z' || key === 'y')) {
                event.preventDefault();
                if (key === 'y' || event.shiftKey) { history.redo(); } else { history.undo(); }
                updateState(toolbar);
                return;
            }

            // Only italics are allowed, so the bold and underline shortcuts do nothing, as in legacy.
            if (event.key === 'Enter' || (modified && (key === 'b' || key === 'u'))) {
                event.preventDefault();
            } else if (!modified && event.key.length === 1 && serialise(editor).length >= MAX_LENGTH
                && window.getSelection().isCollapsed) {
                event.preventDefault();
            }
        });
        editor.addEventListener('paste', function (event) {
            event.preventDefault();
            var text = (event.clipboardData || window.clipboardData).getData('text/plain');
            document.execCommand('insertText', false, text.replace(/[^\x00-\x7F\s]+/g, '').replace(/[\r\n]+/g, ' '));
        });
        if (input.form) {
            input.form.addEventListener('submit', sync);
            input.form.addEventListener('formdata', sync);
        }
    }

    function start() {
        document.querySelectorAll('input[data-single-line-richtext]').forEach(initialise);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
