import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const q1 = '11111111-1111-1111-1111-111111111111';
const q2 = '22222222-2222-2222-2222-222222222222';
const q3 = '33333333-3333-3333-3333-333333333333';
const typeId = '99999999-9999-9999-9999-999999999999';
const noteA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const noteB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

let dialog;
let form;
let banner;
let opened;

const els = {};

function answer({ ok = true, redirected = false, body = null } = {}) {
  return Promise.resolve({ ok, redirected, json: () => Promise.resolve(body) });
}

// Lets every promise the script is waiting on run.
function settle() {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function stubFetch(implementation) {
  const fetchMock = vi.fn(implementation);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function choose(select, value) {
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
}

function submit() {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

function openFor(buttonId) {
  document.getElementById(buttonId).click();
}

function box(questionId) {
  return form.querySelector(`[data-reference-question-choice="${questionId}"] input`);
}

function errorTexts() {
  return Array.from(els.errorList.querySelectorAll('li')).map((item) => item.textContent);
}

const notes = [
  { id: noteA, text: 'Smith 2020', html: '<p>Smith 2020 <em>Bovine</em></p>', lastUpdated: 'AQIDBAUGBwg=', questionIds: [q1, q3] },
  { id: noteB, text: 'Jones 2019', html: '<p>Jones 2019</p>', lastUpdated: 'CAcGBQQDAgE=', questionIds: [] }
];

beforeAll(async () => {
  // jsdom does not implement the dialog methods, so record what the script asks for.
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
    opened.push(this.id);
  };
  HTMLDialogElement.prototype.close = function close() {
    this.removeAttribute('open');
  };

  document.body.innerHTML = `
    <div id="reference-saved-banner" tabindex="-1" hidden><p data-reference-saved-message></p></div>
    <button type="button" id="pencil-1" data-question-references="${q1}" data-question-number="1.1"><img id="pencil-icon" alt="Edit references for 1.1" /></button>
    <button type="button" id="pencil-2" data-question-references="${q2}" data-question-number="1.2">Edit</button>
    <button type="button" id="elsewhere">Elsewhere</button>
    <dialog id="question-reference-dialog"
            data-notes-url="/SurveillanceProfiles/QuestionReferences/p?section=s&amp;handler=Notes"
            data-error-mode="Select an action" data-error-type="Select a reference type" data-error-text="Enter the reference text"
            data-error-note="Select a reference" data-error-load="We could not load the references. Try again later."
            data-error-save="Saving reference was unsuccessful: We could not complete the request. Try again later.">
      <h2>Add/Select a reference to question <span data-reference-question-number></span></h2>
      <form method="post" action="/SurveillanceProfiles/QuestionReferences/p?section=s&amp;handler=Save" data-reference-form>
        <div class="govuk-error-summary" tabindex="-1" data-reference-errors hidden><ul data-reference-error-list></ul></div>
        <input type="hidden" name="__RequestVerificationToken" value="token" />
        <input type="hidden" name="Input.QuestionId" value="" data-reference-question />
        <input type="hidden" name="Input.LastUpdated" value="" data-reference-last-updated />
        <div data-reference-field-group>
          <label for="reference-mode">Choose an action</label>
          <select id="reference-mode" class="govuk-select" name="Input.Mode" data-reference-mode>
            <option value="">Please select...</option><option value="add">Add a reference</option><option value="select">Select a reference</option>
          </select>
        </div>
        <div data-reference-field-group data-reference-type-group hidden>
          <label for="reference-type">Choose a reference type</label>
          <select id="reference-type" class="govuk-select" name="Input.NoteTypeId" data-reference-type>
            <option value="">Please select...</option><option value="${typeId}">Scientific paper reference</option>
          </select>
        </div>
        <div data-reference-field-group data-reference-add-group hidden>
          <label for="reference-note-text">Reference text</label>
          <textarea id="reference-note-text" name="Input.NoteText" data-reference-text></textarea>
        </div>
        <div data-reference-field-group data-reference-select-group hidden>
          <label for="reference-note">Select a reference</label>
          <select id="reference-note" class="govuk-select" name="Input.NoteId" data-reference-note><option value="">Please select...</option></select>
          <div data-reference-current-note hidden></div>
        </div>
        <div data-reference-question-choice="${q1}"><input type="checkbox" name="Input.QuestionIds" value="${q1}" /></div>
        <div data-reference-question-choice="${q2}"><input type="checkbox" name="Input.QuestionIds" value="${q2}" /></div>
        <div data-reference-question-choice="${q3}"><input type="checkbox" name="Input.QuestionIds" value="${q3}" /></div>
        <button type="submit" data-reference-save>Save</button>
      </form>
    </dialog>`;

  dialog = document.getElementById('question-reference-dialog');
  form = dialog.querySelector('[data-reference-form]');
  banner = document.getElementById('reference-saved-banner');
  els.mode = form.querySelector('[data-reference-mode]');
  els.type = form.querySelector('[data-reference-type]');
  els.note = form.querySelector('[data-reference-note]');
  els.text = form.querySelector('[data-reference-text]');
  els.question = form.querySelector('[data-reference-question]');
  els.lastUpdated = form.querySelector('[data-reference-last-updated]');
  els.currentNote = form.querySelector('[data-reference-current-note]');
  els.summary = form.querySelector('[data-reference-errors]');
  els.errorList = form.querySelector('[data-reference-error-list]');
  els.save = form.querySelector('[data-reference-save]');
  els.typeGroup = form.querySelector('[data-reference-type-group]');
  els.addGroup = form.querySelector('[data-reference-add-group]');
  els.selectGroup = form.querySelector('[data-reference-select-group]');

  await import('../../src/CDC.Web/wwwroot/js/question-references.js');
});

beforeEach(() => {
  opened = [];
  dialog.removeAttribute('open');
  stubFetch(() => answer({ body: [] }));
  openFor('pencil-1');
  opened = [];
});

describe('question-references opening', () => {
  it('opens the dialog for the question whose edit references icon was used', () => {
    dialog.removeAttribute('open');
    banner.hidden = false;

    openFor('pencil-2');

    expect(opened).toEqual(['question-reference-dialog']);
    expect(dialog.querySelector('[data-reference-question-number]').textContent).toBe('1.2');
    expect(els.question.value).toBe(q2);
    expect(banner.hidden).toBe(true);
  });

  it('also answers a click on the icon inside the button', () => {
    dialog.removeAttribute('open');

    document.getElementById('pencil-icon').click();

    expect(opened).toEqual(['question-reference-dialog']);
    expect(els.question.value).toBe(q1);
  });

  it('does nothing for a click anywhere else', () => {
    dialog.removeAttribute('open');

    document.getElementById('elsewhere').click();

    expect(opened).toEqual([]);
  });

  it('does not open the dialog again when it is already open', () => {
    openFor('pencil-2');

    expect(opened).toEqual([]);
    expect(els.question.value).toBe(q2);
  });

  it('applies the reference to the question it is for, so does not offer that question with the others', () => {
    expect(box(q1).disabled).toBe(true);
    expect(box(q1).closest('[data-reference-question-choice]').hidden).toBe(true);
    expect(box(q2).disabled).toBe(false);
    expect(box(q2).closest('[data-reference-question-choice]').hidden).toBe(false);

    openFor('pencil-2');

    expect(box(q1).disabled).toBe(false);
    expect(box(q1).closest('[data-reference-question-choice]').hidden).toBe(false);
    expect(box(q2).disabled).toBe(true);
    expect(box(q2).closest('[data-reference-question-choice]').hidden).toBe(true);
  });

  it('starts afresh each time it opens', async () => {
    stubFetch(() => answer({ body: notes }));
    choose(els.mode, 'select');
    choose(els.type, typeId);
    await settle();
    choose(els.note, noteA);
    els.text.value = 'Left over';
    submit();
    expect(els.summary.hidden).toBe(true);

    openFor('pencil-2');

    expect(els.mode.value).toBe('');
    expect(els.type.value).toBe('');
    expect(els.text.value).toBe('');
    expect(els.note.options.length).toBe(1);
    expect(els.lastUpdated.value).toBe('');
    expect(els.currentNote.hidden).toBe(true);
    expect(box(q3).checked).toBe(false);
    expect(els.typeGroup.hidden).toBe(true);
    expect(els.addGroup.hidden).toBe(true);
    expect(els.selectGroup.hidden).toBe(true);
  });

  it('clears the editor the rich text script puts in place of the text area', () => {
    const editor = document.createElement('div');
    editor.id = `${els.text.id}-editor`;
    editor.textContent = 'Typed earlier';
    els.text.before(editor);

    openFor('pencil-2');

    expect(editor.childNodes.length).toBe(0);
    editor.remove();
  });
});

describe('question-references choices', () => {
  it('asks for the type once an action is chosen', () => {
    choose(els.mode, 'add');

    expect(els.typeGroup.hidden).toBe(false);
    expect(els.addGroup.hidden).toBe(true);
    expect(els.selectGroup.hidden).toBe(true);
  });

  it('asks for the text of a new reference once its type is chosen', () => {
    choose(els.mode, 'add');
    choose(els.type, typeId);

    expect(els.addGroup.hidden).toBe(false);
    expect(els.selectGroup.hidden).toBe(true);
  });

  it('does not read the references of the version when adding one', () => {
    const fetchMock = stubFetch(() => answer({ body: notes }));

    choose(els.mode, 'add');
    choose(els.type, typeId);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('offers to select a reference, and reads the version\'s references of the type when it is chosen', async () => {
    const fetchMock = stubFetch(() => answer({ body: notes }));

    choose(els.mode, 'select');

    expect(els.selectGroup.hidden).toBe(false);
    expect(els.addGroup.hidden).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();

    choose(els.type, typeId);
    await settle();

    const url = new URL(fetchMock.mock.calls[0][0]);
    expect(url.pathname).toBe('/SurveillanceProfiles/QuestionReferences/p');
    expect(url.searchParams.get('handler')).toBe('Notes');
    expect(url.searchParams.get('section')).toBe('s');
    expect(url.searchParams.get('noteTypeId')).toBe(typeId);
    expect(Array.from(els.note.options).map((option) => option.textContent)).toEqual(['Please select...', 'Smith 2020', 'Jones 2019']);
    expect(Array.from(els.note.options).map((option) => option.value)).toEqual(['', noteA, noteB]);
  });

  it('reads the references when the action is changed to select after the type was chosen', async () => {
    const fetchMock = stubFetch(() => answer({ body: notes }));
    choose(els.mode, 'add');
    choose(els.type, typeId);

    choose(els.mode, 'select');
    await settle();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(els.note.options.length).toBe(3);
  });

  it('shows the reference selected, and ticks the other questions it already applies to', async () => {
    stubFetch(() => answer({ body: notes }));
    choose(els.mode, 'select');
    choose(els.type, typeId);
    await settle();

    choose(els.note, noteA);

    expect(els.currentNote.hidden).toBe(false);
    expect(els.currentNote.querySelector('em').textContent).toBe('Bovine');
    expect(els.lastUpdated.value).toBe('AQIDBAUGBwg=');
    // The question the dialog is for is never ticked with the others.
    expect(box(q1).checked).toBe(false);
    expect(box(q2).checked).toBe(false);
    expect(box(q3).checked).toBe(true);

    choose(els.note, noteB);

    expect(els.currentNote.textContent).toBe('Jones 2019');
    expect(els.lastUpdated.value).toBe('CAcGBQQDAgE=');
    expect(box(q3).checked).toBe(false);

    choose(els.note, '');

    expect(els.currentNote.hidden).toBe(true);
    expect(els.currentNote.childNodes.length).toBe(0);
    expect(els.lastUpdated.value).toBe('');
  });

  it('starts the choice again when the type or the action changes', async () => {
    stubFetch(() => answer({ body: notes }));
    choose(els.mode, 'select');
    choose(els.type, typeId);
    await settle();
    choose(els.note, noteA);
    box(q2).checked = true;

    choose(els.type, '');

    expect(els.note.options.length).toBe(1);
    expect(els.currentNote.hidden).toBe(true);
    expect(box(q2).checked).toBe(false);
    expect(box(q3).checked).toBe(false);
  });

  it('shows the references of the last type chosen, when an earlier answer arrives late', async () => {
    let releaseFirst;
    const first = new Promise((resolve) => { releaseFirst = resolve; });
    let calls = 0;
    stubFetch(() => {
      calls += 1;
      return calls === 1 ? first : answer({ body: [notes[1]] });
    });
    choose(els.mode, 'select');
    choose(els.type, typeId);
    choose(els.type, '');
    choose(els.type, typeId);
    await settle();

    releaseFirst({ ok: true, redirected: false, json: () => Promise.resolve(notes) });
    await settle();

    expect(Array.from(els.note.options).map((option) => option.textContent)).toEqual(['Please select...', 'Jones 2019']);
  });

  it.each([
    ['CDC.Web could not read them', () => answer({ ok: false })],
    ['the session has ended and the browser was sent to sign in', () => answer({ redirected: true, body: [] })],
    ['the request could not be made', () => Promise.reject(new TypeError('Failed to fetch'))]
  ])('says the references could not be loaded when %s', async (_reason, implementation) => {
    stubFetch(implementation);
    choose(els.mode, 'select');

    choose(els.type, typeId);
    await settle();

    expect(els.summary.hidden).toBe(false);
    expect(errorTexts()).toEqual(['We could not load the references. Try again later.']);
    expect(els.errorList.querySelector('a')).toBeNull();
    expect(els.note.options.length).toBe(1);
  });
});

describe('question-references validation', () => {
  it('asks for an action, saving nothing', () => {
    const fetchMock = stubFetch(() => answer());
    const focus = vi.spyOn(els.summary, 'focus');

    submit();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(els.summary.hidden).toBe(false);
    expect(focus).toHaveBeenCalled();
    expect(errorTexts()).toEqual(['Select an action']);
    const link = els.errorList.querySelector('a');
    expect(link.getAttribute('href')).toBe('#reference-mode');
    // The field says so too, as the Design System asks.
    const message = form.querySelector('#reference-mode-error');
    expect(message.textContent).toContain('Select an action');
    expect(els.mode.getAttribute('aria-describedby')).toBe('reference-mode-error');
    expect(els.mode.classList.contains('govuk-select--error')).toBe(true);
    expect(els.mode.closest('[data-reference-field-group]').classList.contains('govuk-form-group--error')).toBe(true);
  });

  it('asks for the type of reference, as well as the reference when selecting one', () => {
    stubFetch(() => answer());
    choose(els.mode, 'select');

    submit();

    expect(errorTexts()).toEqual(['Select a reference type', 'Select a reference']);
  });

  it.each(['', '   ', '<p></p>', '<p> <br> </p>'])('asks for the text of a new reference that has none (%j)', (text) => {
    stubFetch(() => answer());
    choose(els.mode, 'add');
    choose(els.type, typeId);
    els.text.value = text;

    submit();

    expect(errorTexts()).toEqual(['Enter the reference text']);
    expect(els.errorList.querySelector('a').getAttribute('href')).toBe('#reference-note-text');
    expect(els.text.classList.contains('govuk-textarea--error')).toBe(true);
  });

  it('does not ask for the text of a new reference before its type is chosen', () => {
    stubFetch(() => answer());
    choose(els.mode, 'add');

    submit();

    expect(errorTexts()).toEqual(['Select a reference type']);
  });

  it('asks about the editor the rich text script made, when there is one', () => {
    stubFetch(() => answer());
    const editor = document.createElement('div');
    editor.id = `${els.text.id}-editor`;
    editor.tabIndex = 0;
    els.text.before(editor);
    choose(els.mode, 'add');
    choose(els.type, typeId);

    submit();

    expect(els.errorList.querySelector('a').getAttribute('href')).toBe('#reference-note-text-editor');
    editor.remove();
  });

  it('asks for the reference when none is selected', () => {
    stubFetch(() => answer());
    choose(els.mode, 'select');
    choose(els.type, typeId);

    submit();

    expect(errorTexts()).toEqual(['Select a reference']);
  });

  it('moves to the field an error is about when its link is used', () => {
    stubFetch(() => answer());
    submit();
    const focus = vi.spyOn(els.mode, 'focus');
    const link = els.errorList.querySelector('a');
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });

    link.dispatchEvent(click);

    expect(click.defaultPrevented).toBe(true);
    expect(focus).toHaveBeenCalled();
  });

  it('leaves a click on anything else in the summary alone', () => {
    stubFetch(() => answer());
    submit();
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });

    els.errorList.dispatchEvent(click);

    expect(click.defaultPrevented).toBe(false);
  });

  it('replaces the errors of an earlier attempt with those of the next', () => {
    stubFetch(() => answer());
    submit();
    choose(els.mode, 'select');

    submit();

    expect(errorTexts()).toEqual(['Select a reference type', 'Select a reference']);
    expect(form.querySelector('#reference-mode-error')).toBeNull();
    expect(els.mode.classList.contains('govuk-select--error')).toBe(false);
    expect(els.mode.hasAttribute('aria-describedby')).toBe(false);
  });

  it.each([
    ['action', () => choose(els.mode, 'add')],
    ['type', () => choose(els.type, typeId)],
    ['reference', () => choose(els.note, '')]
  ])('forgets the errors of an earlier attempt once the %s changes, as what is asked changes with it', (_choice, change) => {
    stubFetch(() => answer());
    submit();
    expect(els.summary.hidden).toBe(false);

    change();

    expect(els.summary.hidden).toBe(true);
    expect(errorTexts()).toEqual([]);
    expect(form.querySelector('.govuk-error-message')).toBeNull();
  });
});

