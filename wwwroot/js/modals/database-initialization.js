(() => {
    const form = document.getElementById('database-setup-form');
    const button = document.getElementById('database-setup-confirm');
    const loading = document.getElementById('database-setup-loading');

    form.addEventListener('submit', (event) => {
        if (button.disabled) {
            event.preventDefault();
            return;
        }

        button.disabled = true;
        loading.hidden = false;
    });

    window.addEventListener('pageshow', () => {
        button.disabled = false;
        loading.hidden = true;
    });
})();
