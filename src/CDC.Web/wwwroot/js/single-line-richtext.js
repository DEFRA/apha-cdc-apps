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

    // Builds the editor content as DOM nodes from the stored value. The value is only ever scanned
    // for <em>/<i> tags and added as text nodes, so it is never interpreted as HTML: every other
    // tag is dropped and the text between tags is kept.
    function fillEditor(editor, value) {
        var emphasis = null;
        var text = '';

        function flush() {
            if (text) {
                (emphasis || editor).appendChild(document.createTextNode(text));
                text = '';
            }
        }

        var i = 0;
        while (i < value.length) {
            var ch = value.charAt(i);
            var end = ch === '<' ? value.indexOf('>', i) : -1;
            if (end === -1) {
                text += ch;
                i++;
                continue;
            }
            var tag = value.slice(i + 1, end).toLowerCase();
            if (tag === 'em' || tag === 'i') {
                flush();
                emphasis = editor.appendChild(document.createElement('em'));
            } else if (tag === '/em' || tag === '/i') {
                flush();
                emphasis = null;
            }
            i = end + 1;
        }
        flush();
    }

    var MAX_LENGTH = 50;
    var UNDO_PAUSE_MS = 600;

    function snapshot(editor) {
        return {
            html: editor.innerHTML,
            nodes: Array.prototype.map.call(editor.childNodes, function (node) { return node.cloneNode(true); })
        };
    }

    // Undo levels are taken at word boundaries, pauses and formatting changes, as TinyMCE does,
    // rather than per keystroke. States keep cloned nodes so restoring never re-parses HTML.
    function createHistory(editor, onChange) {
        var states = [snapshot(editor)];
        var index = 0;
        var timer = null;

        function commit() {
            clearTimeout(timer);
            timer = null;
            if (editor.innerHTML !== states[index].html) {
                states.length = index + 1;
                states.push(snapshot(editor));
                index++;
            }
        }

        function restore() {
            editor.replaceChildren.apply(editor, states[index].nodes.map(function (node) { return node.cloneNode(true); }));
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
            canUndo: function () { return index > 0 || editor.innerHTML !== states[index].html; },
            canRedo: function () { return index < states.length - 1 && editor.innerHTML === states[index].html; },
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

        fillEditor(editor, input.value);
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
