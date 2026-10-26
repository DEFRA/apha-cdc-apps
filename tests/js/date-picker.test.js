import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../../src/CDC.Web/wwwroot/js/date-picker.js';

const { DatePicker } = window;

// Sunday 25 October 2026. October 2026 starts on a Thursday, so its grid has three empty cells before the 1st.
const TODAY = new Date(2026, 9, 25, 10, 0, 0);

function single({ value = '', label = 'From', id = 'FromDate', attributes = '' } = {}) {
  document.body.innerHTML = `
    <form>
      <label for="${id}">${label}</label>
      <input id="${id}" name="${id}" type="text" value="${value}" data-module="app-date-picker" ${attributes}>
      <button id="elsewhere" type="button">Elsewhere</button>
    </form>`;
  DatePicker.init();

  return parts(document.getElementById(id));
}

function parts(input) {
  const wrapper = input.parentElement;
  const calendar = wrapper.querySelector('.app-date-picker__calendar');

  return {
    input,
    wrapper,
    calendar,
    toggle: wrapper.querySelector('.app-date-picker__toggle'),
    title: () => calendar.querySelector('.app-date-picker__title').textContent,
    day: (key) => calendar.querySelector(`[data-date="${key}"]`),
    days: () => [...calendar.querySelectorAll('.app-date-picker__day')],
    rows: () => [...calendar.querySelectorAll('tbody tr')],
    stop: () => calendar.querySelector('[tabindex="0"]'),
    nav: (label) => calendar.querySelector(`[aria-label="${label}"]`)
  };
}

function press(target, key, options = {}) {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...options });

  target.dispatchEvent(event);

  return event;
}

function parsed(text, pattern) {
  const date = DatePicker.parseDate(text, pattern);

  return date && [date.getFullYear(), date.getMonth() + 1, date.getDate()];
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(TODAY);
});

