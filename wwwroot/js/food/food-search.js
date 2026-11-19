import {FOOD_KEY, write} from "../core/storage.js";
import {parseDecimal, fmt, setText} from "../core/utils.js";
import {food} from "./food-diary.js?v=20261101-food-nutrition";

function setupFoodSearch(picker, refresh) {
    const dialog = document.querySelector('#food-dialog');
    const form = document.querySelector('#food-form');
    const search = document.querySelector('#food-search');
    const results = document.querySelector('#food-search-results');
    const status = document.querySelector('#food-search-status');
    const warning = document.querySelector('#food-warning');
    const list = document.querySelector('#food-ingredient-list');
    const addButton = document.querySelector('#food-add-ingredient');
    const saveButton = document.querySelector('#food-save-meal');
    const fields = ['kcal', 'protein', 'carbs', 'fat', 'fiber'];
    if (!dialog || !form || !search || !results || !list || !addButton || !saveButton) {
        return;
    }

    let debounce = null;
    let abort = null;
    let searchNumber = 0;
    let ingredients = [];
    let currentSource = '';
    let searchSource = 'frida';
    let currentMode = 'search';
    let editingId = null;
    let selectedRecipe = null;
    let selectedFoodPer100 = null;
    let customizedRecipeIngredients = null;
    const round2 = value => Math.round((Number(value) + Number.EPSILON) * 100) / 100;
    const number = key => parseDecimal(form.elements.namedItem(key).value);

    function activeIngredient() {
        if (form.hidden) {
            return null;
        }
        const name = form.elements.namedItem('name').value.trim();
        const grams = number('grams');
        if (!name || !Number.isFinite(grams) || grams <= 0) {
            return null;
        }
        const per100g = {};
        for (const key of fields) {
            const raw = form.elements.namedItem(key).value;
            if (key === 'fiber' && raw === '') {
                per100g[key] = null;
                continue;
            }
            if (raw === '' || !Number.isFinite(parseDecimal(raw)) || parseDecimal(raw) < 0) {
                return null;
            }
            per100g[key] = currentMode === 'search' ? parseDecimal(raw) * 100 / grams : parseDecimal(raw);
        }
        return currentMode === 'recipe' && customizedRecipeIngredients
            ? {
                name, grams, source: currentSource, entryMode: 'recipe-custom',
                recipeIngredients: selectedRecipe.recipeIngredients.map(ingredient => ({...ingredient})),
                ingredientItems: customizedRecipeIngredients.map(ingredient => ({
                    name: ingredient.name, grams: ingredient.grams, unit: ingredient.unit || 'g',
                    source: currentSource,
                    per100g: {...ingredient.per100g},
                    ...Object.fromEntries(fields.map(key => [key, ingredient.per100g[key] == null
                        ? null : ingredient.per100g[key] * ingredient.grams / 100]))
                })),
                ...Object.fromEntries(fields.map(key => [key, per100g[key]]))
            }
            : currentMode === 'recipe'
                ? {
                    name, grams, source: currentSource, entryMode: 'recipe',
                    recipeIngredients: selectedRecipe.recipeIngredients.map(ingredient => ({...ingredient})),
                    ...Object.fromEntries(fields.map(key => [key, per100g[key]]))
                }
                : currentMode === 'manual'
                    ? {
                        name, grams, source: 'Manual entry', entryMode: 'manual',
                        ...Object.fromEntries(fields.map(key => [key, per100g[key]]))
                    }
                    : {
                        name, grams, per100g, source: currentSource, entryMode: 'search',
                        ...Object.fromEntries(fields.map(key => [key, per100g[key] === null ? null : per100g[key] * grams / 100]))
                    };
    }

    // Ingredient customization is independent from proportional recipe portions.
    function setRecipeCustomizationVisible(visible) {
        const panel = document.querySelector('#food-recipe-customization');
        const button = document.querySelector('#food-customize-recipe');
        panel.hidden = !visible;
        button.hidden = !selectedRecipe;
        button.textContent = visible ? 'Use standard portion' : 'Adjust ingredients';
    }

    function recipeIngredientTotals() {
        const values = customizedRecipeIngredients || [];
        const grams = values.reduce((total, ingredient) => total + ingredient.grams, 0);
        const totals = Object.fromEntries(fields.map(key => [key,
            key === 'fiber' && !values.some(ingredient => ingredient.per100g[key] != null)
                ? null
                : values.reduce((total, ingredient) => total + (ingredient.per100g[key] || 0) * ingredient.grams / 100, 0)
        ]));
        return {grams, ...totals};
    }

    function updateCustomizedRecipe() {
        const totals = recipeIngredientTotals();
        form.elements.namedItem('grams').value = round2(totals.grams);
        for (const key of fields) {
            form.elements.namedItem(key).value = totals[key] == null ? '' : round2(totals[key]);
        }
        const notes = document.querySelector('#food-meal-notes');
        notes.value = customizedRecipeIngredients.map(ingredient =>
            `${ingredient.name} - ${fmt(ingredient.grams)} ${ingredient.unit || 'g'}`).join(' | ');
    }

    function renderRecipeCustomization() {
        const container = document.querySelector('#food-recipe-ingredient-list');
        container.replaceChildren();
        for (const ingredient of customizedRecipeIngredients) {
            const label = document.createElement('label');
            label.className = 'food-recipe-ingredient';
            const name = document.createElement('span');
            name.textContent = ingredient.name;
            const amount = document.createElement('input');
            amount.type = 'text';
            amount.inputMode = 'decimal';
            amount.value = ingredient.grams;
            amount.setAttribute('aria-label', `Amount for ${ingredient.name} in grams`);
            const unit = document.createElement('span');
            unit.textContent = ingredient.unit || 'g';
            amount.addEventListener('input', () => {
                const value = parseDecimal(amount.value);
                if (!Number.isFinite(value) || value <= 0) {
                    amount.setCustomValidity('Enter an amount greater than 0.');
                    return;
                }
                amount.setCustomValidity('');
                ingredient.grams = value;
                updateCustomizedRecipe();
            });
            label.append(name, amount, unit);
            container.append(label);
        }
        updateCustomizedRecipe();
    }

    function resetRecipeCustomization() {
        customizedRecipeIngredients = null;
        setRecipeCustomizationVisible(false);
        if (selectedRecipe) {
            form.elements.namedItem('grams').readOnly = false;
            const notes = document.querySelector('#food-meal-notes');
            notes.value = (selectedRecipe.recipeIngredients || []).map(ingredient =>
                `${ingredient.name} - ${fmt(ingredient.amount)} ${ingredient.unit || 'g'}`).join(' | ');
            recalculate();
        }
    }

    function recalculate() {
        if (currentMode === 'search' && selectedFoodPer100) {
            const grams = number('grams');
            if (!(grams > 0)) {
                return;
            }
            for (const key of fields) {
                const value = selectedFoodPer100[key];
                form.elements.namedItem(key).value = value == null ? '' : round2(value * grams / 100);
            }
            return;
        }
        if (currentMode !== 'recipe' || !selectedRecipe || customizedRecipeIngredients) {
            return;
        }
        const grams = number('grams');
        if (!Number.isFinite(grams) || grams <= 0) {
            return;
        }
        for (const key of fields) {
            const total = selectedRecipe[key];
            form.elements.namedItem(key).value = total == null ? '' : round2(total * grams / selectedRecipe.grams);
        }
    }

    function renderSummary() {
        list.replaceChildren();
        for (const [index, item] of ingredients.entries()) {
            const row = document.createElement('div');
            row.className = 'food-ingredient-row';
            const details = document.createElement('div');
            details.className = 'food-ingredient-details';
            const name = document.createElement('strong');
            name.textContent = item.name;
            const info = document.createElement('small');
            info.textContent = `${fmt(item.kcal)} kcal · P ${fmt(item.protein)} g · C ${fmt(item.carbs)} g · F ${fmt(item.fat)} g`;
            details.append(name, info);
            const amount = document.createElement('input');
            amount.type = 'text';
            amount.inputMode = 'decimal';
            amount.min = '0.001';
            amount.step = 'any';
            amount.value = item.grams;
            amount.className = 'food-row-amount';
            amount.setAttribute('aria-label', `Amount ${item.name} in grams`);
            amount.addEventListener('change', () => {
                const grams = parseDecimal(amount.value);
                if (!(grams > 0)) {
                    amount.value = item.grams;
                    return;
                }
                const oldGrams = item.grams;
                for (const key of fields) {
                    if (item[key] != null) item[key] = Number(item[key]) * grams / oldGrams;
                }
                item.grams = grams;
                if (item.per100g) item.per100g = {...item.per100g};
                renderSummary();
            });
            const unit = document.createElement('span');
            unit.textContent = 'g';
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'btn danger';
            remove.textContent = 'Remove';
            remove.addEventListener('click', () => {
                ingredients.splice(index, 1);
                renderSummary();
            });
            if (item.entryMode === 'recipe' || item.entryMode === 'recipe-custom') {
                const edit = document.createElement('button');
                edit.type = 'button';
                edit.className = 'btn secondary';
                edit.textContent = 'Adjust recipe';
                edit.addEventListener('click', () => {
                    ingredients.splice(index, 1);
                    renderSummary();
                    choose(item);
                });
                row.append(details, amount, unit, edit, remove);
            } else {
                row.append(details, amount, unit, remove);
            }
            list.append(row);
        }
        setText('#food-ingredient-count', `${ingredients.length} added`);
    }


    function choose(item) {
        form.hidden = false;
        form.elements.namedItem('name').value = item.name ?? '';
        form.elements.namedItem('grams').value = item.grams ?? 100;
        selectedRecipe = (item.entryMode === 'recipe' || item.entryMode === 'recipe-custom')
            ? {...item, entryMode: 'recipe'} : null;
        customizedRecipeIngredients = null;
        form.elements.namedItem('grams').readOnly = false;
        setRecipeCustomizationVisible(false);
        currentMode = (item.entryMode === 'recipe' || item.entryMode === 'recipe-custom') ? 'recipe' : item.entryMode === 'manual' || (['Manual entry', 'Manuel registrering'].includes(item.source) && !item.per100g) || (item.type !== 'drink' && !item.per100g && !item.source) ? 'manual' : 'search';
        selectedFoodPer100 = currentMode === 'search'
            ? Object.fromEntries(fields.map(key => [key,
                item.per100g?.[key] ?? (item[key] == null ? null : Number(item[key]) * 100 / Number(item.grams || 100))
            ]))
            : null;
        const per100Heading = document.querySelector('#food-value-heading');
        if (per100Heading) {
            per100Heading.textContent = currentMode === 'recipe' ? 'Nutrition for selected amount' : currentMode === 'manual' ? 'Total nutrition' : 'Nutrition for selected amount';
        }
        for (const key of fields) {
            const value = currentMode === 'search'
                ? selectedFoodPer100[key] == null ? null : selectedFoodPer100[key] * Number(item.grams || 100) / 100
                : item[key];
            form.elements.namedItem(key).value = value == null || !Number.isFinite(Number(value)) ? '' : round2(value);
        }
        currentSource = ({'Manuel registrering': 'Manual entry', 'Mine opskrifter': 'My Recipes', 'Mine fødevarer': 'My Foods'})[item.source] || item.source || 'Manual entry';
        if (['My Recipes', 'Mine opskrifter'].includes(item.source)) {
            document.querySelector('#food-meal-name').value = item.name ?? '';
            const recipeIngredients = Array.isArray(item.recipeIngredients) ? item.recipeIngredients : [];
            document.querySelector('#food-meal-notes').value = recipeIngredients.map(ingredient =>
                `${ingredient.name} - ${fmt(ingredient.amount)} ${ingredient.unit || 'g'}`).join(' | ');
        }
        setText('#food-source', `Source: ${currentSource}.`);
        warning.textContent = '';
        results.replaceChildren();
        document.querySelector('#food-editor-heading').textContent = currentMode === 'manual' ? 'Create food manually' : 'Selected food';
        if (item.entryMode === 'recipe-custom' && Array.isArray(item.ingredientItems)) {
            customizedRecipeIngredients = item.ingredientItems.map(ingredient => ({
                name: ingredient.name,
                unit: ingredient.unit || 'g',
                grams: Number(ingredient.grams),
                per100g: {...ingredient.per100g}
            }));
            form.elements.namedItem('grams').readOnly = true;
            setRecipeCustomizationVisible(true);
            renderRecipeCustomization();
        } else {
            recalculate();
        }
        form.scrollIntoView({behavior: 'smooth', block: 'nearest'});
    }

    function open(item = null) {
        clearTimeout(debounce);
        abort?.abort();
        searchNumber++;
        ingredients = [];
        editingId = null;
        currentSource = '';
        currentMode = 'search';
        selectedRecipe = null;
        selectedFoodPer100 = null;
        customizedRecipeIngredients = null;
        form.elements.namedItem('grams').readOnly = false;
        setRecipeCustomizationVisible(false);
        search.value = '';
        form.reset();
        form.hidden = true;
        results.replaceChildren();
        warning.textContent = '';
        status.textContent = '';
        document.querySelector('#food-meal-name').value = '';
        document.querySelector('#food-meal-notes').value = '';
        renderSummary();
        dialog.showModal();
        if (item) {
            editingId = item.id || null;
            document.querySelector('#food-meal-name').value = item.name ?? '';
            document.querySelector('#food-meal-notes').value = item.notes ?? '';
            if (Array.isArray(item.ingredientItems) && item.ingredientItems.length) {
                ingredients = item.ingredientItems.map(entry => ({...entry}));
                renderSummary();
            } else {
                choose(item);
            }
        } else {
            search.focus();
        }
    }

    window.momentumOpenFoodModal = open;
    document.querySelector('#open-food').addEventListener('click', () => open());
    document.querySelector('#close-food').addEventListener('click', () => dialog.close());
    document.querySelector('#cancel-food').addEventListener('click', () => dialog.close());
    document.querySelector('#food-manual').addEventListener('click', () => choose({
        name: search.value.trim() || 'Ny madvare',
        grams: 100,
        source: 'Manual entry',
        kcal: null, protein: null, carbs: null, fat: null, fiber: null
    }));
    form.elements.namedItem('grams').addEventListener('input', recalculate);
    for (const key of fields) {
        form.elements.namedItem(key).addEventListener('input', () => {
            if (currentMode !== 'search' || !selectedFoodPer100) {
                return;
            }
            const grams = number('grams');
            const value = parseDecimal(form.elements.namedItem(key).value);
            if (grams > 0 && Number.isFinite(value) && value >= 0) {
                selectedFoodPer100[key] = value * 100 / grams;
            }
        });
    }
    document.querySelector('#food-customize-recipe').addEventListener('click', () => {
        if (!selectedRecipe) {
            return;
        }
        if (customizedRecipeIngredients) {
            resetRecipeCustomization();
            return;
        }
        const originals = selectedRecipe.recipeIngredients || [];
        if (!originals.length) {
            warning.textContent = 'This recipe has no ingredients to adjust.';
            return;
        }
        customizedRecipeIngredients = originals.map(ingredient => ({
            name: ingredient.name,
            unit: ingredient.unit,
            grams: Number(ingredient.amount),
            per100g: Object.fromEntries(fields.map(key => [key, ingredient[key] == null ? null : Number(ingredient[key])]))
        }));
        form.elements.namedItem('grams').readOnly = true;
        setRecipeCustomizationVisible(true);
        renderRecipeCustomization();
    });
    document.querySelector('#food-cancel-selection').addEventListener('click', () => {
        form.hidden = true;
        form.reset();
        selectedRecipe = null;
        selectedFoodPer100 = null;
        customizedRecipeIngredients = null;
        form.elements.namedItem('grams').readOnly = false;
        setRecipeCustomizationVisible(false);
        warning.textContent = '';
    });
    addButton.addEventListener('click', () => {
        warning.textContent = '';
        if (form.hidden || !form.reportValidity()) {
            warning.textContent = 'Choose a food and enter all nutritional values.';
            return;
        }
        if (customizedRecipeIngredients?.some(ingredient => !Number.isFinite(ingredient.grams) || ingredient.grams <= 0)) {
            warning.textContent = 'Check ingredient amounts.';
            return;
        }
        const item = activeIngredient();
        if (!item) {
            warning.textContent = 'Check the name, grams and all four nutritional values.';
            return;
        }
        ingredients.push(item);
        form.hidden = true;
        form.reset();
        currentSource = '';
        currentMode = 'search';
        selectedRecipe = null;
        selectedFoodPer100 = null;
        customizedRecipeIngredients = null;
        form.elements.namedItem('grams').readOnly = false;
        setRecipeCustomizationVisible(false);
        search.value = '';
        results.replaceChildren();
        status.textContent = `${item.name} added. Search for the next ingredient.`;
        renderSummary();
        search.focus();
    });
    document.querySelectorAll('[data-food-source]').forEach(button => {
        button.addEventListener('click', () => {
            searchSource = button.dataset.foodSource;
            search.value = '';
            results.replaceChildren();
            form.hidden = true;
            status.textContent = searchSource === 'frida' ? 'Search DTU Frida.' : searchSource === 'recipes' ? 'Search your recipes.' : 'Search your saved foods.';
            document.querySelectorAll('[data-food-source]').forEach(tab => tab.classList.toggle('active', tab === button));
            search.focus();
        });
    });
    searchSource = 'library';
    document.querySelector('[data-food-source="library"]')?.classList.add('active');
    search.addEventListener('input', () => {
        clearTimeout(debounce);
        abort?.abort();
        const current = ++searchNumber;
        const q = search.value.trim();
        // Keep the current ingredient visible while the user searches again.
        results.replaceChildren();
        if (q.length < 2) {
            status.textContent = 'Enter at least 2 characters.';
            return;
        }
        status.textContent = 'Searching…';
        debounce = setTimeout(async () => {
            if (current !== searchNumber) {
                return;
            }
            abort = new AbortController();
            try {
                const url = searchSource === 'frida' ? '/api/foods/search?q=' + encodeURIComponent(q)
                    : searchSource === 'recipes' ? '/api/library/recipes'
                        : '/api/library/foods?q=' + encodeURIComponent(q);
                const response = await fetch(url, {signal: abort.signal});
                const payload = await response.json();
                if (current !== searchNumber) {
                    return;
                }
                if (!response.ok) {
                    throw Error(payload.message || 'Search is unavailable.');
                }
                const items = (Array.isArray(payload) ? payload : []).filter(item =>
                    searchSource !== 'recipes' || (item.finishedWeight > 0 && item.name.toLowerCase().includes(q.toLowerCase())));
                for (const item of items) {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'drink-result';
                    button.textContent = item.name + ' · ' + (searchSource === 'recipes' ? 'Recipe' : searchSource === 'library' ? 'My Foods' : item.source);
                    button.addEventListener('click', () => {
                        if (searchSource === 'recipes') {
                            choose({
                                name: item.name,
                                grams: item.finishedWeight,
                                source: 'My Recipes',
                                entryMode: 'recipe',
                                recipeIngredients: item.ingredients,
                                kcal: item.kcal,
                                protein: item.protein,
                                carbs: item.carbs,
                                fat: item.fat,
                                fiber: item.fiber
                            });
                        } else if (searchSource === 'library') {
                            // The library API returns nutrition per 100 g/ml. Preserve it
                            // as the canonical source and display totals for the chosen amount.
                            const nutrition = Object.fromEntries(fields.map(key => [
                                key, item[key] ?? item[key[0].toUpperCase() + key.slice(1)] ?? null
                            ]));
                            if (fields.slice(0, 4).some(key => nutrition[key] == null)) {
                                status.textContent = 'The food data returned by the API is missing nutritional values.';
                                return;
                            }
                            choose({
                                name: item.name, grams: 100, source: 'My Foods', entryMode: 'search',
                                per100g: nutrition
                            });
                        } else {
                            choose(item);
                        }
                    });
                    results.append(button);
                }
                status.textContent = items.length ? `${items.length} foods found.` : 'No foods found. You can create one manually.';
            } catch (error) {
                if (error.name === 'AbortError') {
                    return;
                }
                status.textContent = error.message || 'Search is unavailable. Use manual entry.';
            }
        }, 450);
    });
    saveButton.addEventListener('click', () => {
        warning.textContent = '';
        if (!form.hidden) {
            warning.textContent = 'Add the selected food to the list before saving.';
            return;
        }
        const all = [...ingredients];
        if (!all.length) {
            warning.textContent = 'Add at least one ingredient before saving the meal.';
            return;
        }
        const sum = key => key === 'fiber' && !all.some(item => item.fiber != null) ? null : all.reduce((total, item) => total + (Number(item[key]) || 0), 0);
        const nameInput = document.querySelector('#food-meal-name');
        const name = nameInput.value.trim();
        if (!name) {
            warning.textContent = 'Enter a meal name.';
            nameInput.focus();
            return;
        }
        if (name.length > 200) {
            warning.textContent = 'Meal name must be 200 characters or fewer.';
            return;
        }
        const grams = sum('grams');
        const item = {
            id: crypto.randomUUID(), date: picker.value, name, type: 'food', grams,
            notes: document.querySelector('#food-meal-notes').value.trim(),
            ingredients: all.map(entry => `${entry.name} (${fmt(entry.grams)} g)`).join(', '),
            ingredientItems: all,
            ...(grams > 0 ? {per100g: Object.fromEntries(fields.map(key => [key, sum(key) === null ? null : sum(key) * 100 / grams]))} : {}),
            ...Object.fromEntries(fields.map(key => [key, sum(key) === null ? null : Number(sum(key).toFixed(3))]))
        };
        write(FOOD_KEY, [...food(), item]);
        dialog.close();
        refresh();
    });
    renderSummary();
}

export {setupFoodSearch};
