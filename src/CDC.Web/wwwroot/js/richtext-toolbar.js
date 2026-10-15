// Rich text editor for "Long Text" profile fields, replicating the legacy TinyMCE editor
// (undo redo | italic bullist | link). The <textarea> stays in the page as the submitted form
// field and the no-JavaScript fallback; with JavaScript a labelled, keyboard-accessible
// contenteditable textbox is shown in its place so formatting appears as it will be read
// rather than as raw HTML tags.
(function () {
    'use strict';

    var ALLOWED_TAGS = ['EM', 'I', 'OL', 'UL', 'LI', 'A', 'P', 'BR'];
    var SAFE_HREF = /^(https?:|mailto:)/i;

    function sanitiseNode(node) {
        Array.prototype.slice.call(node.childNodes).forEach(function (child) {
            if (child.nodeType === Node.ELEMENT_NODE) {
                sanitiseNode(child);

                if (ALLOWED_TAGS.indexOf(child.tagName) === -1) {
                    while (child.firstChild) {
                        node.insertBefore(child.firstChild, child);
                    }
                    node.removeChild(child);
                    return;
                }

                Array.prototype.slice.call(child.attributes).forEach(function (attribute) {
                    var keep = child.tagName === 'A' && ['href', 'target', 'title'].indexOf(attribute.name) !== -1;
                    if (!keep || (attribute.name === 'href' && !SAFE_HREF.test(attribute.value.trim()))) {
                        child.removeAttribute(attribute.name);
                    }
                });
            } else if (child.nodeType !== Node.TEXT_NODE) {
                node.removeChild(child);
            }
        });
    }

    function sanitiseHtml(html) {
        var container = document.createElement('div');
        container.innerHTML = html;
        sanitiseNode(container);
        return container.innerHTML;
    }

    function toEditorHtml(value) {
        if (!/<\/?[a-z][^>]*>/i.test(value)) {
            var escaper = document.createElement('div');
            escaper.textContent = value;
            return escaper.innerHTML.replace(/\r?\n/g, '<br>');
        }
        return sanitiseHtml(value);
    }

    function createEditor(textarea) {
        var editor = document.createElement('div');
        editor.id = textarea.id + '-editor';
        editor.className = 'govuk-textarea app-richtext-editor app-richtext-editor--surface';
        editor.contentEditable = 'true';
        editor.setAttribute('role', 'textbox');
        editor.setAttribute('aria-multiline', 'true');
        editor.spellcheck = true;

        var label = document.querySelector('label[for="' + textarea.id + '"]');
        if (label) {
            label.id = label.id || textarea.id + '-label';
            label.setAttribute('for', editor.id);
            editor.setAttribute('aria-labelledby', label.id);
        }

        var describedBy = textarea.getAttribute('aria-describedby');
        if (describedBy) {
            editor.setAttribute('aria-describedby', describedBy);
        }
        if (textarea.classList.contains('govuk-textarea--error')) {
            editor.classList.add('govuk-textarea--error');
        }

        editor.innerHTML = toEditorHtml(textarea.value);
        textarea.hidden = true;
        textarea.parentNode.insertBefore(editor, textarea);
        return editor;
    }

    function syncToTextarea(editor, textarea) {
        var html = sanitiseHtml(editor.innerHTML);
        var hasContent = editor.textContent.trim().length > 0 || /<li/i.test(html);
        textarea.value = hasContent ? html : '';
    }

    function insertLink(editor) {
        var url = window.prompt('Enter the link URL:', 'https://');
        if (!url || !SAFE_HREF.test(url.trim())) {
            return;
        }

        editor.focus();
        document.execCommand('createLink', false, url.trim());
    }

    function runCommand(command, editor) {
        editor.focus();

        switch (command) {
            case 'undo':
            case 'redo':
                document.execCommand(command);
                break;
            case 'italic':
                document.execCommand('italic');
                break;
            case 'bullist':
                document.execCommand('insertUnorderedList');
                break;
            case 'link':
                insertLink(editor);
                break;
            default:
                break;
        }

        editor.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function initialiseRichTextEditors() {
        var editors = {};

        try {
            document.execCommand('defaultParagraphSeparator', false, 'p');
        } catch (e) {
            // Unsupported in some browsers; the default separator is acceptable.
        }

        document.querySelectorAll('[data-richtext-command]').forEach(function (button) {
            var targetId = button.getAttribute('data-richtext-target');
            var textarea = document.getElementById(targetId);
            if (!textarea) {
                return;
            }

            if (!editors[targetId]) {
                var editor = createEditor(textarea);
                editors[targetId] = editor;

                editor.addEventListener('input', function () { syncToTextarea(editor, textarea); });
                editor.addEventListener('paste', function (event) {
                    event.preventDefault();
                    var text = (event.clipboardData || window.clipboardData).getData('text/plain');
                    document.execCommand('insertText', false, text);
                });
                if (textarea.form) {
                    textarea.form.addEventListener('submit', function () { syncToTextarea(editor, textarea); });
                    textarea.form.addEventListener('formdata', function () { syncToTextarea(editor, textarea); });
                }
            }

            button.addEventListener('mousedown', function (event) { event.preventDefault(); });
            button.addEventListener('click', function () {
                runCommand(button.getAttribute('data-richtext-command'), editors[targetId]);
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseRichTextEditors);
    } else {
        initialiseRichTextEditors();
    }
})();
