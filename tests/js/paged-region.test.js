import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/paged-region.js';
import { click } from './helpers/click.js';
import { paginationHtml } from './helpers/pagination-html.js';

const rowsOf = (current) => `<p>Rows of page ${current}</p>`;

// The whole page the server sends back for a page of the list, from which only the region is used.
function serverPage({ current, total, rows = rowsOf(current), title = `Items (page ${current} of ${total}) - D2R2` }) {
  return `<!DOCTYPE html><html><head><title>${title}</title></head><body>
    <p id="outside">Outside, as the server sent it</p>
    <div id="items-region" data-app-paged-region>${rows}${paginationHtml({ current, total })}</div>
  </body></html>`;
}

const respondWith = (html, extra = {}) => ({ ok: true, redirected: false, text: () => Promise.resolve(html), ...extra });

function buildPage({ current = 2, total = 5 } = {}) {
  document.title = `Items (page ${current} of ${total}) - D2R2`;
  document.body.innerHTML = `
    <p id="outside">Outside, as first loaded</p>
    <div id="items-region" data-app-paged-region>${rowsOf(current)}<a id="edit" href="/items/1/edit">Edit</a>${paginationHtml({ current, total })}</div>
  `;

  return {
    region: document.getElementById('items-region'),
    outside: document.getElementById('outside'),
    next: document.querySelector('.govuk-pagination__next a')
  };
}

// Stands in for the address bar, which jsdom cannot navigate, and returns what follows a link the script gave up on.
function stubLocation() {
  const assign = vi.fn();
  vi.stubGlobal('location', { origin: window.location.origin, href: window.location.href, assign });

  return assign;
}

