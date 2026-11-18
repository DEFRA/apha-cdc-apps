(function () {
    "use strict";

    // The legacy question panels opened collapsed whenever the page was opened, and kept their state only across the page's own
    // postbacks. The govuk-frontend accordion remembers which sections were open in sessionStorage, under "{accordion id}-content-{n}",
    // and restores them when it starts, so a question left open would come back open on the next visit.
    // The server marks the accordion with data-app-collapse-on-open="true" when the page is being opened, and "false" when its own
    // postbacks (Save, Validate, Clear) answer, so only a page being opened forgets what was open. This runs before the accordion starts.
    var accordion = document.querySelector(".govuk-accordion[data-app-collapse-on-open='true']");
    if (!accordion) {
        return;
    }

    var prefix = accordion.id + "-content-";

    try {
        var remembered = [];
        for (var index = 0; index < sessionStorage.length; index++) {
            var key = sessionStorage.key(index);
            if (key?.startsWith(prefix)) {
                remembered.push(key);
            }
        }

        remembered.forEach(function (rememberedKey) {
            sessionStorage.removeItem(rememberedKey);
        });
    } catch {
        // The browser refuses access to the storage, so nothing was remembered to forget.
    }
})();
