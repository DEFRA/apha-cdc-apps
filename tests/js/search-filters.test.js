import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/search-filters.js';

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
});

// The letter and page links carry the address of the last search; the script submits the form instead so the
// filters go as they are now, which is what every postback of the legacy page did.
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

// Reports whether the script took the click over, then cancels it so jsdom does not try to navigate.
function click(element, init = {}) {
  let defaultPrevented;
  const record = (event) => {
    defaultPrevented = event.defaultPrevented;
    event.preventDefault();
  };

  document.addEventListener('click', record, { once: true });
  element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ...init }));
  document.removeEventListener('click', record);

  return defaultPrevented;
}

describe('search-filters letter and page links', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('submits the form with the keyword as it is now, and from the first page, when a letter is clicked', () => {
    const { submissions, letterLink, searchText } = buildSearchPage({ searchText: 'foo', page: '3' });
    searchText.value = '';

    expect(click(letterLink)).toBe(true);

    expect(submissions).toEqual([{ SelectedLetter: 'B', SearchText: '', PageNumber: '1', DisplayDraft: 'true' }]);
  });

  it('submits the form with the keyword as it is now, and the page asked for, when a page link is clicked', () => {
    const { submissions, nextLink, searchText } = buildSearchPage({ letter: 'A' });
    searchText.value = 'bovine';

    expect(click(nextLink)).toBe(true);

    expect(submissions).toEqual([{ SelectedLetter: 'A', SearchText: 'bovine', PageNumber: '3', DisplayDraft: 'true' }]);
  });

  it('takes a click on something inside the link as a click on the link', () => {
    const { submissions, nextLink } = buildSearchPage();

    expect(click(nextLink.querySelector('span'))).toBe(true);

    expect(submissions).toHaveLength(1);
    expect(submissions[0].PageNumber).toBe('3');
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
  });

  it('leaves other links alone', () => {
    const { submissions, elsewhereLink } = buildSearchPage();

    expect(click(elsewhereLink)).toBe(false);

    expect(submissions).toEqual([]);
  });

  it('still works on page links that arrive with a refreshed results fragment', () => {
    const { submissions, results } = buildSearchPage();
    results.innerHTML = `
      <input type="hidden" id="PageNumber" name="PageNumber" value="1" form="search-form" />
      <a id="refreshed" href="/SurveillanceProfiles/Search?PageNumber=2" data-app-page="2">2</a>
    `;

    expect(click(document.getElementById('refreshed'))).toBe(true);

    expect(submissions).toHaveLength(1);
    expect(submissions[0].PageNumber).toBe('2');
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
