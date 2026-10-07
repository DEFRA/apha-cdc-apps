import { beforeEach, describe, expect, it } from 'vitest';
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
