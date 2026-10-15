import { beforeAll, describe, expect, it, vi } from 'vitest';

// jsdom does not implement the dialog methods, so record what the script asks for.
const opened = [];

beforeAll(async () => {
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
    opened.push(this.id);
  };
  HTMLDialogElement.prototype.close = function close() {
    this.removeAttribute('open');
  };

  document.body.innerHTML = `
    <button type="button" id="trigger" data-confirm-target="#confirm-dialog">Open</button>
    <dialog class="app-modal" id="confirm-dialog">
      <button type="button" id="cancel" data-confirm-close>Cancel</button>
    </dialog>
    <dialog class="app-modal" id="server-opened" data-open-on-load="true"></dialog>
    <dialog class="app-modal" id="server-not-opened" data-open-on-load="false"></dialog>
    <dialog class="app-modal" id="plain"></dialog>
  `;

  await import('../../src/CDC.Web/wwwroot/js/confirm-modal.js');
});

describe('confirm-modal', () => {
  it('opens, when the page loads, only the dialogs the server marked to open', () => {
    expect(opened).toEqual(['server-opened']);
    expect(document.getElementById('server-not-opened').hasAttribute('open')).toBe(false);
    expect(document.getElementById('plain').hasAttribute('open')).toBe(false);
  });

  it('opens a dialog from its trigger and closes it from its cancel button', () => {
    const dialog = document.getElementById('confirm-dialog');
    const showModal = vi.spyOn(dialog, 'showModal');

    document.getElementById('trigger').click();

    expect(showModal).toHaveBeenCalledTimes(1);
    expect(dialog.hasAttribute('open')).toBe(true);

    document.getElementById('cancel').click();

    expect(dialog.hasAttribute('open')).toBe(false);
  });
});
