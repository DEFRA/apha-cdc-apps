import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const WELFARE = 'welfare';
const COMMENTS = 'comments';
const IMPACT = 'impact';
const AREAS = 'areas';
const RATE = 'rate';

function hidden(species, field, value) {
  return `<input type="hidden" name="species_${species}_field_${field}" data-species-value="${field}" value="${value.replace(/"/g, '&quot;')}">`;
}

function row(species, name, answers) {
  return `
    <tr data-species-row="${species}" data-species-name="${name}">
      <th scope="row">${name}</th>
      <td data-species-summary="${WELFARE}">${answers.welfareText}</td>
      <td data-species-summary="${COMMENTS}">${answers.commentsText}</td>
      <td data-species-summary="${AREAS}">${answers.areasText}</td>
      <td data-species-summary="${RATE}">${answers.rateText}</td>
      <td>
        <button type="button" id="edit-${species}" data-species-edit="${species}">Edit</button>
        ${hidden(species, WELFARE, answers.welfare)}
        ${hidden(species, COMMENTS, answers.comments)}
        ${hidden(species, IMPACT, answers.impact)}
        ${hidden(species, AREAS, answers.areas)}
        ${hidden(species, RATE, answers.rate)}
      </td>
    </tr>`;
}

function pick(species, name) {
  return `
    <div data-species-pick-item="${species}">
      <input type="checkbox" id="pick-${species}" value="${species}" data-species-pick="${species}" data-species-name="${name}">
      <label for="pick-${species}">${name}</label>
    </div>`;
}

// One question's table and panel, as _ProfileSpeciesQuestion.cshtml writes them, inside the form that posts the answers.
function page({ canEdit = true } = {}) {
  const editable = canEdit ? 'true' : 'false';

  document.body.innerHTML = `
    <form id="form">
      <button type="submit" id="refresh" hidden data-relevance-refresh>Refresh the questions</button>
      <div id="question" data-species-question data-species-can-edit="${editable}">
        <table><tbody>
          ${row('cattle', 'Cattle', { welfare: 'true', welfareText: 'Yes', comments: '<p>First comments</p>', commentsText: 'First comments', impact: 'retired', areas: '', areasText: '', rate: '1.5', rateText: '1.50' })}
          ${row('sheep', 'Sheep', { welfare: 'false', welfareText: 'No', comments: '', commentsText: '', impact: 'mild', areas: 'north', areasText: 'North', rate: '', rateText: '' })}
          ${row('goats', 'Goats', { welfare: '', welfareText: '', comments: '', commentsText: '', impact: '', areas: '', areasText: '', rate: '', rateText: '' })}
        </tbody></table>
        <div id="panel" data-species-panel hidden tabindex="-1">
          <h3 data-species-panel-heading></h3>
          <div data-answer-field="${WELFARE}" data-field-type="Boolean" data-field-editable="${editable}">
            <label for="welfare-select">Compromised welfare?</label>
            <select id="welfare-select" data-answer-editor data-relevance-affects="5.2, 5.3">
              <option value="">Please select...</option><option value="true">Yes</option><option value="false">No</option>
            </select>
          </div>
          <div data-answer-field="${COMMENTS}" data-field-type="Long Text" data-field-editable="${editable}">
            <label for="comments-text">Supporting comments</label>
            <textarea id="comments-text" data-answer-editor></textarea>
          </div>
          <div data-answer-field="${IMPACT}" data-field-type="List" data-field-editable="${editable}">
            <label for="impact-select">Impact</label>
            <select id="impact-select" data-answer-editor>
              <option value="">Please select...</option><option value="mild">Mild</option><option value="severe">Severe</option>
            </select>
          </div>
          <div data-answer-field="${AREAS}" data-field-type="MultiValueList" data-field-editable="${editable}">
            <input type="checkbox" id="area-north" value="north" data-answer-editor><label for="area-north">North</label>
            <input type="checkbox" id="area-south" value="south" data-answer-editor><label for="area-south">South</label>
          </div>
          <div data-answer-field="${RATE}" data-field-type="Decimal" data-field-editable="${editable}">
            <label for="rate-input">Rate</label>
            <input type="text" id="rate-input" data-answer-editor>
          </div>
          <details id="picker" data-species-picker>
            <summary>Apply this answer also to: <span data-species-picker-names></span></summary>
            <input type="checkbox" id="pick-all" data-species-pick-all><label for="pick-all">All</label>
            ${pick('cattle', 'Cattle')}${pick('sheep', 'Sheep')}${pick('goats', 'Goats')}
          </details>
          <button type="button" id="ok" data-species-ok>OK</button>
          <button type="button" id="cancel" data-species-cancel>Cancel</button>
        </div>
      </div>
    </form>`;
}

