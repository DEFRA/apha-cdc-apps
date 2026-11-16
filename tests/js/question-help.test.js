import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const questionId = '11111111-2222-3333-4444-555555555555';
const otherQuestionId = '99999999-8888-7777-6666-555555555555';

let dialog;
let content;
let failure;
let opened;

function answer({ ok = true, redirected = false, body = '' } = {}) {
  return Promise.resolve({ ok, redirected, text: () => Promise.resolve(body) });
}

// Lets every promise the script is waiting on run.
function settle() {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function stubFetch(implementation) {
  const fetchMock = vi.fn(implementation);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

beforeAll(async () => {
  // jsdom does not implement the dialog methods, so record what the script asks for.
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
    opened.push(this.id);
  };
  HTMLDialogElement.prototype.close = function close() {
    this.removeAttribute('open');
  };

  document.body.innerHTML = `
    <button type="button" id="help" data-question-help="${questionId}"><img alt="Help for 1.1" id="help-icon" /></button>
    <button type="button" id="other-help" data-question-help="${otherQuestionId}">Help</button>
    <button type="button" id="elsewhere">Elsewhere</button>
    <dialog class="app-modal" id="user-guidance-dialog" data-help-url="/SurveillanceProfiles/ProfileQuestionHelp">
      <div data-user-guidance-content></div>
      <p data-user-guidance-error hidden>We could not load the user guidance. Try again later.</p>
    </dialog>`;
  dialog = document.getElementById('user-guidance-dialog');
  content = dialog.querySelector('[data-user-guidance-content]');
  failure = dialog.querySelector('[data-user-guidance-error]');

  await import('../../src/CDC.Web/wwwroot/js/question-help.js');
});

beforeEach(() => {
  opened = [];
  dialog.removeAttribute('open');
  content.replaceChildren();
  failure.hidden = true;
});

describe('question-help', () => {
  it('opens the dialog with the guidance of the question whose help icon was used', async () => {
    const fetchMock = stubFetch(() => answer({ body: '<h3 class="govuk-heading-s">Is it endemic?</h3><div class="govuk-body"><p>Say yes.</p></div>' }));

    document.getElementById('help').click();
    await settle();

    const url = new URL(fetchMock.mock.calls[0][0]);
    expect(url.pathname).toBe('/SurveillanceProfiles/ProfileQuestionHelp');
    expect(url.searchParams.get('profileQuestionId')).toBe(questionId);
    expect(content.querySelector('h3').textContent).toBe('Is it endemic?');
    expect(content.querySelector('p').textContent).toBe('Say yes.');
    expect(failure.hidden).toBe(true);
    expect(opened).toEqual(['user-guidance-dialog']);
  });

  it('also answers a click on the icon inside the help button', async () => {
    const fetchMock = stubFetch(() => answer({ body: '<p>Text</p>' }));

    document.getElementById('help-icon').click();
    await settle();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(opened).toEqual(['user-guidance-dialog']);
  });

  it('opens each link of the guidance in a new tab, and says so', async () => {
    stubFetch(() => answer({ body: '<p>See <a href="https://example.test/guide">the guide</a>.</p>' }));

    document.getElementById('help').click();
    await settle();

    const link = content.querySelector('a');
    expect(link.target).toBe('_blank');
    expect(link.rel).toBe('noopener noreferrer');
    expect(link.querySelector('.govuk-visually-hidden').textContent).toBe(' (opens in new tab)');
  });

  it('does nothing for a click anywhere else', async () => {
    const fetchMock = stubFetch(() => answer());

    document.getElementById('elsewhere').click();
    document.body.click();
    await settle();

    expect(fetchMock).not.toHaveBeenCalled();
    expect(opened).toEqual([]);
  });

  it.each([
    ['CDC.Web could not load it', () => answer({ ok: false })],
    ['the session has ended and the browser was sent to sign in', () => answer({ redirected: true, body: '<p>Sign in</p>' })],
    ['the request could not be made', () => Promise.reject(new TypeError('Failed to fetch'))]
  ])('says it could not load the guidance when %s', async (_reason, implementation) => {
    stubFetch(implementation);
    content.innerHTML = '<p>Earlier guidance</p>';

    document.getElementById('help').click();
    await settle();

    expect(failure.hidden).toBe(false);
    expect(content.childNodes.length).toBe(0);
    expect(opened).toEqual(['user-guidance-dialog']);
  });

  it('forgets an earlier failure once guidance loads', async () => {
    stubFetch(() => answer({ ok: false }));
    document.getElementById('help').click();
    await settle();
    expect(failure.hidden).toBe(false);

    stubFetch(() => answer({ body: '<p>Now it works</p>' }));
    dialog.removeAttribute('open');
    document.getElementById('help').click();
    await settle();

    expect(failure.hidden).toBe(true);
    expect(content.textContent).toBe('Now it works');
  });

  it('does not open the dialog again when it is already open', async () => {
    stubFetch(() => answer({ body: '<p>Text</p>' }));
    dialog.setAttribute('open', '');

    document.getElementById('help').click();
    await settle();

    expect(opened).toEqual([]);
  });

  it('shows the guidance of the last icon used, when an earlier answer arrives late', async () => {
    let releaseFirst;
    const first = new Promise((resolve) => { releaseFirst = resolve; });
    let calls = 0;
    stubFetch(() => {
      calls += 1;
      return calls === 1 ? first : answer({ body: '<p>Second question</p>' });
    });

    document.getElementById('help').click();
    document.getElementById('other-help').click();
    await settle();
    releaseFirst({ ok: true, redirected: false, text: () => Promise.resolve('<p>First question</p>') });
    await settle();

    expect(content.textContent).toBe('Second question');
  });
});

describe('question-help on a page without the dialog', () => {
  it('does nothing', async () => {
    document.body.innerHTML = '<button type="button" data-question-help="x">Help</button>';
    vi.resetModules();

    await expect(import('../../src/CDC.Web/wwwroot/js/question-help.js')).resolves.toBeDefined();

    expect(document.querySelector('dialog')).toBeNull();
  });
});
