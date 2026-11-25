import {ingredientRow, nutritionSummary, roundFoodAmount} from "./ingredient-row.js?v=meal-edit";

export const openEntryEditor = (() => {
    'use strict';

    const dialog = document.querySelector('#edit-meal-dialog');
    if (!dialog) {
        return;
    }

    const form = document.querySelector('#edit-meal-form');
    const ingredientList = document.querySelector('#edit-meal-ingredient-list');
    const message = document.querySelector('#edit-meal-message');
    const nutrientKeys = ['kcal', 'protein', 'carbs', 'fat', 'fiber'];
    const requiredKeys = ['grams', 'kcal', 'protein', 'carbs', 'fat'];
    const numericKeys = [...requiredKeys, 'fiber'];
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
        const grams = ingredientItems.reduce((sum, item) => sum + Number(item.grams) * (item.diaryWeightFactor ?? 1), 0);
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
        for (const key of numericKeys) {
            input(key).value = totals[key] == null ? '' : roundFoodAmount(totals[key]);
        }
    }

    function updateFromIngredients() {
        editingIngredients = true;
        const totals = ingredientTotals();
        showTotals(totals);
        input('ingredients').value = ingredientItems.map(item => `${item.name} (${roundFoodAmount(item.grams)} ${item.unit || 'g'})`).join(', ');
        message.textContent = '';
    }

    function renderIngredients() {
        ingredientList.replaceChildren();
        const items = ingredientItems.length ? ingredientItems : [{
            ...original, unit: original.type === 'drink' ? 'ml' : 'g'
        }];
        for (const [index, ingredient] of items.entries()) {
            const nutritionFor = grams => Object.fromEntries(nutrientKeys.map(key => {
                const per100 = ingredientPer100(ingredient, key);
                return [key, per100 == null ? null : per100 * grams / 100];
            }));
            const remove = ingredientItems.length > 1 ? () => {
                ingredientItems.splice(index, 1);
                updateFromIngredients();
                renderIngredients();
            } : null;
            const {row, amount, nutrition} = ingredientRow({
                ...ingredient, ...(ingredientItems.length ? nutritionFor(ingredient.grams) : totalBasis)
            }, remove);
            amount.addEventListener('input', () => {
                const value = parseDecimal(amount.value);
                const valid = Number.isFinite(value) && value > 0;
                amount.setCustomValidity(valid ? '' : 'Enter an amount greater than 0.');
                if (!valid) return;

                if (ingredientItems.length) {
                    ingredient.grams = value;
                    updateFromIngredients();
                    nutrition.textContent = nutritionSummary(nutritionFor(value));
                } else {
                    input('grams').value = value;
                    for (const key of nutrientKeys) {
                        const baseline = totalBasis[key];
                        input(key).value = baseline == null ? '' : roundFoodAmount(baseline * value / totalBasis.grams);
                    }
                    nutrition.textContent = nutritionSummary(Object.fromEntries(nutrientKeys.map(key =>
                        [key, input(key).value === '' ? null : parseDecimal(input(key).value)])));
                }
                message.textContent = '';
            });
            if (!ingredientItems.length) {
                for (const key of nutrientKeys) {
                    input(key).oninput = () => {
                        totalBasis = Object.fromEntries(numericKeys.map(field =>
                            [field, input(field).value === '' ? null : parseDecimal(input(field).value)]));
                        nutrition.textContent = nutritionSummary(totalBasis);
                    };
                }
            }
            ingredientList.append(row);
        }
    }

    function open(item, callback) {
        original = item;
        save = callback;
        editingIngredients = false;
        form.reset();
        for (const key of ['name', 'notes', 'ingredients', ...numericKeys]) {
            input(key).value = item[key] == null ? '' :
                numericKeys.includes(key) ? roundFoodAmount(item[key]) : item[key];
        }
        for (const key of nutrientKeys) input(key).oninput = null;
        ingredientItems = item.type !== 'drink' && Array.isArray(item.ingredientItems)
            ? item.ingredientItems.map(entry => ({...entry, per100g: Object.fromEntries(nutrientKeys.map(key =>
                [key, ingredientPer100(entry, key)]))}))
            : [];
        totalBasis = Object.fromEntries(numericKeys.map(key => [key, item[key]]));
        for (const key of nutrientKeys) input(key).readOnly = ingredientItems.length > 0;
        input('ingredients').readOnly = ingredientItems.length > 0;
        document.querySelector('#edit-meal-details').open = false;
        renderIngredients();
        message.textContent = '';
        dialog.showModal();
        input('name').focus();
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
        if (!editingIngredients && values.grams === roundFoodAmount(original.grams)) {
            for (const key of numericKeys) {
                if (original[key] != null && values[key] === roundFoodAmount(original[key])) {
                    values[key] = Number(original[key]);
                }
            }
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
            updated.per100ml = Object.fromEntries(nutrientKeys.map(key => [key,
                values[key] == null ? null : round(values[key] * 100 / values.grams)]));
        } else {
            updated.per100g = Object.fromEntries(nutrientKeys.map(key => [key,
                values[key] == null ? null : round(values[key] * 100 / values.grams)]));
            if (editingIngredients) {
                updated.ingredientItems = ingredientItems.map(item => ({
                    ...item,
                    ...Object.fromEntries(nutrientKeys.map(key => {
                        const per100 = ingredientPer100(item, key);
                        return [key, per100 == null ? null : round(per100 * item.grams / 100)];
                    }))
                }));
            }
        }
        save(updated);
        close();
    });
    return open;
})();
