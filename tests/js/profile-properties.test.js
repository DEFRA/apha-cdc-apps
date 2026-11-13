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

// A species box as the page renders it: its own "Close all" above its tree.
function panel(kind, checked) {
  return `
    <div class="app-tree__panel" data-species-tree="${kind}">
      <div class="app-tree__controls">
        <button type="button" class="app-tree__toggle-all" data-app-tree-action="toggle-all" aria-expanded="true">
          <span class="app-tree__toggle-all-text">Close all</span>
        </button>
      </div>
      ${tree(kind, checked)}
    </div>`;
}

function build({ profiled = [], other = [], warningEnabled = true } = {}) {
  document.body.innerHTML = `
    <form data-module="app-profile-properties">
      <div id="ProfileTitleEditor" contenteditable="true" data-app-title-hidden-input="ProfileTitle">Anthrax</div>
      <input type="hidden" id="ProfileTitle" name="ProfileTitle" value="Anthrax">
      <div data-module="app-species-selector" data-species-warning-enabled="${warningEnabled}">
        <span class="app-validator-marker" data-species-required-mark aria-hidden="true" hidden>*</span>
        ${panel('profiled', profiled)}
        <output data-species-status="profiled">Selected: <strong data-species-selected></strong></output>
        ${panel('other', other)}
        <output data-species-status="other">Selected: <strong data-species-selected></strong></output>
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
    selected: (kind) => document.querySelector(`[data-species-status="${kind}"] [data-species-selected]`).textContent,
    requiredMark: document.querySelector('[data-species-required-mark]'),
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
    const { box, status, selected } = build();

    tick(box('profiled', 'sheep'));
    tick(box('profiled', 'cattle'));

    expect(status('profiled').textContent).toBe('Selected: Cattle, Sheep');
    expect(selected('profiled')).toBe('Cattle, Sheep');
    expect(selected('other')).toBe('');
  });

  it('includes an inactive species that is still ticked in the summary', () => {
    const { status } = build({ other: ['goats'] });

    expect(status('other').textContent).toBe('Selected: Goats (inactive)');
  });

  // As in legacy, "Selected:" is always there, and only the species named after it are in bold.
  it('shows "Selected:" in both boxes when no species is ticked, with nothing in bold', () => {
    const { status, selected } = build();

    ['profiled', 'other'].forEach((kind) => {
      expect(status(kind).hidden).toBe(false);
      expect(status(kind).textContent.trim()).toBe('Selected:');
      expect(selected(kind)).toBe('');
    });
  });

  it('puts the species in bold, and not the "Selected:" before them', () => {
    const { box, status } = build();

    tick(box('other', 'sheep'));

    const bold = [...status('other').querySelectorAll('strong')];
    expect(bold.map((element) => element.textContent)).toEqual(['Sheep']);
    expect(status('other').textContent).toBe('Selected: Sheep');
  });

  it('goes back to a bare "Selected:" when the last species is unticked', () => {
    const { box, status, selected } = build({ profiled: ['sheep'] });

    tick(box('profiled', 'sheep'), false);

    expect(status('profiled').hidden).toBe(false);
    expect(status('profiled').textContent.trim()).toBe('Selected:');
    expect(selected('profiled')).toBe('');
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

  it('opens and closes the same branch in the other tree', () => {
    build();
    const toggle = (treeName) => document.querySelector(`#${treeName} #${treeName}-cattle`).closest('.app-tree__item').querySelector('.app-tree__toggle');

    toggle('profiled').click();

    expect(toggle('profiled').getAttribute('aria-expanded')).toBe('false');
    expect(toggle('other').getAttribute('aria-expanded')).toBe('false');

    toggle('other').click();

    expect(toggle('profiled').getAttribute('aria-expanded')).toBe('true');
  });

  // As in the legacy page, each box's control works on its own tree only; it is single branches that stay level between the trees.
  it('opens and closes every branch in its own box only, from the Open all / Close all in that box', async () => {
    build();
    const toggleAll = (kind) => document.querySelector(`[data-species-tree="${kind}"] .app-tree__toggle-all`);
    const label = (kind) => toggleAll(kind).querySelector('.app-tree__toggle-all-text').textContent;
    const hidden = (kind) => [...document.querySelectorAll(`[data-species-tree="${kind}"] .app-tree__group`)].map((group) => group.hidden);

    toggleAll('profiled').click();
    // Lets any delayed mirroring to the other tree happen before it is checked.
    await Promise.resolve();

    expect(hidden('profiled')).toEqual([true, true]);
    expect(label('profiled')).toBe('Open all');
    expect(hidden('other')).toEqual([false, false]);
    expect(label('other')).toBe('Close all');
    expect(toggleAll('other').getAttribute('aria-expanded')).toBe('true');

    toggleAll('other').click();
    await Promise.resolve();

    expect(hidden('other')).toEqual([true, true]);
    expect(label('other')).toBe('Open all');

    toggleAll('profiled').click();
    await Promise.resolve();

    expect(hidden('profiled')).toEqual([false, false]);
    expect(label('profiled')).toBe('Close all');
    expect(hidden('other')).toEqual([true, true]);
  });
});

