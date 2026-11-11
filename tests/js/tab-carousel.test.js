import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

let resizeCallbacks;

// jsdom does no layout, so each test says how wide the tabs and their strip are and where the selected tab sits.
function renderCarousel({
  scrollWidth,
  clientWidth,
  scrollLeft = 0,
  scrollerBox = { left: 100, right: 500 },
  selectedBox = { left: 120, right: 220 },
}) {
  document.body.innerHTML = `
    <div class="govuk-tabs app-tab-carousel">
      <div class="app-tab-carousel__nav">
        <button type="button" data-tab-scroll="left">view more tabs</button>
        <button type="button" data-tab-scroll="right">view more tabs</button>
      </div>
      <div class="app-tab-carousel__scroll">
        <ul class="govuk-tabs__list">
          <li class="govuk-tabs__list-item"><a class="govuk-tabs__tab" href="#one">One</a></li>
          <li class="govuk-tabs__list-item govuk-tabs__list-item--selected"><a class="govuk-tabs__tab" href="#two">Two</a></li>
        </ul>
      </div>
    </div>`;

  const carousel = document.querySelector('.app-tab-carousel');
  const scroller = document.querySelector('.app-tab-carousel__scroll');
  const selected = document.querySelector('.govuk-tabs__list-item--selected');

  Object.defineProperty(scroller, 'scrollWidth', { configurable: true, writable: true, value: scrollWidth });
  Object.defineProperty(scroller, 'clientWidth', { configurable: true, value: clientWidth });
  Object.defineProperty(scroller, 'scrollLeft', { configurable: true, writable: true, value: scrollLeft });
  scroller.getBoundingClientRect = () => scrollerBox;
  selected.getBoundingClientRect = () => selectedBox;
  scroller.scrollBy = vi.fn();

  return { carousel, scroller };
}

async function loadScript() {
  vi.resetModules();
  await import('../../src/CDC.Web/wwwroot/js/tab-carousel.js');
}

beforeEach(() => {
  resizeCallbacks = [];
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(callback) {
        resizeCallbacks.push(callback);
      }

      observe() {}
    },
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  delete document.fonts;
  document.body.innerHTML = '';
});

describe('tab-carousel overflow', () => {
  it('marks the strip as overflowing, which shows the view more tabs links, when the tabs do not all fit', async () => {
    const { carousel } = renderCarousel({ scrollWidth: 1000, clientWidth: 600 });

    await loadScript();

    expect(carousel.classList.contains('app-tab-carousel--overflowing')).toBe(true);
  });

  it('leaves the links hidden when the tabs fit, allowing a pixel for rounding', async () => {
    const { carousel } = renderCarousel({ scrollWidth: 601, clientWidth: 600 });

    await loadScript();

    expect(carousel.classList.contains('app-tab-carousel--overflowing')).toBe(false);
  });

  it('checks again whenever the strip changes size', async () => {
    const { carousel, scroller } = renderCarousel({ scrollWidth: 600, clientWidth: 600 });

    await loadScript();
    expect(carousel.classList.contains('app-tab-carousel--overflowing')).toBe(false);

    scroller.scrollWidth = 1000;
    resizeCallbacks.forEach((callback) => callback());
    expect(carousel.classList.contains('app-tab-carousel--overflowing')).toBe(true);

    scroller.scrollWidth = 600;
    resizeCallbacks.forEach((callback) => callback());
    expect(carousel.classList.contains('app-tab-carousel--overflowing')).toBe(false);
  });

  it('checks again when the window is resized, in a browser without ResizeObserver', async () => {
    vi.stubGlobal('ResizeObserver', undefined);
    const { carousel, scroller } = renderCarousel({ scrollWidth: 600, clientWidth: 600 });

    await loadScript();
    scroller.scrollWidth = 1000;
    window.dispatchEvent(new Event('resize'));

    expect(carousel.classList.contains('app-tab-carousel--overflowing')).toBe(true);
  });

  it('does nothing for a carousel that has no scrolling area', async () => {
    document.body.innerHTML = '<div class="app-tab-carousel"></div>';

    await expect(loadScript()).resolves.toBeUndefined();
  });
});

