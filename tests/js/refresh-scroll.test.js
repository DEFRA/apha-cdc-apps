import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const KEY = 'app:refresh-scroll';
const PATH = '/SurveillanceProfiles/EditProfileQuestions/p1';

let loadHandlers;

function page({ refresh = true, error = false } = {}) {
  document.body.innerHTML = `
    ${error ? '<div class="govuk-error-summary" tabindex="-1">There is a problem</div>' : ''}
    <div class="govuk-error-summary" hidden>The references dialog's own, hidden summary</div>
    <form>${refresh ? '<button type="submit" id="refresh" hidden data-relevance-refresh>Refresh the questions</button>' : ''}</form>`;
}

async function load() {
  vi.resetModules();
  await import('../../src/CDC.Web/wwwroot/js/refresh-scroll.js');
}

// The script waits for the page's load event, which the window would also give to the scripts of earlier tests, so each one's is kept here.
const finishLoading = () => loadHandlers.forEach((handler) => handler());
const scrolledTo = () => window.scrollTo.mock.calls;
const keep = (value) => sessionStorage.setItem(KEY, typeof value === 'string' ? value : JSON.stringify(value));

// Presses the refresh button, which would submit the form: jsdom cannot, so the press is stopped once the script has heard it.
function press() {
  const button = document.getElementById('refresh');

  button.addEventListener('click', (event) => event.preventDefault());
  button.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
}

describe('refresh-scroll', () => {
  beforeEach(() => {
    loadHandlers = [];
    sessionStorage.clear();
    window.history.pushState({}, '', PATH);
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

    const addListener = window.addEventListener.bind(window);
    vi.spyOn(window, 'addEventListener').mockImplementation((type, handler, options) => {
      if (type === 'load') {
        loadHandlers.push(handler);
      } else {
        addListener(type, handler, options);
      }
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    Object.defineProperty(document, 'readyState', { value: 'complete', configurable: true });
  });

  describe('when the refresh button is pressed', () => {
    it('keeps where the page is scrolled to, and which page it is', async () => {
      page();
      Object.defineProperty(window, 'scrollY', { value: 640, configurable: true });
      await load();

      press();

      expect(JSON.parse(sessionStorage.getItem(KEY))).toEqual({ path: PATH, top: 640 });
    });

    it('still lets the page refresh when the browser refuses the storage', async () => {
      page();
      await load();
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new DOMException('Denied', 'SecurityError');
      });

      expect(press).not.toThrow();
      expect(sessionStorage.getItem(KEY)).toBeNull();
    });

    it('has nothing to keep it for on a page with no refresh button', async () => {
      page({ refresh: false });

      await expect(load()).resolves.not.toThrow();
      expect(sessionStorage.getItem(KEY)).toBeNull();
    });
  });

  describe('when the page that answers opens', () => {
    beforeEach(() => {
      Object.defineProperty(document, 'readyState', { value: 'loading', configurable: true });
    });

    it('puts the position back once the page has loaded, and only once', async () => {
      keep({ path: PATH, top: 640 });
      page();
      await load();

      expect(scrolledTo()).toEqual([]);

      finishLoading();

      expect(scrolledTo()).toEqual([[0, 640]]);
      expect(sessionStorage.getItem(KEY)).toBeNull();
    });

    it('puts the position back at once when the page has already loaded', async () => {
      Object.defineProperty(document, 'readyState', { value: 'complete', configurable: true });
      keep({ path: PATH, top: 215 });
      page();
      await load();

      expect(scrolledTo()).toEqual([[0, 215]]);
    });

    it('leaves a page that is not the one that was refreshed at the top', async () => {
      keep({ path: '/SurveillanceProfiles/EditProfileQuestions/another', top: 640 });
      page();
      await load();
      finishLoading();

      expect(scrolledTo()).toEqual([]);
      expect(sessionStorage.getItem(KEY)).toBeNull();
    });

    it('leaves a page that answers with an error at the top, where the error summary is', async () => {
      keep({ path: PATH, top: 640 });
      page({ error: true });
      await load();
      finishLoading();

      expect(scrolledTo()).toEqual([]);
    });

    it.each([
      ['nothing was kept', null],
      ['what was kept is not a position', { path: PATH, top: 'far down' }],
      ['what was kept cannot be read', 'not json']
    ])('stays at the top when %s', async (_, kept) => {
      if (kept !== null) {
        keep(kept);
      }

      page();
      await load();
      finishLoading();

      expect(scrolledTo()).toEqual([]);
    });

    it('stays at the top when the browser refuses the storage', async () => {
      page();
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new DOMException('Denied', 'SecurityError');
      });
      await load();
      finishLoading();

      expect(scrolledTo()).toEqual([]);
    });
  });
});
