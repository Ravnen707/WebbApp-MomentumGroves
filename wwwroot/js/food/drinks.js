import {FOOD_KEY, read, write} from "../core/storage.js";
import {parseDecimal, fmt} from "../core/utils.js";

function setupDrinks(picker, refresh) {
    const dialog = document.querySelector('#drink-dialog');
    if (!dialog) return;
    const search = document.querySelector('#drink-search'), results = document.querySelector('#drink-results'),
        status = document.querySelector('#drink-status'), form = document.querySelector('#drink-form'),
        warning = document.querySelector('#drink-warning');
    const fields = ['kcal', 'protein', 'carbs', 'fat', 'fiber'];
    let timer = null, request = null, searchId = 0;
    const defaults = [{
        name: 'Water',
        source: 'Default value',
        kcal: 0,
        protein: 0,
        carbs: 0,
        fat: 0, fiber: 0
    }, {
        name: 'Black coffee (without milk or sugar)',
        source: 'Default value (approximate)',
        kcal: 0,
        protein: 0,
        carbs: 0,
        fat: 0, fiber: 0
    }, {name: 'Unsweetened tea', source: 'Default value (approximate)', kcal: 0, protein: 0, carbs: 0, fat: 0, fiber: 0}];
    const saved = () => read('momentum.drinks.recent.v1', []);
    const display = (products) => {
        results.replaceChildren();
        if (!products.length) {
            results.textContent = 'No products found. You can still create a drink manually.';
            return;
        }
        for (const p of products) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'drink-result';
            b.textContent = p.name + ' · ' + p.source;
            b.addEventListener('click', () => choose(p));
            results.append(b);
        }
    };

    function choose(p) {
        form.hidden = false;
        form.elements.name.value = p.name;
        form.elements.ml.value = 500;
        for (const k of fields) form.elements[k].value = p[k] === null || p[k] === undefined ? '' : p[k];
        document.querySelector('#drink-source').textContent = 'Source: ' + p.source + '. Always check the values against the packaging.';
        warning.textContent = '';
        results.replaceChildren();
        status.textContent = 'Product selected. Check the values and press Save.';
        recalc();
    }

    function recalc() {
        const ml = parseDecimal(form.elements.ml.value);
        document.querySelector('#drink-amount-heading').textContent = `For ${fmt(ml)} ml`;
        for (const k of fields) {
            const v = form.elements[k].value;
            document.querySelector('#drink-total-' + k).textContent = v === '' || !Number.isFinite(parseDecimal(v)) || !Number.isFinite(ml) ? 'Not specified' : fmt(parseDecimal(v) * ml / 100) + (k === 'kcal' ? ' kcal' : ' g');
        }
    }

    form.addEventListener('input', recalc);

    function close() {
        if (request) request.abort();
        clearTimeout(timer);
        dialog.close();
    }

    document.querySelector('#open-drink').addEventListener('click', () => {
        form.reset();
        form.hidden = true;
        search.value = '';
        status.textContent = 'Search for a drink or choose a default drink.';
        display([...defaults, ...saved().slice(0, 6)]);
        dialog.showModal();
        search.focus();
    });
    document.querySelector('#close-drink').addEventListener('click', close);
    document.querySelector('#cancel-drink').addEventListener('click', close);
    search.addEventListener('input', () => {
        clearTimeout(timer);
        if (request) request.abort();
        const id = ++searchId, q = search.value.trim();
        form.hidden = true;
        status.textContent = 'Preparing search…';
        if (!q) {
            status.textContent = 'Choose a default drink or search for a product.';
            display([...defaults, ...saved().slice(0, 6)]);
            return;
        }
        const local = [...defaults, ...saved()].filter(x => x.name.toLocaleLowerCase('en-GB').includes(q.toLocaleLowerCase('en-GB')));
        display(local);
        timer = setTimeout(async () => {
            if (id !== searchId) return;
            status.textContent = 'Searching for drinks…';
            request = new AbortController();
            try {
                const response = await fetch('/api/drinks/search?q=' + encodeURIComponent(q), {signal: request.signal});
                if (!response.ok) throw Error('Search is temporarily unavailable');
                const data = await response.json();
                if (id !== searchId) return;
                const known = new Set(local.map(x => x.name.toLowerCase()));
                display([...local, ...data.filter(x => !known.has(x.name.toLowerCase()))]);
                status.textContent = `Search complete. ${data.length} external results.`;
            } catch (e) {
                if (e.name === 'AbortError') return;
                status.textContent = 'External search is currently unavailable. You can create a drink manually.';
            }
        }, 1200);
    });
    // Allow manual creation even if the remote catalogue has no result.
    const manual = document.createElement('button');
    manual.type = 'button';
    manual.className = 'btn secondary';
    manual.textContent = 'Create drink manually';
    manual.addEventListener('click', () => choose({
        name: search.value.trim() || 'New drink',
        source: 'Manual entry',
        kcal: null,
        protein: null,
        carbs: null,
        fat: null, fiber: null
    }));
    results.after(manual);
    form.addEventListener('submit', e => {
        e.preventDefault();
        warning.textContent = '';
        if (!form.reportValidity()) return;
        const ml = parseDecimal(form.elements.ml.value), name = form.elements.name.value.trim();
        if (!name || !Number.isFinite(ml) || ml <= 0) return;
        const values = {};
        for (const k of fields) {
            const raw = form.elements[k].value;
            if (k === 'fiber' && raw === '') {
                values[k] = null;
                continue;
            }
            if (raw === '' || !Number.isFinite(parseDecimal(raw)) || parseDecimal(raw) < 0) {
                warning.textContent = 'All four nutritional values are required. Missing values cannot be treated as 0.';
                return;
            }
            values[k] = parseDecimal(raw);
        }
        const item = {
            id: crypto.randomUUID(),
            date: picker.value,
            name,
            type: 'drink',
            grams: ml,
            ml,
            notes: 'Drink', ...Object.fromEntries(fields.map(k => [k, values[k] === null ? null : Number((values[k] * ml / 100).toFixed(3))])),
            per100ml: values
        };
        const entries = read(FOOD_KEY, []);
        write(FOOD_KEY, [...(Array.isArray(entries) ? entries : []), item]);
        const old = saved().filter(x => x.name.toLowerCase() !== name.toLowerCase());
        write('momentum.drinks.recent.v1', [{name, source: 'Tidligere anvendt', ...values}, ...old].slice(0, 30));
        close();
        refresh();
    });
}


export {setupDrinks};
