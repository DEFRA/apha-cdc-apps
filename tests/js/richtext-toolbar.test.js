import { beforeEach, describe, expect, it, vi } from 'vitest';

async function setup(initial) {
  document.body.innerHTML = `
    <label for="Notes">Notes</label>
    <textarea id="Notes"></textarea>
    <button type="button" data-richtext-command="italic" data-richtext-target="Notes">I</button>
  `;
  const textarea = document.getElementById('Notes');
  textarea.value = initial;
  // The script initialises when imported, so import it afresh once the page exists.
  vi.resetModules();
  await import('../../src/CDC.Web/wwwroot/js/richtext-toolbar.js');
  const editor = document.getElementById('Notes-editor');
  editor.dispatchEvent(new Event('input', { bubbles: true }));
  return { editor, textarea };
}

describe('richtext-toolbar', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('keeps allowed formatting and round-trips it to the textarea', async () => {
    const { editor, textarea } = await setup('<p>Some <em>bold</em> text</p><ul><li>one</li><li>two</li></ul>');
    expect(editor.querySelector('em').textContent).toBe('bold');
    expect(editor.querySelectorAll('li')).toHaveLength(2);
    expect(textarea.value).toBe('<p>Some <em>bold</em> text</p><ul><li>one</li><li>two</li></ul>');
  });

  it('drops disallowed tags and event attributes but keeps their text', async () => {
    const { editor, textarea } = await setup('<p onclick="x()">Hi <img src=x onerror="alert(1)"><script>alert(2)</script></p>');
    expect(editor.querySelector('img, script')).toBeNull();
    expect(textarea.value).toBe('<p>Hi alert(2)</p>');
  });

  it('keeps safe links and removes unsafe href values', async () => {
    const safe = await setup('<a href="https://example.gov.uk/a?b=1&amp;c=2" target="_blank" onclick="x()">link</a>');
    expect(safe.textarea.value).toBe('<a href="https://example.gov.uk/a?b=1&amp;c=2" target="_blank">link</a>');

    const unsafe = await setup('<a href="javascript:alert(1)">bad</a>');
    expect(unsafe.textarea.value).toBe('<a>bad</a>');
  });

  it('treats plain text as text and keeps line breaks', async () => {
    const { editor, textarea } = await setup('a < b & c\nnext');
    expect(editor.querySelectorAll('br')).toHaveLength(1);
    expect(textarea.value).toBe('a &lt; b &amp; c<br>next');
  });

  it('never creates elements from escaped markup', async () => {
    const { editor } = await setup('&lt;img src=x onerror=alert(1)&gt;');
    expect(editor.querySelector('img')).toBeNull();
    expect(editor.textContent).toBe('<img src=x onerror=alert(1)>');
  });

  it('shows a stored character reference as its character, not as code', async () => {
    const { editor, textarea } = await setup('<p>Farmer&rsquo;s &ldquo;pigs&rdquo; &ndash; &pound;5 &#8217; &#x2019; caf&eacute;</p>');
    expect(editor.textContent).toBe('Farmer\u2019s \u201Cpigs\u201D \u2013 \u00A35 \u2019 \u2019 caf\u00E9');
    // The characters themselves are posted; the server writes anything outside ASCII back as a numeric reference.
    expect(textarea.value).toBe('<p>Farmer\u2019s \u201Cpigs\u201D \u2013 \u00A35 \u2019 \u2019 caf\u00E9</p>');
  });

  it('keeps a reference to markup as text, and leaves an unknown or unfinished reference as it is', async () => {
    const { editor, textarea } = await setup('<p>&lt;b&gt; &amp; &unknown; AT&amp;T R&D &copy</p>');
    expect(editor.querySelector('b')).toBeNull();
    expect(editor.textContent).toBe('<b> & &unknown; AT&T R&D &copy');
    expect(textarea.value).toBe('<p>&lt;b&gt; &amp; &amp;unknown; AT&amp;T R&amp;D &amp;copy</p>');
  });

  it('reads a self-closing line break as a line break, not as text', async () => {
    const { editor, textarea } = await setup('Line one<br/>Line two<br />Line three<br>End');
    expect(editor.querySelectorAll('br')).toHaveLength(3);
    expect(editor.textContent).toBe('Line oneLine twoLine threeEnd');
    expect(textarea.value).toBe('Line one<br>Line two<br>Line three<br>End');
  });

  it('adds no line break for the line feeds that lay out the list tags', async () => {
    const { editor } = await setup('Intro<br/>\n<ul>\n<li>One</li>\n<li>Two</li>\n</ul>');
    expect(editor.querySelectorAll('br')).toHaveLength(1);
    expect(editor.querySelectorAll('li')).toHaveLength(2);
  });

  it('stores an empty value when the editor has no content', async () => {
    const { textarea } = await setup('');
    expect(textarea.value).toBe('');
  });

  it('fills any element with an answer through the same allow-list, so one editor can show each species answer in turn', async () => {
    const { editor } = await setup('<p>First</p>');
    const view = document.createElement('div');
    document.body.appendChild(view);

    window.appRichText.fill(view, '<p onclick="x()">Second <em>one</em></p><script>alert(2)</script>');
    window.appRichText.fill(editor, '<ul><li>Only</li></ul>');

    expect(view.querySelector('script')).toBeNull();
    expect(view.querySelector('p').hasAttribute('onclick')).toBe(false);
    expect(view.querySelector('em').textContent).toBe('one');
    expect(editor.querySelectorAll('li')).toHaveLength(1);
    expect(editor.textContent).toBe('Only');
  });

  it('empties the element it fills when there is no answer', async () => {
    const { editor } = await setup('<p>First</p>');

    window.appRichText.fill(editor, undefined);

    expect(editor.childNodes).toHaveLength(0);
  });

  describe('the link button', () => {
    async function setupWithLink(initial) {
      HTMLDialogElement.prototype.showModal = function showModal() { this.setAttribute('open', ''); };
      HTMLDialogElement.prototype.close = function close() {
        this.removeAttribute('open');
        this.dispatchEvent(new Event('close'));
      };
      document.body.innerHTML = `
        <label for="Notes">Notes</label>
        <textarea id="Notes"></textarea>
        <button type="button" data-richtext-command="link" data-richtext-target="Notes">Link</button>
      `;
      document.getElementById('Notes').value = initial;
      vi.resetModules();
      await import('../../src/CDC.Web/wwwroot/js/richtext-toolbar.js');
      return {
        editor: document.getElementById('Notes-editor'),
        textarea: document.getElementById('Notes'),
        button: document.querySelector('[data-richtext-command="link"]')
      };
    }

    function dialogParts() {
      const dialog = document.getElementById('richtext-link-dialog');
      return {
        dialog,
        url: dialog.querySelector('#richtext-link-url'),
        text: dialog.querySelector('#richtext-link-text'),
        title: dialog.querySelector('#richtext-link-title'),
        target: dialog.querySelector('#richtext-link-target'),
        save: dialog.querySelector('[data-richtext-link-save]'),
        error: dialog.querySelector('#richtext-link-url-error')
      };
    }

    it('opens the legacy Insert/Edit Link dialog: URL, Text to display, Title and Open link in..., then Save and Cancel', async () => {
      const { button } = await setupWithLink('');

      button.click();

      const { dialog, target } = dialogParts();
      expect(dialog.open).toBe(true);
      expect(dialog.querySelector('h2').textContent).toBe('Insert/Edit Link');
      expect([...dialog.querySelectorAll('label')].map(label => label.textContent)).toEqual(['URL', 'Text to display', 'Title', 'Open link in...']);
      expect([...target.options].map(option => option.textContent)).toEqual(['Current window', 'New window']);
      expect([...dialog.querySelectorAll('button')].map(b => b.textContent)).toEqual(['Save', 'Cancel']);
    });

    it('adds the link with its text, title and window when saved', async () => {
      const { button, textarea } = await setupWithLink('');
      button.click();
      const parts = dialogParts();

      parts.url.value = 'https://www.gov.uk/';
      parts.text.value = 'GOV.UK';
      parts.title.value = 'Go to GOV.UK';
      parts.target.value = '_blank';
      parts.save.click();

      expect(parts.dialog.open).toBe(false);
      expect(textarea.value).toBe('<a href="https://www.gov.uk/" target="_blank" title="Go to GOV.UK">GOV.UK</a>');
    });

    it('does not add a link to an unsafe address, and says what to enter', async () => {
      const { button, textarea } = await setupWithLink('');
      button.click();
      const parts = dialogParts();

      parts.url.value = 'javascript:alert(1)';
      parts.save.click();

      expect(parts.dialog.open).toBe(true);
      expect(parts.error.hidden).toBe(false);
      expect(parts.error.textContent).toContain('Enter a web address starting with http://, https:// or mailto:');
      expect(textarea.value).toBe('');
    });

    it('edits the link the cursor is in, starting from its current values', async () => {
      const { button, editor, textarea } = await setupWithLink('<p><a href="https://old.gov.uk/" title="Old">old</a></p>');
      const range = document.createRange();
      range.setStart(editor.querySelector('a').firstChild, 1);
      window.getSelection().removeAllRanges();
      window.getSelection().addRange(range);

      button.click();
      const parts = dialogParts();
      expect([parts.url.value, parts.text.value, parts.title.value, parts.target.value]).toEqual(['https://old.gov.uk/', 'old', 'Old', '']);

      parts.url.value = 'https://new.gov.uk/';
      parts.text.value = 'new';
      parts.title.value = '';
      parts.save.click();

      expect(editor.querySelectorAll('a')).toHaveLength(1);
      expect(textarea.value).toBe('<p><a href="https://new.gov.uk/">new</a></p>');
    });
  });
});
