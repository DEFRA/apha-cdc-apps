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
})();