afterEach(() => {
  document.body.innerHTML = '';
  delete document.documentElement.clientWidth;
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe('date-picker formats', () => {
  it('writes a date as "d MMMM yyyy" unless told otherwise, as the legacy calendar did', () => {
    expect(DatePicker.formatDate(new Date(2027, 1, 1))).toBe('1 February 2027');
  });

  it.each([
    ['dd/MM/yyyy', '01/02/2027'],
    ['d MMM yyyy', '1 Feb 2027'],
    ['yyyy-MM-dd', '2027-02-01'],
    ['M/d/yyyy', '2/1/2027']
  ])('writes a date as %s', (format, expected) => {
    expect(DatePicker.formatDate(new Date(2027, 1, 1), format)).toBe(expected);
  });

  it.each([
    ['1 February 2027', [2027, 2, 1]],
    ['01 February 2027', [2027, 2, 1]],
    ['  1 FEBRUARY 2027 ', [2027, 2, 1]],
    ['1 feb 2027', [2027, 2, 1]],
    ['1/2/2027', [2027, 2, 1]],
    ['31/12/2027', [2027, 12, 31]],
    ['1-2-2027', [2027, 2, 1]],
    ['2027-02-01', [2027, 2, 1]],
    ['29 February 2028', [2028, 2, 29]]
  ])('reads %j as a date', (text, expected) => {
    expect(parsed(text)).toEqual(expected);
  });

  it.each(['', '   ', 'soon', '31 February 2027', '29 February 2027', '0 January 2027', '1 Febtober 2027', '32/1/2027', '1 February 27'])(
    'does not read %j as a date',
    (text) => {
      expect(parsed(text)).toBeNull();
    }
  );

  it('tries the format the box asks for before the others', () => {
    expect(parsed('02/01/2027', 'MM/dd/yyyy')).toEqual([2027, 2, 1]);
    expect(parsed('02/01/2027')).toEqual([2027, 1, 2]);
  });

  it('reads nothing when there is no text', () => {
    expect(DatePicker.parseDate(undefined)).toBeNull();
    expect(DatePicker.parseDate(null)).toBeNull();
  });
});

describe('date-picker icon', () => {
  it('puts a calendar button to the right of the box, with a closed calendar after it', () => {
    const { input, wrapper, toggle, calendar } = single();

    expect(wrapper.classList.contains('app-date-picker')).toBe(true);
    expect([...wrapper.children]).toEqual([input, toggle, calendar]);
    expect(toggle.type).toBe('button');
    expect(toggle.getAttribute('aria-haspopup')).toBe('dialog');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(toggle.getAttribute('aria-controls')).toBe('FromDate-calendar');
    expect(calendar.id).toBe('FromDate-calendar');
    expect(calendar.getAttribute('role')).toBe('dialog');
    expect(calendar.hidden).toBe(true);
  });

  it('names the button after the box it is for, so it is clear to a screen reader', () => {
    const { toggle, calendar } = single({ label: 'Next author review:', id: 'review' });

    expect(toggle.textContent).toBe('Choose date for Next author review');
    expect(calendar.getAttribute('aria-label')).toBe('Choose date for Next author review');
  });

  it('uses the name the page gives it', () => {
    const { toggle } = single({ attributes: 'data-date-picker-label="Choose from date"' });

    expect(toggle.textContent).toBe('Choose from date');
  });

  it('is named simply "Choose date" when the box has no label', () => {
    document.body.innerHTML = '<input id="loose" type="text" data-module="app-date-picker">';
    DatePicker.init();

    expect(document.querySelector('.app-date-picker__toggle').textContent).toBe('Choose date');
  });

  it('is set up once however often it is asked', () => {
    const { input } = single();

    DatePicker.init();
    DatePicker.init(document);

    expect(document.querySelectorAll('.app-date-picker__toggle')).toHaveLength(1);
    expect(input.datePicker).toBeInstanceOf(DatePicker);
  });

  it('leaves a box the user cannot type in without an icon', () => {
    document.body.innerHTML = `
      <input id="locked" type="text" readonly data-module="app-date-picker">
      <input id="off" type="text" disabled data-module="app-date-picker">`;
    DatePicker.init();

    expect(document.querySelector('.app-date-picker')).toBeNull();
  });

  it('is set up on a page that has already loaded when the script runs', async () => {
    vi.resetModules();
    document.body.innerHTML = '<label for="late">When</label><input id="late" type="text" data-module="app-date-picker">';

    await import('../../src/CDC.Web/wwwroot/js/date-picker.js');

    expect(document.querySelector('#late').parentElement.querySelector('.app-date-picker__toggle').textContent).toBe('Choose date for When');
  });
});

describe('date-picker calendar', () => {
  it('opens just below the box when the icon is clicked, on this month, with the focus on today', () => {
    const picker = single();

    picker.toggle.click();

    expect(picker.calendar.hidden).toBe(false);
    expect(picker.toggle.getAttribute('aria-expanded')).toBe('true');
    expect(picker.title()).toBe('October 2026');
    expect(document.activeElement.dataset.date).toBe('2026-10-25');
    expect(picker.day('2026-10-25').getAttribute('aria-current')).toBe('date');
    expect(picker.day('2026-10-25').classList.contains('app-date-picker__day--today')).toBe(true);
  });

  it('opens when the box is clicked, leaving the user in the box so they can still type', () => {
    const picker = single();

    picker.input.focus();
    picker.input.click();

    expect(picker.calendar.hidden).toBe(false);
    expect(document.activeElement).toBe(picker.input);
    expect(picker.toggle.getAttribute('aria-expanded')).toBe('true');
  });

  it('shows the month of the date in the box, with that date marked as selected', () => {
    const picker = single({ value: '1 February 2027' });

    picker.toggle.click();

    expect(picker.title()).toBe('February 2027');
    expect(picker.day('2027-02-01').classList.contains('app-date-picker__day--selected')).toBe(true);
    expect(picker.day('2027-02-01').getAttribute('aria-label')).toBe('Monday 1 February 2027, selected');
    expect(picker.day('2027-02-02').getAttribute('aria-label')).toBe('Tuesday 2 February 2027');
    expect(document.activeElement.dataset.date).toBe('2027-02-01');
  });

  it('reads the box in the format the page gives it', () => {
    const picker = single({ value: '14/10/2026', attributes: 'data-date-picker-format="dd/MM/yyyy"' });

    picker.toggle.click();

    expect(picker.title()).toBe('October 2026');
    expect(picker.day('2026-10-14').classList.contains('app-date-picker__day--selected')).toBe(true);
  });

  it('shows this month when the box holds something that is not a date', () => {
    const picker = single({ value: 'soon' });

    picker.toggle.click();

    expect(picker.title()).toBe('October 2026');
    expect(picker.days().some((day) => day.classList.contains('app-date-picker__day--selected'))).toBe(false);
  });

  it('starts each week on Monday and leaves the days outside the month empty', () => {
    const picker = single({ value: '1 October 2026' });

    picker.toggle.click();

    const [first] = picker.rows();

    expect(picker.rows()).toHaveLength(5);
    expect([...first.children].map((cell) => cell.textContent)).toEqual(['', '', '', '1', '2', '3', '4']);
    expect(picker.days()).toHaveLength(31);
    expect([...picker.calendar.querySelectorAll('thead th')].map((cell) => cell.querySelector('.govuk-visually-hidden').textContent)).toEqual([
      'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'
    ]);
  });

  it('shows only the weeks a month needs', () => {
    const picker = single({ value: '1 February 2027' });

    picker.toggle.click();

    expect(picker.rows()).toHaveLength(4);
    expect(picker.days()).toHaveLength(28);
  });

  it('shows a leap day', () => {
    const picker = single({ value: '29 February 2028' });

    picker.toggle.click();

    expect(picker.days()).toHaveLength(29);
  });

  it('lines the grid up with the right of the box when it would run off the window', () => {
    Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, value: 500 });
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockReturnValue({ right: 900 });
    const picker = single();

    picker.toggle.click();

    expect(picker.calendar.classList.contains('app-date-picker__calendar--right')).toBe(true);
  });

  it('keeps the grid under the box when there is room', () => {
    Object.defineProperty(document.documentElement, 'clientWidth', { configurable: true, value: 1000 });
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockReturnValue({ right: 400 });
    const picker = single();

    picker.toggle.click();

    expect(picker.calendar.classList.contains('app-date-picker__calendar--right')).toBe(false);
  });
});

