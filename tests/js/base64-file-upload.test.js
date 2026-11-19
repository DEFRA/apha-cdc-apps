import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const BASE64_TYPE = 'application/base64';
const PDF_TEXT = '%PDF-1.4 <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF/></x:xmpmeta>';

// jsdom has no DataTransfer, so this stands in for it, and input.files is replaced rather than assigned.
class FakeDataTransfer {
  constructor() {
    this.files = [];
    this.items = { add: (file) => this.files.push(file) };
  }
}

function setFiles(input, files) {
  Object.defineProperty(input, 'files', { value: files, writable: true, configurable: true });
}

function pdf(name = 'DiseaseRankingReport.pdf', text = PDF_TEXT) {
  return new File([text], name, { type: 'application/pdf' });
}

// jsdom's Blob has no text(), so read the file as a browser's FileReader would.
function readText(file) {
  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.readAsText(file);
  });
}

// The submit event a browser fires when the form is used, without going anywhere.
function submit(form, submitter) {
  const event = new SubmitEvent('submit', { submitter, bubbles: true, cancelable: true });
  form.dispatchEvent(event);

  return event;
}

function elements() {
  const form = document.getElementById('upload-form');

  return {
    form,
    input: document.getElementById('file'),
    ok: document.getElementById('ok'),
    cancel: document.getElementById('cancel')
  };
}

beforeAll(async () => {
  document.body.innerHTML = `
    <form id="upload-form" method="post" data-base64-upload>
      <input id="file" type="file" />
      <button id="ok" type="submit">OK</button>
      <button id="cancel" type="submit" formmethod="get">Cancel</button>
    </form>
    <form id="plain-form" method="post"><input id="plain-file" type="file" /></form>
    <form id="no-file-form" method="post" data-base64-upload><button id="no-file-ok" type="submit">OK</button></form>
  `;

  await import('../../src/CDC.Web/wwwroot/js/base64-file-upload.js');
});

beforeEach(() => {
  globalThis.DataTransfer = FakeDataTransfer;
  const { form, input } = elements();
  form.submit = vi.fn();
  setFiles(input, []);
});

afterEach(() => {
  vi.unstubAllGlobals();
  delete globalThis.DataTransfer;
});

describe('base64-file-upload', () => {
  it('replaces the chosen file with its base64 text, keeping the name, then submits the form', async () => {
    const { form, input, ok } = elements();
    setFiles(input, [pdf()]);

    const event = submit(form, ok);
    await vi.waitFor(() => expect(form.submit).toHaveBeenCalledTimes(1));

    expect(event.defaultPrevented).toBe(true);
    const [encoded] = input.files;
    expect(encoded.name).toBe('DiseaseRankingReport.pdf');
    expect(encoded.type).toBe(BASE64_TYPE);
    expect(await readText(encoded)).toBe(btoa(PDF_TEXT));
  });

  it('sends nothing the cross-site scripting rule looks for', async () => {
    const { form, input, ok } = elements();
    setFiles(input, [pdf()]);

    submit(form, ok);
    await vi.waitFor(() => expect(form.submit).toHaveBeenCalled());

    expect(await readText(input.files[0])).toMatch(/^[A-Za-z0-9+/]+={0,2}$/);
  });

  it('encodes an empty file to an empty file so the server can report it', async () => {
    const { form, input, ok } = elements();
    setFiles(input, [pdf('empty.pdf', '')]);

    submit(form, ok);
    await vi.waitFor(() => expect(form.submit).toHaveBeenCalled());

    expect(input.files[0].size).toBe(0);
    expect(input.files[0].type).toBe(BASE64_TYPE);
  });

  it('encodes the file once, however many times the form is submitted while it is being read', async () => {
    const { form, input, ok } = elements();
    setFiles(input, [pdf()]);

    submit(form, ok);
    const second = submit(form, ok);
    await vi.waitFor(() => expect(form.submit).toHaveBeenCalled());

    expect(second.defaultPrevented).toBe(true);
    expect(form.submit).toHaveBeenCalledTimes(1);
  });

  it('leaves the submit alone when no file is chosen', () => {
    const { form, ok } = elements();

    const event = submit(form, ok);

    expect(event.defaultPrevented).toBe(false);
    expect(form.submit).not.toHaveBeenCalled();
  });

  it('leaves the submit alone when the file input has no file list', () => {
    const { form, input, ok } = elements();
    setFiles(input, null);

    expect(submit(form, ok).defaultPrevented).toBe(false);
  });

  it('leaves the submit alone for a button with its own method, such as Cancel', () => {
    const { form, input, cancel } = elements();
    setFiles(input, [pdf()]);

    const event = submit(form, cancel);

    expect(event.defaultPrevented).toBe(false);
    expect(input.files[0].type).toBe('application/pdf');
  });

  it('leaves the submit alone when another handler has already stopped it', () => {
    const { form, input, ok } = elements();
    setFiles(input, [pdf()]);
    // A capturing listener on the document runs before the form's own handlers, as another script's might.
    document.addEventListener('submit', (event) => event.preventDefault(), { capture: true, once: true });

    submit(form, ok);

    expect(form.submit).not.toHaveBeenCalled();
    expect(input.files[0].type).toBe('application/pdf');
  });

  it('does not encode a file it has already encoded, as when the page is restored from the back/forward cache', () => {
    const { form, input, ok } = elements();
    const encoded = new File([btoa(PDF_TEXT)], 'report.pdf', { type: BASE64_TYPE });
    setFiles(input, [encoded]);

    const event = submit(form, ok);

    expect(event.defaultPrevented).toBe(false);
    expect(input.files[0]).toBe(encoded);
  });

  it('sends the file unchanged when the browser cannot replace the file list', () => {
    delete globalThis.DataTransfer;
    const { form, input, ok } = elements();
    setFiles(input, [pdf()]);

    const event = submit(form, ok);

    expect(event.defaultPrevented).toBe(false);
    expect(input.files[0].type).toBe('application/pdf');
  });

  it('sends the file unchanged when the browser cannot read it', async () => {
    class FailingFileReader {
      readAsDataURL() {
        this.error = new Error('unreadable');
        queueMicrotask(() => this.onerror());
      }
    }
    vi.stubGlobal('FileReader', FailingFileReader);
    const { form, input, ok } = elements();
    const original = pdf();
    setFiles(input, [original]);

    submit(form, ok);
    await vi.waitFor(() => expect(form.submit).toHaveBeenCalledTimes(1));

    expect(input.files[0]).toBe(original);
  });

  it('ignores forms that are not marked for base64 upload', () => {
    const plain = document.getElementById('plain-form');
    setFiles(document.getElementById('plain-file'), [pdf()]);

    expect(submit(plain).defaultPrevented).toBe(false);
  });

  it('ignores a marked form that has no file input', () => {
    const form = document.getElementById('no-file-form');

    expect(submit(form, document.getElementById('no-file-ok')).defaultPrevented).toBe(false);
  });
});
