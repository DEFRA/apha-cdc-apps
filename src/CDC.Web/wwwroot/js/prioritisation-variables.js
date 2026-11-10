// Maintain profile prioritisation variables: the category dropdown loads as soon as it changes (legacy AutoPostBack),
// and choosing a criterion swaps in just its edit panel instead of reloading the page.
(function () {
    'use strict';

    // Swaps the category's actions and criteria in place of a full reload; falls back to submitting the form.
    document.querySelectorAll('form[data-category-form] select').forEach(function (select) {
        select.addEventListener('change', function () {
            var form = select.form;
            var url = new URL(form.action || window.location.href, window.location.href);
            url.search = '';
            if (select.value) {
                url.searchParams.set('SelectedCategoryId', select.value);
            }
            var pageUrl = url.toString();
            url.searchParams.set('handler', 'CategorySection');

            fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
                .then(function (response) {
                    if (!response.ok || response.redirected) {
                        throw new Error('Category section unavailable');
                    }
                    return response.text();
                })
                .then(function (html) {
                    var template = document.createElement('template');
                    template.innerHTML = html;
                    ['category-actions', 'category-criteria'].forEach(function (name) {
                        var fragment = template.content.querySelector('[data-fragment="' + name + '"]');
                        var target = document.getElementById(name);
                        if (fragment && target) {
                            target.replaceChildren.apply(target, Array.prototype.slice.call(fragment.childNodes));
                        }
                    });

                    var criterionPanel = document.getElementById('criterion-panel');
                    if (criterionPanel) {
                        criterionPanel.replaceChildren();
                    }
                    var hiddenCategory = document.getElementById('update-selected-category-id');
                    if (hiddenCategory) {
                        hiddenCategory.value = select.value;
                    }
                    window.history.pushState(null, '', pageUrl);
                })
                .catch(function () {
                    form.submit();
                });
        });
    });

    // A long-running submit shows its progress dialog until the response replaces the page; it can't be dismissed.
    document.addEventListener('submit', function (event) {
        var submitter = event.submitter;
        var dialog = submitter && submitter.dataset.progressDialog && document.querySelector(submitter.dataset.progressDialog);
        if (dialog && !event.defaultPrevented && typeof dialog.showModal === 'function') {
            dialog.addEventListener('cancel', function (cancelEvent) { cancelEvent.preventDefault(); });
            dialog.showModal();
        }
    });

    var panel = document.getElementById('criterion-panel');
    if (!panel) {
        return;
    }

    // Criterion edits confirmed with OK are held as JSON in the update form until Update saves them,
    // as the legacy page held them in session.
    var pendingField = document.getElementById('pending-changes-data');
    var pendingMessage = document.getElementById('pending-changes-message');

    function readPending() {
        try {
            return pendingField && pendingField.value ? JSON.parse(pendingField.value) : [];
        } catch (error) {
            return [];
        }
    }

    function writePending(changes) {
        pendingField.value = changes.length ? JSON.stringify(changes) : '';
        if (pendingMessage) {
            pendingMessage.hidden = changes.length === 0;
        }
    }

    // A parsed document is inert, so reading its text never runs script from the value.
    function decode(html) {
        return new DOMParser().parseFromString(html, 'text/html').body.textContent || '';
    }

    function plainText(html) {
        return decode(html).trim();
    }

    // Writes a name keeping only its <em>/<i> italics, building nodes rather than parsing HTML.
    function renderName(target, value) {
        target.replaceChildren();
        var emphasis = null;
        value.split(/(<\/?(?:em|i)>)/i).forEach(function (part) {
            var tag = part.toLowerCase();
            if (tag === '<em>' || tag === '<i>') {
                emphasis = target.appendChild(document.createElement('em'));
            } else if (tag === '</em>' || tag === '</i>') {
                emphasis = null;
            } else if (part) {
                (emphasis || target).appendChild(document.createTextNode(decode(part)));
            }
        });
    }

    function panelContent() {
        return document.getElementById('criterion-panel-content');
    }

    // Shows a criterion's pending edits when it is reopened; runs before the name editor is built from the input.
    function applyPending(content) {
        if (!content) {
            return;
        }
        var change = readPending().find(function (candidate) { return candidate.id === content.dataset.criterionId; });
        if (!change) {
            return;
        }
        content.querySelector('#criterion-name').value = change.name || '';
        content.querySelector('#criterion-weight').value = change.weight;
        (change.valueScores || []).forEach(function (valueScore) {
            var input = content.querySelector('[data-value-id="' + valueScore.id + '"]');
            if (input) {
                input.value = valueScore.score;
            }
        });
    }

    function showPanel(html) {
        panel.innerHTML = html;
        applyPending(panelContent());
        if (window.appSingleLineRichtext) {
            window.appSingleLineRichtext.start(panel);
        }
    }

    function closePanel() {
        panel.replaceChildren();
        var url = new URL(window.location.href);
        url.searchParams.delete('SelectedCriterionId');
        window.history.replaceState(null, '', url);
    }

    function isWholeNumber(value, min, max) {
        return /^\d+$/.test(value) && Number(value) >= min && Number(value) <= max;
    }

    function showErrors(content, errors) {
        var summary = content.querySelector('[data-criterion-errors]');
        var list = content.querySelector('[data-criterion-error-list]');
        content.querySelectorAll('.govuk-input--error').forEach(function (input) { input.classList.remove('govuk-input--error'); });
        list.replaceChildren();
        errors.forEach(function (error) {
            var link = document.createElement('a');
            link.href = '#' + error.target;
            link.textContent = error.message;
            var item = document.createElement('li');
            item.appendChild(link);
            list.appendChild(item);
            var input = document.getElementById(error.target);
            if (input) {
                input.classList.add('govuk-input--error');
            }
        });
        summary.hidden = errors.length === 0;
        if (errors.length) {
            summary.focus();
        }
    }

    // The legacy OK button: checks the edits with the legacy messages, then holds them as pending changes.
    function confirmCriterion(content) {
        var errors = [];
        var nameInput = content.querySelector('#criterion-name');
        var nameTarget = document.getElementById('criterion-name-editor') ? 'criterion-name-editor' : 'criterion-name';
        var name = nameInput.value.trim();
        var plainName = plainText(name);
        if (!plainName) {
            errors.push({ target: nameTarget, message: 'You must enter criterion text' });
        } else if (plainName.length > 255) {
            errors.push({ target: nameTarget, message: 'The criterion text must be no longer than 255 characters' });
        }

        var weight = content.querySelector('#criterion-weight').value.trim();
        if (!weight) {
            errors.push({ target: 'criterion-weight', message: 'You must enter a weighting' });
        } else if (!isWholeNumber(weight, 1, 999)) {
            errors.push({ target: 'criterion-weight', message: 'Criterion weight must be a positive integer' });
        }

        var valueScores = [];
        content.querySelectorAll('[data-value-id]').forEach(function (input) {
            var score = input.value.trim();
            if (!score) {
                errors.push({ target: input.id, message: 'Please enter a score for ' + input.dataset.valueName });
            } else if (!isWholeNumber(score, 0, 999)) {
                errors.push({ target: input.id, message: input.dataset.valueName + ' score must be a positive integer' });
            }
            valueScores.push({ id: input.dataset.valueId, score: Number(score) });
        });

        showErrors(content, errors);
        if (errors.length) {
            return;
        }

        var criterionId = content.dataset.criterionId;
        var changes = readPending().filter(function (change) { return change.id !== criterionId; });
        changes.push({ id: criterionId, name: name, weight: Number(weight), valueScores: valueScores });
        writePending(changes);

        var link = document.querySelector('a[data-criterion-link][data-criterion-id="' + criterionId + '"]');
        if (link) {
            renderName(link.querySelector('[data-criterion-name]'), name);
        }
        var weightCell = document.querySelector('[data-criterion-weight="' + criterionId + '"]');
        if (weightCell) {
            weightCell.textContent = weight;
        }

        closePanel();
        if (link) {
            link.focus();
        }
    }

    document.addEventListener('click', function (event) {
        var content = panelContent();
        if (content && event.target.closest('[data-criterion-ok]')) {
            confirmCriterion(content);
        } else if (content && event.target.closest('[data-criterion-cancel]')) {
            var link = document.querySelector('a[data-criterion-link][data-criterion-id="' + content.dataset.criterionId + '"]');
            closePanel();
            if (link) {
                link.focus();
            }
        }
    });

    applyPending(panelContent());

    function panelUrl(href) {
        var url = new URL(href, window.location.href);
        url.searchParams.set('handler', 'CriterionPanel');
        return url;
    }

    document.addEventListener('click', function (event) {
        var link = event.target.closest('a[data-criterion-link]');
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
            return;
        }

        event.preventDefault();

        fetch(panelUrl(link.href), { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
            .then(function (response) {
                // A redirect means the session has gone (sign-in page), so let the browser follow the link normally.
                if (!response.ok || response.redirected) {
                    throw new Error('Criterion panel unavailable');
                }
                return response.text();
            })
            .then(function (html) {
                showPanel(html);
                window.history.pushState(null, '', link.href);
                var heading = document.getElementById('criterion-panel-heading');
                if (heading) {
                    heading.focus();
                }
            })
            .catch(function () {
                window.location.href = link.href;
            });
    });

    window.addEventListener('popstate', function () {
        window.location.reload();
    });
})();