describe('paged-region', () => {
  let fetchMock;

  beforeEach(() => {
    document.body.innerHTML = '';
    fetchMock = vi.fn().mockResolvedValue(respondWith(serverPage({ current: 3, total: 5 })));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    window.history.replaceState(null, '', '/');
    document.title = '';
  });

  it('loads the page asked for in the background and replaces only the region', async () => {
    const { region, outside, next } = buildPage();

    expect(click(next)).toBe(true);
    await vi.waitFor(() => expect(region.textContent).toContain('Rows of page 3'));

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const requested = new URL(fetchMock.mock.calls[0][0]);
    expect(requested.pathname + requested.search).toBe('/items?PageNumber=3');
    expect(region.textContent).not.toContain('Rows of page 2');
    expect(document.getElementById('outside')).toBe(outside);
    expect(outside.textContent).toBe('Outside, as first loaded');
  });

  it('takes a click on something inside the link as a click on the link', async () => {
    const { region, next } = buildPage();

    expect(click(next.querySelector('span'))).toBe(true);

    await vi.waitFor(() => expect(region.textContent).toContain('Rows of page 3'));
  });

  it('brings the title and the address up to date with the page shown', async () => {
    const { region } = buildPage();

    click(region.querySelector('.govuk-pagination__next a'));

    await vi.waitFor(() => expect(document.title).toBe('Items (page 3 of 5) - D2R2'));
    expect(window.location.pathname + window.location.search).toBe('/items?PageNumber=3');
  });

  it('marks the region busy until the page has loaded', async () => {
    let resolveFetch;
    fetchMock.mockReturnValue(new Promise((resolve) => { resolveFetch = resolve; }));
    const { region, next } = buildPage();

    click(next);
    expect(region.getAttribute('aria-busy')).toBe('true');

    resolveFetch(respondWith(serverPage({ current: 3, total: 5 })));
    await vi.waitFor(() => expect(region.hasAttribute('aria-busy')).toBe(false));
  });

  it.each([
    ['Next', '.govuk-pagination__next a', { current: 2, total: 5 }, { current: 3, total: 5 }, '.govuk-pagination__next a', 'Page 3 of 5'],
    ['Previous', '.govuk-pagination__prev a', { current: 4, total: 5 }, { current: 3, total: 5 }, '.govuk-pagination__prev a', 'Page 3 of 5'],
    ['a page number', 'a[aria-label="Page 4"]', { current: 2, total: 5 }, { current: 4, total: 5 }, '.govuk-pagination__item--current a', 'Page 4 of 5'],
    ['Next to the last page', '.govuk-pagination__next a', { current: 4, total: 5 }, { current: 5, total: 5 }, '.govuk-pagination__item--current a', 'Page 5 of 5'],
    ['Previous to the first page', '.govuk-pagination__prev a', { current: 2, total: 5 }, { current: 1, total: 5 }, '.govuk-pagination__item--current a', 'Page 1 of 5']
  ])('puts focus where %s was and says which page is shown, as the link it was on is gone', async (_name, followed, before, after, focusedSelector, announced) => {
    fetchMock.mockResolvedValue(respondWith(serverPage(after)));
    const { region } = buildPage(before);
    const link = region.querySelector(followed);

    click(link);
    await vi.waitFor(() => expect(link.isConnected).toBe(false));

    expect(document.activeElement).toBe(region.querySelector(focusedSelector));
    expect(document.querySelector('[role="status"]').textContent).toBe(announced);
  });

  it('says nothing and moves no focus when the page shown has no pagination to describe', async () => {
    fetchMock.mockResolvedValue(respondWith(
      '<html><head><title>Items - D2R2</title></head><body><div id="items-region" data-app-paged-region>Only rows</div></body></html>'));
    const { region, next } = buildPage();

    click(next);
    await vi.waitFor(() => expect(region.textContent).toBe('Only rows'));

    expect(document.querySelector('[role="status"]')).toBeNull();
    expect(document.activeElement).toBe(document.body);
  });

  it.each([
    ['control', { ctrlKey: true }],
    ['command', { metaKey: true }],
    ['shift', { shiftKey: true }],
    ['alt', { altKey: true }],
    ['middle button', { button: 1 }]
  ])('leaves a %s click to the link itself', (_name, init) => {
    const { next } = buildPage();

    expect(click(next, init)).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves a link that opens in another tab to the browser', () => {
    const { next } = buildPage();
    next.target = '_blank';

    expect(click(next)).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves a link to another site to the browser', () => {
    const { next } = buildPage();
    next.href = 'https://example.com/items?PageNumber=3';

    expect(click(next)).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves a pagination link without an address to the browser', () => {
    const { next } = buildPage();
    next.removeAttribute('href');

    expect(click(next)).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves other links in the region alone', () => {
    buildPage();

    expect(click(document.getElementById('edit'))).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves the pagination of a page that is not in a paged region alone', () => {
    document.body.innerHTML = paginationHtml({ current: 2, total: 5 });

    expect(click(document.querySelector('.govuk-pagination__next a'))).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves the pagination between sections alone, as that changes more than the region', () => {
    document.body.innerHTML = `
      <div id="items-region" data-app-paged-region>
        <nav class="govuk-pagination govuk-pagination--block" aria-label="Section pagination">
          <div class="govuk-pagination__next"><a class="govuk-link govuk-pagination__link" href="/sections/2" rel="next">Next</a></div>
        </nav>
      </div>`;

    expect(click(document.querySelector('.govuk-pagination__link'))).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves a region without an id alone, as the page it is replaced from could not be matched', () => {
    const { region, next } = buildPage();
    region.removeAttribute('id');

    expect(click(next)).toBe(false);

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([
    ['the server responds with an error', () => respondWith('', { ok: false })],
    ['the server redirects elsewhere', () => respondWith(serverPage({ current: 3, total: 5 }), { redirected: true })],
    ['the page sent back has no such region', () => respondWith('<html><head><title>Sign in</title></head><body>Sign in</body></html>')]
  ])('follows the link as the browser would when %s', async (_name, response) => {
    const assign = stubLocation();
    fetchMock.mockResolvedValue(response());
    const { region, next } = buildPage();

    click(next);

    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith(next.href));
    expect(region.textContent).toContain('Rows of page 2');
  });

  it('follows the link as the browser would when the request fails outright', async () => {
    const assign = stubLocation();
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));
    const { next } = buildPage();

    click(next);

    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith(next.href));
  });

  it('drops a page that arrives after a later click has been answered', async () => {
    const slow = new Promise((resolve) => {
      // Resolved after the second request below, so its (older) page must be ignored.
      setTimeout(() => resolve(respondWith(serverPage({ current: 3, total: 5, rows: '<p>Stale rows</p>' }))), 20);
    });
    const fast = Promise.resolve(respondWith(serverPage({ current: 4, total: 5, rows: '<p>Fresh rows</p>' })));
    fetchMock.mockReturnValueOnce(slow).mockReturnValueOnce(fast);
    const { region } = buildPage();

    click(region.querySelector('.govuk-pagination__next a'));
    click(region.querySelector('a[aria-label="Page 4"]'));

    await vi.waitFor(() => expect(region.textContent).toContain('Fresh rows'));
    await new Promise((resolve) => setTimeout(resolve, 30));
    expect(region.textContent).toContain('Fresh rows');
    expect(region.textContent).not.toContain('Stale rows');
    expect(window.location.search).toBe('?PageNumber=4');
  });
});
