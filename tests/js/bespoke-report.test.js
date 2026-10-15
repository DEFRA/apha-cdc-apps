import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

function buildPage() {
  document.body.innerHTML = `
    <form>
      <button type="button" data-bespoke-save-as>Save template as</button>
      <dialog id="save-as-dialog">
        <input id="NewTemplateTitle" name="NewTemplateTitle" type="text" data-bespoke-title-input value="Alpha">
        <button type="submit" id="save-as-save">Save</button>
      </dialog>
      <dialog id="edit-title-dialog">
        <input id="EditedTemplateTitle" name="EditedTemplateTitle" type="text" data-bespoke-title-input value="Alpha">
        <button type="submit" id="edit-save">Save</button>
      </dialog>
      <input id="other" type="text" value="">
    </form>
  `;
}

function type(input, value) {
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

function pressEnter(input) {
  const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
  input.dispatchEvent(event);

  return event;
}

describe('bespoke report page script', () => {
  beforeAll(async () => {
    // The script reads the Save template as button and title box when it loads, so the page has to exist first.
    buildPage();
    await import('../../src/CDC.Web/wwwroot/js/bespoke-report.js');
  });

  beforeEach(() => {
    document.getElementById('NewTemplateTitle').value = 'Alpha';
    document.getElementById('EditedTemplateTitle').value = 'Alpha';
  });

  it('only lets letters, digits and spaces be typed into a title', () => {
    const title = document.getElementById('NewTemplateTitle');

    type(title, 'My <b>template</b> #2 - draft!');

    expect(title.value).toBe('My btemplateb 2  draft');
  });

  it('leaves an acceptable title alone', () => {
    const title = document.getElementById('EditedTemplateTitle');

    type(title, 'Template 2 for Anthrax');

    expect(title.value).toBe('Template 2 for Anthrax');
  });

  it('leaves other text boxes alone', () => {
    const other = document.getElementById('other');

    type(other, 'a-b!');

    expect(other.value).toBe('a-b!');
  });

  it('saves a dialog, and nothing else, when Enter is pressed in its title box', () => {
    const editSave = vi.fn((event) => event.preventDefault());
    const saveAsSave = vi.fn((event) => event.preventDefault());
    document.getElementById('edit-save').addEventListener('click', editSave);
    document.getElementById('save-as-save').addEventListener('click', saveAsSave);

    const event = pressEnter(document.getElementById('EditedTemplateTitle'));

    expect(event.defaultPrevented).toBe(true);
    expect(editSave).toHaveBeenCalledTimes(1);
    expect(saveAsSave).not.toHaveBeenCalled();
  });

  it('ignores other keys in a title box', () => {
    const save = vi.fn((event) => event.preventDefault());
    document.getElementById('edit-save').addEventListener('click', save);

    const event = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });
    document.getElementById('EditedTemplateTitle').dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });

  it('starts Save template as from the selected title with " Copy" added, and goes back to it when cancelled', () => {
    const title = document.getElementById('NewTemplateTitle');

    document.querySelector('[data-bespoke-save-as]').click();

    expect(title.value).toBe('Alpha Copy');

    document.getElementById('save-as-dialog').dispatchEvent(new Event('close'));

    expect(title.value).toBe('Alpha');
  });
});