describe('profile-properties required profiled species', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    document.body.innerHTML = '';
    vi.useRealTimers();
  });

  it('shows no asterisk when the page loads, whatever is ticked', () => {
    expect(build().requiredMark.hidden).toBe(true);
    expect(build({ profiled: ['sheep'] }).requiredMark.hidden).toBe(true);
  });

  it('shows the asterisk once the last profiled species is unticked', () => {
    const { box, requiredMark } = build({ profiled: ['sheep'] });

    tick(box('profiled', 'sheep'), false);

    expect(requiredMark.hidden).toBe(false);
  });

  it('takes the asterisk away when a profiled species is ticked again', () => {
    const { box, requiredMark } = build({ profiled: ['sheep'] });
    tick(box('profiled', 'sheep'), false);

    tick(box('profiled', 'sheep'));

    expect(requiredMark.hidden).toBe(true);
  });

  it('shows no asterisk while a profiled species is still ticked', () => {
    const { box, requiredMark } = build({ profiled: ['sheep', 'cattle'] });

    tick(box('profiled', 'sheep'), false);

    expect(requiredMark.hidden).toBe(true);
  });

  it('counts a group as a profiled species', () => {
    const { box, requiredMark } = build({ profiled: ['sheep'] });
    tick(box('profiled', 'sheep'), false);
    expect(requiredMark.hidden).toBe(false);

    tick(box('profiled', 'cattle'));
    expect(requiredMark.hidden).toBe(true);

    tick(box('profiled', 'cattle'), false);
    expect(requiredMark.hidden).toBe(false);
  });

  it('leaves the asterisk away when other affected species change and a profiled species stays ticked', () => {
    const { box, requiredMark } = build({ profiled: ['sheep'] });

    tick(box('other', 'cattle'));
    tick(box('other', 'cattle'), false);

    expect(requiredMark.hidden).toBe(true);
  });

  it('shows the asterisk when ticking a group among the other affected species takes the last profiled species', () => {
    const { box, requiredMark } = build({ profiled: ['bulls'] });

    tick(box('other', 'cattle'));

    expect(box('profiled', 'bulls').checked).toBe(false);
    expect(requiredMark.hidden).toBe(false);
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
    const { box, state, editor, reset, selected } = build({ profiled: ['cattle'], other: ['sheep'] });
    const inputSeen = vi.fn();
    editor.addEventListener('input', inputSeen);

    reset.click();

    expect(editor.innerHTML).toBe('');
    expect(inputSeen).toHaveBeenCalledTimes(1);
    expect(box('profiled', 'cattle').checked).toBe(false);
    expect(state('profiled', 'bulls')).toEqual({ checked: false, disabled: false });
    expect(state('other', 'sheep')).toEqual({ checked: false, disabled: false });
    expect(selected('profiled')).toBe('');
    expect(selected('other')).toBe('');
    expect(document.activeElement).toBe(reset);
  });

  // As in legacy, where Reset selections validated the page: both required fields are flagged at once.
  it('asks the title to be checked once it is cleared, after its editor has been told of the change', () => {
    const { editor, reset } = build();
    const seen = [];
    ['input', 'app-title-validate'].forEach((type) => editor.addEventListener(type, () => seen.push(type)));

    reset.click();

    expect(seen).toEqual(['input', 'app-title-validate']);
  });

  it('flags the profiled species as required, whether or not any were ticked', () => {
    const first = build({ profiled: ['cattle'] });
    first.reset.click();
    expect(first.requiredMark.hidden).toBe(false);

    const second = build();
    second.reset.click();
    expect(second.requiredMark.hidden).toBe(false);
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
});
