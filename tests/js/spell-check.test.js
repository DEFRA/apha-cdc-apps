import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const CHECK_URL = '/SpellCheck/Check';
const SUGGEST_URL = '/SpellCheck/Suggest';
const ADD_URL = '/SpellCheck/AddCustomWord';

// The dialog under test is the real partial, so the script is tested against the hooks the page really has. Vitest runs from the
// repository root, where the config file is.
const partial = readFileSync(resolve(process.cwd(), 'src/CDC.Web/Views/Shared/_SpellCheckDialog.cshtml'), 'utf8');
const DIALOG_HTML = partial
  .slice(partial.indexOf('<dialog'))
  .replace(/@Url\.Action\("(\w+)", "(\w+)"\)/g, '/$2/$1')
  .replace('@tokens.HeaderName', 'RequestVerificationToken')
  .replace('@tokens.RequestToken', 'test-token');

// jsdom does not implement the dialog methods, so these stand in for them.
beforeAll(async () => {
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close = function close() {
    if (this.hasAttribute('open')) {
      this.removeAttribute('open');
      this.dispatchEvent(new Event('close'));
    }
  };

  await import('../../src/CDC.Web/wwwroot/js/spell-check.js');
});

let server;

// Answers as CDC.Web does: the words it is given that are in the misspelt list, and the suggestions listed for a word.
function fakeServer({ misspelt = [], suggestions = {}, addStatus = 204, checkFails = false } = {}) {
  const fake = { calls: [] };

  fake.fetch = vi.fn(async (url, options) => {
    const body = JSON.parse(options.body);
    fake.calls.push({ url, body, options });

    if (url === CHECK_URL) {
      if (checkFails) {
        throw new TypeError('Failed to fetch');
      }

      return { ok: true, status: 200, json: async () => ({ misspelt: body.words.filter((word) => misspelt.includes(word)) }) };
    }

    if (url === SUGGEST_URL) {
      return { ok: true, status: 200, json: async () => ({ suggestions: suggestions[body.word] ?? [] }) };
    }

    if (url === ADD_URL) {
      return { ok: addStatus < 400, status: addStatus, json: async () => null };
    }

    throw new Error(`Unexpected request to ${url}`);
  });
  fake.to = (url) => fake.calls.filter((call) => call.url === url);

  return fake;
}

function buildPage(fieldHtml) {
  document.body.innerHTML = `
    <form>
      <button type="button" id="spell-button" data-spellcheck-target="field">Spell check</button>
      ${fieldHtml}
    </form>
    ${DIALOG_HTML}
  `;
}

const textarea = (value) => buildPage(`<textarea id="field" name="field">${value}</textarea>`);
const editor = (html) => buildPage(`<textarea id="field" name="field" hidden></textarea><div id="field-editor" contenteditable="true">${html}</div>`);

const dialog = () => document.getElementById('spell-check-dialog');
const part = (name) => dialog().querySelector(`[data-spell-${name}]`);
const action = (name) => dialog().querySelector(`[data-spell-action="${name}"]`);
const message = () => part('message').textContent.trim();
const shownWord = () => dialog().querySelector('.app-spell-dialog__word')?.textContent;
const suggestionTexts = () => [...part('suggestions').options].map((option) => option.textContent);
const visibleActions = () => [...dialog().querySelectorAll('[data-spell-action]')].filter((button) => !button.hidden).map((button) => button.dataset.spellAction);
const enabledActions = () => visibleActions().filter((name) => !action(name).disabled);
const field = () => document.getElementById('field');

// Opens the dialog and waits for it to show the word it stops at, once its suggestions are in.
async function openAtWord(expected) {
  document.getElementById('spell-button').click();
  await atWord(expected);
}

async function atWord(expected) {
  await vi.waitFor(() => {
    expect(shownWord()).toBe(expected);
    expect(suggestionTexts()).not.toContain('Loading suggestions...');
  });
}

