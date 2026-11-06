import { beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/tree-view.js';

function buildTree() {
  document.body.innerHTML = `
    <form>
      <output class="app-tree__status" id="species-status" hidden></output>
      <button type="button" class="app-tree__toggle-all" data-app-tree-action="toggle-all" aria-expanded="false">
        <span class="app-tree__toggle-all-text">Open all</span>
      </button>
      <button type="button" class="govuk-button" data-app-tree-action="clear">Remove selection</button>

      <div class="app-tree" data-module="app-tree-view" data-app-tree-name="species" data-app-tree-name-plural="species">
        <li class="app-tree__item" data-app-tree-expanded="true">
          <div class="app-tree__row">
            <div class="govuk-radios__item app-tree__radio">
              <input class="govuk-radios__input" id="species-animals" name="species" type="radio" value="animals">
              <label class="govuk-label govuk-radios__label" for="species-animals">Animals</label>
            </div>
          </div>
          <ul class="app-tree__group">
            <li class="app-tree__item" data-app-tree-expanded="false">
              <div class="app-tree__row">
                <div class="govuk-radios__item app-tree__radio">
                  <input class="govuk-radios__input" id="species-cattle" name="species" type="radio" value="cattle">
                  <label class="govuk-label govuk-radios__label" for="species-cattle">Cattle</label>
                </div>
              </div>
            </li>
          </ul>
        </li>
      </div>
    </form>
  `;

  const tree = document.querySelector('[data-module="app-tree-view"]');
  const status = document.querySelector('.app-tree__status');
  const toggleAllButton = document.querySelector('.app-tree__toggle-all');
  const clearButton = document.querySelector('[data-app-tree-action="clear"]');

  return { tree, status, toggleAllButton, clearButton };
}

// Mirrors the Search page's species filter: a multi-select (checkbox) tree with one branch
// (Poultry) and four leaves, used to verify ancestor/descendant exclusivity.
function buildMultiSelectTree() {
  document.body.innerHTML = `
    <form>
      <output class="app-tree__status" id="species-status" hidden></output>
      <div class="app-tree" data-module="app-tree-view" data-app-tree-multiple="true" data-app-tree-name="species" data-app-tree-name-plural="species">
        <li class="app-tree__item" data-app-tree-expanded="true">
          <div class="app-tree__row">
            <div class="govuk-checkboxes__item app-tree__radio">
              <input class="govuk-checkboxes__input" id="species-poultry" name="species" type="checkbox" value="poultry">
              <label class="govuk-label govuk-checkboxes__label" for="species-poultry">Poultry - domestic and commercial</label>
            </div>
          </div>
          <ul class="app-tree__group">
            <li class="app-tree__item">
              <div class="app-tree__row">
                <div class="govuk-checkboxes__item app-tree__radio">
                  <input class="govuk-checkboxes__input" id="species-turkeys" name="species" type="checkbox" value="turkeys">
                  <label class="govuk-label govuk-checkboxes__label" for="species-turkeys">Turkeys</label>
                </div>
              </div>
            </li>
            <li class="app-tree__item">
              <div class="app-tree__row">
                <div class="govuk-checkboxes__item app-tree__radio">
                  <input class="govuk-checkboxes__input" id="species-chickens" name="species" type="checkbox" value="chickens">
                  <label class="govuk-label govuk-checkboxes__label" for="species-chickens">Chickens</label>
                </div>
              </div>
            </li>
            <li class="app-tree__item">
              <div class="app-tree__row">
                <div class="govuk-checkboxes__item app-tree__radio">
                  <input class="govuk-checkboxes__input" id="species-ducks" name="species" type="checkbox" value="ducks">
                  <label class="govuk-label govuk-checkboxes__label" for="species-ducks">Ducks</label>
                </div>
              </div>
            </li>
            <li class="app-tree__item">
              <div class="app-tree__row">
                <div class="govuk-checkboxes__item app-tree__radio">
                  <input class="govuk-checkboxes__input" id="species-geese" name="species" type="checkbox" value="geese">
                  <label class="govuk-label govuk-checkboxes__label" for="species-geese">Geese</label>
                </div>
              </div>
            </li>
          </ul>
        </li>
      </div>
    </form>
  `;

  const tree = document.querySelector('[data-module="app-tree-view"]');

  return { tree };
}

function check(input) {
  input.checked = true;
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

// Two trees in one form, each in a panel with its own "Open all"/"Close all" - the profile properties page.
function buildTwoPanels() {
  const panel = (name) => `
    <div class="app-tree__panel" id="${name}-panel">
      <div class="app-tree__controls">
        <button type="button" class="app-tree__toggle-all" data-app-tree-action="toggle-all" aria-expanded="true">
          <span class="app-tree__toggle-all-text">Close all</span>
        </button>
      </div>
      <ul class="app-tree" data-module="app-tree-view" data-app-tree-multiple="true">
        <li class="app-tree__item" data-app-tree-expanded="true">
          <div class="app-tree__row">
            <div class="govuk-checkboxes__item app-tree__radio">
              <input class="govuk-checkboxes__input" id="${name}-animals" name="${name}" type="checkbox" value="animals">
              <label class="govuk-label govuk-checkboxes__label" for="${name}-animals">Animals</label>
            </div>
          </div>
          <ul class="app-tree__group">
            <li class="app-tree__item">
              <div class="app-tree__row">
                <div class="govuk-checkboxes__item app-tree__radio">
                  <input class="govuk-checkboxes__input" id="${name}-cattle" name="${name}" type="checkbox" value="cattle">
                  <label class="govuk-label govuk-checkboxes__label" for="${name}-cattle">Cattle</label>
                </div>
              </div>
            </li>
          </ul>
        </li>
      </ul>
    </div>`;

  document.body.innerHTML = `<form>${panel('first')}${panel('second')}</form>`;

  document.querySelectorAll('[data-module="app-tree-view"]').forEach((list) => new window.TreeView(list));

  const toggleAll = (name) => document.querySelector(`#${name}-panel .app-tree__toggle-all`);
  const label = (name) => toggleAll(name).querySelector('.app-tree__toggle-all-text').textContent;
  const group = (name) => document.querySelector(`#${name}-panel .app-tree__group`);

  return { toggleAll, label, group };
}

function treeNode(id, children = '', expanded = true) {
  return `
    <li class="app-tree__item" data-app-tree-expanded="${expanded}">
      <div class="app-tree__row">
        <div class="govuk-checkboxes__item app-tree__radio">
          <input class="govuk-checkboxes__input" id="species-${id}" name="species" type="checkbox" value="${id}">
          <label class="govuk-label govuk-checkboxes__label" for="species-${id}">${id}</label>
        </div>
      </div>
      ${children && `<ul class="app-tree__group">${children}</ul>`}
    </li>`;
}

// A tree under its own "Close all", as the Search and Create bespoke report pages render one.
function buildTreeUnderCloseAll(nodes, toggleAttributes = '') {
  document.body.innerHTML = `
    <form>
      <div class="app-tree__panel">
        <div class="app-tree__controls">
          <button type="button" class="app-tree__toggle-all" data-app-tree-action="toggle-all" ${toggleAttributes} aria-expanded="true">
            <span class="app-tree__toggle-all-text">Close all</span>
          </button>
        </div>
        <ul class="app-tree" data-module="app-tree-view" data-app-tree-multiple="true">${nodes}</ul>
      </div>
    </form>`;

  const tree = document.querySelector('[data-module="app-tree-view"]');
  new window.TreeView(tree);

  const toggleAll = document.querySelector('.app-tree__toggle-all');

  return {
    toggleAll,
    label: () => toggleAll.querySelector('.app-tree__toggle-all-text').textContent,
    groups: () => Array.from(tree.querySelectorAll('.app-tree__group')).map((group) => group.hidden),
    branchStates: () => Array.from(tree.querySelectorAll('.app-tree__toggle')).map((toggle) => toggle.getAttribute('aria-expanded')),
    branchToggles: () => Array.from(tree.querySelectorAll('.app-tree__toggle'))
  };
}

// The Search page's species filter: a multi-level tree that starts with every branch open, so its toggle reads "Close all".
function buildSearchSpeciesFilter() {
  return buildTreeUnderCloseAll(
    treeNode('animals', treeNode('cattle', treeNode('dairy')) + treeNode('sheep')) + treeNode('poultry', treeNode('chickens')));
}

// The Create bespoke report tree: only the profile is open, yet its toggle still reads "Close all".
function buildBespokeReportTree() {
  return buildTreeUnderCloseAll(
    treeNode('profile', treeNode('section1', treeNode('question1') + treeNode('question2'), false) + treeNode('section2', treeNode('question3'), false)),
    'data-app-tree-close-all-while-any-open="true"');
}


describe('tree-view', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('toggles all branches and updates the label state', () => {
    const { tree, toggleAllButton } = buildTree();
    new window.TreeView(tree);

    expect(toggleAllButton.getAttribute('aria-expanded')).toBe('true');
    expect(toggleAllButton.querySelector('.app-tree__toggle-all-text').textContent).toBe('Close all');

    toggleAllButton.click();

    expect(toggleAllButton.getAttribute('aria-expanded')).toBe('false');
    expect(toggleAllButton.querySelector('.app-tree__toggle-all-text').textContent).toBe('Open all');
    expect(tree.querySelector('.app-tree__group').hidden).toBe(true);
  });

  it('closes the entire tree from Close all and opens the entire tree from Open all, starting fully open', () => {
    const { toggleAll, label, groups, branchStates } = buildSearchSpeciesFilter();

    expect(label()).toBe('Close all');
    expect(toggleAll.getAttribute('aria-expanded')).toBe('true');
    expect(groups()).toEqual([false, false, false]);
    expect(branchStates()).toEqual(['true', 'true', 'true']);

    toggleAll.click();

    expect(label()).toBe('Open all');
    expect(toggleAll.getAttribute('aria-expanded')).toBe('false');
    expect(groups()).toEqual([true, true, true]);
    expect(branchStates()).toEqual(['false', 'false', 'false']);

    toggleAll.click();

    expect(label()).toBe('Close all');
    expect(toggleAll.getAttribute('aria-expanded')).toBe('true');
    expect(groups()).toEqual([false, false, false]);
    expect(branchStates()).toEqual(['true', 'true', 'true']);
  });

  it('opens a tree one level but still starts as Close all, closing the entire tree from it and opening the entire tree from Open all', () => {
    const { toggleAll, label, groups, branchStates } = buildBespokeReportTree();

    expect(label()).toBe('Close all');
    expect(toggleAll.getAttribute('aria-expanded')).toBe('true');
    expect(groups()).toEqual([false, true, true]);

    toggleAll.click();

    expect(label()).toBe('Open all');
    expect(toggleAll.getAttribute('aria-expanded')).toBe('false');
    expect(groups()).toEqual([true, true, true]);
    expect(branchStates()).toEqual(['false', 'false', 'false']);

    toggleAll.click();

    expect(label()).toBe('Close all');
    expect(toggleAll.getAttribute('aria-expanded')).toBe('true');
    expect(groups()).toEqual([false, false, false]);
    expect(branchStates()).toEqual(['true', 'true', 'true']);
  });

  it('keeps offering to close such a tree while any branch is open, and offers to open it once every branch is closed', () => {
    const { label, branchToggles } = buildBespokeReportTree();
    const [profile, section] = branchToggles();

    section.click();
    expect(label()).toBe('Close all');

    section.click();
    expect(label()).toBe('Close all');

    profile.click();
    expect(label()).toBe('Open all');

    profile.click();
    expect(label()).toBe('Close all');
  });

  it('offers to open a tree that does not open one level as soon as any of its branches is closed', () => {
    const { toggleAll, label, branchToggles, branchStates } = buildSearchSpeciesFilter();

    branchToggles()[1].click();
    expect(label()).toBe('Open all');

    toggleAll.click();
    expect(label()).toBe('Close all');
    expect(branchStates()).toEqual(['true', 'true', 'true']);
  });

  it('gives each tree in a form the Open all / Close all in its own panel, not the other tree\'s', () => {
    const { toggleAll, label, group } = buildTwoPanels();

    toggleAll('first').click();

    expect(group('first').hidden).toBe(true);
    expect(label('first')).toBe('Open all');
    expect(group('second').hidden).toBe(false);
    expect(label('second')).toBe('Close all');
    expect(toggleAll('second').getAttribute('aria-expanded')).toBe('true');
  });

  it('announces a branch the user opens or closes, but not one that is unchanged or moved by Open all / Close all', () => {
    const { tree, toggleAllButton } = buildTree();
    const treeView = new window.TreeView(tree);
    const item = tree.querySelector('.app-tree__item');
    const announced = vi.fn();
    tree.addEventListener('app-tree-toggle', (event) => announced(event.target, event.detail.expanded));

    item.querySelector('.app-tree__toggle').click();

    expect(announced).toHaveBeenCalledTimes(1);
    expect(announced).toHaveBeenLastCalledWith(item, false);

    treeView.setExpanded(item, false);
    toggleAllButton.click();

    expect(item.querySelector('.app-tree__group').hidden).toBe(false);
    expect(announced).toHaveBeenCalledTimes(1);
  });

  it('shows the selected item and clears it on command', () => {
    const { tree, status, clearButton } = buildTree();
    new window.TreeView(tree);

    const input = tree.querySelector('input[value="animals"]');
    input.checked = true;
    input.dispatchEvent(new Event('change', { bubbles: true }));

    expect(status.hidden).toBe(false);
    expect(status.textContent).toBe('Selected: Animals');

    clearButton.click();

    expect(status.hidden).toBe(true);
    expect(status.textContent).toBe('');
    expect(tree.querySelector('input[value="animals"]').checked).toBe(false);
  });

  it('supports keyboard navigation to the next visible item', () => {
    const { tree } = buildTree();
    new window.TreeView(tree);

    const parentRadio = tree.querySelector('input[value="animals"]');
    const childRadio = tree.querySelector('input[value="cattle"]');

    parentRadio.focus();
    parentRadio.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));

    const parentToggle = parentRadio.closest('.app-tree__item').querySelector('.app-tree__toggle');
    expect(parentToggle.getAttribute('aria-expanded')).toBe('true');

    childRadio.focus();
    childRadio.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }));

    expect(document.activeElement).toBe(parentRadio);
  });

  it('skips disabled items when moving with the arrow keys', () => {
    const { tree } = buildMultiSelectTree();
    new window.TreeView(tree);

    const poultry = tree.querySelector('input[value="poultry"]');
    tree.querySelector('input[value="turkeys"]').disabled = true;
    poultry.focus();

    poultry.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));

    expect(document.activeElement).toBe(tree.querySelector('input[value="chickens"]'));
  });

  it('unchecks every descendant when a parent is checked (multi-select only)', () => {
    const { tree } = buildMultiSelectTree();
    new window.TreeView(tree);

    const poultry = tree.querySelector('input[value="poultry"]');
    const turkeys = tree.querySelector('input[value="turkeys"]');
    const chickens = tree.querySelector('input[value="chickens"]');
    check(turkeys);
    check(chickens);

    check(poultry);

    expect(poultry.checked).toBe(true);
    expect(turkeys.checked).toBe(false);
    expect(chickens.checked).toBe(false);
    expect(tree.querySelector('input[value="ducks"]').checked).toBe(false);
    expect(tree.querySelector('input[value="geese"]').checked).toBe(false);
  });

  it('unchecks the parent when a child is checked (multi-select only)', () => {
    const { tree } = buildMultiSelectTree();
    new window.TreeView(tree);

    const poultry = tree.querySelector('input[value="poultry"]');
    const ducks = tree.querySelector('input[value="ducks"]');
    check(poultry);

    check(ducks);

    expect(ducks.checked).toBe(true);
    expect(poultry.checked).toBe(false);
  });

  it('leaves unrelated siblings checked when a child is checked (multi-select only)', () => {
    const { tree } = buildMultiSelectTree();
    new window.TreeView(tree);

    const turkeys = tree.querySelector('input[value="turkeys"]');
    const ducks = tree.querySelector('input[value="ducks"]');
    check(turkeys);

    check(ducks);

    expect(turkeys.checked).toBe(true);
    expect(ducks.checked).toBe(true);
  });

  it('does not cascade unchecking (single-select trees are unaffected)', () => {
    const { tree } = buildTree();
    new window.TreeView(tree);

    const parentRadio = tree.querySelector('input[value="animals"]');
    const childRadio = tree.querySelector('input[value="cattle"]');
    check(parentRadio);

    check(childRadio);

    // Native radio "name" grouping - not the new hierarchy logic - is what clears parentRadio
    // here, so this also confirms enforceHierarchyExclusivity is skipped for single-select.
    expect(parentRadio.checked).toBe(false);
    expect(childRadio.checked).toBe(true);
  });
});
