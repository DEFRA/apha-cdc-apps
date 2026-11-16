import { describe, expect, it, vi } from 'vitest';

// jsdom does not implement the dialog methods, so record what the script asks for.
const opened = [];

HTMLDialogElement.prototype.showModal = function showModal() {
  this.setAttribute('open', '');
  opened.push(this.id);
};
HTMLDialogElement.prototype.close = function close() {
  this.removeAttribute('open');
};

describe('question-references when the page cannot offer the form', () => {
  it('does nothing on a page without the dialog', async () => {
    document.body.innerHTML = '<button type="button" data-question-references="x" data-question-number="1.1">Edit</button>';

    await import('../../src/CDC.Web/wwwroot/js/question-references.js');
    document.querySelector('button').click();

    expect(opened).toEqual([]);
  });

  it('opens the dialog to say the types of reference could not be loaded, for the question it was opened for', async () => {
    document.body.innerHTML = `
      <div id="reference-saved-banner" hidden></div>
      <button type="button" id="edit" data-question-references="x" data-question-number="2.3">Edit</button>
      <dialog id="question-reference-dialog" data-notes-url="/notes">
        <h2>Add/Select a reference to question <span data-reference-question-number></span></h2>
        <p>We could not load the reference types. Try again later.</p>
      </dialog>`;
    vi.resetModules();
    await import('../../src/CDC.Web/wwwroot/js/question-references.js');

    document.getElementById('edit').click();

    expect(opened).toEqual(['question-reference-dialog']);
    expect(document.querySelector('[data-reference-question-number]').textContent).toBe('2.3');
    expect(document.getElementById('reference-saved-banner').hidden).toBe(true);
  });
});
