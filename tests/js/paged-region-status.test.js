import { beforeEach, describe, expect, it, vi } from 'vitest';

// Each test loads the script afresh, as the browser does with every page, so it sees the page that test built.
describe('paged-region status', () => {
  beforeEach(() => {
    vi.resetModules();
    document.body.innerHTML = '';
  });

  async function loadScript() {
    await import('../../src/CDC.Web/wwwroot/js/paged-region.js');
  }

  it.each([
    ['a paged region', '<div id="items-region" data-app-paged-region></div>'],
    ['the search results', '<div id="search-results-container"></div>']
  ])('puts the live status in the page up front when the page has %s, so the first change is announced', async (_name, markup) => {
    document.body.innerHTML = markup;

    await loadScript();

    const status = document.querySelector('[role="status"]');
    expect(status).not.toBeNull();
    expect(status.classList.contains('govuk-visually-hidden')).toBe(true);
    expect(status.textContent).toBe('');
  });

  it('adds nothing to a page without either', async () => {
    document.body.innerHTML = '<p>Nothing paged here</p>';

    await loadScript();

    expect(document.querySelector('[role="status"]')).toBeNull();
  });
});