describe('date-picker closing', () => {
  it('closes when the icon is clicked again', () => {
    const picker = single();

    picker.toggle.click();
    picker.toggle.click();

    expect(picker.calendar.hidden).toBe(true);
    expect(picker.toggle.getAttribute('aria-expanded')).toBe('false');
  });

  it('closes when the user clicks anywhere else', () => {
    const picker = single();

    picker.toggle.click();
    document.getElementById('elsewhere').click();

    expect(picker.calendar.hidden).toBe(true);
    expect(picker.toggle.getAttribute('aria-expanded')).toBe('false');
  });

  it('closes when the user clicks on blank page', () => {
    const picker = single();

    picker.input.click();
    document.body.click();

    expect(picker.calendar.hidden).toBe(true);
  });

  it('closes when the user taps or presses anywhere else', () => {
    const picker = single();

    picker.toggle.click();
    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }));

    expect(picker.calendar.hidden).toBe(true);
  });

  it('stays open when the user clicks inside the calendar', () => {
    const picker = single();

    picker.toggle.click();
    picker.calendar.querySelector('.app-date-picker__title').click();
    picker.calendar.click();

    expect(picker.calendar.hidden).toBe(false);
  });

  it('stays open when the user clicks the box or moves between the box, icon and calendar', () => {
    const picker = single();

    picker.toggle.click();
    picker.input.click();
    picker.input.focus();
    picker.toggle.focus();
    picker.nav('Next month').focus();

    expect(picker.calendar.hidden).toBe(false);
  });

  it('closes when the focus moves to somewhere else', () => {
    const picker = single();

    picker.toggle.click();
    document.getElementById('elsewhere').focus();

    expect(picker.calendar.hidden).toBe(true);
  });

  it('closes on Escape and puts a user in the calendar back on the icon', () => {
    const picker = single();

    picker.toggle.click();
    press(document.activeElement, 'Escape');

    expect(picker.calendar.hidden).toBe(true);
    expect(document.activeElement).toBe(picker.toggle);
  });

  it('closes on Escape and leaves a user in the box where they are', () => {
    const picker = single();

    picker.input.focus();
    picker.input.click();
    const event = press(picker.input, 'Escape');

    expect(picker.calendar.hidden).toBe(true);
    expect(document.activeElement).toBe(picker.input);
    expect(event.defaultPrevented).toBe(true);
  });

  it('leaves Escape alone when the calendar is closed', () => {
    const picker = single();

    const event = press(picker.input, 'Escape');

    expect(event.defaultPrevented).toBe(false);
  });

  it('keeps only one calendar open at a time', () => {
    document.body.innerHTML = `
      <label for="first">First</label><input id="first" type="text" data-module="app-date-picker">
      <label for="second">Second</label><input id="second" type="text" data-module="app-date-picker">`;
    DatePicker.init();
    const first = parts(document.getElementById('first'));
    const second = parts(document.getElementById('second'));

    first.toggle.click();
    second.toggle.click();

    expect(first.calendar.hidden).toBe(true);
    expect(second.calendar.hidden).toBe(false);
  });
});

