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
    // A tag's attributes, if it has any, follow its name after white space, or after the slash of a self-closing tag such as <br/>.
    var TAG_PATTERN = /<(\/?)([a-z][a-z0-9]*)((?:[\s/][^>]*)?)>/iy;
    var ATTRIBUTE_PATTERN = /([a-z-]+)\s*=\s*(?:"([^"]*)"|'([^']*)')/gi;
    var CHARACTER_REFERENCE_PATTERN = /&(?:#x[0-9a-f]+|#\d+|[a-z][a-z0-9]*);/gi;
    var referenceDecoder = document.createElement('textarea');

    // Replaces each character reference with its character as a browser would (&rsquo; &ldquo; &pound; &#8217; ...),
    // which the legacy editor stored for anything outside ASCII. The decoder is a detached textarea, which holds what it
    // is given as text, and a reference holds no markup, so nothing is run or turned into elements. A reference the
    // browser does not know is left as it is.
    function decodeEntities(text) {
        return text.replace(CHARACTER_REFERENCE_PATTERN, function (reference) {
            referenceDecoder.innerHTML = reference;
            return referenceDecoder.textContent;
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
                var allowedIndex = ALLOWED_TAGS.indexOf(tag[2].toUpperCase());
                var allowed = allowedIndex !== -1;
                // The name comes from the allow-list, not from the input text.
                var name = allowed ? ALLOWED_TAGS[allowedIndex].toLowerCase() : '';
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

    var linkDialog = null;

    function createElement(tag, className, attributes, text) {
        var element = document.createElement(tag);
        if (className) {
            element.className = className;
        }
        Object.keys(attributes || {}).forEach(function (key) { element.setAttribute(key, attributes[key]); });
        if (text) {
            element.textContent = text;
        }
        return element;
    }

    function createLinkField(id, labelText, control) {
        var group = createElement('div', 'govuk-form-group');
        group.appendChild(createElement('label', 'govuk-label', { for: id }, labelText));
        group.appendChild(control);
        return group;
    }

    // The legacy TinyMCE "Insert/Edit Link" dialog: URL, Text to display, Title and Open link in..., then Save and Cancel. It is built once,
    // with DOM methods only, and shared by every editor on the page; data-confirm-close lets confirm-modal.js close it on Cancel.
    function getLinkDialog() {
        if (linkDialog) {
            return linkDialog;
        }

        var dialog = createElement('dialog', 'app-modal app-modal--sectioned app-link-dialog', { id: 'richtext-link-dialog', 'aria-labelledby': 'richtext-link-dialog-title' });
        var form = createElement('form', null, { method: 'dialog', novalidate: 'novalidate' });

        var header = createElement('div', 'app-modal__header');
        header.appendChild(createElement('h2', 'govuk-heading-m govuk-!-margin-bottom-0', { id: 'richtext-link-dialog-title' }, 'Insert/Edit Link'));

        var body = createElement('div', 'app-modal__body');
        var urlGroup = createLinkField('richtext-link-url', 'URL', createElement('input', 'govuk-input', { id: 'richtext-link-url', type: 'url', autocomplete: 'off', spellcheck: 'false' }));
        var urlError = createElement('p', 'govuk-error-message', { id: 'richtext-link-url-error', hidden: 'hidden' });
        urlGroup.insertBefore(urlError, urlGroup.lastChild);
        body.appendChild(urlGroup);
        body.appendChild(createLinkField('richtext-link-text', 'Text to display', createElement('input', 'govuk-input', { id: 'richtext-link-text', type: 'text', autocomplete: 'off' })));
        body.appendChild(createLinkField('richtext-link-title', 'Title', createElement('input', 'govuk-input', { id: 'richtext-link-title', type: 'text', autocomplete: 'off' })));

        var target = createElement('select', 'govuk-select', { id: 'richtext-link-target' });
        target.appendChild(createElement('option', null, { value: '' }, 'Current window'));
        target.appendChild(createElement('option', null, { value: '_blank' }, 'New window'));
        var targetGroup = createLinkField('richtext-link-target', 'Open link in...', target);
        targetGroup.classList.add('govuk-!-margin-bottom-0');
        body.appendChild(targetGroup);

        var footer = createElement('div', 'app-modal__footer');
        footer.appendChild(createElement('button', 'govuk-button govuk-!-margin-bottom-0', { type: 'submit', 'data-module': 'govuk-button', 'data-richtext-link-save': '' }, 'Save'));
        footer.appendChild(createElement('button', 'govuk-button govuk-button--secondary govuk-!-margin-bottom-0', { type: 'button', 'data-module': 'govuk-button', 'data-confirm-close': '' }, 'Cancel'));

        form.appendChild(header);
        form.appendChild(body);
        form.appendChild(footer);
        dialog.appendChild(form);
        document.body.appendChild(dialog);

        linkDialog = {
            dialog: dialog,
            form: form,
            url: dialog.querySelector('#richtext-link-url'),
            urlGroup: urlGroup,
            urlError: urlError,
            text: dialog.querySelector('#richtext-link-text'),
            title: dialog.querySelector('#richtext-link-title'),
            target: target,
            state: null
        };

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            saveLink(linkDialog);
        });
        dialog.addEventListener('close', function () {
            var state = linkDialog.state;
            linkDialog.state = null;
            if (state) {
                state.editor.focus();
            }
        });

        return linkDialog;
    }

    function showLinkError(parts, message) {
        parts.urlError.replaceChildren(createElement('span', 'govuk-visually-hidden', null, 'Error:'), document.createTextNode(' ' + message));
        parts.urlError.hidden = !message;
        parts.urlGroup.classList.toggle('govuk-form-group--error', !!message);
        parts.url.classList.toggle('govuk-input--error', !!message);
        if (message) {
            parts.url.setAttribute('aria-describedby', parts.urlError.id);
        } else {
            parts.url.removeAttribute('aria-describedby');
        }
    }

    function selectionRangeIn(editor) {
        var selection = window.getSelection();
        if (selection.rangeCount === 0) {
            return null;
        }
        var range = selection.getRangeAt(0).cloneRange();
        return editor.contains(range.commonAncestorContainer) ? range : null;
    }

    function linkAround(range, editor) {
        if (!range) {
            return null;
        }
        var start = range.startContainer;
        var element = start.nodeType === Node.ELEMENT_NODE ? start : start.parentNode;
        var anchor = element.closest('a');
        return anchor && editor.contains(anchor) ? anchor : null;
    }

    function insertLink(editor) {
        var range = selectionRangeIn(editor);
        var anchor = linkAround(range, editor);
        var selectedText = range ? range.toString() : '';

        var parts = getLinkDialog();
        parts.state = { editor: editor, range: range, anchor: anchor, originalText: anchor ? anchor.textContent : '' };
        parts.url.value = anchor ? anchor.getAttribute('href') || '' : '';
        parts.text.value = anchor ? anchor.textContent : selectedText;
        parts.title.value = anchor ? anchor.getAttribute('title') || '' : '';
        parts.target.value = anchor?.getAttribute('target') === '_blank' ? '_blank' : '';
        showLinkError(parts, '');

        if (typeof parts.dialog.showModal === 'function') {
            parts.dialog.showModal();
        } else {
            parts.dialog.setAttribute('open', '');
        }
        parts.url.focus();
    }

    function saveLink(parts) {
        var state = parts.state;
        var url = parts.url.value.trim();
        if (!url || !SAFE_HREF.test(url)) {
            showLinkError(parts, 'Enter a web address starting with http://, https:// or mailto:');
            parts.url.focus();
            return;
        }

        var editor = state.editor;
        var text = parts.text.value.trim();
        var anchor = state.anchor;

        if (!anchor) {
            var range = state.range;
            if (!range) {
                range = document.createRange();
                range.selectNodeContents(editor);
                range.collapse(false);
            }
            anchor = document.createElement('a');
            anchor.textContent = text || url;
            range.deleteContents();
            range.insertNode(anchor);
        } else if (text && text !== state.originalText) {
            anchor.textContent = text;
        }

        anchor.setAttribute('href', url);
        if (parts.title.value.trim()) {
            anchor.setAttribute('title', parts.title.value.trim());
        } else {
            anchor.removeAttribute('title');
        }
        if (parts.target.value === '_blank') {
            anchor.setAttribute('target', '_blank');
        } else {
            anchor.removeAttribute('target');
        }

        var after = document.createRange();
        after.setStartAfter(anchor);
        after.collapse(true);
        var selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(after);

        editor.dispatchEvent(new Event('input', { bubbles: true }));
        if (typeof parts.dialog.close === 'function') {
            parts.dialog.close();
        } else {
            parts.dialog.removeAttribute('open');
            parts.dialog.dispatchEvent(new Event('close'));
        }
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

    function initialiseRichTextEditors(root) {
        var editors = {};

        try {
            document.execCommand('defaultParagraphSeparator', false, 'p');
        } catch (e) {
            // Unsupported in some browsers; the default separator is acceptable.
        }

        (root || document).querySelectorAll('[data-richtext-command]').forEach(function (button) {
            var targetId = button.getAttribute('data-richtext-target');
            var textarea = document.getElementById(targetId);
            if (!textarea || button.dataset.richtextReady) {
                return;
            }
            button.dataset.richtextReady = 'true';

            if (!editors[targetId]) {
                if (textarea.dataset.richtextReady) {
                    return;
                }
                textarea.dataset.richtextReady = 'true';
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

    // The panel that edits a species' answers (question-species.js) has one editor for all of a question's species and fills it as each
    // is opened, and shows a read-only answer as it is formatted. Both use the same allow-list as the editor, so the answer is never
    // interpreted as HTML.
    window.appRichText = {
        fill: function (target, html) {
            target.replaceChildren();
            fillEditor(target, html || '');
        }
    };

    // Content swapped in by paged-region.js (a section opened in place) has editors of its own to set up.
    document.addEventListener('app:region-updated', function (event) { initialiseRichTextEditors(event.target); });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { initialiseRichTextEditors(); });
    } else {
        initialiseRichTextEditors();
    }
})();
