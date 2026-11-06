document.querySelectorAll('select[data-auto-submit]').forEach(function (select) {
    select.addEventListener('change', function () {
        if (select.value && select.form) {
            select.form.submit();
        }
    });
});
