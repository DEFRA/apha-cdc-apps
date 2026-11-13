// Progressive enhancement: asks the Profile Editor to confirm before an amended score is
// submitted. Without JavaScript the form still posts and updates the score directly.
(function () {
  'use strict';

  const categoryForm = document.getElementById('category-form');
  const categorySelect = document.getElementById('CategoryId');
  if (categoryForm && categorySelect) {
    categorySelect.addEventListener('change', function () {
      categoryForm.submit();
    });
  }

  const form = document.getElementById('update-scores-form');
  if (!form) {
    return;
  }

  form.addEventListener('submit', function (event) {
    const confirmed = window.confirm('Are you sure you want to update the cross-cutting issue scores?');
    if (!confirmed) {
      event.preventDefault();
    }
  });
})();
