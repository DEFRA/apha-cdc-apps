import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const varies = '22222222-2222-2222-2222-222222222222';
const consistent = '33333333-3333-3333-3333-333333333333';

// The script reads the page once, when it runs, so each test builds its page and then runs the script afresh.
async function load(markup) {
  document.body.innerHTML = markup;
  vi.resetModules();
  await import('../../src/CDC.Web/wwwroot/js/field-visibility.js');
}

// The script waits before it shows the page again, so a test that must pass that wait fakes the clock once the script has loaded.
async function loadWithClock(markup) {
  await load(markup);
  vi.useFakeTimers();
}

function dropDown(id, value = '', attributes = '') {
  return `
    <select id="${id}"${attributes}>
      <option value="">Please select...</option>
      <option value="${varies}"${value === varies ? ' selected' : ''}>Yes - varies across UK</option>
      <option value="${consistent}"${value === consistent ? ' selected' : ''}>Yes - consistent across UK</option>
    </select>`;
}

function dependent(id, controlId, hidden = true) {
  return `<div id="${id}" data-visible-when-field="${controlId}" data-visible-when-value="${varies}"${hidden ? ' hidden' : ''}><textarea></textarea></div>`;
}

function choose(id, value) {
  const select = document.getElementById(id);

  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
}

const isHidden = (id) => document.getElementById(id).hidden;

const refreshButton = '<button type="submit" data-relevance-refresh hidden>Refresh the questions</button>';

// The refresh button would submit the form, which jsdom cannot, so a press is stopped once it has been counted.
function watchRefresh() {
  const refresh = vi.fn((event) => event.preventDefault());

  document.querySelector('[data-relevance-refresh]').addEventListener('click', refresh);

  return refresh;
}

let listeners;

