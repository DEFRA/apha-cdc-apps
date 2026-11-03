import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/search-filters.js';
import '../../src/CDC.Web/wwwroot/js/paged-region.js';
import { paginationHtml } from './helpers/pagination-html.js';
import { click } from './helpers/click.js';

function buildForm() {
  document.body.innerHTML = `
    <form id="search-form" method="get" action="/SurveillanceProfiles/Search">
      <input type="checkbox" id="DisplayPublished" name="DisplayPublished" value="true" data-app-search-auto-refresh checked />
      <input type="checkbox" id="DisplayDraft" name="DisplayDraft" value="true" data-app-search-auto-refresh />
      <input type="checkbox" id="DisplayScenarios" name="DisplayScenarios" value="true" data-app-search-auto-refresh />
    </form>
    <div id="search-results-container">Original results</div>
  `;

  // The wiring only runs once, on DOMContentLoaded; re-dispatch it after rebuilding the DOM so
  // the listeners attach to this test's fresh elements.
  document.dispatchEvent(new Event('DOMContentLoaded'));

  return {
    displayPublished: document.getElementById('DisplayPublished'),
    displayDraft: document.getElementById('DisplayDraft'),
    results: document.getElementById('search-results-container')
  };
}

function toggle(checkbox) {
  checkbox.checked = !checkbox.checked;
  checkbox.dispatchEvent(new Event('change', { bubbles: true }));
}

describe('search-filters', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    window.history.replaceState(null, '', '/');
  });

  it('fetches the results fragment as XHR and swaps it in, without changing the URL', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: () => Promise.resolve('<p>New results</p>') });
    vi.stubGlobal('fetch', fetchMock);
    const urlBefore = window.location.href;

    const { displayDraft, results } = buildForm();
    toggle(displayDraft);
    await vi.waitFor(() => expect(results.innerHTML).toBe('<p>New results</p>'));

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toContain('DisplayDraft=true');
    expect(options.headers['X-Requested-With']).toBe('XMLHttpRequest');
    expect(window.location.href).toBe(urlBefore);
  });

  // The server remembers the filters for an address with none; an address that carries a search would win over
  // that with the filters the search was run with, so it follows the change.
  it('brings an address that already carries a search up to date with the filters, so coming back from a profile finds them', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, text: () => Promise.resolve('<p>New results</p>') }));
    window.history.replaceState(null, '', '/SurveillanceProfiles/Search?DisplayPublished=true&SelectedLetter=B');

    const { displayDraft, results } = buildForm();
    toggle(displayDraft);
    await vi.waitFor(() => expect(results.innerHTML).toBe('<p>New results</p>'));

    expect(window.location.pathname).toBe('/SurveillanceProfiles/Search');
    expect(Object.fromEntries(new URLSearchParams(window.location.search)))
      .toEqual({ DisplayPublished: 'true', DisplayDraft: 'true' });
  });

  it('leaves the address that carries a search alone when the filter change cannot be fetched', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, text: () => Promise.resolve('') }));
    window.history.replaceState(null, '', '/SurveillanceProfiles/Search?DisplayPublished=true&SelectedLetter=B');

    const { displayDraft, results } = buildForm();
    toggle(displayDraft);
    await vi.waitFor(() => expect(results.textContent).toContain('There is a problem'));

    expect(window.location.search).toBe('?DisplayPublished=true&SelectedLetter=B');
  });

  it('disables the auto-refresh controls and marks the results as busy while the request is in flight', async () => {
    let resolveFetch;
    vi.stubGlobal('fetch', vi.fn(() => new Promise((resolve) => { resolveFetch = resolve; })));

    const { displayPublished, displayDraft, results } = buildForm();
    toggle(displayPublished);

    await vi.waitFor(() => expect(results.getAttribute('aria-busy')).toBe('true'));
    expect(displayPublished.disabled).toBe(true);
    expect(displayDraft.disabled).toBe(true);

    resolveFetch({ ok: true, text: () => Promise.resolve('<p>Done</p>') });
    await vi.waitFor(() => expect(results.hasAttribute('aria-busy')).toBe(false));
    expect(displayPublished.disabled).toBe(false);
    expect(displayDraft.disabled).toBe(false);
  });

  it('shows an inline error banner when the server responds with a non-success status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, text: () => Promise.resolve('') }));

    const { displayDraft, results } = buildForm();
    toggle(displayDraft);

    await vi.waitFor(() => expect(results.textContent).toContain('There is a problem'));
  });

  it('shows an inline error banner when the request fails outright', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));

    const { displayDraft, results } = buildForm();
    toggle(displayDraft);

    await vi.waitFor(() => expect(results.textContent).toContain('Check your connection'));
  });

  it('discards a stale response that resolves after a later request has already completed', async () => {
    const firstResponse = new Promise((resolve) => {
      // Resolved after the second request below, so its (older) result must be ignored.
      setTimeout(() => resolve({ ok: true, text: () => Promise.resolve('<p>Stale</p>') }), 20);
    });
    const secondResponse = Promise.resolve({ ok: true, text: () => Promise.resolve('<p>Fresh</p>') });

    const fetchMock = vi.fn()
      .mockReturnValueOnce(firstResponse)
      .mockReturnValueOnce(secondResponse);
    vi.stubGlobal('fetch', fetchMock);

    const { displayPublished, displayDraft, results } = buildForm();
    toggle(displayPublished);
    toggle(displayDraft);

    await vi.waitFor(() => expect(results.innerHTML).toBe('<p>Fresh</p>'));
    await new Promise((resolve) => setTimeout(resolve, 30));
    expect(results.innerHTML).toBe('<p>Fresh</p>');
  });

  it('asks for the form fields on the search address, not on top of the query string the page was reached with', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: () => Promise.resolve('<p>New results</p>') });
    vi.stubGlobal('fetch', fetchMock);
    window.history.replaceState(null, '', '/SurveillanceProfiles/Search?SelectedLetter=B&PageNumber=2');
    document.body.innerHTML = `
      <form id="search-form" method="get">
        <input type="checkbox" id="DisplayDraft" name="DisplayDraft" value="true" data-app-search-auto-refresh />
      </form>
      <div id="search-results-container">Original results</div>
    `;
    document.dispatchEvent(new Event('DOMContentLoaded'));

    toggle(document.getElementById('DisplayDraft'));
    await vi.waitFor(() => expect(document.getElementById('search-results-container').innerHTML).toBe('<p>New results</p>'));

    const requested = new URL(fetchMock.mock.calls[0][0]);
    expect(requested.pathname).toBe('/SurveillanceProfiles/Search');
    expect(requested.search).toBe('?DisplayDraft=true');
  });
});