describe('question-references saving', () => {
  function fillNewReference() {
    choose(els.mode, 'add');
    choose(els.type, typeId);
    els.text.value = '<p>New paper</p>';
    box(q3).checked = true;
  }

  it('posts a new reference and tells the user it was saved, leaving the page as it is', async () => {
    const fetchMock = stubFetch(() => answer({ body: { succeeded: true, message: 'Reference was saved successfully', errors: [] } }));
    const focus = vi.spyOn(banner, 'focus');
    fillNewReference();

    submit();
    await settle();

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toContain('/SurveillanceProfiles/QuestionReferences/p?section=s&handler=Save');
    expect(options.method).toBe('POST');
    expect(options.credentials).toBe('same-origin');
    const data = options.body;
    expect(data.get('__RequestVerificationToken')).toBe('token');
    expect(data.get('Input.QuestionId')).toBe(q1);
    expect(data.get('Input.Mode')).toBe('add');
    expect(data.get('Input.NoteTypeId')).toBe(typeId);
    expect(data.get('Input.NoteText')).toBe('<p>New paper</p>');
    expect(data.getAll('Input.QuestionIds')).toEqual([q3]);
    expect(dialog.hasAttribute('open')).toBe(false);
    expect(banner.hidden).toBe(false);
    expect(banner.querySelector('[data-reference-saved-message]').textContent).toBe('Reference was saved successfully');
    expect(focus).toHaveBeenCalled();
  });

  it('posts the selected reference with the row version it was offered with', async () => {
    const fetchMock = vi.fn((url) => (String(url).includes('handler=Notes')
      ? answer({ body: notes })
      : answer({ body: { succeeded: true, message: 'Reference was saved successfully', errors: [] } })));
    vi.stubGlobal('fetch', fetchMock);
    choose(els.mode, 'select');
    choose(els.type, typeId);
    await settle();
    choose(els.note, noteA);

    submit();
    await settle();

    const data = fetchMock.mock.calls[1][1].body;
    expect(data.get('Input.Mode')).toBe('select');
    expect(data.get('Input.NoteId')).toBe(noteA);
    expect(data.get('Input.LastUpdated')).toBe('AQIDBAUGBwg=');
    expect(data.getAll('Input.QuestionIds')).toEqual([q3]);
  });

  it('keeps the dialog open and says why when the reference was not saved', async () => {
    stubFetch(() => answer({ body: { succeeded: false, message: null, errors: ['Saving reference was unsuccessful: Someone else changed it.'] } }));
    fillNewReference();

    submit();
    await settle();

    expect(dialog.hasAttribute('open')).toBe(true);
    expect(banner.hidden).toBe(true);
    expect(els.summary.hidden).toBe(false);
    expect(errorTexts()).toEqual(['Saving reference was unsuccessful: Someone else changed it.']);
    expect(els.save.disabled).toBe(false);
  });

  it.each([
    ['CDC.Web refuses it', () => answer({ ok: false })],
    ['the session has ended and the browser was sent to sign in', () => answer({ redirected: true, body: {} })],
    ['the request could not be made', () => Promise.reject(new TypeError('Failed to fetch'))]
  ])('says the reference was not saved when %s', async (_reason, implementation) => {
    stubFetch(implementation);
    fillNewReference();

    submit();
    await settle();

    expect(dialog.hasAttribute('open')).toBe(true);
    expect(errorTexts()).toEqual(['Saving reference was unsuccessful: We could not complete the request. Try again later.']);
    expect(els.save.disabled).toBe(false);
  });

  it('does not save the reference twice when the dialog is submitted again before the answer', async () => {
    let release;
    const fetchMock = stubFetch(() => new Promise((resolve) => { release = resolve; }));
    fillNewReference();

    submit();
    submit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(els.save.disabled).toBe(true);

    release({ ok: true, redirected: false, json: () => Promise.resolve({ succeeded: true, message: 'Reference was saved successfully', errors: [] }) });
    await settle();

    expect(els.save.disabled).toBe(false);
  });

  it('shows the saved message even when the page has no banner for it', async () => {
    stubFetch(() => answer({ body: { succeeded: true, message: 'Reference was saved successfully', errors: [] } }));
    fillNewReference();
    banner.remove();

    submit();
    await settle();

    expect(dialog.hasAttribute('open')).toBe(false);
    document.body.prepend(banner);
  });
});
