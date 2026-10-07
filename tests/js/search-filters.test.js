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