// The letter and page links carry the address of the last search; the script sends the form instead so the
// filters go as they are now, which is what every postback of the legacy page did. A letter submits the form,
// and a page is fetched in the background like a filter change.
function buildSearchPage({ searchText = '', letter = 'All', page = '2', withPageField = true } = {}) {
  document.body.innerHTML = `
    <form id="search-form" method="get" action="/SurveillanceProfiles/Search">
      <input type="hidden" name="SelectedLetter" value="${letter}" />
      <input type="text" id="SearchText" name="SearchText" value="${searchText}" />
      <a id="letter-b" href="/SurveillanceProfiles/Search?SearchText=foo&SelectedLetter=B" data-app-search-letter="B">B</a>
      <input type="checkbox" id="DisplayDraft" name="DisplayDraft" value="true" data-app-search-auto-refresh checked />
    </form>
    <div id="search-results-container">
      ${withPageField ? `<input type="hidden" id="PageNumber" name="PageNumber" value="${page}" form="search-form" />` : ''}
      <a id="next" href="/SurveillanceProfiles/Search?SearchText=foo&PageNumber=3" data-app-page="3"><span>Next</span></a>
      <a id="elsewhere" href="/somewhere-else">Elsewhere</a>
    </div>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));

  const form = document.getElementById('search-form');
  const submissions = [];
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    submissions.push(Object.fromEntries(new FormData(form)));
  });

  return {
    form,
    submissions,
    searchText: document.getElementById('SearchText'),
    results: document.getElementById('search-results-container'),
    letterLink: document.getElementById('letter-b'),
    nextLink: document.getElementById('next'),
    elsewhereLink: document.getElementById('elsewhere')
  };
}

// What the server sends back for a page of results: the page number field, and the pagination.
function resultsFragment({ current, total }) {
  return `
    <input type="hidden" id="PageNumber" name="PageNumber" value="${current}" form="search-form" />
    ${paginationHtml({ current, total, label: 'Search results pages' })}
  `;
}

const requestedFields = (url) => Object.fromEntries(new URL(url).searchParams);

describe('search-filters letter and page links', () => {
  let fetchMock;

  beforeEach(() => {
    document.body.innerHTML = '';
    fetchMock = vi.fn().mockResolvedValue({ ok: true, text: () => Promise.resolve(resultsFragment({ current: 3, total: 5 })) });
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    window.history.replaceState(null, '', '/');
  });

  it('submits the form with the keyword as it is now, and from the first page, when a letter is clicked', () => {
    const { submissions, letterLink, searchText } = buildSearchPage({ searchText: 'foo', page: '3' });
    searchText.value = '';

    expect(click(letterLink)).toBe(true);

    expect(submissions).toEqual([{ SelectedLetter: 'B', SearchText: '', PageNumber: '1', DisplayDraft: 'true' }]);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('fetches the page asked for in the background, with the keyword as it is now, when a page link is clicked', async () => {
    const { submissions, nextLink, searchText, results } = buildSearchPage({ letter: 'A' });
    searchText.value = 'bovine';

    expect(click(nextLink)).toBe(true);
    await vi.waitFor(() => expect(results.querySelector('.govuk-pagination')).not.toBeNull());

    expect(submissions).toEqual([]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, options] = fetchMock.mock.calls[0];
    expect(requestedFields(url)).toEqual({ SelectedLetter: 'A', SearchText: 'bovine', PageNumber: '3', DisplayDraft: 'true' });
    expect(options.headers['X-Requested-With']).toBe('XMLHttpRequest');
  });

  it('brings the address up to date with the page shown, as following the link used to', async () => {
    const { nextLink, searchText, results } = buildSearchPage({ letter: 'A' });
    searchText.value = 'bovine';

    click(nextLink);
    await vi.waitFor(() => expect(results.querySelector('.govuk-pagination')).not.toBeNull());

    expect(window.location.pathname).toBe('/SurveillanceProfiles/Search');
    expect(Object.fromEntries(new URLSearchParams(window.location.search)))
      .toEqual({ SelectedLetter: 'A', SearchText: 'bovine', PageNumber: '3', DisplayDraft: 'true' });
  });

  it('takes a click on something inside the link as a click on the link', async () => {
    const { submissions, nextLink, results } = buildSearchPage();

    expect(click(nextLink.querySelector('span'))).toBe(true);
    await vi.waitFor(() => expect(results.querySelector('.govuk-pagination')).not.toBeNull());

    expect(submissions).toEqual([]);
    expect(requestedFields(fetchMock.mock.calls[0][0]).PageNumber).toBe('3');
  });

  it.each([
    ['control', { ctrlKey: true }],
    ['command', { metaKey: true }],
    ['shift', { shiftKey: true }],
    ['alt', { altKey: true }],
    ['middle button', { button: 1 }]
  ])('leaves a %s click to the link itself', (_name, init) => {
    const { submissions, letterLink, nextLink } = buildSearchPage();

    expect(click(letterLink, init)).toBe(false);
    expect(click(nextLink, init)).toBe(false);

    expect(submissions).toEqual([]);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('leaves other links alone', () => {
    const { submissions, elsewhereLink } = buildSearchPage();

    expect(click(elsewhereLink)).toBe(false);

    expect(submissions).toEqual([]);
  });

  it('still works on page links that arrive with a refreshed results fragment', async () => {
    const { submissions, results } = buildSearchPage();
    results.innerHTML = `
      <input type="hidden" id="PageNumber" name="PageNumber" value="1" form="search-form" />
      <a id="refreshed" href="/SurveillanceProfiles/Search?PageNumber=2" data-app-page="2">2</a>
    `;

    expect(click(document.getElementById('refreshed'))).toBe(true);
    await vi.waitFor(() => expect(results.querySelector('.govuk-pagination')).not.toBeNull());

    expect(submissions).toEqual([]);
    expect(requestedFields(fetchMock.mock.calls[0][0]).PageNumber).toBe('2');
  });

  it.each([
    ['Next', '.govuk-pagination__next a', { current: 2, total: 5 }, { current: 3, total: 5 }, '.govuk-pagination__next a', 'Page 3 of 5'],
    ['Previous', '.govuk-pagination__prev a', { current: 4, total: 5 }, { current: 3, total: 5 }, '.govuk-pagination__prev a', 'Page 3 of 5'],
    ['a page number', 'a[aria-label="Page 4"]', { current: 2, total: 5 }, { current: 4, total: 5 }, '.govuk-pagination__item--current a', 'Page 4 of 5'],
    ['Next to the last page', '.govuk-pagination__next a', { current: 4, total: 5 }, { current: 5, total: 5 }, '.govuk-pagination__item--current a', 'Page 5 of 5'],
    ['Previous to the first page', '.govuk-pagination__prev a', { current: 2, total: 5 }, { current: 1, total: 5 }, '.govuk-pagination__item--current a', 'Page 1 of 5']
  ])('puts focus where %s was and says which page is shown, as the link it was on is gone', async (_name, followed, before, after, focusedSelector, announced) => {
    fetchMock.mockResolvedValue({ ok: true, text: () => Promise.resolve(resultsFragment(after)) });
    const { results } = buildSearchPage();
    results.innerHTML = resultsFragment(before);
    const link = results.querySelector(followed);

    click(link);
    await vi.waitFor(() => expect(link.isConnected).toBe(false));

    expect(document.activeElement).toBe(results.querySelector(focusedSelector));
    expect(document.querySelector('[role="status"]').textContent).toBe(announced);
  });

  it('shows the error and leaves the address alone when the page cannot be fetched', async () => {
    fetchMock.mockResolvedValue({ ok: false, text: () => Promise.resolve('') });
    const { nextLink, results } = buildSearchPage();

    click(nextLink);

    await vi.waitFor(() => expect(results.textContent).toContain('There is a problem'));
    expect(window.location.search).toBe('');
  });

  it('follows the link as it is when the form has no page number to set', () => {
    const { submissions, letterLink, nextLink } = buildSearchPage({ withPageField: false });

    expect(click(letterLink)).toBe(false);
    expect(click(nextLink)).toBe(false);

    expect(submissions).toEqual([]);
  });

  it('follows a letter link as it is when the form has no selected letter to set', () => {
    const { form, submissions, letterLink } = buildSearchPage();
    form.querySelector('[name="SelectedLetter"]').remove();

    expect(click(letterLink)).toBe(false);

    expect(submissions).toEqual([]);
  });
});
