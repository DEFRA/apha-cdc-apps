(function () {
    "use strict";

    // The edit references icon of a question on the Questions tab opens the "Add/Select a reference to question" dialog, as the legacy
    // page opened its pop-up. The user chooses to add a reference, or to select one of the version's, of a type, and ticks the other
    // questions it applies to. The references of a type are read when the type is chosen, and the choice is posted from here and
    // answered in JSON, so the page, and any answers on it that are not saved yet, stay as they are.
    var dialog = document.getElementById("question-reference-dialog");
    if (!dialog || typeof dialog.showModal !== "function") {
        return;
    }

    var form = dialog.querySelector("[data-reference-form]");
    var questionNumber = dialog.querySelector("[data-reference-question-number]");
    var banner = document.getElementById("reference-saved-banner");

    // Without a form (the types of reference could not be loaded) the dialog only says so.
    var modeSelect = null;
    var typeSelect = null;
    var noteSelect = null;
    var textArea = null;
    var questionField = null;
    var lastUpdatedField = null;
    var currentNote = null;
    var summary = null;
    var errorList = null;
    var saveButton = null;
    var typeGroup = null;
    var addGroup = null;
    var selectGroup = null;
    var choices = [];
    var notes = new Map();
    var notesRequest = 0;
    var saving = false;

    if (form) {
        modeSelect = form.querySelector("[data-reference-mode]");
        typeSelect = form.querySelector("[data-reference-type]");
        noteSelect = form.querySelector("[data-reference-note]");
        textArea = form.querySelector("[data-reference-text]");
        questionField = form.querySelector("[data-reference-question]");
        lastUpdatedField = form.querySelector("[data-reference-last-updated]");
        currentNote = form.querySelector("[data-reference-current-note]");
        summary = form.querySelector("[data-reference-errors]");
        errorList = form.querySelector("[data-reference-error-list]");
        saveButton = form.querySelector("[data-reference-save]");
        typeGroup = form.querySelector("[data-reference-type-group]");
        addGroup = form.querySelector("[data-reference-add-group]");
        selectGroup = form.querySelector("[data-reference-select-group]");
        choices = Array.from(form.querySelectorAll("[data-reference-question-choice]"));

        modeSelect.addEventListener("change", onModeOrTypeChange);
        typeSelect.addEventListener("change", onModeOrTypeChange);
        noteSelect.addEventListener("change", onNoteChange);
        form.addEventListener("submit", onSubmit);
        summary.addEventListener("click", onErrorLinkClick);
    }

    document.addEventListener("click", function (event) {
        var trigger = event.target instanceof Element ? event.target.closest("[data-question-references]") : null;
        if (trigger) {
            open(trigger.dataset.questionReferences, trigger.dataset.questionNumber);
        }
    });

    function open(questionId, number) {
        questionNumber.textContent = number || "";

        if (banner) {
            banner.hidden = true;
        }

        if (form) {
            reset(questionId);
        }

        if (!dialog.open) {
            dialog.showModal();
        }
    }

    // The dialog starts afresh each time it opens, as the legacy pop-up did: nothing chosen, and no one else's question to apply it to.
    function reset(questionId) {
        form.reset();

        var editor = document.getElementById(textArea.id + "-editor");
        if (editor) {
            editor.replaceChildren();
        }

        forgetNotes();
        questionField.value = questionId;

        choices.forEach(function (choice) {
            var isThisQuestion = choice.dataset.referenceQuestionChoice === questionId;
            var box = choice.querySelector("input");

            // The question the reference is for always gets it, so it is not offered with the others.
            choice.hidden = isThisQuestion;
            box.disabled = isThisQuestion;
            box.checked = false;
        });

        clearErrors();
        saving = false;
        saveButton.disabled = false;
        showGroups();
    }

    function showGroups() {
        var mode = modeSelect.value;

        typeGroup.hidden = mode === "";
        addGroup.hidden = !(mode === "add" && typeSelect.value !== "");
        selectGroup.hidden = mode !== "select";
    }

    // Choosing another action or type starts the choice again, as the legacy dialog did when the type changed.
    function onModeOrTypeChange() {
        clearErrors();
        clearTicks();
        forgetNotes();
        showGroups();

        if (modeSelect.value === "select" && typeSelect.value !== "") {
            loadNotes();
        }
    }

    function onNoteChange() {
        clearErrors();
        clearTicks();

        var note = notes.get(noteSelect.value);
        lastUpdatedField.value = note ? note.lastUpdated : "";
        currentNote.replaceChildren();
        currentNote.hidden = !note;

        if (!note) {
            return;
        }

        var template = document.createElement("template");
        template.innerHTML = note.html;
        currentNote.replaceChildren(template.content);

        // Selecting a reference ticks the questions it already applies to.
        var applied = new Set(note.questionIds);
        choices.forEach(function (choice) {
            var box = choice.querySelector("input");
            box.checked = !box.disabled && applied.has(box.value);
        });
    }

    function clearTicks() {
        choices.forEach(function (choice) {
            choice.querySelector("input").checked = false;
        });
    }

    function forgetNotes() {
        // An answer to an earlier request must not fill the list of a later choice.
        notesRequest += 1;
        notes = new Map();
        lastUpdatedField.value = "";
        currentNote.replaceChildren();
        currentNote.hidden = true;

        while (noteSelect.options.length > 1) {
            noteSelect.remove(1);
        }
    }

    function loadNotes() {
        var request = notesRequest;
        var url = new URL(dialog.dataset.notesUrl, window.location.href);
        url.searchParams.set("noteTypeId", typeSelect.value);

        fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } })
            .then(function (response) {
                // A redirect means the session has ended and the browser was sent to sign in.
                if (!response.ok || response.redirected) {
                    throw new Error("References unavailable");
                }
                return response.json();
            })
            .then(function (options) {
                if (request === notesRequest) {
                    options.forEach(function (option) {
                        var item = document.createElement("option");
                        item.value = option.id;
                        item.textContent = option.text;
                        noteSelect.appendChild(item);
                        notes.set(option.id, option);
                    });
                }
            })
            .catch(function () {
                if (request === notesRequest) {
                    showErrors([{ message: dialog.dataset.errorLoad }]);
                }
            });
    }

    // The rules the legacy dialog's validators applied, which only asked about a field that its action made visible.
    function validate(data) {
        var mode = data.get("Input.Mode");
        var errors = [];

        if (mode !== "add" && mode !== "select") {
            return [{ field: modeSelect.id, message: dialog.dataset.errorMode }];
        }

        var hasType = Boolean(data.get("Input.NoteTypeId"));
        if (!hasType) {
            errors.push({ field: typeSelect.id, message: dialog.dataset.errorType });
        }

        if (mode === "add") {
            if (hasType && plainText(data.get("Input.NoteText")) === "") {
                errors.push({ field: addFieldId(), message: dialog.dataset.errorText });
            }
        } else if (!data.get("Input.NoteId")) {
            errors.push({ field: noteSelect.id, message: dialog.dataset.errorNote });
        }

        return errors;
    }

    // The text without its markup, read without running any of it.
    function plainText(html) {
        var template = document.createElement("template");
        template.innerHTML = html || "";

        return template.content.textContent.trim();
    }

    function addFieldId() {
        var editor = document.getElementById(textArea.id + "-editor");

        return editor ? editor.id : textArea.id;
    }

    function onSubmit(event) {
        event.preventDefault();

        if (saving) {
            return;
        }

        // Reading the form's data also brings the rich text editor's content into its text area.
        var data = new FormData(form);
        var errors = validate(data);

        if (errors.length > 0) {
            showErrors(errors);
            return;
        }

        clearErrors();
        save(data);
    }

    function save(data) {
        saving = true;
        saveButton.disabled = true;

        fetch(form.action, {
            method: "POST",
            body: data,
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        })
            .then(function (response) {
                if (!response.ok || response.redirected) {
                    throw new Error("Reference not saved");
                }
                return response.json();
            })
            .then(function (result) {
                if (result.succeeded) {
                    done(result.message);
                } else {
                    showErrors(result.errors.map(function (message) { return { message: message }; }));
                }
            })
            .catch(function () {
                showErrors([{ message: dialog.dataset.errorSave }]);
            })
            .finally(function () {
                saving = false;
                saveButton.disabled = false;
            });
    }

    function done(message) {
        dialog.close();

        if (banner) {
            banner.querySelector("[data-reference-saved-message]").textContent = message;
            banner.hidden = false;
            banner.focus();
        }
    }

    // The summary at the top lists what is wrong, and each field it is about says so too, as the Design System asks.
    function showErrors(errors) {
        clearErrors();

        errors.forEach(function (error) {
            var item = document.createElement("li");

            if (error.field) {
                var link = document.createElement("a");
                link.href = "#" + error.field;
                link.textContent = error.message;
                link.dataset.referenceErrorField = error.field;
                item.appendChild(link);
                markField(error.field, error.message);
            } else {
                item.textContent = error.message;
            }

            errorList.appendChild(item);
        });

        summary.hidden = false;
        summary.focus();
    }

    function markField(fieldId, message) {
        var control = document.getElementById(fieldId);
        var group = control ? control.closest("[data-reference-field-group]") : null;
        var label = group ? group.querySelector("label") : null;

        if (!label) {
            return;
        }

        var hint = document.createElement("span");
        hint.className = "govuk-visually-hidden";
        hint.textContent = "Error:";

        var text = document.createElement("p");
        text.id = fieldId + "-error";
        text.className = "govuk-error-message";
        text.append(hint, " " + message);

        label.after(text);
        group.classList.add("govuk-form-group--error");
        control.classList.add(control.tagName === "SELECT" ? "govuk-select--error" : "govuk-textarea--error");
        control.setAttribute("aria-describedby", text.id);
    }

    function clearErrors() {
        summary.hidden = true;
        errorList.replaceChildren();

        form.querySelectorAll(".govuk-error-message").forEach(function (message) {
            message.remove();
        });
        form.querySelectorAll(".govuk-form-group--error").forEach(function (group) {
            group.classList.remove("govuk-form-group--error");
        });
        form.querySelectorAll(".govuk-select--error, .govuk-textarea--error").forEach(function (control) {
            control.classList.remove("govuk-select--error", "govuk-textarea--error");
            control.removeAttribute("aria-describedby");
        });
    }

    function onErrorLinkClick(event) {
        var link = event.target instanceof Element ? event.target.closest("[data-reference-error-field]") : null;
        var control = link ? document.getElementById(link.dataset.referenceErrorField) : null;

        if (control) {
            event.preventDefault();
            control.focus();
        }
    }
})();
