// "Spell check" button for profile text fields, replacing the legacy Telerik RadSpell control
// (a paid third-party component backed by a custom WCF dictionary service, both retired as part
// of this migration - see CDC.Web copilot-instructions.md "Out of scope: New Integrations").
// Rather than re-implement a dictionary/suggestions engine, this triggers the browser's own
// native spellchecker: browsers only run it on text as it is typed, so a field pre-filled with
// saved text shows no wavy underlines until edited. Toggling the spellcheck attribute off and
// back on forces the browser to re-evaluate the field's current text immediately.
(function () {
    'use strict';

    function runSpellCheck(field) {
        field.setAttribute('spellcheck', 'false');
        // Forces a reflow so the browser re-reads the attribute instead of ignoring the toggle.
        void field.offsetHeight;
        field.setAttribute('spellcheck', 'true');
        field.focus();
    }

    function initialiseSpellCheckButtons() {
        document.querySelectorAll('[data-spellcheck-target]').forEach(function (button) {
            button.addEventListener('click', function () {
                var field = document.getElementById(button.getAttribute('data-spellcheck-target'));
                if (field) {
                    runSpellCheck(field);
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialiseSpellCheckButtons);
    } else {
        initialiseSpellCheckButtons();
    }
})();
