import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const SCRIPT = '../../src/CDC.Web/wwwroot/js/session-timeout.js';
const STATUS_URL = '/Session/Status';
const EXTEND_URL = '/Session/Extend';
const SECOND = 1000;
const MINUTE = 60 * SECOND;

// The defaults: a 20-minute session, with the warning 5 minutes before it ends, so the first check is due after 15 minutes.
const FIRST_CHECK = 15 * MINUTE;

const trackedListeners = [];
let hidden = false;
let server;

// jsdom does not implement the dialog methods, so these stand in for them.
beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close = function close() {
    if (this.hasAttribute('open')) {
      this.removeAttribute('open');
      this.dispatchEvent(new Event('close'));
    }
  };
});

// Each test loads the script afresh, as the browser does with every page, so the listeners one test's copy leaves on the
// document and window are removed afterwards rather than answering the next test's events.
function trackListeners() {
  [document, window].forEach((target) => {
    const original = target.addEventListener.bind(target);

    vi.spyOn(target, 'addEventListener').mockImplementation((type, handler, options) => {
      trackedListeners.push([target, type, handler, options]);
      original(type, handler, options);
    });
  });
}

function fakeServer() {
  const fake = {
    status: { authenticated: true, remainingSeconds: 1200, antiforgeryToken: 'token-1', antiforgeryHeaderName: 'RequestVerificationToken' },
    statusFails: false,
    extend: { ok: true, body: { authenticated: true, remainingSeconds: 1200 } }
  };

  fake.fetch = vi.fn(async (url) => {
    if (url === STATUS_URL) {
      if (fake.statusFails) {
        throw new TypeError('Failed to fetch');
      }

      return { ok: true, json: async () => fake.status };
    }

    if (url === EXTEND_URL) {
      return { ok: fake.extend.ok, json: async () => fake.extend.body };
    }

    throw new Error(`Unexpected request to ${url}`);
  });
  fake.calls = (url) => fake.fetch.mock.calls.filter(([requested]) => requested === url);

  return fake;
}

function buildPage({ idleSeconds = 1200, warningSeconds = 300, withConfig = true } = {}) {
  document.body.innerHTML = `
    ${withConfig
      ? `<div hidden data-session-timeout data-status-url="${STATUS_URL}" data-extend-url="${EXTEND_URL}"
              data-idle-seconds="${idleSeconds}" data-warning-seconds="${warningSeconds}"></div>`
      : ''}
    <dialog id="session-warning-dialog">
      <p>You will be signed out in <strong data-session-countdown>5 minutes</strong>.</p>
      <p data-session-extend-error hidden>Your session could not be extended.</p>
      <button type="button" data-session-extend>Yes, extend session</button>
    </dialog>
    <dialog id="session-expired-dialog">
      <a href="/Session/SignInAgain">Sign in again</a>
    </dialog>
  `;

  return {
    warning: document.getElementById('session-warning-dialog'),
    expired: document.getElementById('session-expired-dialog'),
    extendButton: document.querySelector('[data-session-extend]'),
    countdown: document.querySelector('[data-session-countdown]'),
    error: document.querySelector('[data-session-extend-error]')
  };
}

async function load(pageOptions) {
  const page = buildPage(pageOptions);

  trackListeners();
  vi.resetModules();
  await import(SCRIPT);

  return page;
}

// Lets what the script is awaiting (the fetches and the answers it reads) finish.
const flush = () => vi.advanceTimersByTimeAsync(0);