async function openAndFinish() {
  document.getElementById('spell-button').click();
  await vi.waitFor(() => {
    expect(visibleActions()).toEqual(['close']);
    expect(message()).not.toBe('Spell checking in progress...');
  });
}

function type(input, text) {
  input.value = text;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

describe('spell-check', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function serve(options) {
    server = fakeServer(options);
    vi.stubGlobal('fetch', server.fetch);

    return server;
  }

  describe('finding the words to check', () => {
    it('sends each distinct word of a text box once, leaving out words with numbers, addresses and markup', async () => {
      serve();
      textarea('Teh cattel 2nd H5N1 visit https://example.com/a?b=c mail me@example.org &lt;em&gt;teh&lt;/em&gt; cattel &amp;amp; fine\'s co-op www.example.org');

      await openAndFinish();

      expect(server.to(CHECK_URL)).toHaveLength(1);
      expect(server.to(CHECK_URL)[0].body.words).toEqual(['Teh', 'cattel', 'visit', 'mail', 'teh', 'fine\'s', 'co-op']);
    });

    it('checks the text of an editor, not its formatting', async () => {
      serve();
      editor('<p>One <em>cattel</em> and</p><ul><li>two cattel</li></ul>');

      await openAndFinish();

      expect(server.to(CHECK_URL)[0].body.words).toEqual(['One', 'cattel', 'and', 'two']);
    });

    it('checks the editor that stands in front of the textarea the button names', async () => {
      serve();
      buildPage('<textarea id="field" hidden>Not this</textarea><div id="field-editor" contenteditable="true">But this</div>');

      await openAndFinish();

      expect(server.to(CHECK_URL)[0].body.words).toEqual(['But', 'this']);
    });

    it('treats an apostrophe or a hyphen inside a word as part of it, and a typographic apostrophe as an apostrophe', async () => {
      serve();
      textarea('don\u2019t non-technical \'quoted\'');

      await openAndFinish();

      expect(server.to(CHECK_URL)[0].body.words).toEqual(['don\u2019t', 'non-technical', 'quoted']);
    });

    it('sends the antiforgery token with each request, from this site only', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');

      await openAtWord('cattel');

      [CHECK_URL, SUGGEST_URL].forEach((url) => {
        const { options } = server.to(url)[0];

        expect(options.method).toBe('POST');
        expect(options.credentials).toBe('same-origin');
        expect(options.headers.RequestVerificationToken).toBe('test-token');
        expect(options.headers['Content-Type']).toBe('application/json');
      });
    });

    it('does not ask the server about a text with no words, and says the check is complete', async () => {
      serve();
      textarea('12 345 6789');

      await openAndFinish();

      expect(server.fetch).not.toHaveBeenCalled();
      expect(message()).toBe('The spell check is complete.');
    });

    it('says the check is complete, with nothing to change, when no word is misspelt', async () => {
      serve();
      textarea('All correct here');

      await openAndFinish();

      expect(message()).toBe('The spell check is complete.');
      expect(field().value).toBe('All correct here');
    });
  });

  describe('showing a misspelt word', () => {
    it('stops at the first misspelt word, in its context, with its suggestions and the buttons to deal with it', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle', 'battle'], fiel: ['field'] } });
      textarea('The cattel are in the fiel today.');

      await openAtWord('cattel');

      expect(dialog().hasAttribute('open')).toBe(true);
      expect(part('context').textContent).toBe('The cattel are in the fiel today.');
      expect(suggestionTexts()).toEqual(['cattle', 'battle']);
      expect(visibleActions()).toEqual(['ignore', 'ignore-all', 'add-custom', 'change', 'change-all', 'undo', 'close']);
      expect(enabledActions()).toEqual(['ignore', 'ignore-all', 'add-custom', 'change', 'change-all', 'close']);
      expect(part('message').hidden).toBe(true);
      expect(document.activeElement).toBe(part('suggestions'));
      expect(part('announce').textContent).toBe('Not in dictionary: cattel. 2 suggestions.');
    });

    it('shows ten words either side of the word, with an ellipsis where the text goes on', async () => {
      serve({ misspelt: ['cattel'] });
      const words = Array.from({ length: 30 }, (_, index) => `w${String.fromCharCode(97 + (index % 26))}${String.fromCharCode(97 + Math.floor(index / 26))}`);
      words[14] = 'cattel';
      textarea(words.join(' '));

      await openAtWord('cattel');

      const shown = part('context').textContent;
      expect(shown.startsWith('\u2026')).toBe(true);
      expect(shown.endsWith('\u2026')).toBe(true);
      expect(shown.replace(/\u2026/g, '').trim().split(/\s+/)).toEqual(words.slice(4, 25));
    });

    it('shows the words around a word of an editor as they read: unbroken at formatting, on new lines at blocks and breaks', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      editor('<p>One <em>cattel</em> and<br>some</p><ul><li>two</li><li>three</li></ul>');

      await openAtWord('cattel');

      expect(part('context').textContent).toBe('One cattel and\nsome\ntwo\nthree');
      expect(shownWord()).toBe('cattel');
    });

    it('offers "No suggestions" for a word with none, and only Change by hand', async () => {
      serve({ misspelt: ['xyzzy'] });
      textarea('xyzzy');

      await openAtWord('xyzzy');

      expect(suggestionTexts()).toEqual(['No suggestions']);
      expect(enabledActions()).toEqual(['ignore', 'ignore-all', 'add-custom', 'close']);
    });

    it('carries on without suggestions when they cannot be fetched', async () => {
      serve({ misspelt: ['cattel'] });
      server.fetch.mockImplementation(async (url, options) => {
        if (url === SUGGEST_URL) {
          throw new TypeError('Failed to fetch');
        }

        return { ok: true, status: 200, json: async () => ({ misspelt: JSON.parse(options.body).words }) };
      });
      textarea('cattel');

      await openAtWord('cattel');

      expect(suggestionTexts()).toEqual(['No suggestions']);
    });

    it('is not opened a second time while it is open', async () => {
      serve({ misspelt: ['cattel'] });
      textarea('cattel');
      await openAtWord('cattel');

      document.getElementById('spell-button').click();

      expect(server.to(CHECK_URL)).toHaveLength(1);
    });

    it('says the check could not be completed when the server cannot be reached', async () => {
      serve({ checkFails: true });
      textarea('cattel');

      await openAndFinish();

      expect(message()).toContain('The spell check could not be completed.');
      expect(part('message').classList.contains('govuk-error-message')).toBe(true);
      expect(part('message').querySelector('.govuk-visually-hidden').textContent).toBe('Error: ');
    });
  });

  describe('Change and Change All', () => {
    it('changes the word to the suggestion chosen, and applies the changes when the check is complete', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle', 'battle'], fiel: ['field'] } });
      textarea('The cattel are in the fiel today.');
      const typed = vi.fn();
      field().addEventListener('input', typed);
      await openAtWord('cattel');
      part('suggestions').selectedIndex = 1;

      action('change').click();
      await atWord('fiel');
      expect(field().value).toBe('The cattel are in the fiel today.');
      action('change').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('The battle are in the field today.');
      expect(typed).toHaveBeenCalledTimes(1);
      expect(message()).toBe('The spell check is complete.');
    });

    it('changes a word in a text box without touching the markup around it', async () => {
      serve({ misspelt: ['Teh', 'cattel'], suggestions: { Teh: ['The'], cattel: ['cattle'] } });
      textarea('&lt;p&gt;Teh &amp;amp; &lt;em&gt;cattel&lt;/em&gt;&lt;/p&gt;');
      await openAtWord('Teh');

      action('change').click();
      await atWord('cattel');
      action('change').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('<p>The &amp; <em>cattle</em></p>');
    });

    it('keeps its place in the text when a change makes a word longer or shorter', async () => {
      serve({ misspelt: ['cattel', 'fiel', 'teh'], suggestions: { cattel: ['cattle'], fiel: ['field'], teh: ['the'] } });
      textarea('cattel fiel cattel teh');
      await openAtWord('cattel');

      action('change').click();
      await atWord('fiel');
      action('change').click();
      await atWord('cattel');
      expect(part('context').textContent).toBe('cattle field cattel teh');
      action('change').click();
      await atWord('teh');
      action('change').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('cattle field cattle the');
    });

    it('changes every later occurrence of the word with Change All, across the pieces of an editor', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      editor('<p>A cattel and a <em>cattel</em></p><p>another cattel here in the fiel</p>');
      const typed = vi.fn();
      document.getElementById('field-editor').addEventListener('input', typed);
      await openAtWord('cattel');

      action('change-all').click();
      await atWord('fiel');
      action('ignore').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(document.getElementById('field-editor').innerHTML).toBe('<p>A cattle and a <em>cattle</em></p><p>another cattle here in the fiel</p>');
      expect(typed).toHaveBeenCalledTimes(1);
    });

    it('changes the word to the suggestion a double click chooses', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle', 'battle'] } });
      textarea('cattel');
      await openAtWord('cattel');

      part('suggestions').selectedIndex = 1;
      part('suggestions').dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('battle');
    });

    it('changes the word to the suggestion Enter chooses', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');

      part('suggestions').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('cattle');
    });
  });

  describe('Ignore and Ignore All', () => {
    it('leaves a word as it is with Ignore, and writes nothing when no word was changed', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('The cattel and the fiel');
      const typed = vi.fn();
      field().addEventListener('input', typed);
      await openAtWord('cattel');

      action('ignore').click();
      await atWord('fiel');
      action('ignore').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('The cattel and the fiel');
      expect(typed).not.toHaveBeenCalled();
    });

    it('leaves every later occurrence of a word as it is with Ignore All', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('cattel one cattel two fiel cattel');
      await openAtWord('cattel');

      action('ignore-all').click();
      await atWord('fiel');
      action('change').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('cattel one cattel two field cattel');
    });
  });

  describe('Undo', () => {
    it('is not available until something has been done', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');

      await openAtWord('cattel');

      expect(action('undo').disabled).toBe(true);
    });

    it('takes back the last change and returns to the word', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('The cattel and the fiel');
      await openAtWord('cattel');
      action('change').click();
      await atWord('fiel');
      expect(action('undo').disabled).toBe(false);

      action('undo').click();
      await atWord('cattel');

      expect(part('context').textContent).toBe('The cattel and the fiel');
      expect(action('undo').disabled).toBe(true);
    });

    it('takes back a Change All in one step', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('cattel and cattel and fiel');
      await openAtWord('cattel');
      action('change-all').click();
      await atWord('fiel');
      expect(part('context').textContent).toBe('cattle and cattle and fiel');

      action('undo').click();
      await atWord('cattel');

      expect(part('context').textContent).toBe('cattel and cattel and fiel');
      expect(action('undo').disabled).toBe(true);
    });

    it('takes back an Ignore All, so that its words come up again', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('cattel and cattel and fiel');
      await openAtWord('cattel');
      action('ignore-all').click();
      await atWord('fiel');

      action('undo').click();
      await atWord('cattel');
      action('change').click();
      await atWord('cattel');

      expect(part('context').textContent).toBe('cattle and cattel and fiel');
    });
  });

  describe('Add Custom', () => {
    it('asks before adding the word, then adds it and goes on to the next word, for every case of it', async () => {
      serve({ misspelt: ['Cattel', 'cattel', 'fiel'], suggestions: { fiel: ['field'] } });
      textarea('Cattel and cattel and fiel');
      await openAtWord('Cattel');

      action('add-custom').click();

      expect(part('confirm').hidden).toBe(false);
      expect(part('workspace').hidden).toBe(true);
      expect(part('confirm-message').textContent).toBe('Are you sure you want to add \'Cattel\' to the custom dictionary?');
      expect(part('confirm-yes').textContent).toBe('OK');
      expect(part('confirm-no').textContent).toBe('Cancel');
      expect(server.to(ADD_URL)).toHaveLength(0);

      part('confirm-yes').click();
      await atWord('fiel');

      expect(server.to(ADD_URL)).toHaveLength(1);
      expect(server.to(ADD_URL)[0].body).toEqual({ word: 'Cattel' });
      expect(server.to(ADD_URL)[0].options.headers.RequestVerificationToken).toBe('test-token');
    });

    it('goes back to the word without adding it when the question is answered "Cancel"', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');
      action('add-custom').click();

      part('confirm-no').click();

      expect(part('confirm').hidden).toBe(true);
      expect(part('workspace').hidden).toBe(false);
      expect(shownWord()).toBe('cattel');
      expect(server.to(ADD_URL)).toHaveLength(0);
    });

    it('says so, and stays at the word, when the word cannot be added', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] }, addStatus: 403 });
      textarea('cattel');
      await openAtWord('cattel');
      action('add-custom').click();

      part('confirm-yes').click();
      await vi.waitFor(() => expect(message()).toContain('The word could not be added to the custom dictionary.'));

      expect(part('message').classList.contains('govuk-error-message')).toBe(true);
      expect(shownWord()).toBe('cattel');
      expect(enabledActions()).toContain('add-custom');
    });
  });

  describe('editing the word by hand', () => {
    it('shows a text box for the word, where only Change and Undo Edit apply', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');

      part('context').click();

      expect(part('edit').hidden).toBe(false);
      expect(part('edit').value).toBe('cattel');
      expect(part('context').hidden).toBe(true);
      expect(part('suggestions').disabled).toBe(true);
      expect(action('ignore').textContent).toBe('Undo Edit');
      expect(enabledActions()).toEqual(['ignore', 'change', 'close']);
      expect(document.activeElement).toBe(part('edit'));
    });

    it('changes the word to what is typed', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('A cattel here');
      await openAtWord('cattel');
      part('context').click();

      type(part('edit'), ' cow ');
      action('change').click();
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('A cow here');
    });

    it('changes the word to what is typed when Enter is pressed', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');
      part('context').click();

      type(part('edit'), 'cow');
      part('edit').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
      await vi.waitFor(() => expect(visibleActions()).toEqual(['close']));

      expect(field().value).toBe('cow');
    });

    it('goes back to the word with Undo Edit, leaving it as it was', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');
      part('context').click();
      type(part('edit'), 'cow');

      action('ignore').click();

      expect(part('edit').hidden).toBe(true);
      expect(part('context').hidden).toBe(false);
      expect(part('suggestions').disabled).toBe(false);
      expect(action('ignore').textContent).toBe('Ignore');
      expect(shownWord()).toBe('cattel');
    });

    it('does nothing when Change is pressed with nothing typed', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');
      part('context').click();
      type(part('edit'), '   ');

      action('change').click();

      expect(shownWord()).toBe('cattel');
      expect(visibleActions()).toContain('change');
    });
  });

  describe('Close', () => {
    it('closes at once when nothing has been changed, returning to the button that opened it', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');
      const focused = vi.spyOn(document.getElementById('spell-button'), 'focus');

      action('close').click();

      expect(dialog().hasAttribute('open')).toBe(false);
      expect(field().value).toBe('cattel');
      expect(focused).toHaveBeenCalled();
    });

    it('asks whether to apply the changes made so far, and applies them when told to', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('The cattel and the fiel');
      await openAtWord('cattel');
      action('change').click();
      await atWord('fiel');

      action('close').click();

      expect(part('confirm').hidden).toBe(false);
      expect(part('confirm-message').textContent).toBe('Do you want to apply or cancel the changes to the text so far?');
      expect(part('confirm-yes').textContent).toBe('Apply changes');
      expect(part('confirm-no').textContent).toBe('Cancel changes');
      expect(dialog().hasAttribute('open')).toBe(true);

      part('confirm-yes').click();

      expect(field().value).toBe('The cattle and the fiel');
      expect(dialog().hasAttribute('open')).toBe(false);
    });

    it('closes without applying the changes when told to cancel them', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('The cattel and the fiel');
      const typed = vi.fn();
      field().addEventListener('input', typed);
      await openAtWord('cattel');
      action('change').click();
      await atWord('fiel');
      action('close').click();

      part('confirm-no').click();

      expect(field().value).toBe('The cattel and the fiel');
      expect(typed).not.toHaveBeenCalled();
      expect(dialog().hasAttribute('open')).toBe(false);
    });

    it('closes at once when the only things done were to ignore words', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('The cattel and the fiel');
      await openAtWord('cattel');
      action('ignore').click();
      await atWord('fiel');

      action('close').click();

      expect(part('confirm').hidden).toBe(true);
      expect(dialog().hasAttribute('open')).toBe(false);
    });

    it('treats Escape as Close, and as going back when it answers the question', async () => {
      serve({ misspelt: ['cattel', 'fiel'], suggestions: { cattel: ['cattle'], fiel: ['field'] } });
      textarea('The cattel and the fiel');
      await openAtWord('cattel');
      action('change').click();
      await atWord('fiel');
      const escape = () => {
        const event = new Event('cancel', { cancelable: true });
        dialog().dispatchEvent(event);

        return event;
      };

      const asksFirst = escape();
      expect(asksFirst.defaultPrevented).toBe(true);
      expect(part('confirm').hidden).toBe(false);

      escape();
      expect(part('confirm').hidden).toBe(true);
      expect(shownWord()).toBe('fiel');
      expect(dialog().hasAttribute('open')).toBe(true);
      expect(field().value).toBe('The cattel and the fiel');
    });

    it('can be closed while the words are still being checked, and what comes back is ignored', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      let release;
      server.fetch.mockImplementationOnce(() => new Promise((resolve) => {
        release = () => resolve({ ok: true, status: 200, json: async () => ({ misspelt: ['cattel'] }) });
      }));
      textarea('cattel');
      document.getElementById('spell-button').click();
      expect(message()).toBe('Spell checking in progress...');
      expect(visibleActions()).toEqual(['close']);

      action('close').click();
      release();
      await Promise.resolve();

      expect(dialog().hasAttribute('open')).toBe(false);
      expect(shownWord()).toBeUndefined();
    });

    it('is closed with its state cleared when something else closes it, so that it can be opened again', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      await openAtWord('cattel');

      dialog().close();
      document.getElementById('spell-button').click();

      await vi.waitFor(() => expect(server.to(CHECK_URL)).toHaveLength(2));
    });

    it('is not ended by the close event of an earlier closing that arrives after it has been opened again', async () => {
      serve({ misspelt: ['cattel'], suggestions: { cattel: ['cattle'] } });
      textarea('cattel');
      // A browser may deliver the event some frames after the dialog has closed.
      const delivered = [];
      const close = HTMLDialogElement.prototype.close;
      HTMLDialogElement.prototype.close = function lateClose() {
        if (this.hasAttribute('open')) {
          this.removeAttribute('open');
          delivered.push(() => this.dispatchEvent(new Event('close')));
        }
      };

      try {
        await openAtWord('cattel');
        action('close').click();
        await openAtWord('cattel');

        delivered.forEach((deliver) => deliver());

        expect(dialog().hasAttribute('open')).toBe(true);
        expect(shownWord()).toBe('cattel');
        action('ignore').click();
        expect(message()).toBe('The spell check is complete.');
      } finally {
        HTMLDialogElement.prototype.close = close;
      }
    });
  });
});
