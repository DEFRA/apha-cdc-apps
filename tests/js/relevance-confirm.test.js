import { beforeAll, describe, expect, it } from 'vitest';

const no = '11111111-1111-1111-1111-111111111111';
const varies = '22222222-2222-2222-2222-222222222222';

beforeAll(async () => {
  HTMLDialogElement.prototype.showModal = function showModal() {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.close = function close() {
    this.removeAttribute('open');
  };

  document.body.innerHTML = `
    <select id="field_a" data-affects-relevance-of="2.2, 2.3">
      <option value="">Please select...</option>
      <option value="${no}">No</option>
      <option value="${varies}">Yes - varies</option>
    </select>
    <div id="q22" data-irrelevance-field="field_a" data-irrelevance-value="${varies}" data-irrelevance-flag="true"></div>
    <div id="q23" data-irrelevance-field="field_a" data-irrelevance-value="${no}" data-irrelevance-flag="false"></div>`;

  await import('../../src/CDC.Web/wwwroot/js/relevance-confirm.js');
});

function choose(value) {
  const select = document.getElementById('field_a');
  select.value = value;
  select.dispatchEvent(new Event('change'));
  document.querySelector('[data-relevance-ok]').click();
}

describe('relevance-confirm', () => {
  it('hides a question that is only relevant to another answer once the change is confirmed', () => {
    choose(no);

    expect(document.getElementById('q22').hidden).toBe(true);
    expect(document.getElementById('q23').hidden).toBe(true);
  });

  it('shows the questions the new answer makes relevant', () => {
    choose(varies);

    expect(document.getElementById('q22').hidden).toBe(false);
    expect(document.getElementById('q23').hidden).toBe(false);
  });
});