describe('tab-carousel links', () => {
  it('scrolls the tabs a step to the left or right when a link is clicked', async () => {
    const { scroller } = renderCarousel({ scrollWidth: 1000, clientWidth: 600 });
    await loadScript();

    document.querySelector('[data-tab-scroll="right"]').click();
    expect(scroller.scrollBy).toHaveBeenLastCalledWith({ left: 200, behavior: 'smooth' });

    document.querySelector('[data-tab-scroll="left"]').click();
    expect(scroller.scrollBy).toHaveBeenLastCalledWith({ left: -200, behavior: 'smooth' });
  });

  it('scrolls without animation for a user who prefers reduced motion', async () => {
    vi.stubGlobal('matchMedia', () => ({ matches: true }));
    const { scroller } = renderCarousel({ scrollWidth: 1000, clientWidth: 600 });
    await loadScript();

    document.querySelector('[data-tab-scroll="right"]').click();

    expect(scroller.scrollBy).toHaveBeenLastCalledWith({ left: 200, behavior: 'auto' });
  });

  it('copes with a carousel that has no links', async () => {
    const { scroller } = renderCarousel({ scrollWidth: 1000, clientWidth: 600 });
    document.querySelectorAll('[data-tab-scroll]').forEach((link) => link.remove());

    await loadScript();

    expect(scroller.scrollBy).not.toHaveBeenCalled();
  });
});

describe('tab-carousel selected tab', () => {
  it('scrolls a selected tab that lies beyond the right edge into view, when the tabs overflow', async () => {
    const { scroller } = renderCarousel({
      scrollWidth: 1000,
      clientWidth: 400,
      scrollerBox: { left: 100, right: 500 },
      selectedBox: { left: 520, right: 620 },
    });

    await loadScript();

    expect(scroller.scrollLeft).toBe(120);
  });

  it('scrolls a selected tab that lies beyond the left edge into view, when the tabs overflow', async () => {
    const { scroller } = renderCarousel({
      scrollWidth: 1000,
      clientWidth: 400,
      scrollLeft: 300,
      scrollerBox: { left: 100, right: 500 },
      selectedBox: { left: 40, right: 140 },
    });

    await loadScript();

    expect(scroller.scrollLeft).toBe(240);
  });

  it('leaves the tabs where they are when the selected tab is already in view', async () => {
    const { scroller } = renderCarousel({ scrollWidth: 1000, clientWidth: 400 });

    await loadScript();

    expect(scroller.scrollLeft).toBe(0);
  });

  it('does not move the tabs when none is selected', async () => {
    const { scroller } = renderCarousel({ scrollWidth: 1000, clientWidth: 400 });
    document.querySelector('.govuk-tabs__list-item--selected').classList.remove('govuk-tabs__list-item--selected');

    await loadScript();

    expect(scroller.scrollLeft).toBe(0);
  });

  it('does not move tabs that fit', async () => {
    const { scroller } = renderCarousel({
      scrollWidth: 400,
      clientWidth: 400,
      selectedBox: { left: 520, right: 620 },
    });

    await loadScript();

    expect(scroller.scrollLeft).toBe(0);
  });

  it('shows the selected tab only when the tabs start to overflow, not each time the strip is resized', async () => {
    const { scroller } = renderCarousel({
      scrollWidth: 1000,
      clientWidth: 400,
      selectedBox: { left: 520, right: 620 },
    });
    await loadScript();
    expect(scroller.scrollLeft).toBe(120);

    scroller.scrollLeft = 0;
    resizeCallbacks.forEach((callback) => callback());

    expect(scroller.scrollLeft).toBe(0);
  });

  it('shows the selected tab again once the page font has loaded and changed the widths', async () => {
    let fontsLoaded;
    const ready = new Promise((resolve) => {
      fontsLoaded = resolve;
    });
    Object.defineProperty(document, 'fonts', { configurable: true, value: { ready } });
    const { scroller } = renderCarousel({
      scrollWidth: 1000,
      clientWidth: 400,
      selectedBox: { left: 520, right: 620 },
    });
    await loadScript();
    scroller.scrollLeft = 0;

    fontsLoaded();
    await Promise.resolve();
    await Promise.resolve();

    expect(scroller.scrollLeft).toBe(120);
  });
});
