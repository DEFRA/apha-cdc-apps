// The "Spell check" dialog for the profile text fields. It replaces the legacy Telerik RadSpell dialog and keeps its steps and its
// buttons: it stops at each misspelt word, shows the word in its context with the dictionary's suggestions, and offers Ignore, Ignore
// All, Add Custom, Change, Change All, Undo and Close. Closing with changes made asks whether to apply them, as RadSpell did.
//
// The dictionary is on the server (SpellCheckController). This script finds the words in the field, asks which of them are misspelt
// and, one word at a time, for suggestions, and keeps the state of the check itself. The changes are made to a copy of the text and
// written to the field only when the user applies them or the check completes. A rich text field is an editor, so only its text
// nodes are checked, and its formatting is left as it is.
(function () {
    'use strict';

    var DIALOG_ID = 'spell-check-dialog';
    var CONTEXT_WORDS = 10;

    var TEXT = {
        busy: 'Spell checking in progress...',
        complete: 'The spell check is complete.',
        failed: 'The spell check could not be completed. Close this window and try again.',
        addFailed: 'The word could not be added to the custom dictionary. Try again.',
        loadingSuggestions: 'Loading suggestions...',
        noSuggestions: 'No suggestions',
        ignore: 'Ignore',
        undoEdit: 'Undo Edit',
        applyChanges: 'Do you want to apply or cancel the changes to the text so far?',
        apply: 'Apply changes',
        cancelChanges: 'Cancel changes',
        ok: 'OK',
        cancel: 'Cancel'
    };

    // What the check leaves alone: web and email addresses and, in the source of a field that holds HTML, tags and entities. Every part
    // is bounded, so that a long unbroken string cannot make the match slow.
    var ADDRESS = String.raw`https?:\/\/[^\s<>]+|www\.[^\s<>]+|[^\s@<>]{1,64}@[^\s@<>]{1,255}\.[^\s@<>]{1,63}`;
    var MARKUP = '<[^<>]*>|&#?[a-z0-9]+;';

    // What a word is: letters and digits, with an apostrophe or a hyphen between two of them.
    var WORD = String.raw`[\p{L}\p{M}\p{N}]+(?:['\u2019-][\p{L}\p{M}\p{N}]+)*`;

    // The first group of each pattern is what is left alone, and the second is a word.
    var TEXT_PATTERN = '(' + ADDRESS + ')|(' + WORD + ')';
    var HTML_PATTERN = '(' + MARKUP + '|' + ADDRESS + ')|(' + WORD + ')';
    var HAS_NUMBER = /\p{N}/u;

    // The elements of an editor that start a new line.
    var BLOCKS = 'p, li, div, h1, h2, h3, h4, h5, h6, blockquote, pre, ul, ol, dl, dt, dd, table, tr, td, th';

    // As legacy's RadSpell was set to, words with numbers in them, such as H5N1, are not checked.
    function findWords(text, isHtml) {
        var pattern = new RegExp(isHtml ? HTML_PATTERN : TEXT_PATTERN, 'giu');
        var words = [];
        var match;

        while ((match = pattern.exec(text)) !== null) {
            if (match[2] !== undefined && !HAS_NUMBER.test(match[2])) {
                words.push({ word: match[2], start: match.index });
            }
        }

        return words;
    }

    // The text of a field as pieces that can each be read and written back: a text box is one piece, and an editor has a piece for
    // each of its text nodes, whatever formatting they are in. A piece also says what comes between it and the piece before it, so
    // that the words around a misspelt one read as they do in the field: a line break between blocks and at a <br>, and nothing
    // where only the formatting changes.
    function createSource(field) {
        var segments = [];

        if (field.matches('input, textarea')) {
            segments.push({
                before: '',
                read: function () { return field.value; },
                write: function (text) { field.value = text; }
            });

            return { segments: segments, isHtml: true };
        }

        var walker = document.createTreeWalker(field, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
        var previousBlock = null;
        var broken = false;

        for (var node = walker.nextNode(); node; node = walker.nextNode()) {
            if (node.nodeType === Node.ELEMENT_NODE) {
                broken = broken || node.tagName === 'BR';
            } else if (node.nodeValue.trim() !== '') {
                var block = node.parentElement.closest(BLOCKS) || field;
                var segment = textNodeSegment(node);

                segment.before = segments.length > 0 && (broken || block !== previousBlock) ? '\n' : '';
                segments.push(segment);
                previousBlock = block;
                broken = false;
            }
        }

        return { segments: segments, isHtml: false };
    }

    function textNodeSegment(node) {
        return {
            read: function () { return node.nodeValue; },
            write: function (text) { node.nodeValue = text; }
        };
    }

    // The last few words of some text, and whether any text came before them.
    function trailingWords(text, count) {
        var index = text.length;

        for (var seen = 0; seen < count && index > 0; seen++) {
            while (index > 0 && /\s/.test(text.charAt(index - 1))) {
                index--;
            }
            while (index > 0 && !/\s/.test(text.charAt(index - 1))) {
                index--;
            }
        }

        return { text: text.slice(index), cut: text.slice(0, index).trim() !== '' };
    }

    // The first few words of some text, and whether any text came after them.
    function leadingWords(text, count) {
        var index = 0;

        for (var seen = 0; seen < count && index < text.length; seen++) {
            while (index < text.length && /\s/.test(text.charAt(index))) {
                index++;
            }
            while (index < text.length && !/\s/.test(text.charAt(index))) {
                index++;
            }
        }

        return { text: text.slice(0, index), cut: text.slice(index).trim() !== '' };
    }

    // The state of one spell check, as legacy's SpellProcessor kept it: every misspelt word in the text in the order it comes, the one
    // being considered, and what has been done to each. A word the user has dealt with is resolved; Undo gives back the last
    // action's. The text is a copy, so nothing reaches the field until the user applies it.
    function createSession(originalTexts, joiners, words, misspelt) {
        var texts = originalTexts.slice();
        var occurrences = words
            .filter(function (found) { return misspelt.has(found.word); })
            .map(function (found) {
                return { segment: found.segment, start: found.start, word: found.word, original: found.word, action: null };
            });
        var undoSteps = [];
        var current = nextUnresolved(0);

        function nextUnresolved(from) {
            for (var i = from; i < occurrences.length; i++) {
                if (occurrences[i].action === null) {
                    return i;
                }
            }

            return -1;
        }

        // Puts text in the place of each of the occurrences, and moves along those that follow in the same piece of text.
        function put(indexes, text, action) {
            indexes.forEach(function (index) {
                var occurrence = occurrences[index];
                var piece = texts[occurrence.segment];
                var delta = text.length - occurrence.word.length;

                texts[occurrence.segment] = piece.slice(0, occurrence.start) + text + piece.slice(occurrence.start + occurrence.word.length);

                for (var i = index + 1; i < occurrences.length; i++) {
                    if (occurrences[i].segment === occurrence.segment) {
                        occurrences[i].start += delta;
                    }
                }

                occurrence.word = text;
                occurrence.action = action;
            });
        }

        // The occurrence being considered, and every later one of the same word that is still to be dealt with.
        function sameWord() {
            var word = occurrences[current].word;

            return occurrences.reduce(function (indexes, occurrence, index) {
                if (index >= current && occurrence.action === null && occurrence.word === word) {
                    indexes.push(index);
                }

                return indexes;
            }, []);
        }

        function move() {
            var next = nextUnresolved(current);

            if (next !== -1) {
                current = next;
            }
        }

        function apply(indexes, text, action) {
            put(indexes, text, action);
            undoSteps.push(indexes);
            move();
        }

        return {
            isDone: function () { return nextUnresolved(0) === -1; },
            current: function () { return occurrences[current]; },
            upcoming: function () { return occurrences[nextUnresolved(current + 1)] || null; },
            texts: function () { return texts; },
            canUndo: function () { return undoSteps.length > 0; },
            isChanged: function () {
                return occurrences.some(function (occurrence) { return occurrence.action === 'change' || occurrence.action === 'change-all'; });
            },
            ignore: function () { apply([current], occurrences[current].word, 'ignore'); },
            ignoreAll: function () { apply(sameWord(), occurrences[current].word, 'ignore-all'); },
            change: function (text) { apply([current], text, 'change'); },
            changeAll: function (text) { apply(sameWord(), text, 'change-all'); },
            // The word is in the custom dictionary now, which takes no account of case, so it is right wherever it comes. Not an
            // action to undo: the word stays added.
            addCustom: function () {
                var added = occurrences[current].word.toLowerCase();

                occurrences.forEach(function (occurrence) {
                    if (occurrence.action === null && occurrence.word.toLowerCase() === added) {
                        occurrence.action = 'custom';
                    }
                });
                move();
            },
            undo: function () {
                var indexes = undoSteps.pop();

                if (indexes) {
                    indexes.forEach(function (index) { put([index], occurrences[index].original, null); });
                    current = nextUnresolved(0);
                }
            },
            // The current word with the words around it, from the text as it is now.
            context: function () {
                var occurrence = occurrences[current];
                var flat = '';
                var at = 0;

                texts.forEach(function (text, index) {
                    flat += joiners[index] + text;

                    if (index === occurrence.segment) {
                        at = flat.length - text.length + occurrence.start;
                    }
                });

                return {
                    before: trailingWords(flat.slice(0, at), CONTEXT_WORDS),
                    word: occurrence.word,
                    after: leadingWords(flat.slice(at + occurrence.word.length), CONTEXT_WORDS)
                };
            }
        };
    }

    function show(element, visible) {
        element.hidden = !visible;
    }

    // Writes the changes of a finished check to the field, where the page's own scripts see them as typing.
    function commit(finished) {
        if (!finished.session) {
            return;
        }

        var texts = finished.session.texts();
        var changed = false;

        finished.source.segments.forEach(function (segment, index) {
            if (texts[index] !== finished.texts[index]) {
                segment.write(texts[index]);
                changed = true;
            }
        });

        if (changed) {
            finished.field.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }

    // What is announced once the suggestions for a word are in.
    function describeSuggestions(list) {
        if (list.length === 0) {
            return 'No suggestions.';
        }

        return list.length + (list.length === 1 ? ' suggestion.' : ' suggestions.');
    }

    function createDialog(root) {
        var message = root.querySelector('[data-spell-message]');
        var announcer = root.querySelector('[data-spell-announce]');
        var layout = root.querySelector('[data-spell-layout]');
        var workspace = root.querySelector('[data-spell-workspace]');
        var context = root.querySelector('[data-spell-context]');
        var edit = root.querySelector('[data-spell-edit]');
        var suggestions = root.querySelector('[data-spell-suggestions]');
        var confirmPanel = root.querySelector('[data-spell-confirm]');
        var confirmMessage = root.querySelector('[data-spell-confirm-message]');
        var confirmYes = root.querySelector('[data-spell-confirm-yes]');
        var confirmNo = root.querySelector('[data-spell-confirm-no]');
        var actions = {};

        root.querySelectorAll('[data-spell-action]').forEach(function (button) {
            actions[button.dataset.spellAction] = button;
        });

        // The check that is open, if one is: the field being checked, what has been done to its words, and what the dialog shows.
        var run = null;

        function post(url, payload) {
            var headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
            headers[root.dataset.antiforgeryHeader] = root.dataset.antiforgeryToken;

            return fetch(url, { method: 'POST', credentials: 'same-origin', headers: headers, body: JSON.stringify(payload) })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('The request was refused with status ' + response.status);
                    }

                    return response.status === 204 ? null : response.json();
                });
        }

        // What the dialog is showing: that a check is under way or over ('busy', 'done'), a word to deal with, or a question to answer.
        function setView(view) {
            var isWord = view === 'word';

            run.view = view;
            show(layout, view !== 'confirm');
            show(workspace, isWord);
            show(confirmPanel, view === 'confirm');
            layout.classList.toggle('app-spell-dialog__layout--word', isWord);
            Object.keys(actions).forEach(function (name) {
                show(actions[name], isWord || name === 'close');
            });
        }

        function say(text, isError) {
            message.textContent = '';

            if (isError && text) {
                var prefix = document.createElement('span');
                prefix.className = 'govuk-visually-hidden';
                prefix.textContent = 'Error: ';
                message.append(prefix);
            }

            message.append(text);
            message.className = 'app-spell-dialog__message ' + (isError ? 'govuk-error-message' : 'govuk-body');
            show(message, text !== '');
        }

        function announce(text) {
            announcer.textContent = text;
        }

        function setBusy(busy) {
            run.busy = busy;
            updateButtons();
        }

        // Legacy's rules for which buttons suit the moment: Ignore always does, and means "Undo Edit" while the word is being edited by
        // hand, when only Change can put the new word in; the others need suggestions, or the word as it is, to work on.
        function updateButtons() {
            var editing = run.editing;
            var hasSuggestions = run.hasSuggestions;
            var enabled = {
                'ignore': true,
                'ignore-all': !editing,
                'add-custom': !editing,
                'change': editing || hasSuggestions,
                'change-all': !editing && hasSuggestions,
                'undo': Boolean(run.session) && run.session.canUndo(),
                'close': true
            };

            Object.keys(actions).forEach(function (name) {
                actions[name].disabled = run.busy || !enabled[name];
            });
            actions.ignore.textContent = editing ? TEXT.undoEdit : TEXT.ignore;
        }

        // A control that has just been disabled or hidden takes the keyboard's place with it, so the focus goes where it is of use.
        function keepFocus(force) {
            var active = document.activeElement;
            var inUse = active && root.contains(active) && active !== root && !active.disabled && !active.closest('[hidden]');

            if (force || !inUse) {
                (run.editing ? edit : suggestions).focus();
            }
        }

        function setSuggestions(list, loading) {
            var placeholder;

            suggestions.replaceChildren();
            run.hasSuggestions = !loading && list.length > 0;

            if (!run.hasSuggestions) {
                placeholder = new Option(loading ? TEXT.loadingSuggestions : TEXT.noSuggestions, '');
                placeholder.disabled = true;
                suggestions.append(placeholder);

                return;
            }

            list.forEach(function (suggestion) {
                suggestions.append(new Option(suggestion, suggestion));
            });
            suggestions.selectedIndex = 0;
        }

        // Suggestions take the server longer to find than a word does to check, so they are fetched for one word at a time, and kept.
        function fetchSuggestions(word) {
            var cache = run.suggestions;

            if (!cache.has(word)) {
                cache.set(word, post(root.dataset.suggestUrl, { word: word }).then(
                    function (reply) { return Array.isArray(reply?.suggestions) ? reply.suggestions : []; },
                    function () {
                        // Not kept, so that coming back to the word tries again.
                        cache.delete(word);

                        return [];
                    }));
            }

            return cache.get(word);
        }

        function showWord(isFirst) {
            var session = run.session;
            var view = session.context();
            var word = view.word;
            var upcoming = session.upcoming();
            var token = ++run.render;
            var highlighted = document.createElement('strong');

            highlighted.className = 'app-spell-dialog__word';
            highlighted.textContent = word;
            context.replaceChildren(
                document.createTextNode((view.before.cut ? '\u2026' : '') + view.before.text),
                highlighted,
                document.createTextNode(view.after.text + (view.after.cut ? '\u2026' : '')));

            run.editing = false;
            show(context, true);
            show(edit, false);
            suggestions.disabled = false;
            setView('word');
            say('', false);
            setSuggestions([], true);
            updateButtons();
            keepFocus(isFirst);

            fetchSuggestions(word).then(function (list) {
                if (run?.render !== token) {
                    return;
                }

                setSuggestions(list, false);
                updateButtons();
                announce('Not in dictionary: ' + word + '. ' + describeSuggestions(list));
            });

            if (upcoming) {
                fetchSuggestions(upcoming.word);
            }
        }

        // Moves on from the word just dealt with: to the next, or, with none left, to the end of the check.
        function advance() {
            if (run.session.isDone()) {
                finish();
            } else {
                showWord(false);
            }
        }

        // As legacy did, a check that runs to the end applies its changes without asking.
        function finish() {
            commit(run);
            setView('done');
            say(TEXT.complete, false);
            announce('');
            run.busy = false;
            updateButtons();
            actions.close.focus();
        }

        function fail(text) {
            setView('done');
            say(text, true);
            run.busy = false;
            updateButtons();
            actions.close.focus();
        }

        function closeDialog() {
            var opener = run.opener;

            reset();
            if (root.open) {
                root.close();
            }
            if (opener?.isConnected) {
                opener.focus();
            }
        }

        function reset() {
            run = null;
            context.replaceChildren();
            suggestions.replaceChildren();
            edit.value = '';
            announce('');
        }

        // A question that has to be answered before the check goes on, which takes the place of the rest of the dialog. Escape
        // answers it with onEscape, which unless given is the same as the second answer.
        function ask(question) {
            run.question = question;
            confirmMessage.textContent = question.message;
            confirmYes.textContent = question.yes;
            confirmNo.textContent = question.no;
            say('', false);
            setView('confirm');
            confirmYes.focus();
        }

        function answer(isYes) {
            var question = run.question;

            run.question = null;
            (isYes ? question.onYes : question.onNo)();
        }

        function backToTheWord() {
            setView('word');
            updateButtons();
            keepFocus(false);
        }

        function requestClose() {
            if (run.view === 'word' && run.session?.isChanged()) {
                ask({
                    message: TEXT.applyChanges,
                    yes: TEXT.apply,
                    no: TEXT.cancelChanges,
                    onYes: function () {
                        commit(run);
                        closeDialog();
                    },
                    onNo: closeDialog,
                    onEscape: backToTheWord
                });

                return;
            }

            closeDialog();
        }

        function askToAddWord() {
            var word = run.session.current().word;

            ask({
                message: 'Are you sure you want to add \'' + word + '\' to the custom dictionary?',
                yes: TEXT.ok,
                no: TEXT.cancel,
                onYes: addWord,
                onNo: backToTheWord
            });
        }

        async function addWord() {
            var thisRun = run;
            var word = thisRun.session.current().word;

            backToTheWord();
            setBusy(true);

            try {
                await post(root.dataset.addWordUrl, { word: word });
            } catch {
                // The user is told the word was not added, and can try again.
                if (run === thisRun) {
                    setBusy(false);
                    say(TEXT.addFailed, true);
                    keepFocus(false);
                }

                return;
            }

            if (run === thisRun) {
                setBusy(false);
                thisRun.session.addCustom();
                advance();
            }
        }

        function toggleEdit() {
            run.editing = !run.editing;

            if (run.editing) {
                edit.value = run.session.current().word;
            }

            show(context, !run.editing);
            show(edit, run.editing);
            suggestions.disabled = run.editing;
            updateButtons();
            keepFocus(false);

            if (run.editing) {
                edit.select();
            }
        }

        function replace(everywhere) {
            var text = run.editing ? edit.value.trim() : suggestions.value;

            if (text === '') {
                return;
            }

            if (everywhere) {
                run.session.changeAll(text);
            } else {
                run.session.change(text);
            }

            advance();
        }

        function perform(action) {
            var session = run.session;

            switch (action) {
                case 'ignore':
                    if (run.editing) {
                        toggleEdit();
                    } else {
                        session.ignore();
                        advance();
                    }
                    break;
                case 'ignore-all':
                    session.ignoreAll();
                    advance();
                    break;
                case 'add-custom':
                    askToAddWord();
                    break;
                case 'change':
                    replace(false);
                    break;
                case 'change-all':
                    replace(true);
                    break;
                case 'undo':
                    session.undo();
                    showWord(false);
                    break;
                default:
                    requestClose();
                    break;
            }
        }

        async function check(thisRun, words) {
            var reply;

            try {
                var distinct = Array.from(new Set(words.map(function (found) { return found.word; })));
                reply = await post(root.dataset.checkUrl, { words: distinct });
            } catch {
                // The user is told the check could not be completed, which is all there is for them to act on.
                if (run === thisRun) {
                    fail(TEXT.failed);
                }

                return;
            }

            if (run !== thisRun) {
                return;
            }

            if (!reply || !Array.isArray(reply.misspelt)) {
                fail(TEXT.failed);

                return;
            }

            thisRun.busy = false;
            thisRun.session = createSession(thisRun.texts, thisRun.joiners, words, new Set(reply.misspelt));

            if (thisRun.session.isDone()) {
                finish();
            } else {
                showWord(true);
            }
        }

        root.addEventListener('click', function (event) {
            var button = event.target.closest('[data-spell-action]');

            if (button && run && !button.disabled) {
                perform(button.dataset.spellAction);
            }
        });

        confirmYes.addEventListener('click', function () {
            if (run?.question) {
                answer(true);
            }
        });

        confirmNo.addEventListener('click', function () {
            if (run?.question) {
                answer(false);
            }
        });

        context.addEventListener('click', function () {
            if (run?.view === 'word' && !run.busy && !run.editing) {
                toggleEdit();
            }
        });

        edit.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && run && !actions.change.disabled) {
                event.preventDefault();
                replace(false);
            }
        });

        suggestions.addEventListener('dblclick', function () {
            if (run?.hasSuggestions && !run.busy) {
                replace(false);
            }
        });

        suggestions.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && run?.hasSuggestions && !run.busy) {
                event.preventDefault();
                replace(false);
            }
        });

        // Escape is a way out, not an answer to nothing: it asks to close, or answers a question the way "Cancel" would.
        root.addEventListener('cancel', function (event) {
            if (!run) {
                return;
            }

            event.preventDefault();

            if (run.view === 'confirm') {
                var question = run.question;

                run.question = null;
                (question.onEscape || question.onNo)();
            } else if (!run.busy) {
                requestClose();
            }
        });

        // Closed by something other than the dialog's own buttons, such as the page being left. A browser may deliver the event some
        // frames after the closing, by when the dialog can be open again for another check, which it must not end.
        root.addEventListener('close', function () {
            if (run && !root.open) {
                reset();
            }
        });

        return {
            open: function (field, opener) {
                if (run) {
                    return;
                }

                var source = createSource(field);
                var texts = source.segments.map(function (segment) { return segment.read(); });
                var words = [];

                texts.forEach(function (text, segment) {
                    findWords(text, source.isHtml).forEach(function (found) {
                        words.push({ segment: segment, word: found.word, start: found.start });
                    });
                });

                var thisRun = {
                    field: field,
                    opener: opener,
                    source: source,
                    texts: texts,
                    joiners: source.segments.map(function (segment) { return segment.before; }),
                    session: null,
                    view: 'busy',
                    editing: false,
                    hasSuggestions: false,
                    busy: true,
                    render: 0,
                    question: null,
                    suggestions: new Map()
                };

                run = thisRun;
                setView('busy');
                say(TEXT.busy, false);
                updateButtons();
                actions.close.disabled = false;

                if (!root.open) {
                    root.showModal();
                }

                actions.close.focus();

                if (words.length === 0) {
                    finish();
                } else {
                    void check(thisRun, words);
                }
            }
        };
    }

    var dialogs = new WeakMap();

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-spellcheck-target]');

        if (!button) {
            return;
        }

        var targetId = button.dataset.spellcheckTarget;
        // A rich text field is an editor that stands in front of its textarea, which is what the button names.
        var field = document.getElementById(targetId + '-editor') || document.getElementById(targetId);
        var root = document.getElementById(DIALOG_ID);

        if (!field || !root || typeof root.showModal !== 'function' || typeof fetch !== 'function') {
            return;
        }

        event.preventDefault();

        if (!dialogs.has(root)) {
            dialogs.set(root, createDialog(root));
        }

        dialogs.get(root).open(field, button);
    });
})();