describe('date-picker choosing a date', () => {
  it('writes the chosen day into the box, as typing it would, closes and returns to the box', () => {
    const picker = single({ value: '1 February 2027' });
    const events = [];

    picker.input.addEventListener('input', () => events.push('input'));
    picker.input.addEventListener('change', () => events.push('change'));
    picker.toggle.click();
    picker.day('2027-02-14').click();

    expect(picker.input.value).toBe('14 February 2027');
    expect(events).toEqual(['input', 'change']);
    expect(picker.calendar.hidden).toBe(true);
    expect(document.activeElement).toBe(picker.input);
  });

  it('writes the date in the format the page gives it', () => {
    const picker = single({ attributes: 'data-date-picker-format="dd/MM/yyyy"' });

    picker.toggle.click();
    picker.day('2026-10-09').click();

    expect(picker.input.value).toBe('09/10/2026');
  });

  it('chooses today from the Today button', () => {
    const picker = single({ value: '1 February 2027' });

    picker.toggle.click();
    picker.calendar.querySelector('.app-date-picker__today').click();

    expect(picker.input.value).toBe('25 October 2026');
    expect(picker.calendar.hidden).toBe(true);
  });

  it('chooses a day from a calendar opened by clicking the box', () => {
    const picker = single();

    picker.input.click();
    picker.day('2026-10-03').click();

    expect(picker.input.value).toBe('3 October 2026');
  });

  it('still lets the user type a date, and follows it while the calendar is open', () => {
    const picker = single();

    picker.input.click();
    picker.input.value = '3 March 2027';
    picker.input.dispatchEvent(new Event('input', { bubbles: true }));

    expect(picker.title()).toBe('March 2027');
    expect(picker.day('2027-03-03').classList.contains('app-date-picker__day--selected')).toBe(true);
  });

  it('keeps the month on view while what is typed is not yet a date', () => {
    const picker = single({ value: '3 March 2027' });

    picker.input.click();
    picker.input.value = '3 Mar';
    picker.input.dispatchEvent(new Event('input', { bubbles: true }));

    expect(picker.title()).toBe('March 2027');
  });

  it('ignores typing while the calendar is closed', () => {
    const picker = single();

    picker.input.value = '3 March 2027';
    picker.input.dispatchEvent(new Event('input', { bubbles: true }));

    expect(picker.calendar.querySelector('.app-date-picker__title')).toBeNull();
  });
});

