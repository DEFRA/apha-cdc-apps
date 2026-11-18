import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

let dialog;
let questions;
let refresh;
let opened;
let events;

function options() {
  return '<option value="">Please select...</option><option value="a">A</option><option value="b">B</option>';
}

function radios(group, checked) {
  return `
    <input type="radio" name="${group}" id="${group}-yes" value="true" data-relevance-affects="3.2, 3.3"${checked === 'yes' ? ' checked' : ''}>
    <input type="radio" name="${group}" id="${group}-no" value="false" data-relevance-affects="3.2, 3.3"${checked === 'no' ? ' checked' : ''}>`;
}

const get = (id) => document.getElementById(id);

// The script reads the page once, so the page is built once and each test uses controls of its own.
beforeAll(async () => {
  // jsdom does not implement the dialog methods, so record what the script asks for. Closing a dialog fires its close event.
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
    opened.push(this.id);
  };
  HTMLDialogElement.prototype.close = function close() {
    this.removeAttribute('open');
    this.dispatchEvent(new Event('close'));
  };

  document.body.innerHTML = `
    <form id="form">
      <button type="submit" id="refresh" hidden data-relevance-refresh>Refresh the questions</button>
      <select id="standard" data-relevance-affects="2.2, 2.3">${options()}</select>
      <select id="second" data-relevance-affects="6.2">${options()}</select>
      <select id="empty" data-relevance-affects="">${options()}</select>
      <select id="unmarked">${options()}</select>
      ${radios('keep', 'yes')}
      ${radios('none', null)}
      ${radios('later', 'yes')}
      <div data-species-panel>
        <select id="panel-select" data-relevance-affects="5.2, 5.3">${options()}</select>
      </div>
    </form>
    <dialog class="app-modal" id="relevance-confirm-dialog">
      <p>Questions that may be affected are:</p>
      <p><strong data-relevance-questions></strong>.</p>
      <button type="button" id="ok" data-relevance-ok>OK</button>
      <button type="button" id="cancel">Cancel</button>
    </dialog>`;

  dialog = get('relevance-confirm-dialog');
  questions = dialog.querySelector('[data-relevance-questions]');
  refresh = vi.fn((event) => event.preventDefault());
  get('refresh').addEventListener('click', refresh);
  get('cancel').addEventListener('click', () => dialog.close());

  ['app:relevance-confirmed', 'app:relevance-cancelled'].forEach((name) => {
    document.addEventListener(name, (event) => events.push(`${name}:${event.target.id}`));
  });

  await import('../../src/CDC.Web/wwwroot/js/relevance-confirm.js');
});

beforeEach(() => {
  // A test may leave the dialog open, which the script would take for a question still being asked.
  if (dialog.hasAttribute('open')) {
    dialog.close();
  }

  opened = [];
  events = [];
  refresh.mockClear();
  ['standard', 'second', 'empty', 'unmarked', 'panel-select'].forEach((id) => {
    get(id).value = '';
    get(id).setAttribute('data-relevance-held', '');
  });
});

function choose(select, value) {
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
}

function pickRadio(id) {
  const radio = get(id);
  radio.checked = true;
  radio.dispatchEvent(new Event('change', { bubbles: true }));
}

describe('relevance-confirm', () => {
  describe('a drop-down that decides whether other questions apply', () => {
    it('asks the user to confirm a change, naming the questions it may affect', () => {
      choose(get('standard'), 'a');

      expect(opened).toEqual(['relevance-confirm-dialog']);
      expect(dialog.hasAttribute('open')).toBe(true);
      expect(questions.textContent).toBe('2.2, 2.3');
    });

    it('keeps the change and saves the section when OK is pressed, so the questions it makes irrelevant are hidden', () => {
      choose(get('standard'), 'a');

      get('ok').click();

      expect(get('standard').value).toBe('a');
      expect(dialog.hasAttribute('open')).toBe(false);
      expect(refresh).toHaveBeenCalledTimes(1);
      expect(events).toEqual(['app:relevance-confirmed:standard']);
    });

    it('puts the answer back when Cancel is pressed', () => {
      choose(get('standard'), 'b');

      get('cancel').click();

      expect(get('standard').value).toBe('');
      expect(refresh).not.toHaveBeenCalled();
      expect(events).toEqual(['app:relevance-cancelled:standard']);
    });

    it('puts the answer back when the dialog is dismissed with Escape', () => {
      choose(get('standard'), 'b');

      dialog.close();

      expect(get('standard').value).toBe('');
      expect(refresh).not.toHaveBeenCalled();
    });

    it('puts back the answer that was last confirmed, not the first the page held', () => {
      choose(get('second'), 'a');
      get('ok').click();
      refresh.mockClear();

      choose(get('second'), 'b');
      get('cancel').click();

      expect(get('second').value).toBe('a');
      expect(refresh).not.toHaveBeenCalled();
    });

    it('does not ask a second time while the dialog is open', () => {
      choose(get('standard'), 'a');
      get('standard').value = 'b';
      get('standard').dispatchEvent(new Event('change', { bubbles: true }));

      expect(opened).toEqual(['relevance-confirm-dialog']);
    });

    it.each([['empty'], ['unmarked']])('does not ask about a drop-down that names no questions (%s)', (id) => {
      choose(get(id), 'a');

      expect(opened).toEqual([]);
      expect(refresh).not.toHaveBeenCalled();
    });
  });

  describe('a drop-down in the panel that edits a species answers', () => {
    it('asks, but leaves it to the panel to save the section when its own OK is pressed', () => {
      choose(get('panel-select'), 'a');
      expect(questions.textContent).toBe('5.2, 5.3');

      get('ok').click();

      expect(get('panel-select').value).toBe('a');
      expect(events).toEqual(['app:relevance-confirmed:panel-select']);
      expect(refresh).not.toHaveBeenCalled();
    });

    it('puts the answer back as the panel last loaded it, which it holds for a drop-down it fills in turn for each species', () => {
      get('panel-select').value = 'a';
      get('panel-select').setAttribute('data-relevance-held', 'a');

      choose(get('panel-select'), 'b');
      get('cancel').click();

      expect(get('panel-select').value).toBe('a');
    });

    it('puts the answer back to none when Cancel is pressed for a species with no answer', () => {
      choose(get('panel-select'), 'b');

      get('cancel').click();

      expect(get('panel-select').value).toBe('');
    });
  });

  describe('radios that decide whether other questions apply', () => {
    it('asks when another radio is chosen, and puts back the one that was checked when Cancel is pressed', () => {
      pickRadio('keep-no');

      expect(questions.textContent).toBe('3.2, 3.3');

      get('cancel').click();

      expect(get('keep-yes').checked).toBe(true);
      expect(get('keep-no').checked).toBe(false);
      expect(refresh).not.toHaveBeenCalled();
    });

    it('unchecks the radio chosen when none was checked before and Cancel is pressed', () => {
      pickRadio('none-yes');
      get('cancel').click();

      expect(get('none-yes').checked).toBe(false);
      expect(get('none-no').checked).toBe(false);
    });

    it('keeps the choice and saves the section when OK is pressed, and puts back that choice after a later Cancel', () => {
      pickRadio('later-no');
      get('ok').click();

      expect(get('later-no').checked).toBe(true);
      expect(refresh).toHaveBeenCalledTimes(1);

      pickRadio('later-yes');
      get('cancel').click();

      expect(get('later-no').checked).toBe(true);
      expect(get('later-yes').checked).toBe(false);
      expect(refresh).toHaveBeenCalledTimes(1);
    });
  });
});
