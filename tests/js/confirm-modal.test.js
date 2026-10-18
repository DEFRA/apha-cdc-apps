import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

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

describe('confirm-modal when the page is left and restored from the back/forward cache', () => {
  const reload = vi.fn();

  function leavePage() {
    window.dispatchEvent(new Event('pagehide'));
  }

  function showPage(persisted) {
    const event = new Event('pageshow');
    Object.defineProperty(event, 'persisted', { value: persisted });
    window.dispatchEvent(event);
  }

  beforeEach(() => {
    document.querySelectorAll('dialog').forEach((dialog) => dialog.removeAttribute('open'));
    document.documentElement.removeAttribute('data-left-with-dialog-open');
    reload.mockClear();
    vi.stubGlobal('location', { reload });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('closes an open dialog when the page is hidden', () => {
    document.getElementById('trigger').click();
    expect(document.getElementById('confirm-dialog').hasAttribute('open')).toBe(true);

    leavePage();

    expect(document.getElementById('confirm-dialog').hasAttribute('open')).toBe(false);
  });

  it('reloads a restored page that was left with a dialog open', () => {
    document.getElementById('trigger').click();

    leavePage();
    showPage(true);

    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('does not reload a restored page that was left with no dialog open', () => {
    leavePage();
    showPage(true);

    expect(reload).not.toHaveBeenCalled();
  });

  it('does not reload a page that was not restored from the cache', () => {
    document.getElementById('trigger').click();

    leavePage();
    showPage(false);

    expect(reload).not.toHaveBeenCalled();
  });
});
