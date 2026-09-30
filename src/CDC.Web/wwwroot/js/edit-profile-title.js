// Formatting toolbar (undo/redo/italic) above the profile title editor on EditProfileTitle.
// The title is edited in a contenteditable div (so HTML formatting renders as the user types)
// and mirrored into a hidden input on every change, which is what actually gets posted.
(function () {
    'use strict';

    var HISTORY_DEBOUNCE_MS = 300;

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

        if (!editor || !hiddenInput || !undoButton || !redoButton || !italicButton) {
            return;
        }

        // contenteditable has no native, reliable undo/redo API, so history is tracked manually.
        var history = [editor.innerHTML];
        var historyIndex = 0;
        var isApplyingHistory = false;
        var debounceTimer;

        function syncHiddenInput() {
            hiddenInput.value = editor.innerHTML;
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
            syncHiddenInput();
            editor.focus();
            isApplyingHistory = false;
            updateButtonStates();
        }

        editor.addEventListener('input', function () {
            syncHiddenInput();
            window.clearTimeout(debounceTimer);
            debounceTimer = window.setTimeout(pushHistory, HISTORY_DEBOUNCE_MS);
        });

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

                    syncHiddenInput();
                    pushHistory();
                    return;
                }
            }

            if (typeof document.execCommand === 'function') {
                document.execCommand('italic');
                syncHiddenInput();
                pushHistory();
            }
        });

        syncHiddenInput();
        updateButtonStates();
    });
})();

