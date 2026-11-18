import {FOOD_KEY, read, write} from "../core/storage.js";
import {fmt, esc, setText, setHTML, localDate} from "../core/utils.js";
import {isCheatDay, setCheatDay} from "../tracking/cheat-days.js";
import {isSickDay, fiberActive, fiberKnown, fiberComplete, setSickDay} from "../tracking/sick-days.js";
import {updateZoneDisplay, getGoals, setProgress} from "../tracking/nutrition-targets.js?v=20261118-empty-calculator";
import {drawChart, renderCalorieBalance} from "../home/dashboard.js?v=20261119-training-groups";
import {setupDrinks} from "./drinks.js";
import {setupMealCopy} from "./meal-copy.js";
import {setupFoodSearch} from "./food-search.js?v=20261101-food-nutrition";

const food = () => {
    const value = read(FOOD_KEY, []);
    return Array.isArray(value) ? value : [];
};
const totals = date => food().filter(item => item.date === date).reduce((result, item) => {
    for (const k of ['kcal', 'protein', 'carbs', 'fat', 'fiber', 'grams']) result[k] += Number(item[k]) || 0;
    return result;
}, {kcal: 0, protein: 0, carbs: 0, fat: 0, fiber: 0, grams: 0});

function renderFood() {
    const picker = document.querySelector('#food-date');
    if (!picker) return;
    picker.value = localDate();

    function update() {
        const t = totals(picker.value), g = getGoals(), items = food().filter(x => x.date === picker.value);
        const sick = isSickDay(picker.value);
        const cheat = isCheatDay(picker.value);
        document.querySelector('#cheat-day')?.closest('.cheat-day-panel')?.classList.toggle('cheat-day-active', cheat);
        document.querySelector('#food-list')?.classList.toggle('cheat-day-entries', cheat);
        document.querySelector('.stats.stats-four')?.classList.toggle('cheat-day-stats', cheat && !sick);
        for (const key of ['kcal', 'protein', 'carbs', 'fat', 'fiber']) {
            setText(`#food-${key}`, key === 'fiber' && !fiberActive(picker.value) ? '–' : key === 'fiber' && !fiberComplete(picker.value) ? (fiberKnown(picker.value) ? `${fmt(t.fiber)} g (partial)` : 'Not specified') : (sick || cheat) ? `${fmt(t[key])}${key === 'kcal' ? ' kcal' : ' g'}` : `${fmt(t[key])} / ${fmt(g[key])}${key === 'kcal' ? '' : ' g'}`);
            setProgress(`#food-${key}-bar`, t[key], g[key], picker.value, key);
            updateZoneDisplay(key, t[key], g[key], picker.value, 'food');
            const stat = document.querySelector(`#food-${key}`);
            if (stat) stat.classList.toggle('cheat-over', !sick && isCheatDay(picker.value) && t[key] > g[key]);
        }
        const sickToggle = document.querySelector('#sick-day');
        const sickStatus = document.querySelector('#sick-day-status');
        if (sickToggle) sickToggle.checked = isSickDay(picker.value);
        if (sickStatus) sickStatus.textContent = isSickDay(picker.value)
            ? 'Sick day: intake is logged normally, but the day is excluded from the weekly nutrition balance.'
            : 'Mark as a sick day if illness makes your usual nutrition targets inappropriate.';
        const cheatToggle = document.querySelector('#cheat-day');
        const cheatStatus = document.querySelector('#cheat-day-status');
        if (cheatToggle) {
            cheatToggle.checked = cheat;
        }
        if (cheatStatus) {
            cheatStatus.textContent = cheat
                ? 'Cheat day: intake is logged and still counts towards the weekly nutrition balance.'
                : 'Mark as a cheat day to highlight it in orange. The day still counts towards the balance.';
        }
        setText('#meal-count', `${items.length} entries`);
        setHTML('#food-list', items.length ? items.map(x => `<div class="meal-row"><div><strong>${esc(x.name)}</strong><small>${fmt(x.grams)} ${x.type === 'drink' ? 'ml' : 'g'} · ${fmt(x.kcal)} kcal · P: ${fmt(x.protein)} g · C: ${fmt(x.carbs || 0)} g · F: ${fmt(x.fat)} g · Fibre: ${x.fiber == null ? 'not specified' : fmt(x.fiber) + ' g'}</small>${x.notes ? `<small>${esc(x.notes)}</small>` : ''}</div><div class="meal-actions"><button type="button" class="btn secondary" data-edit="${esc(x.id)}">Edit</button><button type="button" class="btn danger" data-remove="${esc(x.id)}">Delete</button></div></div>`).join('') : '<div class="empty">No entries for this date.</div>');
        document.querySelectorAll('[data-remove]').forEach(btn => btn.addEventListener('click', () => {
            if (!confirm('Delete this entry?')) return;
            write(FOOD_KEY, food().filter(x => String(x.id) !== btn.dataset.remove));
            update();
        }));
        document.querySelectorAll('[data-edit]').forEach(btn => btn.addEventListener('click', () => {
            const item = food().find(x => String(x.id) === btn.dataset.edit);
            if (item && window.momentumOpenMealEditor) window.momentumOpenMealEditor(item, update);
        }));
        drawChart();
        renderCalorieBalance();
    }

    const sickToggle = document.querySelector('#sick-day');
    if (sickToggle) sickToggle.addEventListener('change', () => {
        setSickDay(picker.value, sickToggle.checked);
        update();
    });
    const cheatToggle = document.querySelector('#cheat-day');
    if (cheatToggle) cheatToggle.addEventListener('change', () => {
        setCheatDay(picker.value, cheatToggle.checked);
        update();
    });
    window.momentumRefreshFood = update;
    picker.addEventListener('change', update);
    setupFoodSearch(picker, update);
    setupDrinks(picker, update);
    setupMealCopy(picker);
    update();
}

// The drink picker never changes the diary until the user explicitly presses Save.


export {food, totals, renderFood};
