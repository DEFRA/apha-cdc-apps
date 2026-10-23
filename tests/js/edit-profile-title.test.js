import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/edit-profile-title.js';

const SERVER_ERROR = 'The profile title is already in use for a different profile';

function buildEditor({ html = 'Initial title', withSaveButton = true, withRequiredError = true, serverError = null } = {}) {
  document.body.innerHTML = `
    <div class="govuk-form-group${serverError ? ' govuk-form-group--error' : ''}">
      ${serverError ? `<p id="ProfileTitle-error" class="govuk-error-message">${serverError}</p>` : ''}
      ${withRequiredError ? '<div aria-live="polite"><p id="ProfileTitle-required-error" class="govuk-error-message" data-app-title-required-error hidden>You must enter a profile title</p></div>' : ''}
      <div class="app-title-toolbar" data-app-title-toolbar="ProfileTitleEditor" role="toolbar" aria-label="Profile title formatting">
        <button type="button" data-app-title-action="undo" aria-label="Undo">Undo</button>
        <button type="button" data-app-title-action="redo" aria-label="Redo">Redo</button>
        <button type="button" data-app-title-action="italic" aria-label="Italic">I</button>
      </div>
      <div id="ProfileTitleEditor" class="govuk-input${serverError ? ' govuk-input--error' : ''}" contenteditable="true"${serverError ? ' aria-describedby="ProfileTitle-error"' : ''} data-app-title-hidden-input="ProfileTitle">${html}</div>
      <input id="ProfileTitle" type="hidden" value="" />
    </div>
    ${withSaveButton ? '<button type="submit" data-app-title-save>Save</button>' : ''}
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));

  return {
    editor: document.getElementById('ProfileTitleEditor'),
    hiddenInput: document.getElementById('ProfileTitle'),
    undoButton: document.querySelector('[data-app-title-action="undo"]'),
    redoButton: document.querySelector('[data-app-title-action="redo"]'),
    italicButton: document.querySelector('[data-app-title-action="italic"]'),
    saveButton: document.querySelector('[data-app-title-save]'),
    group: document.querySelector('.govuk-form-group'),
    requiredError: document.getElementById('ProfileTitle-required-error'),
    serverError: document.getElementById('ProfileTitle-error')
  };
}

function pressKey(editor, key, modifiers = {}) {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...modifiers });
  editor.dispatchEvent(event);

  return event;
}

function pasteText(editor, text) {
  const event = new Event('paste', { bubbles: true, cancelable: true });
  event.clipboardData = { getData: vi.fn(() => text) };
  editor.dispatchEvent(event);

  return event;
}

function typeInto(editor, html) {
  editor.innerHTML = html;
  editor.dispatchEvent(new Event('input', { bubbles: true }));
}

describe('edit-profile-title', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    document.body.innerHTML = '';
    window.getSelection().removeAllRanges();
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  it('initialises the hidden input from the editor content', () => {
    const { editor, hiddenInput } = buildEditor();

    expect(editor.innerHTML).toBe('Initial title');
    expect(hiddenInput.value).toBe('Initial title');
  });

  it('keeps the title on a single line by ignoring Enter', () => {
    const { editor } = buildEditor();
    const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });

    editor.dispatchEvent(enter);

    expect(enter.defaultPrevented).toBe(true);
  });

  it('leaves other keys alone', () => {
    const { editor } = buildEditor();
    const letter = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });

    editor.dispatchEvent(letter);

    expect(letter.defaultPrevented).toBe(false);
  });

  it('exposes the title editor as a single-line textbox', () => {
    const { editor } = buildEditor();

    expect(editor.getAttribute('role')).toBe('textbox');
    expect(editor.getAttribute('aria-multiline')).toBe('false');
  });

  it('tracks undo and redo history and keeps the hidden value in sync', async () => {
    const { editor, hiddenInput, undoButton, redoButton } = buildEditor();

    editor.innerHTML = 'Alpha';
    editor.dispatchEvent(new Event('input', { bubbles: true }));
    await vi.advanceTimersByTimeAsync(300);
    expect(hiddenInput.value).toBe('Alpha');

    editor.innerHTML = 'Bravo';
    editor.dispatchEvent(new Event('input', { bubbles: true }));
    await vi.advanceTimersByTimeAsync(300);
    expect(hiddenInput.value).toBe('Bravo');
    expect(undoButton.disabled).toBe(false);

    undoButton.click();
    expect(editor.innerHTML).toBe('Alpha');
    expect(hiddenInput.value).toBe('Alpha');

    redoButton.click();
    expect(editor.innerHTML).toBe('Bravo');
    expect(hiddenInput.value).toBe('Bravo');
  });

  it('applies italics through the editor fallback and updates the hidden input', () => {
    const { editor, hiddenInput, italicButton } = buildEditor();

    document.execCommand = vi.fn((commandName) => {
      if (commandName === 'italic') {
        editor.innerHTML = '<i>Initial title</i>';
        return true;
      }

      return false;
    });

    italicButton.click();

    expect(document.execCommand).toHaveBeenCalledWith('italic');
    expect(editor.innerHTML).toBe('<i>Initial title</i>');
    expect(hiddenInput.value).toBe('<i>Initial title</i>');
  });

  describe('formatting shortcuts', () => {
    it.each([
      ['b', { ctrlKey: true }],
      ['B', { ctrlKey: true }],
      ['u', { metaKey: true }],
      ['U', { metaKey: true }]
    ])('blocks the bold and underline shortcut %s, as only italics are allowed', (key, modifiers) => {
      const { editor } = buildEditor();

      expect(pressKey(editor, key, modifiers).defaultPrevented).toBe(true);
    });

    it.each(['i', 'z', 'y'])('leaves Ctrl+%s alone', (key) => {
      const { editor } = buildEditor();

      expect(pressKey(editor, key, { ctrlKey: true }).defaultPrevented).toBe(false);
    });
  });

  describe('the length of the title', () => {
    it('stops taking characters once the title fills the column', () => {
      const { editor } = buildEditor({ html: 'a'.repeat(255) });

      expect(pressKey(editor, 'b').defaultPrevented).toBe(true);
    });

    it('counts the italic tags towards the column', () => {
      const { editor } = buildEditor({ html: `<i>${'a'.repeat(248)}</i>` });

      expect(pressKey(editor, 'b').defaultPrevented).toBe(true);
    });

    it('still takes a character when there is room for one', () => {
      const { editor } = buildEditor({ html: 'a'.repeat(254) });

      expect(pressKey(editor, 'b').defaultPrevented).toBe(false);
    });

    it('still lets a full title be deleted from', () => {
      const { editor } = buildEditor({ html: 'a'.repeat(255) });

      expect(pressKey(editor, 'Backspace').defaultPrevented).toBe(false);
    });

    it('still lets a selection of a full title be replaced', () => {
      const { editor } = buildEditor({ html: 'a'.repeat(255) });
      window.getSelection().selectAllChildren(editor);

      expect(pressKey(editor, 'b').defaultPrevented).toBe(false);
    });

    it('cuts a title that grows past the column back to plain text that fits', () => {
      const { editor, hiddenInput } = buildEditor();

      typeInto(editor, `<i>${'a'.repeat(300)}</i>`);

      expect(editor.innerHTML).toBe('a'.repeat(255));
      expect(hiddenInput.value).toBe('a'.repeat(255));
    });

    it('counts characters as the editor stores them when cutting the title back', () => {
      const { editor, hiddenInput } = buildEditor();

      typeInto(editor, '&amp;'.repeat(100));

      expect(editor.innerHTML).toBe('&amp;'.repeat(51));
      expect(hiddenInput.value).toBe('&amp;'.repeat(51));
    });

    it('puts the caret at the end of the title it cut back', () => {
      const { editor } = buildEditor();

      typeInto(editor, 'a'.repeat(300));

      const selection = window.getSelection();
      expect(selection.isCollapsed).toBe(true);
      expect(selection.focusNode).toBe(editor);
      expect(selection.focusOffset).toBe(editor.childNodes.length);
    });

    it('drops the formatting when applying italics pushes the title past the column', () => {
      const { editor, hiddenInput, italicButton } = buildEditor({ html: 'a'.repeat(254) });
      document.execCommand = vi.fn(() => {
        editor.innerHTML = `<i>${'a'.repeat(254)}</i>`;
        return true;
      });

      italicButton.click();

      expect(editor.innerHTML).toBe('a'.repeat(254));
      expect(hiddenInput.value).toBe('a'.repeat(254));
    });
  });

  describe('pasting', () => {
    it.each([
      ['Caf\u00e9\r\nBovine', 'Caf Bovine'],
      ['\u2018quoted\u2019 title', 'quoted title'],
      ['two\nlines\r\nhere', 'two lines here']
    ])('inserts %j as the plain text %j', (pasted, inserted) => {
      const { editor } = buildEditor();
      document.execCommand = vi.fn(() => true);

      const paste = pasteText(editor, pasted);

      expect(paste.defaultPrevented).toBe(true);
      expect(document.execCommand).toHaveBeenCalledWith('insertText', false, inserted);
    });

    it('takes the plain text of the clipboard, never its formatting', () => {
      const { editor } = buildEditor();
      document.execCommand = vi.fn(() => true);

      const paste = pasteText(editor, 'Anthrax');

      expect(paste.clipboardData.getData).toHaveBeenCalledWith('text/plain');
    });

    it('does nothing when the browser cannot insert text', () => {
      const { editor, hiddenInput } = buildEditor();
      document.execCommand = undefined;

      const paste = pasteText(editor, 'Anthrax');

      expect(paste.defaultPrevented).toBe(true);
      expect(hiddenInput.value).toBe('Initial title');
    });
  });

  describe('the Save button', () => {
    it('is hidden while the title has no text, and shown again when text returns', () => {
      const { editor, saveButton } = buildEditor();

      typeInto(editor, '');
      expect(saveButton.style.visibility).toBe('hidden');

      typeInto(editor, 'Anthrax');
      expect(saveButton.style.visibility).toBe('visible');
    });

    it.each(['<br>', '<i></i>', '&nbsp;&nbsp;', '   '])('is hidden for a title of only %j', (html) => {
      const { editor, saveButton } = buildEditor();

      typeInto(editor, html);

      expect(saveButton.style.visibility).toBe('hidden');
    });

    it('is not hidden when the page loads, as in legacy', () => {
      const { saveButton } = buildEditor({ html: '' });

      expect(saveButton.style.visibility).toBe('');
    });

    it('is hidden when the editor loses focus with no text', () => {
      const { editor, saveButton } = buildEditor();

      editor.innerHTML = '';
      editor.dispatchEvent(new Event('blur'));

      expect(saveButton.style.visibility).toBe('hidden');
    });

    it('is hidden when undo returns to a title with no text', async () => {
      const { editor, undoButton, saveButton } = buildEditor({ html: '' });

      typeInto(editor, 'Alpha');
      await vi.advanceTimersByTimeAsync(300);
      expect(saveButton.style.visibility).toBe('visible');

      undoButton.click();

      expect(editor.innerHTML).toBe('');
      expect(saveButton.style.visibility).toBe('hidden');
    });

    it('is left alone on a page that has none to hide', () => {
      const { editor, hiddenInput } = buildEditor({ withSaveButton: false });

      typeInto(editor, '');

      expect(hiddenInput.value).toBe('');
    });
  });

  describe('the required message', () => {
    function expectShown({ editor, group, requiredError }) {
      expect(requiredError.hidden).toBe(false);
      expect(group.classList.contains('govuk-form-group--error')).toBe(true);
      expect(editor.classList.contains('govuk-input--error')).toBe(true);
      expect(editor.getAttribute('aria-describedby')).toBe('ProfileTitle-required-error');
    }

    it('is not shown when the page loads, even with no text, as in legacy', () => {
      const { requiredError } = buildEditor({ html: '' });

      expect(requiredError.hidden).toBe(true);
    });

    it('is shown with the error styling once the title is emptied, and goes when text returns', () => {
      const page = buildEditor();
      const { editor, group, requiredError } = page;

      typeInto(editor, '');
      expectShown(page);

      typeInto(editor, 'Anthrax');
      expect(requiredError.hidden).toBe(true);
      expect(group.classList.contains('govuk-form-group--error')).toBe(false);
      expect(editor.classList.contains('govuk-input--error')).toBe(false);
      expect(editor.hasAttribute('aria-describedby')).toBe(false);
    });

    it.each(['<br>', '<i></i>', '&nbsp;&nbsp;', '   '])('is shown for a title of only %j', (html) => {
      const page = buildEditor();

      typeInto(page.editor, html);

      expectShown(page);
    });

    it('is shown when the editor loses focus with no text', () => {
      const page = buildEditor();

      page.editor.innerHTML = '';
      page.editor.dispatchEvent(new Event('blur'));

      expectShown(page);
    });

    it('is shown when undo returns to a title with no text', async () => {
      const page = buildEditor({ html: '' });

      typeInto(page.editor, 'Alpha');
      await vi.advanceTimersByTimeAsync(300);
      expect(page.requiredError.hidden).toBe(true);

      page.undoButton.click();

      expectShown(page);
    });

    it('stands in for the message the server gave for the title while the title is empty, then gives it back', () => {
      const page = buildEditor({ serverError: SERVER_ERROR });
      const { editor, group, requiredError, serverError } = page;

      typeInto(editor, '');
      expectShown(page);
      expect(serverError.hidden).toBe(true);

      typeInto(editor, 'Anthrax');
      expect(requiredError.hidden).toBe(true);
      expect(serverError.hidden).toBe(false);
      expect(group.classList.contains('govuk-form-group--error')).toBe(true);
      expect(editor.classList.contains('govuk-input--error')).toBe(true);
      expect(editor.getAttribute('aria-describedby')).toBe('ProfileTitle-error');
    });

    it('is left alone on a page that has none', () => {
      const { editor, group, hiddenInput } = buildEditor({ withRequiredError: false });

      typeInto(editor, '');

      expect(hiddenInput.value).toBe('');
      expect(group.classList.contains('govuk-form-group--error')).toBe(false);
      expect(editor.hasAttribute('aria-describedby')).toBe(false);
    });
  });
});