const get = (id) => document.getElementById(id);
const stored = (species, field) => get('question').querySelector(`[data-species-row="${species}"] [data-species-value="${field}"]`).value;
const summary = (species, field) => get('question').querySelector(`[data-species-row="${species}"] [data-species-summary="${field}"]`).textContent;

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
  await import('../../src/CDC.Web/wwwroot/js/question-species.js');
}

describe('question-species', () => {
  beforeEach(async () => {
    page();
    await load();
  });

  afterEach(() => {
    delete window.appRichText;
    delete window.appAnswerFields;
  });

  describe('opening a species', () => {
    it('opens the panel with the answers of the species whose Edit button was used', () => {
      get('edit-cattle').click();

      expect(get('panel').hidden).toBe(false);
      expect(get('panel').querySelector('[data-species-panel-heading]').textContent).toBe('Edit answer for cattle');
      expect(get('welfare-select').value).toBe('true');
      expect(get('comments-text').value).toBe('<p>First comments</p>');
      expect(get('rate-input').value).toBe('1.5');
      expect(document.activeElement).toBe(get('panel'));
    });

    it('loads a multi-value answer into its checkboxes and clears the ones the species did not choose', () => {
      get('edit-sheep').click();

      expect(get('area-north').checked).toBe(true);
      expect(get('area-south').checked).toBe(false);

      get('edit-cattle').click();

      expect(get('area-north').checked).toBe(false);
    });

    it('holds the answer it loads into a drop-down that decides whether other questions apply, for a change that is not confirmed to be put back', () => {
      get('edit-cattle').click();
      expect(get('welfare-select').getAttribute('data-relevance-held')).toBe('true');

      get('edit-sheep').click();
      expect(get('welfare-select').getAttribute('data-relevance-held')).toBe('false');

      get('edit-goats').click();
      expect(get('welfare-select').getAttribute('data-relevance-held')).toBe('');
    });

    it('titles the panel in lower case, as the legacy panel did', () => {
      get('edit-goats').click();

      expect(get('panel').querySelector('[data-species-panel-heading]').textContent).toBe('Edit answer for goats');
    });

    it('tells assistive technology which Edit button has opened the panel', () => {
      get('edit-cattle').click();

      expect(get('edit-cattle').getAttribute('aria-expanded')).toBe('true');
      expect(get('edit-sheep').getAttribute('aria-expanded')).not.toBe('true');

      get('edit-sheep').click();

      expect(get('edit-cattle').getAttribute('aria-expanded')).toBe('false');
      expect(get('edit-sheep').getAttribute('aria-expanded')).toBe('true');

      get('cancel').click();

      expect(get('edit-sheep').getAttribute('aria-expanded')).toBe('false');
    });

    it('does nothing for a species it does not know', () => {
      const unknown = document.createElement('button');
      unknown.type = 'button';
      unknown.setAttribute('data-species-edit', 'pigs');
      get('question').appendChild(unknown);

      unknown.click();

      expect(get('panel').hidden).toBe(true);
    });

    it('keeps the changes made to the species that is open when another is opened', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      get('edit-sheep').click();

      expect(stored('cattle', WELFARE)).toBe('false');
      expect(summary('cattle', WELFARE)).toBe('No');
      expect(get('welfare-select').value).toBe('false');
      expect(get('panel').querySelector('[data-species-panel-heading]').textContent).toBe('Edit answer for sheep');
    });
  });

  describe('OK', () => {
    it('writes the changed answers to the hidden inputs the form posts, and to the table', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      type(get('comments-text'), '<p>New <em>comments</em></p>');
      change(get('impact-select'), 'severe');
      tick(get('area-south'));
      type(get('rate-input'), '2.5');

      get('ok').click();

      expect(stored('cattle', WELFARE)).toBe('false');
      expect(stored('cattle', COMMENTS)).toBe('<p>New <em>comments</em></p>');
      expect(stored('cattle', IMPACT)).toBe('severe');
      expect(stored('cattle', AREAS)).toBe('south');
      expect(stored('cattle', RATE)).toBe('2.5');
      expect(summary('cattle', WELFARE)).toBe('No');
      expect(summary('cattle', COMMENTS)).toBe('New comments');
    });

    it('closes the panel and returns focus to the species Edit button', () => {
      get('edit-cattle').click();
      get('ok').click();

      expect(get('panel').hidden).toBe(true);
      expect(document.activeElement).toBe(get('edit-cattle'));
    });

    it('shows the first fifty characters of a text answer with its markup removed', () => {
      get('edit-cattle').click();
      type(get('comments-text'), `<p>${'abcdefghij'.repeat(8)}</p>`);

      get('ok').click();

      expect(summary('cattle', COMMENTS)).toBe('abcdefghij'.repeat(5));
    });

    it('shows a decimal to two places, and an entry that is not a number as it was typed', () => {
      get('edit-cattle').click();
      type(get('rate-input'), '2.5');
      get('ok').click();

      expect(stored('cattle', RATE)).toBe('2.5');
      expect(summary('cattle', RATE)).toBe('2.50');

      get('edit-sheep').click();
      type(get('rate-input'), 'lots');
      get('ok').click();

      expect(stored('sheep', RATE)).toBe('lots');
      expect(summary('sheep', RATE)).toBe('lots');
    });

    it('shows the names of the items chosen from a multi-value list', () => {
      get('edit-goats').click();
      tick(get('area-north'));
      tick(get('area-south'));
      get('ok').click();

      expect(stored('goats', AREAS)).toBe('north,south');
      expect(summary('goats', AREAS)).toBe('North, South');
    });

    it('keeps an answer the user did not change as it was held, even when the panel cannot show it', () => {
      // The list item "retired" is not among the panel's options, so the drop-down shows nothing selected.
      get('edit-cattle').click();
      expect(get('impact-select').value).toBe('');

      change(get('welfare-select'), 'false');
      get('ok').click();

      expect(stored('cattle', IMPACT)).toBe('retired');
      expect(stored('cattle', COMMENTS)).toBe('<p>First comments</p>');
      expect(summary('cattle', COMMENTS)).toBe('First comments');
    });

    it('empties an answer the user cleared', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), '');
      get('ok').click();

      expect(stored('cattle', WELFARE)).toBe('');
      expect(summary('cattle', WELFARE)).toBe('');
    });

    it('leaves every other species as it was', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      get('ok').click();

      expect(stored('sheep', WELFARE)).toBe('false');
      expect(stored('goats', WELFARE)).toBe('');
      expect(summary('goats', WELFARE)).toBe('');
    });
  });

  describe('Cancel', () => {
    it('drops the changes and closes the panel', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      type(get('comments-text'), '<p>Dropped</p>');

      get('cancel').click();

      expect(get('panel').hidden).toBe(true);
      expect(stored('cattle', WELFARE)).toBe('true');
      expect(stored('cattle', COMMENTS)).toBe('<p>First comments</p>');
      expect(summary('cattle', WELFARE)).toBe('Yes');
      expect(document.activeElement).toBe(get('edit-cattle'));
    });
  });

  describe('Apply this answer also to', () => {
    it('leaves the species being edited out of the list, and starts with none picked and the list closed', () => {
      get('picker').open = true;
      get('edit-cattle').click();

      expect(get('picker').open).toBe(false);
      expect(document.querySelector('[data-species-pick-item="cattle"]').hidden).toBe(true);
      expect(document.querySelector('[data-species-pick-item="sheep"]').hidden).toBe(false);
      expect(document.querySelectorAll('[data-species-pick]:checked')).toHaveLength(0);

      tick(get('pick-sheep'));
      get('edit-sheep').click();

      expect(document.querySelector('[data-species-pick-item="cattle"]').hidden).toBe(false);
      expect(document.querySelector('[data-species-pick-item="sheep"]').hidden).toBe(true);
      expect(get('pick-goats').checked).toBe(false);
    });

    it('applies the answers to each species picked, as well as to the one being edited', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      type(get('comments-text'), '<p>For both</p>');
      tick(get('pick-sheep'));
      tick(get('pick-goats'));

      get('ok').click();

      ['cattle', 'sheep', 'goats'].forEach((species) => {
        expect(stored(species, WELFARE)).toBe('false');
        expect(stored(species, COMMENTS)).toBe('<p>For both</p>');
        expect(summary(species, WELFARE)).toBe('No');
        expect(summary(species, COMMENTS)).toBe('For both');
      });
    });

    it('applies every answer of the panel to the species picked, including those the user did not change', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      tick(get('pick-sheep'));

      get('ok').click();

      expect(stored('sheep', COMMENTS)).toBe('<p>First comments</p>');
      expect(stored('sheep', IMPACT)).toBe('retired');
      expect(stored('sheep', RATE)).toBe('1.5');
    });

    it('does not apply the answers to a species that was not picked', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      tick(get('pick-sheep'));
      tick(get('pick-sheep'), false);

      get('ok').click();

      expect(stored('goats', WELFARE)).toBe('');
      expect(stored('sheep', COMMENTS)).toBe('');
    });

    it('names the species picked, and picks every other species with All', () => {
      get('edit-cattle').click();
      tick(get('pick-sheep'));

      expect(document.querySelector('[data-species-picker-names]').textContent).toBe('Sheep');

      tick(get('pick-all'));

      expect(get('pick-sheep').checked).toBe(true);
      expect(get('pick-goats').checked).toBe(true);
      expect(get('pick-cattle').checked).toBe(false);
      expect(document.querySelector('[data-species-picker-names]').textContent).toBe('Sheep, Goats');

      tick(get('pick-all'), false);

      expect(document.querySelectorAll('[data-species-pick]:checked')).toHaveLength(0);
      expect(document.querySelector('[data-species-picker-names]').textContent).toBe('');
    });

    it('ticks All when every other species is picked, and unticks it when one is not', () => {
      get('edit-cattle').click();
      tick(get('pick-sheep'));
      expect(get('pick-all').checked).toBe(false);

      tick(get('pick-goats'));
      expect(get('pick-all').checked).toBe(true);

      tick(get('pick-goats'), false);
      expect(get('pick-all').checked).toBe(false);
    });
  });

  describe('a user who may only view the answers', () => {
    beforeEach(async () => {
      page({ canEdit: false });
      await load();
    });

    it('titles the panel View, and changes nothing when it is closed', () => {
      get('edit-cattle').click();

      expect(get('panel').querySelector('[data-species-panel-heading]').textContent).toBe('View answer for cattle');
      expect(get('welfare-select').value).toBe('true');

      change(get('welfare-select'), 'false');
      get('ok').click();

      expect(get('panel').hidden).toBe(true);
      expect(stored('cattle', WELFARE)).toBe('true');
      expect(summary('cattle', WELFARE)).toBe('Yes');
    });

    it('does not ask the page to refresh, whatever the panel says', () => {
      const refresh = vi.fn((event) => event.preventDefault());
      get('refresh').addEventListener('click', refresh);

      get('edit-cattle').click();
      get('welfare-select').dispatchEvent(new CustomEvent('app:relevance-confirmed', { bubbles: true }));
      get('ok').click();

      expect(refresh).not.toHaveBeenCalled();
    });
  });

  // A list or Boolean answer the user may not change is shown as boxes that cannot be changed, as the legacy View link showed it.
  describe('a list or Boolean answer the user may not change', () => {
    beforeEach(async () => {
      document.body.innerHTML = `
        <form id="form">
          <div id="question" data-species-question data-species-can-edit="false">
            <table><tbody>
              <tr data-species-row="cattle" data-species-name="Cattle">
                <th scope="row">Cattle</th>
                <td>
                  <button type="button" id="view-cattle" data-species-edit="cattle">View</button>
                  <input type="hidden" data-species-value="trade" value="true">
                  <input type="hidden" data-species-value="level" value="high">
                </td>
              </tr>
              <tr data-species-row="sheep" data-species-name="Sheep">
                <th scope="row">Sheep</th>
                <td>
                  <button type="button" id="view-sheep" data-species-edit="sheep">View</button>
                  <input type="hidden" data-species-value="trade" value="false">
                  <input type="hidden" data-species-value="level" value="">
                </td>
              </tr>
            </tbody></table>
            <div id="panel" data-species-panel hidden tabindex="-1">
              <h3 data-species-panel-heading></h3>
              <div data-answer-field="trade" data-field-type="Boolean" data-field-editable="false">
                <fieldset>
                  <legend>Commercial trade?</legend>
                  <input type="checkbox" id="trade-yes" value="true" disabled data-answer-choice><label for="trade-yes">Yes</label>
                  <input type="checkbox" id="trade-no" value="false" disabled data-answer-choice><label for="trade-no">No</label>
                </fieldset>
              </div>
              <div data-answer-field="level" data-field-type="List" data-field-editable="false">
                <fieldset>
                  <legend>Level</legend>
                  <input type="checkbox" id="level-low" value="low" disabled data-answer-choice><label for="level-low">Low</label>
                  <input type="checkbox" id="level-high" value="high" disabled data-answer-choice><label for="level-high">High</label>
                </fieldset>
              </div>
              <button type="button" id="ok" data-species-ok>Close</button>
            </div>
          </div>
        </form>`;
      await load();
    });

    const ticked = () => ['trade-yes', 'trade-no', 'level-low', 'level-high'].filter((id) => get(id).checked);

    it('ticks the box that holds the answer of the species being viewed', () => {
      get('view-cattle').click();

      expect(get('panel').querySelector('[data-species-panel-heading]').textContent).toBe('View answer for cattle');
      expect(ticked()).toEqual(['trade-yes', 'level-high']);
    });

    it('ticks the boxes of the next species when it is viewed, and none for an answer it has not given', () => {
      get('view-cattle').click();
      get('view-sheep').click();

      expect(ticked()).toEqual(['trade-no']);
    });

    it('keeps the answers the row holds when the panel is closed', () => {
      get('view-cattle').click();
      get('ok').click();

      expect(get('panel').hidden).toBe(true);
      expect(get('question').querySelector('[data-species-row="cattle"] [data-species-value="trade"]').value).toBe('true');
      expect(get('question').querySelector('[data-species-row="cattle"] [data-species-value="level"]').value).toBe('high');
    });
  });

  describe('saving the section', () => {
    it('keeps the changes of a panel that is still open when the form is submitted', () => {
      const submitted = vi.fn((event) => event.preventDefault());
      get('form').addEventListener('submit', submitted);

      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      tick(get('pick-sheep'));
      get('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

      expect(submitted).toHaveBeenCalledTimes(1);
      expect(stored('cattle', WELFARE)).toBe('false');
      expect(stored('sheep', WELFARE)).toBe('false');
    });

    it('does nothing to the answers when no panel is open', () => {
      get('form').addEventListener('submit', (event) => event.preventDefault());

      get('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

      expect(stored('cattle', WELFARE)).toBe('true');
    });

    it('applies the answers, rather than submitting the form, when Enter is pressed in a text box of the panel', () => {
      get('edit-cattle').click();
      type(get('rate-input'), '4');

      const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
      get('rate-input').dispatchEvent(enter);

      expect(enter.defaultPrevented).toBe(true);
      expect(stored('cattle', RATE)).toBe('4');
      expect(get('panel').hidden).toBe(true);
    });

    it('leaves Enter alone in a text area, where it starts a new line', () => {
      get('edit-cattle').click();

      const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });
      get('comments-text').dispatchEvent(enter);

      expect(enter.defaultPrevented).toBe(false);
      expect(get('panel').hidden).toBe(false);
    });
  });

  describe('an answer that decides whether other questions apply', () => {
    let refresh;

    beforeEach(() => {
      refresh = vi.fn((event) => event.preventDefault());
      get('refresh').addEventListener('click', refresh);
    });

    it('saves the section when OK is pressed after the change was confirmed, so the questions made irrelevant are hidden', () => {
      get('edit-cattle').click();
      change(get('welfare-select'), 'false');
      get('welfare-select').dispatchEvent(new CustomEvent('app:relevance-confirmed', { bubbles: true }));
      get('ok').click();

      expect(stored('cattle', WELFARE)).toBe('false');
      expect(refresh).toHaveBeenCalledTimes(1);
    });

    it('does not save the section when no such change was confirmed', () => {
      get('edit-cattle').click();
      type(get('comments-text'), '<p>Other</p>');
      get('ok').click();

      expect(refresh).not.toHaveBeenCalled();
    });

    it('saves the section once, not at each OK that follows', () => {
      get('edit-cattle').click();
      get('welfare-select').dispatchEvent(new CustomEvent('app:relevance-confirmed', { bubbles: true }));
      get('ok').click();
      get('edit-sheep').click();
      get('ok').click();

      expect(refresh).toHaveBeenCalledTimes(1);
    });

    it('does not save the section when the panel is cancelled', () => {
      get('edit-cattle').click();
      get('welfare-select').dispatchEvent(new CustomEvent('app:relevance-confirmed', { bubbles: true }));
      get('cancel').click();

      expect(refresh).not.toHaveBeenCalled();
    });
  });

  describe('rich text answers', () => {
    it('fills the rich text editor of the panel with the answer of the species opened', () => {
      const editor = document.createElement('div');
      editor.id = 'comments-text-editor';
      get('comments-text').after(editor);
      window.appRichText = { fill: vi.fn() };

      get('edit-cattle').click();

      expect(window.appRichText.fill).toHaveBeenCalledWith(editor, '<p>First comments</p>');
    });

    it('shows a read-only answer as it is formatted', async () => {
      page({ canEdit: false });
      get('comments-text').replaceWith(Object.assign(document.createElement('div'), { id: 'comments-view' }));
      get('comments-view').setAttribute('data-answer-text-view', '');
      await load();
      window.appRichText = { fill: vi.fn() };

      get('edit-cattle').click();

      expect(window.appRichText.fill).toHaveBeenCalledWith(get('comments-view'), '<p>First comments</p>');
    });

    it('shows a read-only answer as plain text when the rich text script is not there', async () => {
      page({ canEdit: false });
      get('comments-text').replaceWith(Object.assign(document.createElement('div'), { id: 'comments-view' }));
      get('comments-view').setAttribute('data-answer-text-view', '');
      await load();

      get('edit-cattle').click();

      expect(get('comments-view').textContent).toBe('First comments');
    });
  });

  it('leaves a question that has no species to edit alone', async () => {
    document.body.innerHTML = '<div data-species-question data-species-can-edit="true"></div>';

    await expect(load()).resolves.toBeUndefined();
  });
});
