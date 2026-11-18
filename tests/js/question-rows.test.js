import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const BLANK = '00000000-0000-0000-0000-000000000000';
const QUESTION = 'question-1';
const MECHANISM = 'mechanism';
const PROJECT = 'project';
const PURPOSE = 'purpose';
const AREAS = 'areas';
const COST = 'cost';

function hidden(row, field, value) {
  return `<input type="hidden" name="row_${row}_field_${field}" data-row-value="${field}" value="${value.replace(/"/g, '&quot;')}">`;
}

// One row of the table, as _ProfileRepeatingRow.cshtml writes it.
function row(id, position, answers) {
  return `
    <tr data-row="${id}">
      <td data-row-summary="${MECHANISM}">${answers.mechanismText}</td>
      <td data-row-summary="${PROJECT}">${answers.projectText}</td>
      <td>
        <button type="button" data-row-edit aria-expanded="false">Edit<span> answer <span data-row-number>${position}</span></span></button>
        <input type="hidden" name="rows_${QUESTION}" value="${id}">
        ${hidden(id, MECHANISM, answers.mechanism)}
        ${hidden(id, PROJECT, answers.project)}
        ${hidden(id, PURPOSE, answers.purpose)}
        ${hidden(id, AREAS, answers.areas)}
        ${hidden(id, COST, answers.cost)}
      </td>
      <td><button type="button" data-row-delete>Delete<span> answer <span data-row-number>${position}</span></span></button></td>
    </tr>`;
}

const BLANK_ANSWERS = { mechanism: '', mechanismText: '', project: '', projectText: '', purpose: '', areas: '', cost: '' };
const FIRST = { mechanism: 'clinical', mechanismText: 'Clinical', project: 'Clinical project', projectText: 'Clinical project', purpose: '<p>Looks for signs</p>', areas: 'agency', cost: '12.5' };
const SECOND = { mechanism: 'abattoir', mechanismText: 'Abattoir', project: 'Abattoir inspection', projectText: 'Abattoir inspection', purpose: '', areas: '', cost: '' };

// One repeating question's table and panel, as _ProfileRepeatingQuestion.cshtml writes them, inside the form that posts the answers.
function page({ canEdit = true, rows = [['r1', FIRST], ['r2', SECOND]] } = {}) {
  const editable = canEdit ? 'true' : 'false';

  document.body.innerHTML = `
    <form id="form">
      <div id="question" data-row-question data-row-can-edit="${editable}">
        ${canEdit ? `<input type="hidden" name="rows_${QUESTION}" value="">` : ''}
        <p id="empty" data-row-empty ${rows.length > 0 ? 'hidden' : ''}>There are no answers to display.</p>
        <table id="table" data-row-table ${rows.length === 0 ? 'hidden' : ''}>
          <tbody data-row-body>${rows.map(([id, answers], index) => row(id, index + 1, answers)).join('')}</tbody>
        </table>
        ${canEdit ? '<button type="button" id="add" data-row-add aria-expanded="false">Add</button>' : ''}
        <div id="status" role="status" data-row-status></div>
        <fieldset id="panel" data-row-panel hidden tabindex="-1">
          <div data-answer-field="${MECHANISM}" data-field-type="List" data-field-editable="${editable}">
            <label for="mechanism-select">Surveillance mechanism</label>
            <select id="mechanism-select" data-answer-editor>
              <option value="">Please select...</option><option value="clinical">Clinical</option><option value="abattoir">Abattoir</option>
            </select>
          </div>
          <div data-answer-field="${PROJECT}" data-field-type="Text" data-field-editable="${editable}">
            <label for="project-input">Project</label>
            <input type="text" id="project-input" data-answer-editor>
          </div>
          <div data-answer-field="${PURPOSE}" data-field-type="Long Text" data-field-editable="${editable}">
            <label for="purpose-text">Purpose</label>
            <textarea id="purpose-text" data-answer-editor></textarea>
          </div>
          <div data-answer-field="${AREAS}" data-field-type="MultiValueList" data-field-editable="${editable}">
            <input type="checkbox" id="area-agency" value="agency" data-answer-editor><label for="area-agency">Agency</label>
            <input type="checkbox" id="area-university" value="university" data-answer-editor><label for="area-university">University</label>
          </div>
          <div data-answer-field="${COST}" data-field-type="Decimal" data-field-editable="${editable}">
            <label for="cost-input">Cost</label>
            <input type="text" id="cost-input" data-answer-editor>
          </div>
          <button type="button" id="ok" data-row-ok>OK</button>
          <button type="button" id="cancel" data-row-cancel>Cancel</button>
        </fieldset>
        <template id="template" data-row-template>${row(BLANK, 0, BLANK_ANSWERS)}</template>
      </div>
    </form>`;
}

