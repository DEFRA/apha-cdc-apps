import { beforeEach, describe, expect, it, vi } from 'vitest';

// The govuk-frontend accordion remembers each open section under "{accordion id}-content-{position}".
const remembered = {
  first: 'profile-questions-accordion-content-1',
  second: 'profile-questions-accordion-content-2'
};

function renderAccordion(collapseOnOpen) {
  document.body.innerHTML = `
    <div class="govuk-accordion" id="profile-questions-accordion" data-app-collapse-on-open="${collapseOnOpen}">
      <div class="govuk-accordion__section"><div id="${remembered.first}" class="govuk-accordion__section-content"></div></div>
      <div class="govuk-accordion__section"><div id="${remembered.second}" class="govuk-accordion__section-content"></div></div>
    </div>`;
}

async function runScript() {
  vi.resetModules();
  await import('../../src/CDC.Web/wwwroot/js/question-accordion.js');
}

describe('question-accordion', () => {
  beforeEach(() => {
    sessionStorage.clear();
    document.body.innerHTML = '';
  });

  it('forgets what the accordion remembered of an earlier visit, so every question opens collapsed', async () => {
    sessionStorage.setItem(remembered.first, 'true');
    sessionStorage.setItem(remembered.second, 'true');
    renderAccordion('true');

    await runScript();

    expect(sessionStorage.getItem(remembered.first)).toBeNull();
    expect(sessionStorage.getItem(remembered.second)).toBeNull();
  });

  it('keeps what the accordion remembered when the answers come back from the page itself, as from Save', async () => {
    sessionStorage.setItem(remembered.first, 'true');
    renderAccordion('false');

    await runScript();

    expect(sessionStorage.getItem(remembered.first)).toBe('true');
  });

  it('leaves what another accordion or page remembered alone', async () => {
    sessionStorage.setItem('another-accordion-content-1', 'true');
    sessionStorage.setItem('profile-questions-accordion', 'true');
    sessionStorage.setItem(remembered.first, 'true');
    renderAccordion('true');

    await runScript();

    expect(sessionStorage.getItem('another-accordion-content-1')).toBe('true');
    expect(sessionStorage.getItem('profile-questions-accordion')).toBe('true');
  });

  it('does nothing on a page without the accordion', async () => {
    sessionStorage.setItem(remembered.first, 'true');

    await runScript();

    expect(sessionStorage.getItem(remembered.first)).toBe('true');
  });

  it('carries on when the browser refuses access to the storage', async () => {
    renderAccordion('true');
    const key = vi.spyOn(Storage.prototype, 'key').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError');
    });
    sessionStorage.setItem(remembered.first, 'true');

    await expect(runScript()).resolves.toBeUndefined();

    key.mockRestore();
  });
});
