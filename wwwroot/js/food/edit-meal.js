(() => {
    'use strict';

    const dialog = document.querySelector('#edit-meal-dialog');
    if (!dialog) {
        return;
    }

    const form = document.querySelector('#edit-meal-form');
    const panel = document.querySelector('#edit-meal-ingredients-panel');
    const ingredientList = document.querySelector('#edit-meal-ingredient-list');
    const message = document.querySelector('#edit-meal-message');
    const nutrientKeys = ['kcal', 'protein', 'carbs', 'fat', 'fiber'];
    const requiredKeys = ['grams', 'kcal', 'protein', 'carbs', 'fat'];
    const parseDecimal = value => {
        const text = String(value ?? '').trim().replace(',', '.');
        return text && /^[-+]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(text) ? Number(text) : NaN;
    };
    const round = value => Number(Number(value).toFixed(3));
    const input = key => form.elements.namedItem(key);
    let original = null;
    let save = null;
    let ingredientItems = [];
    let editingIngredients = false;
    let changingTotals = false;
    let totalBasis = null;

    const close = () => dialog.close();
    document.querySelector('#close-edit-meal').addEventListener('click', close);
    document.querySelector('#cancel-edit-meal').addEventListener('click', close);

    function ingredientPer100(item, key) {
        if (item.per100g && item.per100g[key] != null) {
            return Number(item.per100g[key]);
        }
        if (item[key] == null) {
            return null;
        }
        const grams = Number(item.grams);
        return grams > 0 ? Number(item[key]) * 100 / grams : null;
    }

    function ingredientTotals() {
        const grams = ingredientItems.reduce((sum, item) => sum + Number(item.grams), 0);
        const totals = {grams};
        for (const key of nutrientKeys) {
            const known = ingredientItems.some(item => ingredientPer100(item, key) != null);
            totals[key] = !known && key === 'fiber' ? null : round(ingredientItems.reduce((sum, item) => {
                const per100 = ingredientPer100(item, key);
                return sum + (per100 == null ? 0 : per100 * Number(item.grams) / 100);
            }, 0));
        }
        return totals;
    }

    function showTotals(totals) {
        for (const key of [...requiredKeys, 'fiber']) {
            input(key).value = totals[key] == null ? '' : round(totals[key]);
        }
        totalBasis = {...totals};
    }

    function updateFromIngredients() {
        editingIngredients = true;
        const totals = ingredientTotals();
        showTotals(totals);
        input('ingredients').value = ingredientItems.map(item => `${item.name} (${round(item.grams)} ${item.unit || 'g'})`).join(', ');
        message.textContent = '';
    }

    function renderIngredients() {
        ingredientList.replaceChildren();
        for (const ingredient of ingredientItems) {
            const row = document.createElement('label');
            row.className = 'edit-meal-ingredient';
            const name = document.createElement('span');
            name.textContent = ingredient.name;
            const amount = document.createElement('input');
            amount.type = 'text';
            amount.inputMode = 'decimal';
            amount.value = ingredient.grams;
            amount.setAttribute('aria-label', `Amount for ${ingredient.name}`);
            const unit = document.createElement('span');
            unit.textContent = ingredient.unit || 'g';
            amount.addEventListener('input', () => {
                const value = parseDecimal(amount.value);
                if (!Number.isFinite(value) || value <= 0) {
                    amount.setCustomValidity('Enter an amount greater than 0.');
                    message.textContent = 'All ingredients must have a valid amount.';
                    return;
                }
                amount.setCustomValidity('');
                ingredient.grams = value;
                updateFromIngredients();
            });
            row.append(name, amount, unit);
            ingredientList.append(row);
        }
    }

    window.momentumMealEditorOpen = (item, callback) => {
        original = item;
        save = callback;
        editingIngredients = false;
        changingTotals = false;
        form.reset();
        for (const key of ['name', 'notes', 'ingredients', ...requiredKeys, 'fiber']) {
            input(key).value = item[key] ?? '';
        }
        ingredientItems = Array.isArray(item.ingredientItems)
            ? item.ingredientItems.flatMap(entry => {
                // A recipe added as one item retains its original ingredient list.
                // Expose those ingredients individually, just like recipe customization in the create modal.
                if ((entry.entryMode === 'recipe' || entry.entryMode === 'recipe-custom')
                    && Array.isArray(entry.recipeIngredients) && entry.recipeIngredients.length) {
                    const baseWeight = entry.recipeIngredients.reduce((sum, part) => sum + Number(part.amount || 0), 0);
                    const ratio = baseWeight > 0 ? Number(entry.grams) / baseWeight : 1;
                    return entry.recipeIngredients.map(part => ({
                        name: part.name,
                        unit: part.unit || 'g',
                        grams: round(Number(part.amount || 0) * ratio),
                        per100g: Object.fromEntries(nutrientKeys.map(key => [key, part[key] == null ? null : Number(part[key])]))
                    }));
                }
                return [{...entry, per100g: entry.per100g ? {...entry.per100g} : undefined}];
            })
            : [];
        panel.hidden = ingredientItems.length === 0 || item.type === 'drink';
        if (!panel.hidden) {
            renderIngredients();
        } else {
            ingredientList.replaceChildren();
        }
        totalBasis = Object.fromEntries([...requiredKeys, 'fiber'].map(key => [key, item[key]]));
        const drink = item.type === 'drink';
        document.querySelector('#edit-amount-label').firstChild.textContent = drink ? 'Amount (ml)' : 'Amount (g)';
        document.querySelector('#edit-meal-hint').textContent = drink
            ? 'Total nutritional values for the selected amount in ml.'
            : 'Total nutritional values for the selected amount in grams. You can also edit the totals manually.';
        message.textContent = '';
        dialog.showModal();
        input('name').focus();
    };

    input('grams').addEventListener('input', () => {
        const grams = parseDecimal(input('grams').value);
        const previous = Number(totalBasis?.grams);
        if (!(grams > 0) || !(previous > 0)) {
            return;
        }
        // Changing the total quantity scales the displayed totals without rewriting individual ingredients.
        for (const key of nutrientKeys) {
            const value = totalBasis[key];
            input(key).value = value == null ? '' : round(Number(value) * grams / previous);
        }
        changingTotals = true;
        message.textContent = '';
    });

    for (const key of nutrientKeys) {
        input(key).addEventListener('input', () => {
            changingTotals = true;
        });
    }

    form.addEventListener('submit', event => {
        event.preventDefault();
        if (!original || !save || !form.reportValidity()) {
            return;
        }
        const values = {};
        for (const key of requiredKeys) {
            const value = parseDecimal(input(key).value);
            if (!Number.isFinite(value) || value < 0 || (key === 'grams' && value <= 0)) {
                message.textContent = 'Check the amount and nutritional values.';
                return;
            }
            values[key] = value;
        }
        const fiberText = input('fiber').value.trim();
        values.fiber = fiberText ? parseDecimal(fiberText) : null;
        if (values.fiber != null && (!Number.isFinite(values.fiber) || values.fiber < 0)) {
            message.textContent = 'Check fibre.';
            return;
        }
        const name = input('name').value.trim();
        if (!name) {
            return;
        }
        const updated = {
            ...values, name,
            notes: input('notes').value.trim(),
            ingredients: input('ingredients').value.trim()
        };
        if (original.type === 'drink') {
            updated.ml = values.grams;
            updated.per100ml = Object.fromEntries(nutrientKeys.filter(key => key !== 'fiber')
                .map(key => [key, round(values[key] * 100 / values.grams)]));
        } else {
            updated.per100g = Object.fromEntries(nutrientKeys.map(key => [key,
                values[key] == null ? null : round(values[key] * 100 / values.grams)]));
            if (ingredientItems.length) {
                if (editingIngredients && changingTotals) {
                    // The total fields take precedence if both ingredient amounts and totals were edited.
                    updated.ingredientItems = ingredientItems.map(item => ({...item}));
                } else if (editingIngredients) {
                    updated.ingredientItems = ingredientItems.map(item => ({...item}));
                }
            }
        }
        save(updated);
        close();
    });
})();