const get = (id) => document.getElementById(id);
const rowsInTable = () => Array.from(document.querySelectorAll('[data-row-body] > tr'));
const rowIds = () => rowsInTable().map((tr) => tr.dataset.row);
const rowOf = (id) => document.querySelector(`[data-row-body] > tr[data-row="${id}"]`);
const stored = (id, field) => rowOf(id).querySelector(`[data-row-value="${field}"]`).value;
const summary = (id, field) => rowOf(id).querySelector(`[data-row-summary="${field}"]`).textContent;
const edit = (id) => rowOf(id).querySelector('[data-row-edit]');
const remove = (id) => rowOf(id).querySelector('[data-row-delete]');
const listed = () => Array.from(document.querySelectorAll(`input[name="rows_${QUESTION}"]`)).map((input) => input.value);
const posted = () => Array.from(new FormData(get('form')).entries());

function change(control, value) {
  control.value = value;
  control.dispatchEvent(new Event('change', { bubbles: true }));
}

function type(control, value) {
  control.value = value;
  control.dispatchEvent(new Event('input', { bubbles: true }));
}

function tick(box, checked = true) {
  box.checked = checked;
  box.dispatchEvent(new Event('change', { bubbles: true }));
}

async function load() {
  vi.resetModules();
  await import('../../src/CDC.Web/wwwroot/js/answer-fields.js');
  await import('../../src/CDC.Web/wwwroot/js/question-rows.js');
}

