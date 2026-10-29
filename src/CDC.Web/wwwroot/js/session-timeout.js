(function () {
    "use strict";

    // The "Extend session?" warning and "Session expired" dialogs, as in the legacy application, backed by the native <dialog>
    // elements in _SessionTimeout.cshtml (showModal() gives focus trapping and Escape handling for free).
    //
    // The server decides when a session ends: any request to the app renews it, so this page cannot tell from its own clock whether
    // it is still signed in. The script therefore only ever asks. When it expects the warning to be due it checks the session's real
    // state (SessionController.Status, which never extends it) and acts on the answer, so a session kept alive from another tab, or
    // by a background request, gets no false warning, and the expired dialog only ever appears for a session that has really ended.

    var MAX_TIMER_MS = 2147483647;

    var root = document.querySelector("[data-session-timeout]");
    var warningDialog = document.getElementById("session-warning-dialog");
    var expiredDialog = document.getElementById("session-expired-dialog");

    if (!root || !warningDialog || !expiredDialog || typeof fetch !== "function" || typeof warningDialog.showModal !== "function") {
        return;
    }

    var idleMs = Number(root.dataset.idleSeconds) * 1000;
    var warningMs = Number(root.dataset.warningSeconds) * 1000;

    if (!isPositive(idleMs) || !isPositive(warningMs)) {
        return;
    }

    var statusUrl = root.dataset.statusUrl;
    var extendUrl = root.dataset.extendUrl;
    var extendButton = warningDialog.querySelector("[data-session-extend]");
    var countdown = warningDialog.querySelector("[data-session-countdown]");
    var extendError = warningDialog.querySelector("[data-session-extend-error]");

    // When this page believes the session ends. The page was served by a request, which renewed the session, so it starts a full
    // timeout from now; every answer from the server replaces it.
    var deadline = Date.now() + idleMs;
    var antiforgery = null;
    var checkTimer = null;
    var countdownTimer = null;
    var checking = false;
    var extending = false;
    var ended = false;

    function isPositive(value) {
        return Number.isFinite(value) && value > 0;
    }

    function schedule(at) {
        window.clearTimeout(checkTimer);
        checkTimer = window.setTimeout(check, Math.min(Math.max(at - Date.now(), 0), MAX_TIMER_MS));
    }

    // An answer that does not say whether the session has ended, and if it has not how long is left, is as good as none: acting on
    // it would re-check in a tight loop.
    function isUsable(status) {
        return Boolean(status) && (status.authenticated === false || (status.authenticated === true && typeof status.remainingSeconds === "number"));
    }

    function readStatus() {
        return fetch(statusUrl, {
            credentials: "same-origin",
            cache: "no-store",
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (status) {
                return isUsable(status) ? status : null;
            })
            .catch(function () {
                // The server could not be asked, so the estimate stands.
                return null;
            });
    }

    async function check() {
        if (checking || ended) {
            return;
        }

        checking = true;

        try {
            var status = await readStatus();

            if (status && !status.authenticated) {
                showExpired();

                return;
            }

            if (status) {
                deadline = Date.now() + status.remainingSeconds * 1000;

                if (status.antiforgeryToken && status.antiforgeryHeaderName) {
                    antiforgery = { headerName: status.antiforgeryHeaderName, token: status.antiforgeryToken };
                }
            }

            var remaining = deadline - Date.now();

            if (remaining <= 0) {
                showExpired();
            } else if (remaining <= warningMs) {
                showWarning();
                schedule(deadline);
            } else {
                hideWarning();
                schedule(deadline - warningMs);
            }
        } finally {
            checking = false;
        }
    }

    function describe(seconds) {
        return seconds > 60 ? Math.ceil(seconds / 60) + " minutes" : "less than a minute";
    }

    function updateCountdown() {
        if (!countdown) {
            return;
        }

        var text = describe(Math.max(Math.ceil((deadline - Date.now()) / 1000), 0));

        if (countdown.textContent !== text) {
            countdown.textContent = text;
        }
    }

    function stopCountdown() {
        if (countdownTimer !== null) {
            window.clearInterval(countdownTimer);
            countdownTimer = null;
        }
    }

    function showWarning() {
        updateCountdown();

        if (!warningDialog.open) {
            warningDialog.showModal();
        }

        if (countdownTimer === null) {
            countdownTimer = window.setInterval(updateCountdown, 1000);
        }
    }

    function hideWarning() {
        stopCountdown();
        setExtendError(false);

        if (warningDialog.open) {
            warningDialog.close();
        }
    }

    function showExpired() {
        ended = true;
        window.clearTimeout(checkTimer);
        hideWarning();

        if (!expiredDialog.open) {
            expiredDialog.showModal();
        }
    }

    function setExtendError(visible) {
        if (extendError) {
            extendError.hidden = !visible;
        }
    }

    async function extendSession() {
        // The button is marked unavailable rather than disabled, so that it keeps the keyboard focus the dialog gave it.
        if (extending) {
            return;
        }

        extending = true;
        extendButton.setAttribute("aria-disabled", "true");
        setExtendError(false);

        var extended = false;

        try {
            var headers = { "Accept": "application/json" };

            if (antiforgery) {
                headers[antiforgery.headerName] = antiforgery.token;
            }

            var response = await fetch(extendUrl, { method: "POST", credentials: "same-origin", headers: headers });

            if (response.ok) {
                var extendedStatus = await response.json();

                if (typeof extendedStatus.remainingSeconds === "number") {
                    deadline = Date.now() + extendedStatus.remainingSeconds * 1000;
                    extended = true;
                }
            }
        } catch (error) {
            // The request did not get through, so the session is as it was: the check below finds out how that stands.
        }

        if (extended) {
            hideWarning();
            schedule(deadline - warningMs);
        } else {
            // A session that has ended has to be signed in to again; one that has not can simply be extended again.
            await check();
            setExtendError(!ended && warningDialog.open);
        }

        extendButton.removeAttribute("aria-disabled");
        extending = false;
    }

    extendButton.addEventListener("click", extendSession);

    // Escape closes the warning without extending the session, as it did in the legacy dialog; the expired dialog has to be answered.
    warningDialog.addEventListener("close", stopCountdown);
    expiredDialog.addEventListener("cancel", function (event) {
        event.preventDefault();
    });

    // Timers do not run on time in a background tab or while a device sleeps, so ask again as soon as the page is back in view.
    document.addEventListener("visibilitychange", function () {
        if (!document.hidden) {
            void check();
        }
    });

    window.addEventListener("pageshow", function (event) {
        if (event.persisted) {
            void check();
        }
    });

    schedule(deadline - warningMs);
})();
