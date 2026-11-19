import {ingredientRow, nutritionSummary} from "./ingredient-row.js";

(() => {
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
        for (const key of [...requiredKeys, 'fiber']) {
            input(key).value = totals[key] == null ? '' : round(totals[key]);
        }
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
                        input(key).value = baseline == null ? '' : round(baseline * value / totalBasis.grams);
                    }
                    nutrition.textContent = nutritionSummary(Object.fromEntries(nutrientKeys.map(key =>
                        [key, input(key).value === '' ? null : parseDecimal(input(key).value)])));
                }
                message.textContent = '';
            });
            if (!ingredientItems.length) {
                for (const key of nutrientKeys) {
                    input(key).oninput = () => {
                        totalBasis = Object.fromEntries([...requiredKeys, 'fiber'].map(field =>
                            [field, input(field).value === '' ? null : parseDecimal(input(field).value)]));
                        nutrition.textContent = nutritionSummary(totalBasis);
                    };
                }
            }
            ingredientList.append(row);
        }
    }

    window.momentumMealEditorOpen = (item, callback) => {
        original = item;
        save = callback;
        editingIngredients = false;
        form.reset();
        for (const key of ['name', 'notes', 'ingredients', ...requiredKeys, 'fiber']) {
            input(key).value = item[key] ?? '';
        }
        for (const key of nutrientKeys) input(key).oninput = null;
        ingredientItems = item.type !== 'drink' && Array.isArray(item.ingredientItems)
            ? item.ingredientItems.flatMap(entry => {
                // A recipe added as one item retains its original ingredient list.
                // Expose those ingredients individually, just like recipe customization in the create modal.
                if (entry.entryMode === 'recipe-custom' && entry.ingredientItems?.length) {
                    return entry.ingredientItems.map(part => ({...part, per100g: {...part.per100g}}));
                }
                if ((entry.entryMode === 'recipe' || entry.entryMode === 'recipe-custom')
                    && Array.isArray(entry.recipeIngredients) && entry.recipeIngredients.length) {
                    const baseWeight = entry.recipeIngredients.reduce((sum, part) => sum + Number(part.amount), 0);
                    const baseKcal = entry.recipeIngredients.reduce((sum, part) => sum + Number(part.kcal) * Number(part.amount) / 100, 0);
                    const portion = entry.recipeFinishedWeight > 0
                        ? Number(entry.grams) / entry.recipeFinishedWeight
                        : baseKcal > 0 && entry.kcal > 0
                            ? Number(entry.kcal) / baseKcal
                            : baseWeight > 0 ? Number(entry.grams) / baseWeight : 1;
                    const portionWeight = baseWeight * portion;
                    return entry.recipeIngredients.map(part => ({
                        name: part.name,
                        unit: part.unit || 'g',
                        grams: Number(part.amount) * portion,
                        diaryWeightFactor: portionWeight > 0 ? Number(entry.grams) / portionWeight : 1,
                        per100g: Object.fromEntries(nutrientKeys.map(key => [key, part[key] == null ? null : Number(part[key])]))
                    }));
                }
                return [{...entry, per100g: Object.fromEntries(nutrientKeys.map(key =>
                    [key, ingredientPer100(entry, key)]))}];
            })
            : [];
        totalBasis = Object.fromEntries([...requiredKeys, 'fiber'].map(key => [key, item[key]]));
        for (const key of nutrientKeys) input(key).readOnly = ingredientItems.length > 0;
        input('ingredients').readOnly = ingredientItems.length > 0;
        document.querySelector('#edit-meal-details').open = false;
        renderIngredients();
        message.textContent = '';
        dialog.showModal();
        input('name').focus();
    };

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
})();
