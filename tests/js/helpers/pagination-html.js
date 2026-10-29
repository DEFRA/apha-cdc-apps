// The markup of the shared _Pagination partial, for the tests of the scripts that follow its links.
export function paginationHtml({ current, total, urlFor = (page) => `/items?PageNumber=${page}`, label = 'Items pages' }) {
  const link = (page, attributes) =>
    `<a class="govuk-link govuk-pagination__link" href="${urlFor(page)}" ${attributes} data-app-page="${page}">`;

  const previous = current > 1
    ? `<div class="govuk-pagination__prev">${link(current - 1, 'rel="prev"')}<span class="govuk-pagination__link-title">Previous</span></a></div>`
    : '';
  const next = current < total
    ? `<div class="govuk-pagination__next">${link(current + 1, 'rel="next"')}<span class="govuk-pagination__link-title">Next</span></a></div>`
    : '';
  const item = (page) => {
    const isCurrent = page === current;
    const className = isCurrent ? 'govuk-pagination__item govuk-pagination__item--current' : 'govuk-pagination__item';
    const pageLabel = `aria-label="Page ${page}"`;
    const attributes = isCurrent ? `${pageLabel} aria-current="page"` : pageLabel;

    return `<li class="${className}">${link(page, attributes)}${page}</a></li>`;
  };
  const items = Array.from({ length: total }, (_unused, index) => item(index + 1)).join('');

  return `<div class="app-pagination-center"><nav class="govuk-pagination" aria-label="${label}">${previous}<ul class="govuk-pagination__list">${items}</ul>${next}</nav></div>`;
}
