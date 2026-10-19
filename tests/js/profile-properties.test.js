import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/tree-view.js';
import '../../src/CDC.Web/wwwroot/js/profile-properties.js';

// A species hierarchy for the two trees:
//   Animals
//     Cattle          (a group)
//       Bulls
//       Cows
//     Sheep
//     Goats           (inactive)
function node(prefix, id, label, { inactive = false, checked = false, children = [] } = {}) {
  const tag = inactive ? '<span class="app-tree__inactive-tag">(inactive)</span>' : '';

  return `
    <li class="app-tree__item" data-app-tree-expanded="true">
      <div class="app-tree__row">
        <div class="govuk-checkboxes__item app-tree__radio">
          <input class="govuk-checkboxes__input" id="${prefix}-${id}" name="${prefix}" type="checkbox" value="${id}"${checked ? ' checked' : ''}>
          <label class="govuk-label govuk-checkboxes__label${inactive ? ' app-tree__label--inactive' : ''}" for="${prefix}-${id}">${label} ${tag}</label>
        </div>
      </div>
      ${children.length > 0 ? `<ul class="app-tree__group">${children.join('')}</ul>` : ''}
    </li>`;
}

function tree(prefix, checked) {
  const is = (id) => checked.includes(id);

  return `
    <ul class="app-tree" id="${prefix}" data-module="app-tree-view" data-app-tree-multiple="true">
      ${node(prefix, 'animals', 'Animals', {
        checked: is('animals'),
        children: [
          node(prefix, 'cattle', 'Cattle', {
            checked: is('cattle'),
            children: [
              node(prefix, 'bulls', 'Bulls', { checked: is('bulls') }),
              node(prefix, 'cows', 'Cows', { checked: is('cows') })
            ]
          }),
          node(prefix, 'sheep', 'Sheep', { checked: is('sheep') }),
          node(prefix, 'goats', 'Goats', { inactive: true, checked: is('goats') })
        ]
      })}
    </ul>`;
}

function build({ profiled = [], other = [], warningEnabled = true } = {}) {
  document.body.innerHTML = `
    <form data-module="app-profile-properties">
      <div id="ProfileTitleEditor" contenteditable="true" data-app-title-hidden-input="ProfileTitle">Anthrax</div>
      <input type="hidden" id="ProfileTitle" name="ProfileTitle" value="Anthrax">
      <button type="button" class="app-tree__toggle-all" data-app-tree-action="toggle-all" aria-expanded="true">
        <span class="app-tree__toggle-all-text">Close all</span>
      </button>
      <div data-module="app-species-selector" data-species-warning-enabled="${warningEnabled}">
        <div data-species-tree="profiled">${tree('profiled', profiled)}</div>
        <output data-species-status="profiled" hidden></output>
        <div data-species-tree="other">${tree('other', other)}</div>
        <output data-species-status="other" hidden></output>
        <div data-species-warning hidden>Removing profiled species removes their data.</div>
        <div data-species-announcement></div>
      </div>
      <button type="button" data-profile-properties-reset>Reset selections</button>
    </form>`;

  document.querySelectorAll('[data-module="app-tree-view"]').forEach((list) => {
    list.treeView = new window.TreeView(list);
  });

  const form = document.querySelector('form');
  form.profileProperties = new window.ProfileProperties(form);

  const box = (treeName, id) => document.getElementById(`${treeName}-${id}`);

  return {
    form,
    box,
    status: (kind) => document.querySelector(`[data-species-status="${kind}"]`),
    warning: document.querySelector('[data-species-warning]'),
    announcement: document.querySelector('[data-species-announcement]'),
    editor: document.getElementById('ProfileTitleEditor'),
    reset: document.querySelector('[data-profile-properties-reset]'),
    state: (treeName, id) => {
      const input = box(treeName, id);

      return { checked: input.checked, disabled: input.disabled };
    }
  };
}

