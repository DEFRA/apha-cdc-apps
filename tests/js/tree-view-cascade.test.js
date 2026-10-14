import { beforeEach, describe, expect, it } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/tree-view.js';

// Mirrors the "Create bespoke report" tree: a profile, two sections, and for each question a
// guidance node followed by a question node.
function buildBespokeTree() {
  const node = (id, label, kind = '', children = '') => `
    <li class="app-tree__item" data-app-tree-expanded="true"${kind ? ` data-app-tree-kind="${kind}"` : ''}>
      <div class="app-tree__row">
        <div class="govuk-checkboxes__item app-tree__radio">
          <input class="govuk-checkboxes__input" id="${id}" name="CheckedNodes" type="checkbox" value="${id}">
          <label class="govuk-label govuk-checkboxes__label" for="${id}">${label}</label>
        </div>
      </div>
      ${children ? `<ul class="app-tree__group">${children}</ul>` : ''}
    </li>`;

  document.body.innerHTML = `
    <form>
      <button type="button" data-app-tree-action="exclude-guidance">Exclude guidance questions</button>
      <button type="button" data-app-tree-action="include-guidance">Include guidance questions</button>
      <button type="button" data-app-tree-action="clear">Remove selection</button>
      <output class="app-tree__status" hidden></output>
      <div data-app-tree-announcement></div>
      <ul class="app-tree" data-module="app-tree-view" data-app-tree-multiple="true" data-app-tree-cascade="true">
        ${node('profile', 'Anthrax', '', [
          node('s1', '1 : Description', '', [
            node('g1', '1 Guidance : Is it here?', 'guidance'),
            node('q1', '1 : Is it here?'),
            node('g2', '2 Guidance : Where is it?', 'guidance'),
            node('q2', '2 : Where is it?')
          ].join('')),
          node('s2', '2 : Epidemiology', '', [
            node('g3', '1 Guidance : Who?', 'guidance'),
            node('q3', '1 : Who?')
          ].join(''))
        ].join(''))}
      </ul>
    </form>
  `;

  const tree = document.querySelector('[data-module="app-tree-view"]');
  const view = new window.TreeView(tree);

  return {
    view,
    status: document.querySelector('.app-tree__status'),
    announcement: document.querySelector('[data-app-tree-announcement]'),
    box: (id) => document.getElementById(id),
    click: (action) => document.querySelector(`[data-app-tree-action="${action}"]`).click(),
    ticked: () => [...document.querySelectorAll('input:checked')].map((input) => input.id)
  };
}

function setChecked(input, checked) {
  input.checked = checked;
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

describe('tree-view cascading selection', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('ticks everything under a ticked section, and the profile above it', () => {
    const { box, ticked, status } = buildBespokeTree();

    setChecked(box('s1'), true);

    expect(ticked()).toEqual(['profile', 's1', 'g1', 'q1', 'g2', 'q2']);
    expect(status.hidden).toBe(false);
    expect(status.textContent).toBe('Selected: 1 : Description');
  });

  it('ticks the section and profile above a single ticked question, but not its siblings', () => {
    const { box, ticked, status } = buildBespokeTree();

    setChecked(box('q2'), true);

    expect(ticked()).toEqual(['profile', 's1', 'q2']);
    expect(status.textContent).toBe('Selected: 2 : Where is it?');
  });

  it('unticks the section and profile when the last ticked question is unticked', () => {
    const { box, ticked, status } = buildBespokeTree();
    setChecked(box('q1'), true);

    setChecked(box('q1'), false);

    expect(ticked()).toEqual([]);
    expect(status.hidden).toBe(true);
  });

  it('keeps a section ticked while another of its questions is still ticked', () => {
    const { box, ticked } = buildBespokeTree();
    setChecked(box('q1'), true);
    setChecked(box('g1'), true);

    setChecked(box('q1'), false);

    expect(ticked()).toEqual(['profile', 's1', 'g1']);
  });

  it('clears a section and its questions when it is unticked, leaving the other section and the profile', () => {
    const { box, ticked } = buildBespokeTree();
    setChecked(box('s1'), true);
    setChecked(box('s2'), true);

    setChecked(box('s1'), false);

    expect(ticked()).toEqual(['profile', 's2', 'g3', 'q3']);
  });

  it('lists a section rather than its questions once all of them are ticked, and never the profile', () => {
    const { box, status } = buildBespokeTree();

    setChecked(box('s1'), true);
    setChecked(box('s2'), true);

    expect(status.textContent).toBe('Selected: 1 : Description, 2 : Epidemiology');
  });

  it('lists the questions of a section when only some are ticked', () => {
    const { box, status } = buildBespokeTree();

    setChecked(box('q1'), true);
    setChecked(box('g2'), true);
    setChecked(box('q3'), true);

    expect(status.textContent).toBe('Selected: 1 : Is it here?, 2 Guidance : Where is it?, 1 : Who?');
  });

  it('clears the whole selection on command', () => {
    const { box, click, ticked, status } = buildBespokeTree();
    setChecked(box('s1'), true);

    click('clear');

    expect(ticked()).toEqual([]);
    expect(status.hidden).toBe(true);
  });
});

describe('tree-view include and exclude guidance', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('ticks the guidance only under ticked sections', () => {
    const { box, click, ticked, announcement } = buildBespokeTree();
    setChecked(box('q1'), true);
    setChecked(box('q3'), true);
    setChecked(box('s2'), false);

    click('include-guidance');

    expect(ticked()).toEqual(['profile', 's1', 'g1', 'q1', 'g2']);
    expect(announcement.textContent).toBe('Included all guidance questions');
  });

  it('unticks the guidance, keeping the questions', () => {
    const { box, click, ticked, announcement } = buildBespokeTree();
    setChecked(box('s1'), true);

    click('exclude-guidance');

    expect(ticked()).toEqual(['profile', 's1', 'q1', 'q2']);
    expect(announcement.textContent).toBe('Excluded all guidance questions');
  });

  it('unticks a section, and the profile above it, when only its guidance was ticked', () => {
    const { box, click, ticked } = buildBespokeTree();
    setChecked(box('g1'), true);
    setChecked(box('g3'), true);

    click('exclude-guidance');

    expect(ticked()).toEqual([]);
  });

  it('does nothing to a section that is not ticked', () => {
    const { click, ticked } = buildBespokeTree();

    click('include-guidance');

    expect(ticked()).toEqual([]);
  });
});
