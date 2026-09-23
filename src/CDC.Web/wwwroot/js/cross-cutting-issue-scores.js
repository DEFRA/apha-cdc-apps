// Progressive enhancement: asks the Profile Editor to confirm before an amended score is
// submitted. Without JavaScript the form still posts and updates the score directly.
(function () {
  'use strict';

  var form = document.getElementById('update-score-form');
  if (!form) {
    return;
  }

  form.addEventListener('submit', function (event) {
    var confirmed = window.confirm('Are you sure you want to update the cross-cutting issue scores?');
    if (!confirmed) {
      event.preventDefault();
    }
  });
})();