describe('field-visibility', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    listeners = [];

    const addListener = document.addEventListener.bind(document);
    vi.spyOn(document, 'addEventListener').mockImplementation((type, handler, options) => {
      listeners.push([type, handler, options]);
      addListener(type, handler, options);
    });
  });

  // Each test runs the script afresh, so the script of an earlier test must not hear the page of the next.
  afterEach(() => {
    listeners.forEach(([type, handler, options]) => document.removeEventListener(type, handler, options));
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('shows a field when the drop-down holds the answer that shows it, and hides it when the drop-down holds another', async () => {
    await load(`${dropDown('decides')}${dependent('scotland', 'decides')}`);

    expect(isHidden('scotland')).toBe(true);

    choose('decides', varies);
    expect(isHidden('scotland')).toBe(false);

    choose('decides', consistent);
    expect(isHidden('scotland')).toBe(true);

    choose('decides', varies);
    choose('decides', '');
    expect(isHidden('scotland')).toBe(true);
  });

  it('keeps every field that depends on the drop-down in step with it', async () => {
    await load(`${dropDown('decides')}${dependent('scotland', 'decides')}${dependent('wales', 'decides')}${dependent('ireland', 'decides')}`);

    choose('decides', varies);

    expect(['scotland', 'wales', 'ireland'].map(isHidden)).toEqual([false, false, false]);

    choose('decides', consistent);

    expect(['scotland', 'wales', 'ireland'].map(isHidden)).toEqual([true, true, true]);
  });

  it('follows only the drop-down a field names', async () => {
    await load(`${dropDown('first')}${dropDown('second', varies)}${dependent('of-first', 'first')}${dependent('of-second', 'second', false)}`);

    choose('first', varies);

    expect(isHidden('of-first')).toBe(false);
    expect(isHidden('of-second')).toBe(false);

    choose('second', consistent);

    expect(isHidden('of-first')).toBe(false);
    expect(isHidden('of-second')).toBe(true);
  });

  it('brings a field into step with the drop-down when the page opens', async () => {
    await load(`${dropDown('decides', consistent)}${dependent('scotland', 'decides', false)}${dropDown('other', varies)}${dependent('wales', 'other')}`);

    expect(isHidden('scotland')).toBe(true);
    expect(isHidden('wales')).toBe(false);
  });

  it('puts the field back with the answer when a change to it is not confirmed', async () => {
    await load(`${dropDown('decides', consistent)}${dependent('scotland', 'decides')}`);

    choose('decides', varies);
    expect(isHidden('scotland')).toBe(false);

    const select = document.getElementById('decides');
    select.value = consistent;
    select.dispatchEvent(new CustomEvent('app:relevance-cancelled', { bubbles: true }));

    expect(isHidden('scotland')).toBe(true);
  });

  it('ignores a change to a control that is not a drop-down', async () => {
    await load(`${dropDown('decides')}${dependent('scotland', 'decides')}<input type="checkbox" id="box">`);

    document.getElementById('decides').value = varies;
    document.getElementById('box').dispatchEvent(new Event('change', { bubbles: true }));

    expect(isHidden('scotland')).toBe(true);
  });

  it.each([
    ['there is no control for the answer', '', true],
    ['the answer is shown as boxes that cannot be changed', '<div id="decides"><input type="checkbox" disabled checked></div>', false]
  ])('leaves a field as the server showed it when %s', async (_, control, hidden) => {
    await load(`${control}${dependent('scotland', 'decides', hidden)}`);

    expect(isHidden('scotland')).toBe(hidden);
  });

  it('does nothing on a page with no field that depends on an answer', async () => {
    await load(dropDown('decides'));

    expect(() => choose('decides', varies)).not.toThrow();
  });

  describe('when the answer a field depends on changes', () => {
    const page = (attributes = '') => `${refreshButton}${dropDown('decides', '', attributes)}${dependent('scotland', 'decides')}${dropDown('other')}`;

    it('saves the section and shows the page again, as the legacy page did', async () => {
      await loadWithClock(page());
      const refresh = watchRefresh();

      choose('decides', consistent);

      expect(refresh).not.toHaveBeenCalled();

      vi.runAllTimers();

      expect(refresh).toHaveBeenCalledTimes(1);
    });

    it('waits until the user has stopped stepping through the options', async () => {
      await loadWithClock(page());
      const refresh = watchRefresh();

      choose('decides', varies);
      vi.advanceTimersByTime(300);
      choose('decides', consistent);
      vi.advanceTimersByTime(300);

      expect(refresh).not.toHaveBeenCalled();

      vi.runAllTimers();

      expect(refresh).toHaveBeenCalledTimes(1);
    });

    it('leaves the saving to the confirmation when the answer also decides whether other questions apply', async () => {
      await loadWithClock(page(' data-relevance-affects="2.2, 2.3"'));
      const refresh = watchRefresh();

      choose('decides', varies);
      vi.runAllTimers();

      expect(refresh).not.toHaveBeenCalled();
      expect(isHidden('scotland')).toBe(false);
    });

    it('takes an answer that names no questions it affects for one that needs no confirmation', async () => {
      await loadWithClock(page(' data-relevance-affects=""'));
      const refresh = watchRefresh();

      choose('decides', varies);
      vi.runAllTimers();

      expect(refresh).toHaveBeenCalledTimes(1);
    });

    it('leaves a drop-down that no field depends on alone', async () => {
      await loadWithClock(page());
      const refresh = watchRefresh();

      choose('other', varies);
      vi.runAllTimers();

      expect(refresh).not.toHaveBeenCalled();
    });

    it('does not save the section for a change that the user does not confirm', async () => {
      await loadWithClock(page(' data-relevance-affects="2.2"'));
      const refresh = watchRefresh();

      document.getElementById('decides').dispatchEvent(new CustomEvent('app:relevance-cancelled', { bubbles: true }));
      vi.runAllTimers();

      expect(refresh).not.toHaveBeenCalled();
    });

    it('does nothing where the page has no refresh button', async () => {
      await loadWithClock(`${dropDown('decides')}${dependent('scotland', 'decides')}`);

      choose('decides', varies);

      expect(() => vi.runAllTimers()).not.toThrow();
    });
  });
});
