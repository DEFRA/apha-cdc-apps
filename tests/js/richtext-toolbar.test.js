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

  it('stores an empty value when the editor has no content', async () => {
    const { textarea } = await setup('');
    expect(textarea.value).toBe('');
  });
});
