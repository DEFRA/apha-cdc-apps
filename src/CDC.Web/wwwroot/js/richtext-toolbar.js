// Rich text toolbar for "Long Text" profile fields, replicating the legacy TinyMCE toolbar
// (undo redo | italic bullist | link) by directly editing the HTML stored in a plain
// <textarea>, so the control remains a native, fully accessible form field.
(function () {
    'use strict';

    function wrapSelection(textarea, prefix, suffix) {
        var start = textarea.selectionStart;
        var end = textarea.selectionEnd;
        var value = textarea.value;
        var selected = value.slice(start, end);

        textarea.value = value.slice(0, start) + prefix + selected + suffix + value.slice(end);
        textarea.selectionStart = start + prefix.length;
        textarea.selectionEnd = start + prefix.length + selected.length;
    }

    function wrapAsList(textarea) {
        var start = textarea.selectionStart;
        var end = textarea.selectionEnd;
        var value = textarea.value;
        var selected = value.slice(start, end) || 'List item';

        var items = selected
            .split('\n')
            .filter(function (line) { return line.trim().length > 0; })
            .map(function (line) { return '<li>' + line.trim() + '</li>'; })
            .join('');

        var replacement = '<ul>' + items + '</ul>';
        textarea.value = value.slice(0, start) + replacement + value.slice(end);
        textarea.selectionStart = start;
        textarea.selectionEnd = start + replacement.length;
    }

    function insertLink(textarea) {
        var url = window.prompt('Enter the link URL:', 'https://');
        if (!url) {
            return;
        }

        wrapSelection(textarea, '<a href="' + url + '" target="_blank">', '</a>');
    }

    function runCommand(command, textarea) {
        textarea.focus();

        switch (command) {
            case 'undo':
            case 'redo':
                document.execCommand(command);
                break;
            case 'italic':
                wrapSelection(textarea, '<em>', '</em>');
                break;
            case 'bullist':
                wrapAsList(textarea);
                break;
            case 'link':
                insertLink(textarea);
                break;
            default:
                break;
        }
    }

    function initialiseRichTextToolbars() {
        document.querySelectorAll('[data-richtext-command]').forEach(function (button) {
            button.addEventListener('click', function () {
                var targetId = button.getAttribute('data-richtext-target');
                var textarea = document.getElementById(targetId);
                if (textarea) {
                    runCommand(button.getAttribute('data-richtext-command'), textarea);
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseRichTextToolbars);
    } else {
        initialiseRichTextToolbars();
    }
})();