describe('session-timeout', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    hidden = false;
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden });
    server = fakeServer();
    vi.stubGlobal('fetch', server.fetch);
  });

  afterEach(() => {
    trackedListeners.splice(0).forEach(([target, type, handler, options]) => target.removeEventListener(type, handler, options));
    vi.restoreAllMocks();
    vi.clearAllTimers();
    vi.useRealTimers();
    vi.unstubAllGlobals();
    delete document.hidden;
    document.body.innerHTML = '';
  });

  describe('when the page has nothing for it to do', () => {
    it('does nothing without the session configuration', async () => {
      await load({ withConfig: false });

      await vi.advanceTimersByTimeAsync(60 * MINUTE);

      expect(server.fetch).not.toHaveBeenCalled();
      expect(vi.getTimerCount()).toBe(0);
    });

    it.each([['idle', { idleSeconds: 0 }], ['warning', { warningSeconds: 'soon' }]])('does nothing when the %s time is not a positive number', async (_name, options) => {
      await load(options);

      await vi.advanceTimersByTimeAsync(60 * MINUTE);

      expect(server.fetch).not.toHaveBeenCalled();
      expect(vi.getTimerCount()).toBe(0);
    });

    it('does nothing in a browser that cannot fetch', async () => {
      vi.stubGlobal('fetch', undefined);

      await load();

      expect(vi.getTimerCount()).toBe(0);
    });

    it('does nothing in a browser that cannot show a dialog', async () => {
      const showModal = HTMLDialogElement.prototype.showModal;
      HTMLDialogElement.prototype.showModal = undefined;

      try {
        await load();

        expect(vi.getTimerCount()).toBe(0);
      } finally {
        HTMLDialogElement.prototype.showModal = showModal;
      }
    });
  });

  describe('checking the session', () => {
    it('does not ask the server before the warning is expected', async () => {
      await load();

      await vi.advanceTimersByTimeAsync(FIRST_CHECK - SECOND);

      expect(server.fetch).not.toHaveBeenCalled();
    });

    it('asks the server, without sending anything that would extend the session, when the warning is expected', async () => {
      await load();

      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      expect(server.calls(STATUS_URL)).toHaveLength(1);
      expect(server.fetch).toHaveBeenCalledWith(STATUS_URL, expect.objectContaining({ credentials: 'same-origin', cache: 'no-store' }));
      expect(server.fetch.mock.calls[0][1].method).toBeUndefined();
    });

    it('stays quiet while the session has plenty of time, which a request from elsewhere may have given it, and asks again later', async () => {
      const { warning, expired } = await load();

      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      expect(warning.open).toBe(false);
      expect(expired.open).toBe(false);

      await vi.advanceTimersByTimeAsync(FIRST_CHECK - SECOND);
      expect(server.calls(STATUS_URL)).toHaveLength(1);

      await vi.advanceTimersByTimeAsync(SECOND);
      expect(server.calls(STATUS_URL)).toHaveLength(2);
    });

    it('still asks, rather than waiting for ever, when the session is far longer than a timer can count', async () => {
      await load({ idleSeconds: 10000000 });

      await vi.advanceTimersByTimeAsync(2147483646);
      expect(server.fetch).not.toHaveBeenCalled();

      await vi.advanceTimersByTimeAsync(1);
      expect(server.calls(STATUS_URL)).toHaveLength(1);
    });
  });

  describe('the warning', () => {
    beforeEach(() => {
      server.status.remainingSeconds = 280;
    });

    it('opens, with the time left, when the session is within the warning period', async () => {
      const { warning, expired, countdown } = await load();

      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      expect(warning.open).toBe(true);
      expect(expired.open).toBe(false);
      expect(countdown.textContent).toBe('5 minutes');
    });

    it('counts the time left down', async () => {
      const { countdown } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      await vi.advanceTimersByTimeAsync(MINUTE);
      expect(countdown.textContent).toBe('4 minutes');

      await vi.advanceTimersByTimeAsync(3 * MINUTE);
      expect(countdown.textContent).toBe('less than a minute');
    });

    it('closes, and the expired dialog opens, when its time runs out and the server confirms the session has ended', async () => {
      const { warning, expired } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.status = { authenticated: false, remainingSeconds: 0 };

      await vi.advanceTimersByTimeAsync(280 * SECOND);

      expect(warning.open).toBe(false);
      expect(expired.open).toBe(true);
    });

    it('closes, without the expired dialog, when the session was kept alive elsewhere while it was open', async () => {
      const { warning, expired } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.status.remainingSeconds = 1200;

      await vi.advanceTimersByTimeAsync(280 * SECOND);

      expect(warning.open).toBe(false);
      expect(expired.open).toBe(false);
    });

    it('leaves Escape to close it without extending the session', async () => {
      const { warning } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      const escape = new Event('cancel', { cancelable: true });

      warning.dispatchEvent(escape);

      expect(escape.defaultPrevented).toBe(false);
      expect(server.calls(EXTEND_URL)).toHaveLength(0);
    });

    it('stops counting down once it has closed, and is asked about again when the page comes back into view', async () => {
      const { warning, countdown } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      warning.close();
      countdown.textContent = 'unchanged';

      await vi.advanceTimersByTimeAsync(MINUTE);
      expect(countdown.textContent).toBe('unchanged');

      document.dispatchEvent(new Event('visibilitychange'));
      await flush();
      expect(warning.open).toBe(true);
    });
  });

  describe('extending the session', () => {
    beforeEach(() => {
      server.status.remainingSeconds = 100;
    });

    it('posts to the server with the token it was given, then closes the warning and asks again a full warning period before the end', async () => {
      const { warning, extendButton } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      extendButton.click();
      await flush();

      const [url, options] = server.calls(EXTEND_URL)[0];
      expect(url).toBe(EXTEND_URL);
      expect(options.method).toBe('POST');
      expect(options.credentials).toBe('same-origin');
      expect(options.headers.RequestVerificationToken).toBe('token-1');
      expect(warning.open).toBe(false);

      await vi.advanceTimersByTimeAsync(FIRST_CHECK - SECOND);
      expect(server.calls(STATUS_URL)).toHaveLength(1);

      await vi.advanceTimersByTimeAsync(SECOND);
      expect(server.calls(STATUS_URL)).toHaveLength(2);
    });

    it('cannot be pressed again while the request is on its way, and keeps the focus the dialog gave the button', async () => {
      const { extendButton } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      let answer;
      server.fetch.mockImplementation((url) => (url === EXTEND_URL
        ? new Promise((resolve) => { answer = resolve; })
        : Promise.resolve({ ok: true, json: async () => server.status })));
      extendButton.focus();

      extendButton.click();
      await flush();
      expect(extendButton.getAttribute('aria-disabled')).toBe('true');
      expect(document.activeElement).toBe(extendButton);

      extendButton.click();
      await flush();
      expect(server.calls(EXTEND_URL)).toHaveLength(1);

      answer({ ok: true, json: async () => server.extend.body });
      await flush();
      expect(extendButton.hasAttribute('aria-disabled')).toBe(false);
    });

    it('says so, and keeps the warning open, when it fails but the session has not ended', async () => {
      const { warning, expired, extendButton, error } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.extend = { ok: false, body: null };
      extendButton.focus();

      extendButton.click();
      await flush();

      expect(warning.open).toBe(true);
      expect(expired.open).toBe(false);
      expect(error.hidden).toBe(false);
      expect(extendButton.hasAttribute('aria-disabled')).toBe(false);
      expect(document.activeElement).toBe(extendButton);
    });

    it('says so, and keeps the warning open, when the request does not get through', async () => {
      const { warning, extendButton, error } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.fetch.mockImplementation((url) => (url === EXTEND_URL
        ? Promise.reject(new TypeError('Failed to fetch'))
        : Promise.resolve({ ok: true, json: async () => server.status })));

      extendButton.click();
      await flush();

      expect(warning.open).toBe(true);
      expect(error.hidden).toBe(false);
    });

    it('does not say so in a warning that closed because the session turned out to be fine', async () => {
      const { warning, extendButton, error } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.extend = { ok: false, body: null };
      server.status.remainingSeconds = 1200;

      extendButton.click();
      await flush();

      expect(warning.open).toBe(false);
      expect(error.hidden).toBe(true);
    });

    it('treats an answer that does not say how long is left as a failure', async () => {
      const { warning, extendButton, error } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.extend = { ok: true, body: {} };

      extendButton.click();
      await flush();

      expect(warning.open).toBe(true);
      expect(error.hidden).toBe(false);
    });

    it('opens the expired dialog instead when it fails because the session has ended', async () => {
      const { warning, expired, extendButton, error } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.extend = { ok: false, body: null };
      server.status = { authenticated: false, remainingSeconds: 0 };

      extendButton.click();
      await flush();

      expect(warning.open).toBe(false);
      expect(expired.open).toBe(true);
      expect(error.hidden).toBe(true);
    });

    it('is tried without a token when none was ever received, and the failure is reported', async () => {
      server.statusFails = true;
      const { warning, extendButton, error } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      server.extend = { ok: false, body: null };

      extendButton.click();
      await flush();

      expect(server.calls(EXTEND_URL)[0][1].headers.RequestVerificationToken).toBeUndefined();
      expect(warning.open).toBe(true);
      expect(error.hidden).toBe(false);
    });
  });

  describe('the expired dialog', () => {
    beforeEach(() => {
      server.status = { authenticated: false, remainingSeconds: 0 };
    });

    it('opens when the server says the session has ended, and the script stops checking', async () => {
      const { warning, expired } = await load();

      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      expect(expired.open).toBe(true);
      expect(warning.open).toBe(false);

      await vi.advanceTimersByTimeAsync(60 * MINUTE);
      expect(server.calls(STATUS_URL)).toHaveLength(1);
    });

    it('has to be answered: Escape does not close it', async () => {
      const { expired } = await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      const escape = new Event('cancel', { cancelable: true });

      expired.dispatchEvent(escape);

      expect(escape.defaultPrevented).toBe(true);
    });

    it('is not shown again, or asked about, when the page comes back into view', async () => {
      await load();
      await vi.advanceTimersByTimeAsync(FIRST_CHECK);

      document.dispatchEvent(new Event('visibilitychange'));
      await flush();

      expect(server.calls(STATUS_URL)).toHaveLength(1);
    });
  });

  describe('when the server cannot be asked', () => {
    it.each([
      ['it cannot be reached', () => { server.statusFails = true; }],
      ['it answers with an error', () => { server.fetch.mockImplementation(async () => ({ ok: false, json: async () => ({}) })); }],
      ['it answers without saying how long is left', () => { server.status = { authenticated: true }; }],
      ['it answers with nothing', () => { server.status = null; }]
    ])('goes by its own estimate when %s, with no tight loop of asking', async (_name, breakServer) => {
      breakServer();
      const { warning, expired } = await load();

      await vi.advanceTimersByTimeAsync(FIRST_CHECK);
      expect(warning.open).toBe(true);
      expect(expired.open).toBe(false);
      expect(server.fetch).toHaveBeenCalledTimes(1);

      await vi.advanceTimersByTimeAsync(SECOND);
      expect(server.fetch).toHaveBeenCalledTimes(1);

      await vi.advanceTimersByTimeAsync(5 * MINUTE);
      expect(expired.open).toBe(true);
      expect(warning.open).toBe(false);
    });
  });

  describe('when the page comes back into view', () => {
    it('asks again when it becomes visible, but not when it is hidden', async () => {
      await load();

      hidden = true;
      document.dispatchEvent(new Event('visibilitychange'));
      await flush();
      expect(server.fetch).not.toHaveBeenCalled();

      hidden = false;
      document.dispatchEvent(new Event('visibilitychange'));
      await flush();
      expect(server.calls(STATUS_URL)).toHaveLength(1);
    });

    it('asks again when the page is restored from the back/forward cache, but not on an ordinary load', async () => {
      await load();
      const show = (persisted) => {
        const event = new Event('pageshow');
        Object.defineProperty(event, 'persisted', { value: persisted });
        window.dispatchEvent(event);
      };

      show(false);
      await flush();
      expect(server.fetch).not.toHaveBeenCalled();

      show(true);
      await flush();
      expect(server.calls(STATUS_URL)).toHaveLength(1);
    });

    it('does not ask twice at once', async () => {
      server.fetch.mockImplementation(() => new Promise(() => {}));
      await load();

      document.dispatchEvent(new Event('visibilitychange'));
      document.dispatchEvent(new Event('visibilitychange'));
      await flush();

      expect(server.fetch).toHaveBeenCalledTimes(1);
    });
  });
});
