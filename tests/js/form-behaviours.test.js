import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/form-behaviours.js';

function change($control) {
  $control.dispatchEvent(new Event('change', { bubbles: true }));
}

function submit($form) {
  const event = new Event('submit', { bubbles: true, cancelable: true });
  $form.dispatchEvent(event);
  return event;
}

describe('form-behaviours', () => {
  let submitted;

  beforeEach(() => {
    submitted = [];
    document.body.innerHTML = '';
    vi.spyOn(HTMLFormElement.prototype, 'requestSubmit').mockImplementation(function () {
      submitted.push(this.id);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('submits the form of a control marked to submit on change', () => {
    document.body.innerHTML = `
      <form id="size-form"><select id="size" data-app-submit-on-change><option>10</option><option>20</option></select></form>`;

    change(document.getElementById('size'));

    expect(submitted).toEqual(['size-form']);
  });

  it('submits the form a control names with its form attribute, from outside it', () => {
    document.body.innerHTML = `
      <form id="search-form"></form>
      <select id="sort" form="search-form" data-app-submit-on-change><option>Title</option></select>`;

    change(document.getElementById('sort'));

    expect(submitted).toEqual(['search-form']);
  });

  it('goes back to page 1 before submitting a new page size', () => {
    document.body.innerHTML = `
      <form id="notes-form">
        <input id="PageNumber" name="pageNumber" value="4" />
        <select id="PageSize" data-app-submit-on-change data-app-reset-page="PageNumber"><option>10</option></select>
      </form>`;

    change(document.getElementById('PageSize'));

    expect(document.getElementById('PageNumber').value).toBe('1');
    expect(submitted).toEqual(['notes-form']);
  });

  it('leaves the page alone when the field it names is not there', () => {
    document.body.innerHTML = `
      <form id="notes-form"><select id="PageSize" data-app-submit-on-change data-app-reset-page="Missing"></select></form>`;

    change(document.getElementById('PageSize'));

    expect(submitted).toEqual(['notes-form']);
  });

  it('covers controls added to the page after it loaded', () => {
    document.body.innerHTML = '<div id="results"></div>';
    document.getElementById('results').innerHTML = `
      <form id="results-form"><select id="size" data-app-submit-on-change></select></form>`;

    change(document.getElementById('size'));

    expect(submitted).toEqual(['results-form']);
  });

  it('does nothing for an unmarked control, or a marked one with no form', () => {
    document.body.innerHTML = `
      <form id="plain-form"><select id="plain"></select></form>
      <select id="orphan" data-app-submit-on-change></select>`;

    change(document.getElementById('plain'));
    change(document.getElementById('orphan'));

    expect(submitted).toEqual([]);
  });

  it('asks the question on a confirm form, and submits only when the user agrees', () => {
    document.body.innerHTML = '<form id="delete-form" data-app-confirm="Delete this comment?"></form>';
    const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);
    const $form = document.getElementById('delete-form');

    expect(submit($form).defaultPrevented).toBe(true);
    expect(submit($form).defaultPrevented).toBe(false);
    expect(confirm).toHaveBeenCalledWith('Delete this comment?');
  });

  it('has the cancelled submit already prevented when the form\'s own submit listeners run', () => {
    document.body.innerHTML = '<form id="delete-form" data-app-confirm="Delete this comment?"></form>';
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const $form = document.getElementById('delete-form');
    let seenPrevented = null;
    $form.addEventListener('submit', (event) => { seenPrevented = event.defaultPrevented; });

    submit($form);

    expect(seenPrevented).toBe(true);
  });

  it('never asks on a form with no question', () => {
    document.body.innerHTML = '<form id="save-form"></form>';
    const confirm = vi.spyOn(window, 'confirm');

    expect(submit(document.getElementById('save-form')).defaultPrevented).toBe(false);
    expect(confirm).not.toHaveBeenCalled();
  });
});
