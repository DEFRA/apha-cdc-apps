// Rich text editor for "Long Text" profile fields, replicating the legacy TinyMCE editor
// (undo redo | italic bullist | link). The <textarea> stays in the page as the submitted form
// field and the no-JavaScript fallback; with JavaScript a labelled, keyboard-accessible
// contenteditable textbox is shown in its place so formatting appears as it will be read
// rather than as raw HTML tags.
(function () {
    'use strict';

    var ALLOWED_TAGS = ['EM', 'I', 'OL', 'UL', 'LI', 'A', 'P', 'BR'];
    var SAFE_HREF = /^(https?:|mailto:)/i;

    var KEPT_ATTRIBUTES = ['href', 'target', 'title'];
    var TAG_PATTERN = /<(\/?)([a-z][a-z0-9]*)((?:\s[^>]*)?)>/iy;
    var ATTRIBUTE_PATTERN = /([a-z-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi;
    var ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: '\u00a0' };

    function decodeEntities(text) {
        return text.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, function (match, name) {
            if (name.charAt(0) === '#') {
                var isHex = name.charAt(1).toLowerCase() === 'x';
                var code = parseInt(name.slice(isHex ? 2 : 1), isHex ? 16 : 10);
                return code > 0 && code <= 0x10ffff ? String.fromCodePoint(code) : match;
            }
            var decoded = ENTITIES[name.toLowerCase()];
            return decoded === undefined ? match : decoded;
        });
    }

    function escapeText(text) {
        return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\u00a0/g, '&nbsp;');
    }

    function escapeAttribute(text) {
        return escapeText(text).replace(/"/g, '&quot;');
    }

    function readAttributes(source, tagName) {
        var attributes = {};
        if (tagName !== 'a') {
            return attributes;
        }
        var match;
        ATTRIBUTE_PATTERN.lastIndex = 0;
        while ((match = ATTRIBUTE_PATTERN.exec(source)) !== null) {
            var name = match[1].toLowerCase();
            var value = decodeEntities(match[2] !== undefined ? match[2] : match[3]);
            if (KEPT_ATTRIBUTES.indexOf(name) !== -1 && (name !== 'href' || SAFE_HREF.test(value.trim()))) {
                attributes[name] = value;
            }
        }
        return attributes;
    }

    // Builds the editor content as DOM nodes from the stored value. The value is only scanned for
    // the allowed tags and added as text nodes, so it is never interpreted as HTML: every other
    // tag is dropped, its text kept, and only safe link attributes survive.
    function fillEditor(editor, value) {
        var open = [];
        var text = '';
        var hasTags = /<\/?[a-z][^>]*>/i.test(value);

        function current() {
            return open.length ? open[open.length - 1] : editor;
        }

        function flush() {
            if (text) {
                current().appendChild(document.createTextNode(decodeEntities(text)));
                text = '';
            }
        }

        var i = 0;
        while (i < value.length) {
            var ch = value.charAt(i);

            if (ch === '<' && value.startsWith('<!--', i)) {
                var commentEnd = value.indexOf('-->', i + 4);
                i = commentEnd === -1 ? value.length : commentEnd + 3;
                continue;
            }

            TAG_PATTERN.lastIndex = i;
            var tag = ch === '<' ? TAG_PATTERN.exec(value) : null;

            if (tag) {
                var name = tag[2].toLowerCase();
                var allowed = ALLOWED_TAGS.indexOf(name.toUpperCase()) !== -1;
                flush();

                if (allowed && tag[1]) {
                    var position = open.map(function (element) { return element.localName; }).lastIndexOf(name);
                    if (position !== -1) {
                        open.length = position;
                    }
                } else if (allowed) {
                    var element = document.createElement(name);
                    var attributes = readAttributes(tag[3], name);
                    Object.keys(attributes).forEach(function (key) { element.setAttribute(key, attributes[key]); });
                    current().appendChild(element);
                    if (name !== 'br') {
                        open.push(element);
                    }
                }
                i = TAG_PATTERN.lastIndex;
            } else if (!hasTags && (ch === '\n' || ch === '\r')) {
                flush();
                editor.appendChild(document.createElement('br'));
                i += ch === '\r' && value.charAt(i + 1) === '\n' ? 2 : 1;
            } else {
                text += ch;
                i++;
            }
        }
        flush();
    }

    function serialise(node) {
        var html = '';
        node.childNodes.forEach(function (child) {
            if (child.nodeType === Node.TEXT_NODE) {
                html += escapeText(child.nodeValue);
            } else if (child.nodeType === Node.ELEMENT_NODE) {
                var tag = child.tagName;
                if (ALLOWED_TAGS.indexOf(tag) === -1) {
                    html += serialise(child);
                    return;
                }
                var name = tag.toLowerCase();
                var attributes = '';
                if (name === 'a') {
                    KEPT_ATTRIBUTES.forEach(function (key) {
                        var attributeValue = child.getAttribute(key);
                        if (attributeValue !== null && (key !== 'href' || SAFE_HREF.test(attributeValue.trim()))) {
                            attributes += ' ' + key + '="' + escapeAttribute(attributeValue) + '"';
                        }
                    });
                }
                html += name === 'br' ? '<br>' : '<' + name + attributes + '>' + serialise(child) + '</' + name + '>';
            }
        });
        return html;
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

        fillEditor(editor, textarea.value);
        textarea.hidden = true;
        textarea.parentNode.insertBefore(editor, textarea);
        return editor;
    }

    function syncToTextarea(editor, textarea) {
        var html = serialise(editor);
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