describe('date-picker navigation', () => {
  it('moves between months and years with the buttons above the grid', () => {
    const picker = single({ value: '15 October 2026' });

    picker.toggle.click();
    picker.nav('Next month').click();
    expect(picker.title()).toBe('November 2026');
    expect(picker.stop().dataset.date).toBe('2026-11-15');

    picker.nav('Previous month').click();
    picker.nav('Previous month').click();
    expect(picker.title()).toBe('September 2026');

    picker.nav('Next year').click();
    expect(picker.title()).toBe('September 2027');

    picker.nav('Previous year').click();
    picker.nav('Previous year').click();
    expect(picker.title()).toBe('September 2025');
  });

  it('goes from January back to December of the year before', () => {
    const picker = single({ value: '1 January 2027' });

    picker.toggle.click();
    picker.nav('Previous month').click();

    expect(picker.title()).toBe('December 2026');
  });

  it('moves to the last day of a shorter month', () => {
    const picker = single({ value: '31 January 2027' });

    picker.toggle.click();
    picker.nav('Next month').click();

    expect(picker.stop().dataset.date).toBe('2027-02-28');
  });

  it('keeps the focus on the button the user pressed', () => {
    const picker = single();

    picker.toggle.click();
    picker.nav('Next month').focus();
    picker.nav('Next month').click();

    expect(document.activeElement).toBe(picker.nav('Next month'));
  });

  it('names its buttons for a screen reader and hides the arrows', () => {
    const picker = single();

    picker.toggle.click();

    expect(['Previous year', 'Previous month', 'Next month', 'Next year'].every((label) => picker.nav(label) !== null)).toBe(true);
    expect(picker.nav('Next month').querySelector('[aria-hidden="true"]')).not.toBeNull();
    expect(picker.calendar.querySelector('.app-date-picker__title').getAttribute('aria-live')).toBe('polite');
  });
});

describe('date-picker keyboard', () => {
  function opened(value = '15 October 2026') {
    const picker = single({ value });

    picker.toggle.click();

    return picker;
  }

  it('is reached by Tab: only the focused day is a tab stop', () => {
    const picker = opened();

    expect(picker.days().filter((day) => day.tabIndex === 0)).toHaveLength(1);
    expect(picker.stop().dataset.date).toBe('2026-10-15');
  });

  it.each([
    ['ArrowRight', {}, '2026-10-16'],
    ['ArrowLeft', {}, '2026-10-14'],
    ['ArrowDown', {}, '2026-10-22'],
    ['ArrowUp', {}, '2026-10-08'],
    ['Home', {}, '2026-10-12'],
    ['End', {}, '2026-10-18'],
    ['PageDown', {}, '2026-11-15'],
    ['PageUp', {}, '2026-09-15'],
    ['PageDown', { shiftKey: true }, '2027-10-15'],
    ['PageUp', { shiftKey: true }, '2025-10-15']
  ])('%s %j moves to %s', (key, options, expected) => {
    const picker = opened();
    const event = press(document.activeElement, key, options);

    expect(event.defaultPrevented).toBe(true);
    expect(document.activeElement.dataset.date).toBe(expected);
    expect(picker.stop()).toBe(document.activeElement);
  });

  it('carries on into the next month and shows it', () => {
    const picker = opened('31 October 2026');

    press(document.activeElement, 'ArrowRight');

    expect(picker.title()).toBe('November 2026');
    expect(document.activeElement.dataset.date).toBe('2026-11-01');
  });

  it('carries back into the month before and shows it', () => {
    const picker = opened('1 October 2026');

    press(document.activeElement, 'ArrowLeft');

    expect(picker.title()).toBe('September 2026');
    expect(document.activeElement.dataset.date).toBe('2026-09-30');
  });

  it('moves a week at a time across a month end', () => {
    const picker = opened('29 October 2026');

    press(document.activeElement, 'ArrowDown');

    expect(picker.title()).toBe('November 2026');
    expect(document.activeElement.dataset.date).toBe('2026-11-05');
  });

  it('leaves keys that do not move, and shortcuts the browser uses, alone', () => {
    const picker = opened();

    expect(press(document.activeElement, 'a').defaultPrevented).toBe(false);
    expect(press(document.activeElement, 'ArrowLeft', { altKey: true }).defaultPrevented).toBe(false);
    expect(press(document.activeElement, 'ArrowLeft', { ctrlKey: true }).defaultPrevented).toBe(false);
    expect(press(document.activeElement, 'ArrowLeft', { metaKey: true }).defaultPrevented).toBe(false);
    expect(document.activeElement.dataset.date).toBe('2026-10-15');
    expect(picker.title()).toBe('October 2026');
  });

  it('leaves the arrow keys to the buttons above the grid', () => {
    const picker = opened();

    picker.nav('Next month').focus();

    expect(press(picker.nav('Next month'), 'ArrowRight').defaultPrevented).toBe(false);
  });

  it('chooses the focused day when it is pressed', () => {
    const picker = opened();

    press(document.activeElement, 'ArrowRight');
    document.activeElement.click();

    expect(picker.input.value).toBe('16 October 2026');
  });

  it('is opened from the box with the down arrow, with the focus on the focused day', () => {
    const picker = single({ value: '15 October 2026' });
    picker.input.focus();

    const event = press(picker.input, 'ArrowDown');

    expect(event.defaultPrevented).toBe(true);
    expect(picker.calendar.hidden).toBe(false);
    expect(document.activeElement.dataset.date).toBe('2026-10-15');
  });

  it('moves the focus into an open calendar with the down arrow', () => {
    const picker = single({ value: '15 October 2026' });

    picker.input.click();
    picker.input.focus();
    press(picker.input, 'ArrowDown');

    expect(document.activeElement.dataset.date).toBe('2026-10-15');
  });

  it('is tabbed through in the order of the page: box, icon, then the calendar', () => {
    const picker = opened();
    const order = [...picker.wrapper.querySelectorAll('input, button')].map((element) => element.className || element.tagName.toLowerCase());

    expect(order.slice(0, 2)).toEqual(['input', 'app-date-picker__toggle']);
    expect(order[2]).toBe('app-date-picker__nav');
  });
});

