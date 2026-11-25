(() => {
    'use strict';
    const dialog = document.querySelector('#edit-meal-dialog');
    if (!dialog) {
        return;
    }
    const form = document.querySelector('#edit-meal-form');
    const parseDecimal = value => Number(String(value).trim().replace(',', '.'));
    const keys = ['grams', 'kcal', 'protein', 'carbs', 'fat'];
    let original = null, save = null;
    const close = () => dialog.close();
    document.querySelector('#close-edit-meal').addEventListener('click', close);
    document.querySelector('#cancel-edit-meal').addEventListener('click', close);
    window.momentumMealEditorOpen = (item, callback) => {
        original = item;
        save = callback;
        form.reset();
        for (const key of ['name', 'notes', 'ingredients', ...keys]) {
            form.elements.namedItem(key).value = item[key] ?? '';
        }
        const drink = item.type === 'drink';
        document.querySelector('#edit-amount-label').firstChild.textContent = drink ? 'Amount (ml)' : 'Amount (g)';
        document.querySelector('#edit-meal-hint').textContent = drink
            ? 'Enter total nutritional values for the selected amount in ml.'
            : 'Enter total nutritional values for the selected amount in grams.';
        document.querySelector('#edit-meal-message').textContent = '';
        dialog.showModal();
        form.elements.namedItem('name').focus();
    };
    form.addEventListener('submit', event => {
        event.preventDefault();
        if (!original || !save || !form.reportValidity()) {
            return;
        }
        const values = {};
        for (const key of keys) {
            const value = parseDecimal(form.elements.namedItem(key).value);
            if (!Number.isFinite(value) || value < 0 || (key === 'grams' && value <= 0)) {
                document.querySelector('#edit-meal-message').textContent = 'Check the amount and nutritional values.';
                return;
            }
            values[key] = value;
        }
        const name = form.elements.namedItem('name').value.trim();
        if (!name) {
            return;
        }
        const updated = {
            ...values, name,
            notes: form.elements.namedItem('notes').value.trim(),
            ingredients: form.elements.namedItem('ingredients').value.trim()
        };
        if (original.type === 'drink') {
            updated.ml = values.grams;
            updated.per100ml = Object.fromEntries(['kcal', 'protein', 'carbs', 'fat'].map(key => [key,
                Number((values[key] * 100 / values.grams).toFixed(3))]));
        }
        save(updated);
        close();
    });
})();
