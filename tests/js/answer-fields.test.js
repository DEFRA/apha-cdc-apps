import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

function wrapper(type, inner, editable = 'true') {
  document.body.innerHTML = `<div id="field" data-answer-field="field-1" data-field-type="${type}" data-field-editable="${editable}">${inner}</div>`;

  return document.getElementById('field');
}

const list = () => wrapper('List', `
  <label for="choice">Choice</label>
  <select id="choice" data-answer-editor><option value="">Please select...</option><option value="a">Alpha</option><option value="b">Beta</option></select>`);

const multi = () => wrapper('MultiValueList', `
  <input type="checkbox" id="m-a" value="a" data-answer-editor><label for="m-a">Alpha</label>
  <input type="checkbox" id="m-b" value="b" data-answer-editor><label for="m-b">Beta</label>
  <input type="checkbox" id="m-c" value="c" data-answer-editor><label for="m-c">Gamma</label>`);

const text = (type) => wrapper(type, '<textarea id="text" data-answer-editor></textarea>');

describe('answer-fields', () => {
  let fields;

  beforeEach(async () => {
    vi.resetModules();
    await import('../../src/CDC.Web/wwwroot/js/answer-fields.js');
    fields = window.appAnswerFields;
  });

  afterEach(() => {
    vi.restoreAllMocks();
    delete window.appAnswerFields;
    delete window.appRichText;
  });

  describe('the field a wrapper stands for', () => {
    it('is named by data-answer-field', () => {
      expect(fields.fieldId(list())).toBe('field-1');
    });

    it('can be changed only when data-field-editable says so', () => {
      expect(fields.isEditable(wrapper('Text', '', 'true'))).toBe(true);
      expect(fields.isEditable(wrapper('Text', '', 'false'))).toBe(false);
    });
  });

  describe('summaryText', () => {
    it('is empty for an answer that is empty, whatever the type', () => {
      expect(fields.summaryText(list(), '')).toBe('');
      expect(fields.summaryText(wrapper('Boolean', ''), '')).toBe('');
      expect(fields.summaryText(text('Long Text'), '')).toBe('');
    });

    it('is Yes or No for a Boolean', () => {
      const field = wrapper('Boolean', '');

      expect(fields.summaryText(field, 'true')).toBe('Yes');
      expect(fields.summaryText(field, 'false')).toBe('No');
      expect(fields.summaryText(field, 'something else')).toBe('');
    });

    it('is the text of the chosen option for a list, and nothing for one the list does not offer', () => {
      const field = list();

      expect(fields.summaryText(field, 'b')).toBe('Beta');
      expect(fields.summaryText(field, 'retired')).toBe('');
    });

    it('is the labels of the chosen checkboxes, in the order the panel has them, for a list of choices', () => {
      expect(fields.summaryText(multi(), 'c,a')).toBe('Alpha, Gamma');
    });

    it('is a decimal to two places, or what was typed when it is not a number', () => {
      const field = wrapper('Decimal', '');

      expect(fields.summaryText(field, '12.5')).toBe('12.50');
      expect(fields.summaryText(field, '3')).toBe('3.00');
      expect(fields.summaryText(field, 'abc')).toBe('abc');
    });

    it('is a date as it was written', () => {
      expect(fields.summaryText(wrapper('Date', ''), '17/08/2026')).toBe('17/08/2026');
    });

    it('is the text with its markup removed, cut to 50 characters', () => {
      const field = text('Long Text');

      expect(fields.summaryText(field, '<p>Some <em>marked up</em> text</p>')).toBe('Some marked up text');
      expect(fields.summaryText(field, `<p>${'x'.repeat(60)}</p>`)).toBe('x'.repeat(50));
    });

    it.each([
      ['Tom &amp; Jerry', 'Tom & Jerry'],
      ['5 &lt; 6 &gt; 4', '5 < 6 > 4'],
      ['&quot;quoted&quot; and &apos;single&apos;', '"quoted" and \'single\''],
      ['isn&rsquo;t &ndash; &ldquo;quoted&rdquo;&hellip;', 'isn\u2019t \u2013 \u201cquoted\u201d\u2026'],
      ['caf&eacute; &Eacute;cole', 'caf\u00e9 \u00c9cole'],
      ['&beta;-lactam, 5 &mu;m, 20&deg;C, &plusmn;2', '\u03b2-lactam, 5 \u03bcm, 20\u00b0C, \u00b12'],
      ['&euro;5 and &pound;3', '\u20ac5 and \u00a33'],
      ['a&nbsp;b', 'a\u00a0b'],
      ['&#8217; &#x2019; &#X2019;', '\u2019 \u2019 \u2019'],
      ['smile &#128512;', 'smile \u{1f600}']
    ])('is the text with the character references in %j decoded, as the server decodes them', (answer, expected) => {
      expect(fields.summaryText(text('Long Text'), answer)).toBe(expected);
    });

    it('decodes a character reference once, so that what it spells is left as text', () => {
      const field = text('Long Text');

      expect(fields.summaryText(field, '&amp;lt;b&amp;gt;')).toBe('&lt;b&gt;');
      expect(fields.summaryText(field, '&amp;#65;')).toBe('&#65;');
    });

    it('shows markup that was encoded as the text it spells, not as tags to remove', () => {
      expect(fields.summaryText(text('Long Text'), '&lt;b&gt;bold&lt;/b&gt;')).toBe('<b>bold</b>');
    });

    it.each([
      '&unknown;', '&amp', 'AT&T', '&#65', '&#;', '&#x;', '&AMP;', '&#0;', '&#xD800;', '&#1114112;', '&constructor;', '&toString;'
    ])('leaves %j as it was written, because the server does not decode it', (reference) => {
      expect(fields.summaryText(text('Long Text'), `x ${reference} y`)).toBe(`x ${reference} y`);
    });

    it('removes a tag that has another inside it, leaving no markup behind', () => {
      const field = text('Long Text');

      expect(fields.summaryText(field, '<scr<b>ipt>x</scr<b>ipt>')).toBe('ipt>xipt>');
      expect(fields.summaryText(field, '<<b>script>alert(1)<</b>/script>')).toBe('script>alert(1)/script>');
    });

    it('removes a tag from its < to the next >, and keeps a < or > that is not part of one, as the server does', () => {
      const field = text('Long Text');

      expect(fields.summaryText(field, 'a<b>c<d')).toBe('ac<d');
      expect(fields.summaryText(field, 'a > b < c')).toBe('a > b < c');
      expect(fields.summaryText(field, '1 < 2 and 3 > 2')).toBe('1  2');
      expect(fields.summaryText(field, '<>x')).toBe('x');
    });

    it('works through a very long text that has many < and no > without delay', () => {
      const field = text('Long Text');

      expect(fields.summaryText(field, '<'.repeat(200000))).toBe('<'.repeat(50));
      expect(fields.summaryText(field, `&#${'1'.repeat(200000)}`)).toBe(`&#${'1'.repeat(48)}`);
    });

    it('works the text out without parsing the answer as HTML', () => {
      const field = text('Long Text');
      const parse = vi.spyOn(DOMParser.prototype, 'parseFromString');
      const innerHtml = vi.spyOn(Element.prototype, 'innerHTML', 'set');

      expect(fields.summaryText(field, '<img src=x onerror=alert(1)>&lt;b&gt;')).toBe('<b>');
      expect(parse).not.toHaveBeenCalled();
      expect(innerHtml).not.toHaveBeenCalled();
    });

    it('is plain text as it was written for a text field', () => {
      expect(fields.summaryText(wrapper('Text', ''), '  Plain text ')).toBe('Plain text');
    });
  });

  describe('read', () => {
    it('is the value of the control', () => {
      const field = list();
      document.getElementById('choice').value = 'a';

      expect(fields.read(field)).toBe('a');
    });

    it('is the values of the checkboxes that are checked, separated by commas, for a list of choices', () => {
      const field = multi();
      document.getElementById('m-a').checked = true;
      document.getElementById('m-c').checked = true;

      expect(fields.read(field)).toBe('a,c');
    });

    it('is empty when no checkbox is checked', () => {
      expect(fields.read(multi())).toBe('');
    });

    it('is empty for a field with no control', () => {
      expect(fields.read(wrapper('Text', '<span data-answer-text-view></span>'))).toBe('');
    });
  });

  describe('write', () => {
    it('sets the value of the control', () => {
      const field = list();
      fields.write(field, 'b');

      expect(document.getElementById('choice').value).toBe('b');
    });

    it('checks the checkboxes for the values given, and unchecks the others, for a list of choices', () => {
      const field = multi();
      document.getElementById('m-b').checked = true;
      fields.write(field, 'a,c');

      expect([...document.querySelectorAll('input')].map((box) => box.checked)).toEqual([true, false, true]);

      fields.write(field, '');

      expect([...document.querySelectorAll('input')].some((box) => box.checked)).toBe(false);
    });

    it('records the value a drop-down that decides whether other questions apply is put back to when a change is not confirmed', () => {
      const field = wrapper('List', '<select id="gate" data-answer-editor data-relevance-affects="5.2"><option value="">Please select...</option><option value="a">Alpha</option></select>');
      fields.write(field, 'a');

      expect(document.getElementById('gate').dataset.relevanceHeld).toBe('a');
    });

    it('fills the rich text editor of a long text field as well as its text area', () => {
      window.appRichText = { fill: vi.fn() };
      document.body.innerHTML = `
        <div id="field" data-answer-field="field-1" data-field-type="Long Text" data-field-editable="true">
          <textarea id="notes" data-answer-editor></textarea><div id="notes-editor"></div>
        </div>`;
      fields.write(document.getElementById('field'), '<p>Notes</p>');

      expect(document.getElementById('notes').value).toBe('<p>Notes</p>');
      expect(window.appRichText.fill).toHaveBeenCalledWith(document.getElementById('notes-editor'), '<p>Notes</p>');
    });

    it('leaves the text area alone when there is no rich text editor to fill', () => {
      const field = text('Long Text');
      fields.write(field, '<p>Notes</p>');

      expect(document.getElementById('text').value).toBe('<p>Notes</p>');
    });

    it('shows a text answer the user may not change in the view that formats it', () => {
      window.appRichText = { fill: vi.fn() };
      const field = wrapper('Long Text', '<div id="view" data-answer-text-view></div>', 'false');
      fields.write(field, '<p>Formatted</p>');

      expect(window.appRichText.fill).toHaveBeenCalledWith(document.getElementById('view'), '<p>Formatted</p>');
    });

    it('shows a text answer the user may not change as plain text when there is no rich text script', () => {
      const field = wrapper('Long Text', '<div id="view" data-answer-text-view></div>', 'false');
      fields.write(field, '<p>Formatted <em>text</em></p>');

      expect(document.getElementById('view').textContent).toBe('Formatted text');
    });

    it('does nothing for a field with neither a control nor a view', () => {
      const field = wrapper('Text', '');

      expect(() => fields.write(field, 'value')).not.toThrow();
    });
  });
});
