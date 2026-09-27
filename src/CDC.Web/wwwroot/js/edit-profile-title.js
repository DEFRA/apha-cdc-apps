// Formatting toolbar (undo/redo/italic) above the profile title textbox on EditProfileTitle.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var toolbar = document.querySelector('[data-app-title-toolbar]');
        if (!toolbar) {
            return;
        }

        var input = document.getElementById(toolbar.getAttribute('data-app-title-toolbar'));
        var undoButton = toolbar.querySelector('[data-app-title-action="undo"]');
        var redoButton = toolbar.querySelector('[data-app-title-action="redo"]');
        var italicButton = toolbar.querySelector('[data-app-title-action="italic"]');

        if (!input || !undoButton || !redoButton || !italicButton) {
            return;
        }

        // Text inputs have no native, reliable undo/redo API, so history is tracked manually.
        var history = [input.value];
        var historyIndex = 0;
        var isApplyingHistory = false;
        var debounceTimer;

        function updateButtonStates() {
            undoButton.disabled = historyIndex <= 0;
            redoButton.disabled = historyIndex >= history.length - 1;
        }

        function pushHistory() {
            if (isApplyingHistory || input.value === history[historyIndex]) {
                return;
            }

            history = history.slice(0, historyIndex + 1);
            history.push(input.value);
            historyIndex = history.length - 1;
            updateButtonStates();
        }

        function applyHistory(index) {
            isApplyingHistory = true;
            historyIndex = index;
            input.value = history[historyIndex];
            input.focus();
            isApplyingHistory = false;
            updateButtonStates();
        }

        input.addEventListener('input', function () {
            window.clearTimeout(debounceTimer);
            debounceTimer = window.setTimeout(pushHistory, 300);
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
            var start = input.selectionStart || 0;
            var end = input.selectionEnd || 0;
            var selected = input.value.slice(start, end) || 'italic text';

            input.focus();
            input.setRangeText('<i>' + selected + '</i>', start, end, 'end');
            pushHistory();
        });

        updateButtonStates();
    });
})();