function tick(input, checked = true) {
  input.checked = checked;
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

describe('profile-properties species selector', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    document.body.innerHTML = '';
    vi.useRealTimers();
  });

  it('ticks and disables the active species beneath a ticked group, leaving the inactive ones unticked', () => {
    const { box, state } = build();

    tick(box('profiled', 'animals'));

    expect(state('profiled', 'animals')).toEqual({ checked: true, disabled: false });
    expect(state('profiled', 'cattle')).toEqual({ checked: true, disabled: true });
    expect(state('profiled', 'bulls')).toEqual({ checked: true, disabled: true });
    expect(state('profiled', 'sheep')).toEqual({ checked: true, disabled: true });
    expect(state('profiled', 'goats')).toEqual({ checked: false, disabled: true });
  });

  it('disables the ticked group and everything beneath it in the other tree', () => {
    const { box, state } = build();

    tick(box('profiled', 'cattle'));

    expect(state('other', 'cattle')).toEqual({ checked: false, disabled: true });
    expect(state('other', 'bulls')).toEqual({ checked: false, disabled: true });
    expect(state('other', 'cows')).toEqual({ checked: false, disabled: true });
    expect(state('other', 'sheep')).toEqual({ checked: false, disabled: false });
    expect(state('other', 'animals')).toEqual({ checked: false, disabled: false });
  });

  it('does not let a species be both profiled and other', () => {
    const { box, state } = build();

    tick(box('profiled', 'sheep'));

    expect(state('other', 'sheep')).toEqual({ checked: false, disabled: true });
  });

  it('frees everything again when the group is unticked', () => {
    const { box, state } = build();
    tick(box('profiled', 'cattle'));

    tick(box('profiled', 'cattle'), false);

    expect(state('profiled', 'cattle')).toEqual({ checked: false, disabled: false });
    expect(state('profiled', 'bulls')).toEqual({ checked: false, disabled: false });
    expect(state('other', 'cattle')).toEqual({ checked: false, disabled: false });
    expect(state('other', 'bulls')).toEqual({ checked: false, disabled: false });
  });

  it('takes a species back from the other tree when its group is ticked', () => {
    const { box, state } = build({ other: ['bulls'] });

    tick(box('profiled', 'cattle'));

    expect(state('other', 'bulls')).toEqual({ checked: false, disabled: true });
    expect(state('profiled', 'bulls')).toEqual({ checked: true, disabled: true });
  });

  it('absorbs a species ticked beneath a group that is then ticked', () => {
    const { box, state } = build({ profiled: ['bulls'] });

    tick(box('profiled', 'cattle'));

    expect(state('profiled', 'cattle')).toEqual({ checked: true, disabled: false });
    expect(state('profiled', 'bulls')).toEqual({ checked: true, disabled: true });
  });

  it('shows what was already ticked when the page loads, with the rules applied', () => {
    const { state } = build({ profiled: ['cattle'], other: ['sheep'] });

    expect(state('profiled', 'bulls')).toEqual({ checked: true, disabled: true });
    expect(state('other', 'bulls')).toEqual({ checked: false, disabled: true });
    expect(state('profiled', 'sheep')).toEqual({ checked: false, disabled: true });
    expect(state('other', 'sheep')).toEqual({ checked: true, disabled: false });
  });

  it('disables an inactive species unless it is already ticked, so it can be unticked', () => {
    const { box, state } = build({ profiled: ['goats'] });

    expect(state('profiled', 'goats')).toEqual({ checked: true, disabled: false });
    expect(state('other', 'goats')).toEqual({ checked: false, disabled: true });

    tick(box('profiled', 'goats'), false);

    expect(state('profiled', 'goats')).toEqual({ checked: false, disabled: true });
  });

  it('submits only the group, because the species beneath it are disabled', () => {
    const { box, form } = build();

    tick(box('profiled', 'cattle'));

    const submitted = [...new FormData(form).getAll('profiled')];
    expect(submitted).toEqual(['cattle']);
  });

  it('lists the species ticked directly, in order, and not those beneath a group', () => {
    const { box, status } = build();

    tick(box('profiled', 'sheep'));
    tick(box('profiled', 'cattle'));

    expect(status('profiled').hidden).toBe(false);
    expect(status('profiled').textContent).toBe('Selected: Cattle, Sheep');
    expect(status('other').hidden).toBe(true);
  });

  it('includes an inactive species that is still ticked in the summary', () => {
    const { status } = build({ other: ['goats'] });

    expect(status('other').textContent).toBe('Selected: Goats (inactive)');
  });

  it('warns that data will be removed when a profiled species is unticked', () => {
    const { box, warning } = build({ profiled: ['sheep'] });

    expect(warning.hidden).toBe(true);

    tick(box('profiled', 'sheep'), false);

    expect(warning.hidden).toBe(false);
  });

  it('does not warn when an other affected species is unticked', () => {
    const { box, warning } = build({ other: ['sheep'] });

    tick(box('other', 'sheep'), false);

    expect(warning.hidden).toBe(true);
  });

  it('never warns when the page is not editing a profile', () => {
    const { box, warning } = build({ profiled: ['sheep'], warningEnabled: false });

    tick(box('profiled', 'sheep'), false);

    expect(warning.hidden).toBe(true);
  });

  it('opens and closes the same branch in the other tree', async () => {
    build();
    const toggle = (treeName) => document.querySelector(`#${treeName} #${treeName}-cattle`).closest('.app-tree__item').querySelector('.app-tree__toggle');

    toggle('profiled').click();
    await Promise.resolve();

    expect(toggle('profiled').getAttribute('aria-expanded')).toBe('false');
    expect(toggle('other').getAttribute('aria-expanded')).toBe('false');

    toggle('other').click();
    await Promise.resolve();

    expect(toggle('profiled').getAttribute('aria-expanded')).toBe('true');
  });
});

