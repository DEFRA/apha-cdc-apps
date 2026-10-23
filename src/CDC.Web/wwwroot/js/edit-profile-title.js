// Formatting toolbar (undo/redo/italic) above the profile title editor on EditProfileTitle and ProfileProperties.
// The title is edited in a contenteditable div (so HTML formatting renders as the user types)
// and mirrored into a hidden input on every change, which is what actually gets posted.
// The editor behaves as the legacy TinyMCE one did: italic only, one line, plain ASCII pasted text, and no more
// than the title column holds. Where the page marks them, Save is hidden and a required message shown while the title has no text.
// A toolbar marked data-app-title-toolbar-on-focus shows only while the title or the toolbar has focus, like legacy's inline editor.
(function () {
    'use strict';

    var HISTORY_DEBOUNCE_MS = 300;
    // What the title column holds, counted on the markup the editor stores (italic tags included).
    var MAX_STORED_LENGTH = 255;

    document.addEventListener('DOMContentLoaded', function () {
        var toolbar = document.querySelector('[data-app-title-toolbar]');
        if (!toolbar) {
            return;
        }

        var editor = document.getElementById(toolbar.dataset.appTitleToolbar);
        var hiddenInput = editor ? document.getElementById(editor.dataset.appTitleHiddenInput) : null;
        var undoButton = toolbar.querySelector('[data-app-title-action="undo"]');
        var redoButton = toolbar.querySelector('[data-app-title-action="redo"]');
        var italicButton = toolbar.querySelector('[data-app-title-action="italic"]');
        var saveButton = document.querySelector('[data-app-title-save]');
        var requiredError = document.querySelector('[data-app-title-required-error]');

        if (!editor || !hiddenInput || !undoButton || !redoButton || !italicButton) {
            return;
        }

        var formGroup = editor.closest('.govuk-form-group');
        // GOV.UK ids a field's error message "<field id>-error", so this is the one the server rendered for the title, if any.
        var serverError = document.getElementById(hiddenInput.id + '-error');

        // The title is a single line, so Enter must not start a new one. Formatted text has no native element, so the editor is given its role here.
        editor.setAttribute('role', 'textbox');
        editor.setAttribute('aria-multiline', 'false');

        // Keys that do nothing, as in legacy: Enter, the bold and underline shortcuts (only italics are allowed),
        // and any character typed into a full title, which can still be deleted from or replaced.
        function isBlocked(event) {
            if (event.key === 'Enter') {
                return true;
            }

            if (event.ctrlKey || event.metaKey) {
                var shortcut = event.key.toLowerCase();

                return shortcut === 'b' || shortcut === 'u';
            }

            return event.key.length === 1 && storedLength() >= MAX_STORED_LENGTH && window.getSelection().isCollapsed;
        }

        editor.addEventListener('keydown', function (event) {
            if (isBlocked(event)) {
                event.preventDefault();
            }
        });

        // Pasting inserts plain text without characters beyond ASCII or line breaks, as in legacy.
        editor.addEventListener('paste', function (event) {
            event.preventDefault();

            if (typeof document.execCommand === 'function') {
                var text = (event.clipboardData || window.clipboardData).getData('text/plain');
                document.execCommand('insertText', false, text.replace(/[^\x00-\x7F\s]+/g, '').replace(/[\r\n]+/g, ' '));
            }
        });

        // contenteditable has no native, reliable undo/redo API, so history is tracked manually.
        var history = [editor.innerHTML];
        var historyIndex = 0;
        var isApplyingHistory = false;
        var debounceTimer;

        function storedLength() {
            return editor.innerHTML.trim().length;
        }

        function syncHiddenInput() {
            hiddenInput.value = editor.innerHTML;
        }

        // The longest start of the text that still fits the column once stored, which encodes it as HTML.
        function fitToColumn(text) {
            var fitted = text.slice(0, MAX_STORED_LENGTH);
            var probe = document.createElement('div');
            probe.textContent = fitted;

            while (fitted.length > 0 && probe.innerHTML.length > MAX_STORED_LENGTH) {
                fitted = fitted.slice(0, -1);
                probe.textContent = fitted;
            }

            return fitted;
        }

        // A title that still ends up too long (a long paste, or italic tags adding to it) is cut back to plain text that fits, as legacy did.
        function enforceLimit() {
            if (storedLength() <= MAX_STORED_LENGTH) {
                return;
            }

            editor.textContent = fitToColumn(editor.textContent);

            var range = document.createRange();
            range.selectNodeContents(editor);
            range.collapse(false);

            var selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
        }

        function isTitleEmpty() {
            return editor.textContent.trim() === '';
        }

        function updateSaveButton() {
            if (saveButton) {
                saveButton.style.visibility = isTitleEmpty() ? 'hidden' : 'visible';
            }
        }

        // Legacy's required-field validator: the message replaces any the server gave for the title, so only one shows.
        function updateRequiredError() {
            if (!requiredError) {
                return;
            }

            var isEmpty = isTitleEmpty();
            var shownMessage = isEmpty ? requiredError : serverError;

            requiredError.hidden = !isEmpty;
            if (serverError) {
                serverError.hidden = isEmpty;
            }

            formGroup.classList.toggle('govuk-form-group--error', Boolean(shownMessage));
            editor.classList.toggle('govuk-input--error', Boolean(shownMessage));

            if (shownMessage) {
                editor.setAttribute('aria-describedby', shownMessage.id);
            } else {
                editor.removeAttribute('aria-describedby');
            }
        }

        // Everything that follows a change to the title.
        function onChange() {
            enforceLimit();
            syncHiddenInput();
            updateSaveButton();
            updateRequiredError();
        }

        function updateButtonStates() {
            undoButton.disabled = historyIndex <= 0;
            redoButton.disabled = historyIndex >= history.length - 1;
        }

        function pushHistory() {
            if (isApplyingHistory || editor.innerHTML === history[historyIndex]) {
                return;
            }

            history = history.slice(0, historyIndex + 1);
            history.push(editor.innerHTML);
            historyIndex = history.length - 1;
            updateButtonStates();
        }

        function applyHistory(index) {
            isApplyingHistory = true;
            historyIndex = index;
            editor.innerHTML = history[historyIndex];
            onChange();
            editor.focus();
            isApplyingHistory = false;
            updateButtonStates();
        }

        editor.addEventListener('input', function () {
            onChange();
            window.clearTimeout(debounceTimer);
            debounceTimer = window.setTimeout(pushHistory, HISTORY_DEBOUNCE_MS);
        });

        editor.addEventListener('blur', onChange);

        undoButton.addEventListener('click', function () {
            window.clearTimeout(debounceTimer);
            pushHistory();

            if (historyIndex > 0) {
                applyHistory(historyIndex - 1);
            }
        });

        redoButton.addEventListener('click', function () {
            if (historyIndex < history.length - 1) {
                applyHistory(historyIndex + 1);
            }
        });

        italicButton.addEventListener('click', function () {
            editor.focus();

            var selection = window.getSelection();
            if (selection && selection.rangeCount > 0 && !selection.isCollapsed) {
                var range = selection.getRangeAt(0);
                if (editor.contains(range.commonAncestorContainer)) {
                    var italic = document.createElement('i');
                    italic.appendChild(range.extractContents());
                    range.insertNode(italic);

                    selection.removeAllRanges();
                    var newRange = document.createRange();
                    newRange.selectNodeContents(italic);
                    selection.addRange(newRange);

                    onChange();
                    pushHistory();
                    return;
                }
            }

            if (typeof document.execCommand === 'function') {
                document.execCommand('italic');
                onChange();
                pushHistory();
            }
        });

        syncHiddenInput();
        updateButtonStates();

        // Only where the page asks: a page that opens with no title (creating a profile) must not offer to save it.
        if (saveButton && saveButton.dataset.appTitleSave === 'on-load') {
            updateSaveButton();
        }

        if (toolbar.hasAttribute('data-app-title-toolbar-on-focus')) {
            showToolbarOnFocus();
        }

        function showToolbarOnFocus() {
            toolbar.hidden = true;

            // Clicking a button would otherwise take focus (and the selection to italicise) from the title.
            toolbar.addEventListener('mousedown', function (event) {
                event.preventDefault();
            });

            [editor, toolbar].forEach(function (element) {
                element.addEventListener('focusin', function () {
                    toolbar.hidden = false;
                });

                element.addEventListener('focusout', function (event) {
                    var next = event.relatedTarget;

                    if (!next || (!editor.contains(next) && !toolbar.contains(next))) {
                        toolbar.hidden = true;
                    }
                });
            });
        }
    });
})();

