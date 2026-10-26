// Date picker: the small calendar icon beside a date box, and the month grid it opens just below the box.
// It replaces the legacy AjaxControlToolkit CalendarExtender (a Calendar.gif icon and a pop-up calendar) on the Review
// timings page, the All Revision dates tab and the "Date" fields of the profile questions.
//
// Add data-module="app-date-picker" to either of:
// - a text input holding one date, written as data-date-picker-format says ("d MMMM yyyy" unless it says otherwise,
//   as the legacy calendar did). The format may use d, dd, M, MM, MMM, MMMM and yyyy.
// - a GOV.UK date input (.govuk-date-input) whose three boxes carry data-date-picker-part="day", "month" and "year".
// data-date-picker-label names the icon button for assistive technology. Without it the name is
// "Choose date for <the label of the box>".
//
// The grid opens when the icon is clicked and, for a single text box, when the box is clicked. It closes when the user
// clicks or tabs to anywhere else, presses Escape or picks a day. A date can still be typed: the box stays an ordinary
// text box, and picking a day fires the same input and change events typing does.
(function () {
    'use strict';

    const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    const WEEKDAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
    const DAYS_IN_WEEK = 7;
    const MONTHS_IN_YEAR = 12;
    const DEFAULT_FORMAT = 'd MMMM yyyy';
    // The formats the server accepts too, tried after the box's own when reading what was typed.
    const FALLBACK_FORMATS = ['d MMMM yyyy', 'd MMM yyyy', 'd/M/yyyy', 'dd/MM/yyyy', 'd-M-yyyy', 'yyyy-MM-dd'];
    const TOKENS = /yyyy|MMMM|MMM|MM|M|dd|d/g;
    const TOKEN_PATTERNS = {
        yyyy: '(\\d{4})',
        MMMM: '([A-Za-z]+)',
        MMM: '([A-Za-z]{3})',
        MM: '(\\d{2})',
        M: '(\\d{1,2})',
        dd: '(\\d{2})',
        d: '(\\d{1,2})'
    };
    const PART_NAMES = ['day', 'month', 'year'];

    const compiledPatterns = new Map();
    let openPicker = null;
    let sequence = 0;

    function pad(number, length = 2) {
        return String(number).padStart(length, '0');
    }

    // Dates are held at noon, so a clock change at midnight cannot move one to the next day.
    function createDate(year, monthIndex, day) {
        const date = new Date(2000, 0, 1, 12);

        date.setFullYear(year, monthIndex, day);

        return date;
    }

    function today() {
        const now = new Date();

        return createDate(now.getFullYear(), now.getMonth(), now.getDate());
    }

    function isRealDate(year, monthIndex, day) {
        const date = createDate(year, monthIndex, day);

        return date.getFullYear() === year && date.getMonth() === monthIndex && date.getDate() === day;
    }

    function addDays(date, days) {
        return createDate(date.getFullYear(), date.getMonth(), date.getDate() + days);
    }

    // The same day of the month some months away, or that month's last day when it has no such day.
    function addMonths(date, months) {
        const first = createDate(date.getFullYear(), date.getMonth() + months, 1);
        const lastDay = createDate(first.getFullYear(), first.getMonth() + 1, 0).getDate();

        return createDate(first.getFullYear(), first.getMonth(), Math.min(date.getDate(), lastDay));
    }

    function dayKey(date) {
        return `${pad(date.getFullYear(), 4)}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
    }

    function fromDayKey(key) {
        const [year, month, day] = key.split('-').map(Number);

        return createDate(year, month - 1, day);
    }

    function isSameDay(first, second) {
        return Boolean(first && second) && dayKey(first) === dayKey(second);
    }

    function isSameMonth(first, second) {
        return first.getFullYear() === second.getFullYear() && first.getMonth() === second.getMonth();
    }

    // Monday is 0, the grid's first column.
    function weekdayIndex(date) {
        return (date.getDay() + DAYS_IN_WEEK - 1) % DAYS_IN_WEEK;
    }

    function formatDate(date, pattern = DEFAULT_FORMAT) {
        return pattern.replace(TOKENS, (token) => {
            switch (token) {
                case 'yyyy':
                    return pad(date.getFullYear(), 4);
                case 'MMMM':
                    return MONTHS[date.getMonth()];
                case 'MMM':
                    return MONTHS[date.getMonth()].slice(0, 3);
                case 'MM':
                    return pad(date.getMonth() + 1);
                case 'M':
                    return String(date.getMonth() + 1);
                case 'dd':
                    return pad(date.getDate());
                default:
                    return String(date.getDate());
            }
        });
    }

    function escapeLiteral(text) {
        return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\s+/g, '\\s+');
    }

    // Turns a format into a regular expression with one group per token, in the order the tokens appear.
    function compilePattern(pattern) {
        if (!compiledPatterns.has(pattern)) {
            const tokens = [];
            let source = '';
            let position = 0;

            for (const match of pattern.matchAll(TOKENS)) {
                source += escapeLiteral(pattern.slice(position, match.index)) + TOKEN_PATTERNS[match[0]];
                tokens.push(match[0]);
                position = match.index + match[0].length;
            }

            source += escapeLiteral(pattern.slice(position));
            compiledPatterns.set(pattern, { regex: new RegExp(`^${source}$`, 'i'), tokens });
        }

        return compiledPatterns.get(pattern);
    }

    // The month's index from its number, its name or its three-letter abbreviation; -1 when it is none of them.
    function monthIndexOf(text) {
        if (/^\d+$/.test(text)) {
            return Number(text) - 1;
        }

        const name = text.toLowerCase();

        return MONTHS.findIndex((month) => month.toLowerCase() === name || month.slice(0, 3).toLowerCase() === name);
    }

    function parseWith(text, pattern) {
        const { regex, tokens } = compilePattern(pattern);
        const match = regex.exec(text);

        if (!match) {
            return null;
        }

        // Each token's first letter says which part of the date it is: d, M or y.
        const parts = {};

        tokens.forEach((token, index) => {
            parts[token[0]] = match[index + 1];
        });

        const year = Number(parts.y);
        const monthIndex = monthIndexOf(parts.M ?? '');
        const day = Number(parts.d);

        return monthIndex >= 0 && isRealDate(year, monthIndex, day) ? createDate(year, monthIndex, day) : null;
    }

    // Reads typed text as a date, in the box's format or any the server accepts; null when it is not a real date.
    function parseDate(text, pattern = DEFAULT_FORMAT) {
        const trimmed = (text ?? '').trim();

        if (!trimmed) {
            return null;
        }

        for (const candidate of [pattern, ...FALLBACK_FORMATS]) {
            const date = parseWith(trimmed, candidate);

            if (date) {
                return date;
            }
        }

        return null;
    }

    function createElement(tag, className = '', attributes = {}, text = '') {
        const $element = document.createElement(tag);

        if (className) {
            $element.className = className;
        }

        Object.entries(attributes).forEach(([name, value]) => $element.setAttribute(name, value));

        if (text) {
            $element.textContent = text;
        }

        return $element;
    }

    // Tells the page's own scripts, such as "Set For All Sections", that the box changed, as typing would.
    function announceChange($field) {
        $field.dispatchEvent(new Event('input', { bubbles: true }));
        $field.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function findParts($root) {
        const parts = {};

        PART_NAMES.forEach((name) => {
            parts[name] = $root.querySelector(`[data-date-picker-part="${name}"]`);
        });

        return PART_NAMES.every((name) => parts[name]) ? parts : null;
    }

    class DatePicker {
        constructor($root) {
            this.$root = $root;
            this.$input = $root.matches('input') ? $root : null;
            this.parts = this.$input ? null : findParts($root);
            this.format = $root.dataset.datePickerFormat || DEFAULT_FORMAT;
            this.selected = null;
            this.focusDay = today();
            this.onOutside = this.onOutside.bind(this);

            sequence += 1;

            const base = $root.id || `app-date-picker-${sequence}`;

            this.id = `${base}-calendar`;
            this.name = this.resolveName();
            this.build();
        }

        // Boxes that are not for typing in are left as they are, and so is a box set up for something else.
        static canEnhance($element) {
            if ($element.matches('input')) {
                return !$element.disabled && !$element.readOnly;
            }

            const parts = findParts($element);

            return parts !== null && PART_NAMES.every((name) => !parts[name].disabled && !parts[name].readOnly);
        }

        static init(root = document) {
            root.querySelectorAll('[data-module="app-date-picker"]').forEach(($element) => {
                if (!$element.datePicker && DatePicker.canEnhance($element)) {
                    $element.datePicker = new DatePicker($element);
                }
            });
        }

        get isOpen() {
            return !this.$calendar.hidden;
        }

        resolveName() {
            const explicit = this.$root.dataset.datePickerLabel;

            if (explicit) {
                return explicit;
            }

            const $label = this.$input ? this.$input.labels?.[0] : this.$root.closest('fieldset')?.querySelector('legend');
            const subject = ($label?.textContent ?? '').replace(/\s+/g, ' ').replace(/:\s*$/, '').trim();

            return subject ? `Choose date for ${subject}` : 'Choose date';
        }

        build() {
            this.$toggle = createElement('button', 'app-date-picker__toggle', {
                type: 'button',
                'aria-haspopup': 'dialog',
                'aria-expanded': 'false',
                'aria-controls': this.id
            });
            this.$toggle.append(createElement('span', 'govuk-visually-hidden', {}, this.name));
            this.$calendar = createElement('div', 'app-date-picker__calendar', {
                id: this.id,
                role: 'dialog',
                'aria-label': this.name,
                hidden: ''
            });

            if (this.$input) {
                this.$wrapper = createElement('div', 'app-date-picker app-date-picker--single');
                this.$input.before(this.$wrapper);
                this.$wrapper.append(this.$input, this.$toggle, this.$calendar);
            } else {
                const $item = createElement('div', 'govuk-date-input__item app-date-picker__toggle-item');

                this.$wrapper = this.$root;
                this.$wrapper.classList.add('app-date-picker', 'app-date-picker--parts');
                $item.append(this.$toggle);
                this.$wrapper.append($item, this.$calendar);
            }

            this.$toggle.addEventListener('click', () => this.toggle());
            this.$calendar.addEventListener('click', (event) => this.onCalendarClick(event));
            this.$calendar.addEventListener('keydown', (event) => this.onCalendarKeyDown(event));
            this.$wrapper.addEventListener('keydown', (event) => this.onKeyDown(event));

            if (this.$input) {
                this.$input.addEventListener('click', () => this.open());
                this.$input.addEventListener('input', () => this.onTyping());
            }
        }

        // The grid is built the first time it is opened, as a page such as All Revision dates has dozens of boxes.
        buildCalendar() {
            const $header = createElement('div', 'app-date-picker__header');

            this.$title = createElement('h2', 'app-date-picker__title', { 'aria-live': 'polite', 'aria-atomic': 'true' });
            $header.append(
                this.createNavButton(-MONTHS_IN_YEAR, 'Previous year', '\u00AB'),
                this.createNavButton(-1, 'Previous month', '\u2039'),
                this.$title,
                this.createNavButton(1, 'Next month', '\u203A'),
                this.createNavButton(MONTHS_IN_YEAR, 'Next year', '\u00BB')
            );

            const $head = createElement('tr');

            WEEKDAYS.forEach((weekday) => {
                const $cell = createElement('th', '', { scope: 'col' });

                $cell.append(createElement('span', '', { 'aria-hidden': 'true' }, weekday.slice(0, 2)), createElement('span', 'govuk-visually-hidden', {}, weekday));
                $head.append($cell);
            });

            const $thead = createElement('thead');
            const $grid = createElement('table', 'app-date-picker__grid');
            const $footer = createElement('div', 'app-date-picker__footer');

            $thead.append($head);
            this.$body = createElement('tbody');
            $grid.append($thead, this.$body);
            this.$today = createElement('button', 'app-date-picker__today', { type: 'button' }, 'Today');
            $footer.append(this.$today);
            this.$calendar.append($header, $grid, $footer);
        }

        createNavButton(months, label, symbol) {
            const $button = createElement('button', 'app-date-picker__nav', {
                type: 'button',
                'aria-label': label,
                'data-date-picker-step': String(months)
            });

            $button.append(createElement('span', '', { 'aria-hidden': 'true' }, symbol));

            return $button;
        }

        // The date in the box, or the three boxes; null when there is none or it is not a real date.
        readValue() {
            if (this.$input) {
                return parseDate(this.$input.value, this.format);
            }

            const [day, month, year] = PART_NAMES.map((name) => this.parts[name].value.trim());
            const isNumeric = /^\d{1,2}$/.test(day) && /^\d{1,2}$/.test(month) && /^\d{4}$/.test(year);

            return isNumeric && isRealDate(Number(year), Number(month) - 1, Number(day))
                ? createDate(Number(year), Number(month) - 1, Number(day))
                : null;
        }

        writeValue(date) {
            if (this.$input) {
                this.$input.value = formatDate(date, this.format);
                announceChange(this.$input);

                return;
            }

            const values = { day: String(date.getDate()), month: String(date.getMonth() + 1), year: String(date.getFullYear()) };

            PART_NAMES.forEach((name) => {
                this.parts[name].value = values[name];
                announceChange(this.parts[name]);
            });
        }

        toggle() {
            if (this.isOpen) {
                this.close();
            } else {
                this.open({ focus: true });
            }
        }

        // Shows the grid for the date in the box, or this month when there is none. The user's place stays in the
        // box when it was clicked, and moves to the chosen day when the icon was used.
        open({ focus = false } = {}) {
            if (this.isOpen) {
                if (focus) {
                    this.focusDayButton();
                }

                return;
            }

            if (openPicker) {
                openPicker.close();
            }

            openPicker = this;
            this.selected = this.readValue();
            this.focusDay = this.selected ?? today();

            if (!this.$body) {
                this.buildCalendar();
            }

            this.render();
            this.$calendar.hidden = false;
            this.$toggle.setAttribute('aria-expanded', 'true');
            this.alignCalendar();

            ['pointerdown', 'click'].forEach((type) => document.addEventListener(type, this.onOutside, true));
            document.addEventListener('focusin', this.onOutside);

            if (focus) {
                this.focusDayButton();
            }
        }

        close() {
            if (!this.isOpen) {
                return;
            }

            this.$calendar.hidden = true;
            this.$toggle.setAttribute('aria-expanded', 'false');
            ['pointerdown', 'click'].forEach((type) => document.removeEventListener(type, this.onOutside, true));
            document.removeEventListener('focusin', this.onOutside);

            if (openPicker === this) {
                openPicker = null;
            }
        }

        // Anything the user does outside the box, icon and grid - a click, a tap or moving focus there - closes the grid.
        onOutside(event) {
            if (!this.$wrapper.contains(event.target)) {
                this.close();
            }
        }

        // A grid near the right edge of the window would run off it, so it lines up with the right edge of the box instead.
        alignCalendar() {
            this.$calendar.classList.remove('app-date-picker__calendar--right');

            if (this.$calendar.getBoundingClientRect().right > document.documentElement.clientWidth) {
                this.$calendar.classList.add('app-date-picker__calendar--right');
            }
        }

        render() {
            const title = `${MONTHS[this.focusDay.getMonth()]} ${this.focusDay.getFullYear()}`;

            // Only a new month is announced, not each move to another day of the same one.
            if (this.$title.textContent !== title) {
                this.$title.textContent = title;
            }

            this.$body.replaceChildren(...this.buildWeeks(today()));
        }

        buildWeeks(now) {
            const year = this.focusDay.getFullYear();
            const monthIndex = this.focusDay.getMonth();
            const daysInMonth = createDate(year, monthIndex + 1, 0).getDate();
            const weeks = [];
            let day = 1 - weekdayIndex(createDate(year, monthIndex, 1));

            while (day <= daysInMonth) {
                const $week = createElement('tr');

                for (let column = 0; column < DAYS_IN_WEEK; column += 1) {
                    const inMonth = day >= 1 && day <= daysInMonth;

                    $week.append(inMonth ? this.buildDay(createDate(year, monthIndex, day), now) : createElement('td'));
                    day += 1;
                }

                weeks.push($week);
            }

            return weeks;
        }

        buildDay(date, now) {
            const isToday = isSameDay(date, now);
            const isSelected = isSameDay(date, this.selected);
            const description = [`${WEEKDAYS[weekdayIndex(date)]} ${formatDate(date)}`, isSelected ? 'selected' : ''].filter(Boolean).join(', ');
            const $cell = createElement('td');
            const $day = createElement('button', 'app-date-picker__day', {
                type: 'button',
                'data-date': dayKey(date),
                tabindex: isSameDay(date, this.focusDay) ? '0' : '-1',
                'aria-label': description
            }, String(date.getDate()));

            $day.classList.toggle('app-date-picker__day--today', isToday);
            $day.classList.toggle('app-date-picker__day--selected', isSelected);

            if (isToday) {
                $day.setAttribute('aria-current', 'date');
            }

            $cell.append($day);

            return $cell;
        }

        select(date) {
            this.close();
            this.writeValue(date);
            (this.$input ?? this.$toggle).focus();
        }

        focusDayButton() {
            const $day = this.$body.querySelector('.app-date-picker__day[tabindex="0"]');

            if ($day) {
                $day.focus();
            }
        }

        // Moves the user to a day, showing its month first when it is not the one on view.
        focusDate(date) {
            const sameMonth = isSameMonth(date, this.focusDay);

            this.focusDay = date;

            if (sameMonth) {
                this.$body.querySelectorAll('.app-date-picker__day').forEach(($day) => {
                    $day.tabIndex = isSameDay(fromDayKey($day.dataset.date), date) ? 0 : -1;
                });
            } else {
                this.render();
            }

            this.focusDayButton();
        }

        onCalendarClick(event) {
            const $button = event.target.closest('button');

            if (!$button) {
                return;
            }

            if ($button.dataset.date) {
                this.select(fromDayKey($button.dataset.date));
            } else if ($button.dataset.datePickerStep) {
                this.focusDay = addMonths(this.focusDay, Number($button.dataset.datePickerStep));
                this.render();
            } else if ($button === this.$today) {
                this.select(today());
            }
        }

        // The day a key moves to from the given one, or null for a key that does not move.
        static targetFor(key, shiftKey, date) {
            switch (key) {
                case 'ArrowLeft':
                    return addDays(date, -1);
                case 'ArrowRight':
                    return addDays(date, 1);
                case 'ArrowUp':
                    return addDays(date, -DAYS_IN_WEEK);
                case 'ArrowDown':
                    return addDays(date, DAYS_IN_WEEK);
                case 'Home':
                    return addDays(date, -weekdayIndex(date));
                case 'End':
                    return addDays(date, DAYS_IN_WEEK - 1 - weekdayIndex(date));
                case 'PageUp':
                    return addMonths(date, shiftKey ? -MONTHS_IN_YEAR : -1);
                case 'PageDown':
                    return addMonths(date, shiftKey ? MONTHS_IN_YEAR : 1);
                default:
                    return null;
            }
        }

        onCalendarKeyDown(event) {
            const $day = event.target.closest('.app-date-picker__day');

            if (!$day || event.altKey || event.ctrlKey || event.metaKey) {
                return;
            }

            const target = DatePicker.targetFor(event.key, event.shiftKey, fromDayKey($day.dataset.date));

            if (target) {
                event.preventDefault();
                this.focusDate(target);
            }
        }

        // Escape closes the grid from anywhere in the picker, and from inside the grid puts the user back on the icon.
        onKeyDown(event) {
            if (event.key === 'Escape' && this.isOpen) {
                event.preventDefault();
                event.stopPropagation();
                this.close();

                if (this.$calendar.contains(event.target)) {
                    this.$toggle.focus();
                }
            } else if (event.key === 'ArrowDown' && event.target === this.$input) {
                event.preventDefault();
                this.open({ focus: true });
            }
        }

        // While the grid is open, a date typed into the box moves it to that date.
        onTyping() {
            const typed = this.isOpen ? this.readValue() : null;

            if (typed) {
                this.selected = typed;
                this.focusDay = typed;
                this.render();
            }
        }
    }

    DatePicker.formatDate = formatDate;
    DatePicker.parseDate = parseDate;
    window.DatePicker = DatePicker;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => DatePicker.init());
    } else {
        DatePicker.init();
    }
})();