describe('profile-properties reset', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    document.body.innerHTML = '';
    vi.useRealTimers();
  });

  it('clears the title and every selection, and keeps focus on the button', () => {
    const { box, state, editor, reset, status } = build({ profiled: ['cattle'], other: ['sheep'] });
    const inputSeen = vi.fn();
    editor.addEventListener('input', inputSeen);

    reset.click();

    expect(editor.innerHTML).toBe('');
    expect(inputSeen).toHaveBeenCalledTimes(1);
    expect(box('profiled', 'cattle').checked).toBe(false);
    expect(state('profiled', 'bulls')).toEqual({ checked: false, disabled: false });
    expect(state('other', 'sheep')).toEqual({ checked: false, disabled: false });
    expect(status('profiled').hidden).toBe(true);
    expect(document.activeElement).toBe(reset);
  });

  it('announces that the selection was cleared', () => {
    const { reset, announcement } = build({ profiled: ['cattle'] });

    reset.click();
    expect(announcement.textContent).toBe('');

    vi.advanceTimersByTime(100);

    expect(announcement.textContent).toBe('Selection cleared');
  });

  it('warns that data will be removed when profiled species were ticked', () => {
    const { reset, warning } = build({ profiled: ['cattle'] });

    reset.click();

    expect(warning.hidden).toBe(false);
  });

  it('does not warn when nothing profiled was ticked', () => {
    const { reset, warning } = build({ other: ['sheep'] });

    reset.click();

    expect(warning.hidden).toBe(true);
  });

  it('keeps the title on a single line by ignoring Enter', () => {
    const { editor } = build();
    const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });

    editor.dispatchEvent(enter);

    expect(enter.defaultPrevented).toBe(true);
  });

  it('exposes the title editor as a single-line textbox', () => {
    const { editor } = build();

    expect(editor.getAttribute('role')).toBe('textbox');
    expect(editor.getAttribute('aria-multiline')).toBe('false');
  });
});
