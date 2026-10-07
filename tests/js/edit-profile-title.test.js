import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/edit-profile-title.js';

function buildEditor() {
  document.body.innerHTML = `
    <div class="app-title-toolbar" data-app-title-toolbar="ProfileTitleEditor" role="toolbar" aria-label="Profile title formatting">
      <button type="button" data-app-title-action="undo" aria-label="Undo">Undo</button>
      <button type="button" data-app-title-action="redo" aria-label="Redo">Redo</button>
      <button type="button" data-app-title-action="italic" aria-label="Italic">I</button>
    </div>
    <div id="ProfileTitleEditor" contenteditable="true" data-app-title-hidden-input="ProfileTitle">Initial title</div>
    <input id="ProfileTitle" type="hidden" value="" />
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));

  return {
    editor: document.getElementById('ProfileTitleEditor'),
    hiddenInput: document.getElementById('ProfileTitle'),
    undoButton: document.querySelector('[data-app-title-action="undo"]'),
    redoButton: document.querySelector('[data-app-title-action="redo"]'),
    italicButton: document.querySelector('[data-app-title-action="italic"]')
  };
}

describe('edit-profile-title', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    document.body.innerHTML = '';
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  it('initialises the hidden input from the editor content', () => {
    const { editor, hiddenInput } = buildEditor();

    expect(editor.innerHTML).toBe('Initial title');
    expect(hiddenInput.value).toBe('Initial title');
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
});
