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
});
