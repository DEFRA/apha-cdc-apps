(function () {
    "use strict";

    // The help icon of a question on the Questions tab opens that question's user guidance in a dialog, as the legacy page opened
    // ProfileQuestionHelp.aspx in a pop-up window. The guidance is a fragment the server has already made safe to show, loaded when
    // the icon is used rather than sent with every question.
    var dialog = document.getElementById("user-guidance-dialog");
    if (!dialog || typeof dialog.showModal !== "function") {
        return;
    }

    var content = dialog.querySelector("[data-user-guidance-content]");
    var failure = dialog.querySelector("[data-user-guidance-error]");
    var requests = 0;

    document.addEventListener("click", function (event) {
        var trigger = event.target instanceof Element ? event.target.closest("[data-question-help]") : null;
        if (trigger) {
            load(trigger.dataset.questionHelp);
        }
    });

    function load(questionId) {
        // A slow answer to an earlier click must not replace the guidance of a later one.
        var request = ++requests;
        var url = new URL(dialog.dataset.helpUrl, window.location.href);
        url.searchParams.set("profileQuestionId", questionId);

        fetch(url, { credentials: "same-origin" })
            .then(function (response) {
                // A redirect means the session has ended and the browser was sent to sign in, which is not guidance to show.
                if (!response.ok || response.redirected) {
                    throw new Error("User guidance unavailable");
                }
                return response.text();
            })
            .then(function (html) {
                show(request, html);
            })
            .catch(function () {
                show(request, null);
            });
    }

    function show(request, html) {
        if (request !== requests) {
            return;
        }

        if (html === null) {
            content.replaceChildren();
            failure.hidden = false;
        } else {
            var template = document.createElement("template");
            template.innerHTML = html;
            openLinksInNewTab(template.content);
            content.replaceChildren(template.content);
            failure.hidden = true;
        }

        if (!dialog.open) {
            dialog.showModal();
        }
    }

    // A link followed in this window would leave the page, and with it any answers that have not been saved.
    function openLinksInNewTab(fragment) {
        fragment.querySelectorAll("a[href]").forEach(function (link) {
            var hint = document.createElement("span");
            hint.className = "govuk-visually-hidden";
            hint.textContent = " (opens in new tab)";

            link.target = "_blank";
            link.rel = "noopener noreferrer";
            link.appendChild(hint);
        });
    }
})();