describe('question-rows', () => {
  beforeEach(async () => {
    page();
    await load();
  });

  afterEach(() => {
    delete window.appRichText;
    delete window.appAnswerFields;
  });

  describe('opening a row', () => {
    it('opens the panel with the answers of the row whose Edit button was used', () => {
      edit('r1').click();

      expect(get('panel').hidden).toBe(false);
      expect(get('mechanism-select').value).toBe('clinical');
      expect(get('project-input').value).toBe('Clinical project');
      expect(get('purpose-text').value).toBe('<p>Looks for signs</p>');
      expect(get('area-agency').checked).toBe(true);
      expect(get('area-university').checked).toBe(false);
      expect(get('cost-input').value).toBe('12.5');
    });

    it('moves focus to the panel, and tells assistive technology which button opened it', () => {
      edit('r1').click();

      expect(document.activeElement).toBe(get('panel'));
      expect(edit('r1').getAttribute('aria-expanded')).toBe('true');
      expect(edit('r2').getAttribute('aria-expanded')).toBe('false');
    });

    it('loads the answers of another row, keeping those typed into the one that was open, when another Edit button is used', () => {
      edit('r1').click();
      type(get('project-input'), 'Typed into the first row');
      edit('r2').click();

      expect(stored('r1', PROJECT)).toBe('Typed into the first row');
      expect(get('project-input').value).toBe('Abattoir inspection');
      expect(edit('r1').getAttribute('aria-expanded')).toBe('false');
      expect(edit('r2').getAttribute('aria-expanded')).toBe('true');
    });

    it('does nothing when the question has no panel to edit the rows in', async () => {
      document.body.innerHTML = '<div data-row-question data-row-can-edit="true"></div>';

      await expect(load()).resolves.toBeUndefined();
    });
  });

  describe('OK', () => {
    it('writes the answers that were changed back to the row, and its table cells', () => {
      edit('r1').click();
      change(get('mechanism-select'), 'abattoir');
      type(get('project-input'), 'Renamed project');
      tick(get('area-university'));
      type(get('cost-input'), '3');
      get('ok').click();

      expect(stored('r1', MECHANISM)).toBe('abattoir');
      expect(stored('r1', PROJECT)).toBe('Renamed project');
      expect(stored('r1', AREAS)).toBe('agency,university');
      expect(stored('r1', COST)).toBe('3');
      expect(summary('r1', MECHANISM)).toBe('Abattoir');
      expect(summary('r1', PROJECT)).toBe('Renamed project');
    });

    it('leaves an answer the user did not change as it was held, even when the panel cannot show it', () => {
      rowOf('r1').querySelector(`[data-row-value="${MECHANISM}"]`).value = 'retired';
      edit('r1').click();
      type(get('project-input'), 'Renamed project');
      get('ok').click();

      expect(stored('r1', MECHANISM)).toBe('retired');
      expect(stored('r1', PROJECT)).toBe('Renamed project');
    });

    it('closes the panel, and puts focus on the row\'s Edit button', () => {
      edit('r1').click();
      get('ok').click();

      expect(get('panel').hidden).toBe(true);
      expect(document.activeElement).toBe(edit('r1'));
      expect(edit('r1').getAttribute('aria-expanded')).toBe('false');
    });

    it('does nothing when no row is open', () => {
      expect(() => get('ok').click()).not.toThrow();
      expect(rowIds()).toEqual(['r1', 'r2']);
    });

    it('is what Enter does in a text box of the panel, in place of submitting the form', () => {
      edit('r1').click();
      type(get('project-input'), 'Entered');
      const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
      get('project-input').dispatchEvent(enter);

      expect(enter.defaultPrevented).toBe(true);
      expect(stored('r1', PROJECT)).toBe('Entered');
      expect(get('panel').hidden).toBe(true);
    });

    it('is not what Enter does in a text area', () => {
      edit('r1').click();
      const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
      get('purpose-text').dispatchEvent(enter);

      expect(enter.defaultPrevented).toBe(false);
      expect(get('panel').hidden).toBe(false);
    });
  });

  describe('Cancel', () => {
    it('drops the changes, closes the panel and returns focus to the Edit button', () => {
      edit('r1').click();
      type(get('project-input'), 'Dropped');
      get('cancel').click();

      expect(stored('r1', PROJECT)).toBe('Clinical project');
      expect(get('panel').hidden).toBe(true);
      expect(document.activeElement).toBe(edit('r1'));
    });
  });

  describe('Add', () => {
    it('opens the panel with no answers, and does not add the row until OK', () => {
      get('add').click();

      expect(get('panel').hidden).toBe(false);
      expect(get('mechanism-select').value).toBe('');
      expect(get('project-input').value).toBe('');
      expect(get('area-agency').checked).toBe(false);
      expect(rowIds()).toEqual(['r1', 'r2']);
      expect(get('add').getAttribute('aria-expanded')).toBe('true');
    });

    it('adds the row at the end of the table when OK is used, with the answers the user gave', () => {
      get('add').click();
      change(get('mechanism-select'), 'abattoir');
      type(get('project-input'), 'A new project');
      tick(get('area-university'));
      get('ok').click();

      const ids = rowIds();
      expect(ids).toHaveLength(3);
      expect(ids.slice(0, 2)).toEqual(['r1', 'r2']);
      const added = ids[2];
      expect(added).not.toBe(BLANK);
      expect(stored(added, MECHANISM)).toBe('abattoir');
      expect(stored(added, PROJECT)).toBe('A new project');
      expect(stored(added, AREAS)).toBe('university');
      expect(summary(added, MECHANISM)).toBe('Abattoir');
      expect(summary(added, PROJECT)).toBe('A new project');
    });

    it('names the new row\'s inputs for an identifier of its own, which the form posts after the others', () => {
      get('add').click();
      type(get('project-input'), 'A new project');
      get('ok').click();

      const added = rowIds()[2];
      expect(listed()).toEqual(['', 'r1', 'r2', added]);
      expect(posted()).toContainEqual([`row_${added}_field_${PROJECT}`, 'A new project']);
      expect(posted().some(([name, value]) => name.includes(BLANK) || value === BLANK)).toBe(false);

      const inputs = Array.from(rowOf(added).querySelectorAll('input[name]'));
      expect(inputs.filter((input) => input.name === `rows_${QUESTION}`).map((input) => input.value)).toEqual([added]);
      expect(inputs.filter((input) => input.name !== `rows_${QUESTION}`).every((input) => input.name.startsWith(`row_${added}_field_`))).toBe(true);
    });

    it('numbers the new row after the others, and says it was added', () => {
      get('add').click();
      get('ok').click();

      expect(rowsInTable()[2].querySelector('[data-row-number]').textContent).toBe('3');
      expect(get('status').textContent).toBe('Answer added.');
    });

    it('moves focus to the new row\'s Edit button', () => {
      get('add').click();
      get('ok').click();

      expect(document.activeElement).toBe(rowsInTable()[2].querySelector('[data-row-edit]'));
      expect(get('add').getAttribute('aria-expanded')).toBe('false');
    });

    it('gives each row it adds an identifier of its own', () => {
      get('add').click();
      get('ok').click();
      get('add').click();
      get('ok').click();

      const ids = rowIds();
      expect(new Set(ids).size).toBe(4);
    });

    it('adds no row when Cancel is used', () => {
      get('add').click();
      type(get('project-input'), 'Dropped');
      get('cancel').click();

      expect(rowIds()).toEqual(['r1', 'r2']);
      expect(get('panel').hidden).toBe(true);
      expect(document.activeElement).toBe(get('add'));
    });

    it('keeps the row that was being added when another Edit button is used', () => {
      get('add').click();
      type(get('project-input'), 'Kept');
      edit('r1').click();

      expect(rowIds()).toHaveLength(3);
      expect(stored(rowIds()[2], PROJECT)).toBe('Kept');
    });

    it('shows the table, and hides the message that there are no answers, for the first row of an empty question', async () => {
      page({ rows: [] });
      await load();

      expect(get('table').hidden).toBe(true);
      expect(get('empty').hidden).toBe(false);

      get('add').click();
      type(get('project-input'), 'First');
      get('ok').click();

      expect(get('table').hidden).toBe(false);
      expect(get('empty').hidden).toBe(true);
      expect(rowIds()).toHaveLength(1);
    });
  });

  describe('Delete', () => {
    it('takes the row out of the table, with the inputs that post its answers', () => {
      remove('r1').click();

      expect(rowIds()).toEqual(['r2']);
      expect(listed()).toEqual(['', 'r2']);
      expect(posted().some(([name]) => name.startsWith('row_r1_'))).toBe(false);
    });

    it('numbers the rows that remain, and says the row was deleted', () => {
      remove('r1').click();

      expect(rowsInTable()[0].querySelector('[data-row-number]').textContent).toBe('1');
      expect(get('status').textContent).toBe('Answer deleted.');
    });

    it('moves focus to the Add button', () => {
      remove('r1').click();

      expect(document.activeElement).toBe(get('add'));
    });

    it('hides the table, and says there are no answers, when the last row is deleted', () => {
      remove('r1').click();
      remove('r2').click();

      expect(rowIds()).toEqual([]);
      expect(listed()).toEqual(['']);
      expect(get('table').hidden).toBe(true);
      expect(get('empty').hidden).toBe(false);
    });

    it('closes the panel without keeping the answers typed into it, when the row being edited is deleted', () => {
      edit('r1').click();
      type(get('project-input'), 'Typed into a row that is deleted');
      remove('r1').click();

      expect(get('panel').hidden).toBe(true);
      expect(rowIds()).toEqual(['r2']);
      expect(edit('r2').getAttribute('aria-expanded')).toBe('false');
    });

    it('keeps the answers typed into the row that is open, when another row is deleted', () => {
      edit('r1').click();
      type(get('project-input'), 'Kept');
      remove('r2').click();

      expect(stored('r1', PROJECT)).toBe('Kept');
      expect(rowIds()).toEqual(['r1']);
    });
  });

  describe('saving the section', () => {
    it('keeps the answers of an open panel when the form is submitted', () => {
      edit('r1').click();
      type(get('project-input'), 'Typed just before Save');
      get('form').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));

      expect(stored('r1', PROJECT)).toBe('Typed just before Save');
    });

    it('adds the row being added when the form is submitted', () => {
      get('add').click();
      type(get('project-input'), 'Typed just before Save');
      get('form').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));

      expect(rowIds()).toHaveLength(3);
    });
  });

  describe('a user who cannot edit the section', () => {
    beforeEach(async () => {
      page({ canEdit: false });
      await load();
    });

    it('opens the answers of a row in the panel, and closes it with Close', () => {
      edit('r1').click();

      expect(get('panel').hidden).toBe(false);
      expect(get('project-input').value).toBe('Clinical project');

      get('ok').click();

      expect(get('panel').hidden).toBe(true);
    });

    it('changes nothing in the row', () => {
      edit('r1').click();
      type(get('project-input'), 'Not kept');
      get('ok').click();

      expect(stored('r1', PROJECT)).toBe('Clinical project');
    });
  });

  describe('an answer the user may not change', () => {
    it('is not written back to the row', async () => {
      page();
      get('project-input').closest('[data-answer-field]').dataset.fieldEditable = 'false';
      await load();

      edit('r1').click();
      type(get('project-input'), 'Not kept');
      get('ok').click();

      expect(stored('r1', PROJECT)).toBe('Clinical project');
    });
  });

  describe('the identifier of a new row', () => {
    it('is made from random values when the browser has no randomUUID, as on an origin that is not secure', async () => {
      Object.defineProperty(window.crypto, 'randomUUID', { value: undefined, configurable: true });

      try {
        page();
        await load();
        get('add').click();
        get('ok').click();

        expect(rowIds()[2]).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
      } finally {
        delete window.crypto.randomUUID;
      }
    });
  });
});
