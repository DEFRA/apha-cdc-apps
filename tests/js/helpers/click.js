// Reports whether a script took the click over, then cancels it so jsdom does not try to navigate.
export function click(element, init = {}) {
  let defaultPrevented;
  const record = (event) => {
    defaultPrevented = event.defaultPrevented;
    event.preventDefault();
  };

  document.addEventListener('click', record, { once: true });
  element.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ...init }));
  document.removeEventListener('click', record);

  return defaultPrevented;
}