describe('date-picker with a GOV.UK date input', () => {
  function dateInput({ day = '', month = '', year = '', attributes = 'data-date-picker-label="Choose date for Date of onset"' } = {}) {
    document.body.innerHTML = `
      <form>
        <fieldset class="govuk-fieldset" role="group">
          <legend>Date of onset</legend>
          <div class="govuk-date-input" id="field_1" data-module="app-date-picker" ${attributes}>
            <div class="govuk-date-input__item"><label for="field_1_day">Day</label><input id="field_1_day" name="field_1_day" type="text" value="${day}" data-date-picker-part="day"></div>
            <div class="govuk-date-input__item"><label for="field_1_month">Month</label><input id="field_1_month" name="field_1_month" type="text" value="${month}" data-date-picker-part="month"></div>
            <div class="govuk-date-input__item"><label for="field_1_year">Year</label><input id="field_1_year" name="field_1_year" type="text" value="${year}" data-date-picker-part="year"></div>
          </div>
        </fieldset>
        <button id="elsewhere" type="button">Elsewhere</button>
      </form>`;
    DatePicker.init();

    const root = document.getElementById('field_1');
    const calendar = root.querySelector('.app-date-picker__calendar');

    return {
      root,
      calendar,
      toggle: root.querySelector('.app-date-picker__toggle'),
      boxes: ['day', 'month', 'year'].map((name) => document.getElementById(`field_1_${name}`)),
      title: () => calendar.querySelector('.app-date-picker__title').textContent,
      day: (key) => calendar.querySelector(`[data-date="${key}"]`),
      stop: () => calendar.querySelector('[tabindex="0"]')
    };
  }

  it('adds the icon after the year box, in the same row as the boxes', () => {
    const { root, toggle, calendar } = dateInput();

    expect(root.classList.contains('app-date-picker--parts')).toBe(true);
    expect(toggle.parentElement.classList.contains('govuk-date-input__item')).toBe(true);
    expect(toggle.parentElement.previousElementSibling.querySelector('input').id).toBe('field_1_year');
    expect(toggle.textContent).toBe('Choose date for Date of onset');
    expect(calendar.parentElement).toBe(root);
    expect(calendar.hidden).toBe(true);
  });

  it('is named after the legend when the page does not name it', () => {
    const { toggle } = dateInput({ attributes: '' });

    expect(toggle.textContent).toBe('Choose date for Date of onset');
  });

  it('opens from the icon only, so the boxes stay free for typing', () => {
    const { boxes, calendar, toggle } = dateInput();

    boxes.forEach((box) => box.click());
    expect(calendar.hidden).toBe(true);

    toggle.click();
    expect(calendar.hidden).toBe(false);
  });

  it('shows the date held in the three boxes', () => {
    const { toggle, title, day } = dateInput({ day: '14', month: '10', year: '2026' });

    toggle.click();

    expect(title()).toBe('October 2026');
    expect(day('2026-10-14').classList.contains('app-date-picker__day--selected')).toBe(true);
    expect(document.activeElement.dataset.date).toBe('2026-10-14');
  });

  it.each([
    ['31', '2', '2027'],
    ['', '10', '2026'],
    ['14', '', '2026'],
    ['14', '10', ''],
    ['14', '10', '26'],
    ['ab', '10', '2026']
  ])('shows this month when the boxes hold %j/%j/%j, which is not a date', (dayText, month, year) => {
    const { toggle, title } = dateInput({ day: dayText, month, year });

    toggle.click();

    expect(title()).toBe('October 2026');
  });

  it('fills the three boxes with the chosen day, announces each, closes and returns to the icon', () => {
    const { boxes, toggle, calendar, day } = dateInput({ day: '1', month: '2', year: '2027' });
    const events = [];

    boxes.forEach((box, index) => {
      box.addEventListener('input', () => events.push(`input ${index}`));
      box.addEventListener('change', () => events.push(`change ${index}`));
    });
    toggle.click();
    day('2027-02-14').click();

    expect(boxes.map((box) => box.value)).toEqual(['14', '2', '2027']);
    expect(events).toEqual(['input 0', 'change 0', 'input 1', 'change 1', 'input 2', 'change 2']);
    expect(calendar.hidden).toBe(true);
    expect(document.activeElement).toBe(toggle);
  });

  it('closes when the user clicks anywhere else', () => {
    const { toggle, calendar } = dateInput();

    toggle.click();
    document.getElementById('elsewhere').click();

    expect(calendar.hidden).toBe(true);
  });

  it('is left alone when a box is missing or cannot be typed in', () => {
    document.body.innerHTML = `
      <div class="govuk-date-input" id="partial" data-module="app-date-picker">
        <input data-date-picker-part="day"><input data-date-picker-part="month">
      </div>
      <div class="govuk-date-input" id="locked" data-module="app-date-picker">
        <input data-date-picker-part="day" readonly><input data-date-picker-part="month"><input data-date-picker-part="year">
      </div>`;
    DatePicker.init();

    expect(document.querySelector('.app-date-picker')).toBeNull();
  });
});

describe('date-picker on All Revision dates', () => {
  it('copies a date chosen in "Set For All Sections" to every section, as typing it does', async () => {
    vi.resetModules();
    document.body.innerHTML = `
      <label for="all">Next author review:</label>
      <input id="all" type="text" data-revision-setall="NextAuthor" data-module="app-date-picker">
      <label for="one">Next author review:</label>
      <input id="one" type="text" data-revision-field="NextAuthor" data-module="app-date-picker">
      <label for="two">Next author review:</label>
      <input id="two" type="text" data-revision-field="NextAuthor" data-module="app-date-picker">`;
    await import('../../src/CDC.Web/wwwroot/js/revision-dates.js');
    await import('../../src/CDC.Web/wwwroot/js/date-picker.js');

    const all = parts(document.getElementById('all'));

    all.toggle.click();
    all.day('2026-10-30').click();

    expect(document.getElementById('all').value).toBe('30 October 2026');
    expect(document.getElementById('one').value).toBe('30 October 2026');
    expect(document.getElementById('two').value).toBe('30 October 2026');
  });
});
