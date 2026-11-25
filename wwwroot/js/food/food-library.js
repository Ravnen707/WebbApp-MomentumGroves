import {nutritionSummary} from "./ingredient-row.js";

(() => {
    const $ = (selector, root = document) => root.querySelector(selector);
    const el = (tag, text, className) => {
        const node = document.createElement(tag);
        if (text !== undefined) node.textContent = text;
        if (className) node.className = className;
        return node;
    };
    const number = value => {
        const text = String(value).trim().replace(',', '.');
        return text && /^[-+]?(?:\d+(?:\.\d*)?|\.\d+)$/.test(text) ? Number(text) : NaN;
    };
    const fmt = value => Number(value).toLocaleString('en-GB', {maximumFractionDigits: 2});
    const request = async (url, options) => {
        const response = await fetch(url, options);
        if (!response.ok) {
            let message = 'The action could not be completed.';
            try {
                message = (await response.json()).message || message;
            } catch {
            }
            throw new Error(message);
        }
        return response.status === 204 ? null : response.json();
    };
    const send = (url, method, value) => request(url, {
        method,
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify(value)
    });
    const foodPage = $('#library-foods');
    const recipePage = $('#library-recipes');

    if (foodPage) {
        const form = $('#library-food-form');
        const search = $('#library-food-search');
        const savedSearch = $('#library-saved-search');
        const moreButton = $('#library-food-more');
        const counter = $('#library-food-count');
        const results = $('#library-frida-results');
        const list = $('#library-food-list');
        const message = $('#library-food-message');
        let editId = null;
        let editVersion = null;
        const foodUrl = (id, version) => `/api/library/foods/${id}` +
            (version ? `?version=${encodeURIComponent(version)}` : '');
        let saved = [];
        let currentPage = 0;
        let total = 0;
        let hasMore = false;
        let savedSequence = 0;
        let searchSequence = 0;
        const fill = food => {
            editId = food.id || null;
            editVersion = food.version;
            for (const key of ['name', 'unit', 'kcal', 'protein', 'carbs', 'fat', 'fiber']) {
                form.elements.namedItem(key).value = food[key] ?? '';
            }
            $('#library-food-heading').textContent = editId ? 'Edit food' : 'Create food manually';
            form.scrollIntoView({behavior: 'smooth', block: 'start'});
        };
        const reset = () => {
            editId = null;
            editVersion = null;
            form.reset();
            $('#library-food-heading').textContent = 'Create food manually';
        };
        const reload = async (append = false) => {
            const sequence = ++savedSequence;
            const nextPage = append ? currentPage + 1 : 1;
            const query = savedSearch.value.trim();
            moreButton.disabled = true;
            try {
                const page =
                    await request(`/api/library/foods/paged?page=${nextPage}&pageSize=10&q=${encodeURIComponent(
                    query)}`);
                if (sequence !== savedSequence) return;
                saved = append ? [...saved, ...page.items] : page.items;
                currentPage = nextPage;
                total = page.total;
                hasMore = page.hasMore;
                render();
            } finally {
                if (sequence === savedSequence) moreButton.disabled = false;
            }
        };
        const render = () => {
            list.replaceChildren();
            for (const food of saved) {
                const row = el('div', undefined, 'library-item');
                const details = el('div');
                details.append(el('strong', food.name), el('small',
                    `${fmt(food.kcal)} kcal · P ${fmt(food.protein)} · C ${fmt(food.carbs)} · F ${fmt(
                    food.fat)} · Fibre ${food.fiber == null ? '–' : fmt(food.fiber)} / 100 ${food.unit}`));
                const edit = el('button', 'Edit', 'btn secondary');
                edit.type = 'button';
                edit.onclick = () => fill(food);
                const remove = el('button', 'Delete', 'btn danger');
                remove.type = 'button';
                remove.onclick = async () => {
                    if (!confirm(`Delete ${food.name}?`)) return;
                    try {
                        await request(foodUrl(food.id, food.version), {method: 'DELETE'});
                        await reload();
                    } catch (error) {
                        message.textContent = error.message;
                    }
                };
                row.append(details, edit, remove);
                list.append(row);
            }
            if (!list.childElementCount) list.append(el('p', 'No saved foods found.', 'muted'));
            counter.textContent = `Showing ${saved.length} of ${total} saved foods`;
            moreButton.hidden = !hasMore;
        };
        let timeout;
        search.addEventListener('input', () => {
            results.replaceChildren();
            clearTimeout(timeout);
            const query = search.value.trim();
            const seq = ++searchSequence;
            if (query.length < 2) return;
            timeout = setTimeout(async () => {
                try {
                    const foods = await request('/api/foods/search?q=' + encodeURIComponent(query));
                    if (seq !== searchSequence) return;
                    results.append(el('h3', 'Results from DTU Frida'));
                    for (const food of foods) {
                        const button = el('button', `${food.name} · ${fmt(food.kcal ?? 0)} kcal / 100 g`,
                            'library-result-button');
                        button.type = 'button';
                        button.onclick = () => fill({
                            ...food,
                            unit: 'g',
                            kcal: food.kcal ?? 0,
                            protein: food.protein ?? 0,
                            carbs: food.carbs ?? 0,
                            fat: food.fat ?? 0
                        });
                        results.append(button);
                    }
                } catch (error) {
                    message.textContent = error.message;
                }
            }, 350);
        });
        let savedSearchTimeout;
        savedSearch.addEventListener('input', () => {
            clearTimeout(savedSearchTimeout);
            savedSearchTimeout = setTimeout(() => reload().catch(error => message.textContent = error.message), 250);
        });
        moreButton.addEventListener('click', () => reload(true).catch(error => message.textContent = error.message));
        $('#library-food-reset').onclick = reset;
        form.addEventListener('submit', async event => {
            event.preventDefault();
            const value = Object.fromEntries(['name', 'unit', 'kcal', 'protein', 'carbs', 'fat',
                'fiber'].map(key => [key, form.elements.namedItem(key).value]));
            for (const key of ['kcal', 'protein', 'carbs', 'fat']) value[key] = number(value[key]);
            value.fiber = value.fiber === '' ? null : number(value.fiber);
            try {
                await send(editId ? foodUrl(editId, editVersion) : '/api/library/foods', editId ? 'PUT' : 'POST',
                    value);
                reset();
                await reload();
                message.textContent = 'Food saved.';
            } catch (error) {
                message.textContent = error.message;
            }
        });
        reload().catch(error => message.textContent = error.message);
    }

    if (recipePage) {
        const recipeDate = new Intl.DateTimeFormat('da-DK', {
            timeZone: 'Europe/Copenhagen',
            dateStyle: 'short',
            timeStyle: 'short'
        });
        const form = $('#library-recipe-form');
        const ingredientsRoot = $('#library-recipe-ingredients');
        const search = $('#library-recipe-search');
        const results = $('#library-recipe-food-results');
        const list = $('#library-recipe-list');
        const message = $('#library-recipe-message');
        let foods = [], recipes = [], ingredients = [], editId = null;
        const reset = () => {
            editId = null;
            ingredients = [];
            form.reset();
            search.value = '';
            $('#library-recipe-heading').textContent = 'Create recipe';
            renderIngredients();
            renderSearch();
        };
        const renderIngredients = () => {
            ingredientsRoot.replaceChildren();
            for (const item of ingredients) {
                const food = foods.find(x => x.id === item.foodId);
                if (!food) continue;
                const row = el('div', undefined, 'library-item');
                const details = el('div');
                const nutrition = el('small');
                details.append(el('strong', food.name), nutrition);
                const showNutrition = () => {
                    nutrition.textContent = item.amount > 0
                        ? nutritionSummary(Object.fromEntries(['kcal', 'protein', 'carbs', 'fat', 'fiber'].map(key =>
                            [key, food[key] == null ? null : food[key] * item.amount / 100])), fmt)
                        : '';
                };
                showNutrition();
                const input = el('input');
                input.type = 'text';
                input.inputMode = 'decimal';
                input.min = '0.001';
                input.step = 'any';
                input.value = item.amount;
                input.setAttribute('aria-label', `Amount ${food.name} (${food.unit})`);
                input.oninput = () => {
                    item.amount = number(input.value);
                    showNutrition();
                    preview();
                };
                const unit = el('span', food.unit, 'muted');
                const remove = el('button', '✕', 'btn secondary');
                remove.type = 'button';
                remove.onclick = () => {
                    ingredients = ingredients.filter(x => x !== item);
                    renderIngredients();
                    renderSearch();
                };
                row.classList.add('library-ingredient-row');
                row.append(details, input, unit, remove);
                ingredientsRoot.append(row);
            }
            if (!ingredients.length) ingredientsRoot.append(el('p', 'No ingredients selected.', 'muted'));
            preview();
        };
        const preview = () => {
            const weight = number(form.elements.namedItem('finishedWeight').value);
            const totals = key => ingredients.reduce((sum, item) => {
                const food = foods.find(x => x.id === item.foodId);
                const value = Number(food?.[key]);
                return sum + (Number.isFinite(value) && item.amount > 0 ? value * item.amount / 100 : 0);
            }, 0);
            const nutrients = {kcal: ' kcal', protein: ' g', carbs: ' g', fat: ' g'};
            for (const [key, suffix] of Object.entries(nutrients)) {
                const field = $(`#recipe-live-${key}`);
                if (field) field.textContent = `${fmt(totals(key))}${suffix}`;
            }
            const fiber = $('#recipe-live-fiber');
            const hasFiber = ingredients.some(item => {
                const food = foods.find(x => x.id === item.foodId);
                return food && food.fiber !== null && food.fiber !== undefined && Number.isFinite(Number(food.fiber));
            });
            if (fiber) fiber.textContent = hasFiber ? `${fmt(totals('fiber'))} g${ingredients.some(item => {
                const food = foods.find(x => x.id === item.foodId);
                return !food || food.fiber === null || food.fiber === undefined;
            }) ? ' (partial)' : ''}` : 'Not specified';
            const totalWeight = $('#recipe-live-weight');
            if (totalWeight) totalWeight.textContent = `${fmt(ingredients.reduce((sum,
                item) => sum + (item.amount > 0 ? item.amount : 0), 0))} g`;
            const node = $('#library-recipe-preview');
            if (!(weight > 0) || !ingredients.length) {
                node.textContent = 'Waiting for ingredients and meal weight';
                node.classList.remove('ready');
                return;
            }
            const total = totals;
            node.textContent =
                `Ready to use · ${fmt(total('kcal'))} kcal total · P ${fmt(total('protein'))} · C ${fmt(total(
                'carbs'))} · F ${fmt(total('fat'))}`;
            node.classList.add('ready');
        };
        const renderSearch = () => {
            results.replaceChildren();
            const query = search.value.trim().toLowerCase();
            if (!query) return;
            for (const food of foods.filter(x => x.name.toLowerCase().includes(query)
                && !ingredients.some(i => i.foodId === x.id)).slice(0, 15)) {
                const button = el('button', `${food.name} (per 100 ${food.unit})`, 'library-result-button');
                button.type = 'button';
                button.onclick = () => {
                    ingredients.push({foodId: food.id, amount: 100});
                    search.value = '';
                    renderIngredients();
                    renderSearch();
                };
                results.append(button);
            }
        };
        const reload = async () => {
            [foods, recipes] = await Promise.all([request('/api/library/foods'), request('/api/library/recipes')]);
            renderList();
            renderSearch();
        };
        const edit = recipe => {
            editId = recipe.id;
            form.elements.namedItem('name').value = recipe.name;
            form.elements.namedItem('finishedWeight').value = recipe.finishedWeight ?? '';
            ingredients = recipe.ingredients.map(x => ({foodId: x.foodId, amount: x.amount}));
            $('#library-recipe-heading').textContent = 'Edit recipe';
            renderIngredients();
            form.scrollIntoView({behavior: 'smooth', block: 'start'});
        };
        const renderList = () => {
            list.replaceChildren();
            for (const recipe of recipes) {
                const row = el('div', undefined, 'library-item');
                const info = el('div');
                info.append(el('strong', recipe.name), el('small',
                    recipe.finishedWeight
                    ? `${fmt(recipe.finishedWeight)} g meal · ${fmt(recipe.kcal)} kcal total · P ${fmt(
                    recipe.protein)} · C ${fmt(recipe.carbs)} · F ${fmt(recipe.fat)}` : 'Waiting for finished weight'));
                const created = el('time', recipeDate.format(new Date(recipe.createdUtc)));
                created.dateTime = recipe.createdUtc;
                const creationDetails = el('small', 'Created ');
                creationDetails.append(created, ' Copenhagen time');
                info.append(creationDetails);
                const editButton = el('button', 'Edit', 'btn secondary');
                editButton.type = 'button';
                editButton.onclick = () => edit(recipe);
                const cloneButton = el('button', 'Clone', 'btn secondary');
                cloneButton.type = 'button';
                cloneButton.onclick = () => {
                    edit({...recipe, id: null, name: `${recipe.name} – kopi`, finishedWeight: null});
                    editId = null;
                    $('#library-recipe-heading').textContent = 'New recipe (clone)';
                };
                const deleteButton = el('button', 'Delete', 'btn danger');
                deleteButton.type = 'button';
                deleteButton.onclick = async () => {
                    if (!confirm(`Delete ${recipe.name}?`)) return;
                    try {
                        await request(`/api/library/recipes/${recipe.id}`, {method: 'DELETE'});
                        await reload();
                    } catch (error) {
                        message.textContent = error.message;
                    }
                };
                row.append(info, editButton, cloneButton, deleteButton);
                list.append(row);
            }
            if (!recipes.length) list.append(el('p', 'No recipes saved yet.', 'muted'));
        };
        search.addEventListener('input', renderSearch);
        form.elements.namedItem('finishedWeight').addEventListener('input', preview);
        $('#library-recipe-reset').onclick = reset;
        $('#library-recipe-clone').onclick = () => {
            if (!form.elements.namedItem('name').value.trim() || !ingredients.length) {
                message.textContent = 'Enter the recipe name and ingredients first.';
                return;
            }
            editId = null;
            form.elements.namedItem('name').value = `${form.elements.namedItem('name').value.trim()} – kopi`;
            form.elements.namedItem('finishedWeight').value = '';
            $('#library-recipe-heading').textContent = 'New recipe (clone)';
            form.requestSubmit();
        };
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (!ingredients.length || ingredients.some(x => !(x.amount > 0))) {
                message.textContent = 'Choose ingredients and valid amounts.';
                return;
            }
            const weight = form.elements.namedItem('finishedWeight').value;
            const value = {
                name: form.elements.namedItem('name').value,
                finishedWeight: weight ? number(weight) : null,
                ingredients
            };
            try {
                await send(editId ? `/api/library/recipes/${editId}` : '/api/library/recipes', editId ? 'PUT' : 'POST',
                    value);
                reset();
                await reload();
                message.textContent = 'Recipe saved.';
            } catch (error) {
                message.textContent = error.message;
            }
        });
        reload().catch(error => message.textContent = error.message);
        renderIngredients();
    }
})();
