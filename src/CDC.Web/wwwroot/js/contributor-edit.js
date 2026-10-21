// Edit profile contributor panel: unobtrusive enhancements only - the panel itself is fully
// server-rendered, and the form works (minus the live role/permissions toggle) without this.
(function () {
    'use strict';

    // Toggles the section permissions fieldset to match the selected role, matching the legacy
    // ddlRole_SelectedIndexChanged postback without needing a server round-trip.
    document.querySelectorAll('[data-module="app-contributor-role"]').forEach(function ($role) {
        const $fieldset = document.getElementById($role.dataset.permissionsTarget);

        if (!$fieldset) {
            return;
        }

        $role.addEventListener('change', function () {
            const option = $role.options[$role.selectedIndex];
            const isContributor = option && option.dataset.isContributor === 'true';

            $fieldset.hidden = !isContributor;
        });
    });

    document.querySelectorAll('[data-contributor-permissions-action]').forEach(function ($button) {
        $button.addEventListener('click', function () {
            const $fieldset = $button.closest('fieldset');

            if (!$fieldset) {
                return;
            }

            const checked = $button.dataset.contributorPermissionsAction === 'check-all';

            $fieldset.querySelectorAll('.govuk-checkboxes__input').forEach(function ($checkbox) {
                $checkbox.checked = checked;
            });
        });
    });

    // Greys out a form's OK button until a field differs from its server-rendered value.
    document.querySelectorAll('[data-module="app-track-changes"]').forEach(function ($form) {
        const $submit = $form.querySelector('[data-requires-change]');

        if (!$submit) {
            return;
        }

        const isChanged = function ($field) {
            if ($field.tagName === 'SELECT') {
                return Array.from($field.options).some(function ($option) {
                    return $option.selected !== $option.defaultSelected;
                });
            }

            if ($field.type === 'checkbox' || $field.type === 'radio') {
                return $field.checked !== $field.defaultChecked;
            }

            if ($field.type === 'hidden' || $field.type === 'submit' || $field.type === 'button') {
                return false;
            }

            return $field.value !== $field.defaultValue;
        };

        const update = function () {
            const unchanged = $form.dataset.hasPending !== 'true' && !Array.from($form.elements).some(isChanged);

            $submit.disabled = unchanged;
            $submit.classList.toggle('govuk-button--disabled', unchanged);
        };

        $form.addEventListener('input', update);
        $form.addEventListener('change', update);
        $form.addEventListener('click', function (event) {
            if (event.target.closest('[data-contributor-permissions-action]')) {
                update();
            }
        });
        window.addEventListener('pageshow', update);

        update();
    });
})();