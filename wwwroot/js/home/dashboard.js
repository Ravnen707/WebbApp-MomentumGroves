import {
    completedProgrammesThisWeek,
    readTrainingProgrammes,
    setProgrammeCompleted
} from "../core/training-storage.js?v=weekly-completions";
import {fmt, esc, setText, setHTML, monday, shift} from "../core/utils.js";
import {selectedDate} from "../core/selected-date.js";
import {isCheatDay} from "../tracking/cheat-days.js";
import {isSickDay, fiberActive, fiberKnown, fiberComplete} from "../tracking/sick-days.js";
import {
    nutritionColor,
    updateZoneDisplay,
    getGoals,
    setProgress
} from "../tracking/nutrition-targets.js?v=20261118-empty-calculator";
import {food, totals} from "../food/food-diary.js?v=selected-date";

function drawChart() {
    const root = document.querySelector('#week-chart');
    if (!root) {
        return;
    }
    const selector = document.querySelector('#week-chart-metric');
    const metric = ['kcal', 'protein', 'carbs', 'fat', 'fiber'].includes(selector?.value) ? selector.value : 'kcal';
    const definitions = {
        kcal: {name: 'calories', unit: 'kcal'},
        protein: {name: 'protein', unit: 'g'},
        carbs: {name: 'carbohydrates', unit: 'g'},
        fat: {name: 'fat', unit: 'g'},
        fiber: {name: 'fibre', unit: 'g'}
    };
    const definition = definitions[metric];
    const day = selectedDate();
    const dates = Array.from({length: 7}, (_, index) => shift(day, index - 6));
    const goal = Math.max(0, Number(getGoals()[metric]) || 0);
    const values = dates.map(date => metric === 'fiber' && (!fiberActive(date) || !fiberComplete(date))
        ? null : totals(date)[metric]);
    const max = Math.max(goal, ...values.filter(v => v !== null), 1) * 1.12;
    const signed = value => `${value > 0 ? '+' : value < 0 ? '−' : ''}${fmt(Math.abs(value))}`;
    root.innerHTML = dates.map((date, index) => {
        const value = values[index];
        const difference = (value ?? 0) - goal;
        const label = new Date(date + 'T12:00:00').toLocaleDateString('en-GB', {weekday: 'short'});
        const sick = isSickDay(date);
        const cheat = isCheatDay(date);
        const excluded = sick;
        const inactive = metric === 'fiber' && value === null;
        const title = sick
            ? `${date}: Sick day. Logged ${fmt(
            value)} ${definition.unit}; the day is not assessed against the target.` : cheat
            ? `${date}: Cheat day. Logged ${fmt(
            value)} ${definition.unit}; the day counts towards the 7-day balance.` : inactive
            ? `${date}: Fibre not logged or not yet enabled.` : `${date}: ${fmt(
            value)} ${definition.unit}. Target: ${fmt(goal)} ${definition.unit}. ${difference > 0
            ? 'Over' : difference < 0 ? 'Under' : 'At'} target by ${fmt(Math.abs(difference))} ${definition.unit}.`;
        return `<div class="bar-col${sick ? ' sick-chart-day' : cheat
            ? ' cheat-chart-day' : ''}" title="${title}"><div class="bar-number">${inactive
            ? '–' : fmt(
            value)}</div><div class="bar-wrap"><div class="goal-line" style="bottom:${goal / max * 100}%;${excluded
            ? 'display:none;' : ''}"></div><div class="bar" style="height:${value ? Math.max(1,
            value / max * 100) : 0}%;background:${nutritionColor(metric, value, goal,
            date)}"></div></div><div class="bar-label">${label}${sick ? ' 🩺' : cheat
            ? ' ★' : ''}<small class="bar-date">${date.slice(8, 10)}/${date.slice(5,
            7)}</small></div><div class="bar-difference" title="${sick ? 'Sick day – excluded from balance' : cheat
            ? 'Cheat day – included in balance' : 'Difference from daily target'}">${sick ? 'Sick' : cheat
            ? `★ ${signed(difference)} ${definition.unit}` : inactive
            ? '–' : `${signed(difference)} ${definition.unit}`}</div></div>`;
    }).join('');
    setText('#week-chart-goal', `Daily target for ${definition.name}: ${fmt(goal)} ${definition.unit}. ` +
        'Sick days are shown without target assessment. Cheat days are highlighted in orange and count ' +
        'towards the balance.');
}

function renderCalorieBalance() {
    const totalElement = document.querySelector('#balance-total');
    const select = document.querySelector('#balance-metric');
    if (!totalElement || !select) {
        return;
    }
    const metrics = {
        kcal: {name: 'Calorie balance', unit: 'kcal'},
        protein: {name: 'Protein balance', unit: 'g'},
        carbs: {name: 'Carbohydrate balance', unit: 'g'},
        fat: {name: 'Fat balance', unit: 'g'},
        fiber: {name: 'Fibre balance', unit: 'g'}
    };
    const key = metrics[select.value] ? select.value : 'kcal';
    const metric = metrics[key];
    const goal = Number(getGoals()[key]);
    const day = selectedDate();
    const dates = Array.from({length: 7}, (_, i) => shift(day, i - 6));
    const entries = food();
    const sickCount = dates.filter(isSickDay).length;
    const cheatCount = dates.filter(date => isCheatDay(date) && !isSickDay(date)).length;
    const recorded = dates.filter(date => !isSickDay(date) && (key !== 'fiber' || (fiberActive(date)
        && fiberComplete(date))) && entries.some(item => item.date === date));
    const daysCount = recorded.length;
    const expected = goal * daysCount;
    const actual = recorded.reduce((sum, date) => sum + totals(date)[key], 0);
    const balance = actual - expected;
    const statusElement = document.querySelector('#balance-status');
    const formatSigned = value => `${value > 0 ? '+' : value < 0 ? '−' : ''}${fmt(Math.abs(value))} ${metric.unit}`;
    setText('#balance-heading', `${metric.name} – last 7 days`);
    setText('#balance-days',
        `${daysCount} of 7 days${sickCount ? ` · ${sickCount} sick day${sickCount === 1 ? '' : 's'}` : ''}${cheatCount
        ? ` · ${cheatCount} cheat day${cheatCount === 1 ? '' : 's'}` : ''}`);
    setText('#balance-actual',
        daysCount
        ? `Logged: ${fmt(actual)} ${metric.unit} · Target for ${daysCount} days: ${fmt(
        expected)} ${metric.unit}` : 'No logged days yet');
    const sickNote = sickCount
        ? `${sickCount} sick day${sickCount === 1 ? '' : 's'} excluded from both actual intake and targets. ` : '';
    const cheatNote = cheatCount
        ? `${cheatCount} cheat day${cheatCount === 1 ? '' : 's'} included in both actual intake and targets. ` : '';
    setText('#balance-note', 'All logged days except sick days count towards the balance. ' +
        `${sickNote}${cheatNote}Fixed ranges for the total difference; no averaging.`);

    const zoneElements = document.querySelectorAll('#balance-zones .balance-zone');
    // Fixed ranges for the total difference over the last 7 days.
    // Calories retain the original thresholds and status names.
    const fixedZones = {
        kcal: {
            limits: [-3500, -1500, 200, 700],
            labels: ['Large deficit', 'Moderate deficit', 'Good balance', 'Small surplus', 'Large surplus']
        },
        protein: {
            limits: [-250, -100, 100, 200],
            labels: ['Large deficit', 'Moderate deficit', 'Good balance', 'Small surplus', 'Large surplus']
        },
        carbs: {
            limits: [-450, -180, 180, 350],
            labels: ['Large deficit', 'Moderate deficit', 'Good balance', 'Small surplus', 'Large surplus']
        },
        fiber: {
            limits: [-80, -30, 30, 60],
            labels: ['Large deficit', 'Moderate deficit', 'Good balance', 'Small surplus', 'Large surplus']
        },
        fat: {
            limits: [-130, -50, 50, 100],
            labels: ['Large deficit', 'Moderate deficit', 'Good balance', 'Small surplus', 'Large surplus']
        }
    };
    const {limits, labels} = fixedZones[key];
    const colors = ['#ef4444', '#f59e0b', '#a6da7e', '#f59e0b', '#ef4444'];
    const ranges = [
        `Under ${formatSigned(limits[0])}`,
        `${formatSigned(limits[0])} to ${formatSigned(limits[1])}`,
        `${formatSigned(limits[1])} to ${formatSigned(limits[2])}`,
        `${formatSigned(limits[2])} to ${formatSigned(limits[3])}`,
        `Over ${formatSigned(limits[3])}`
    ];
    zoneElements.forEach((element, index) => {
        element.querySelector('strong').textContent = labels[index];
        element.querySelector('small').textContent = ranges[index];
        element.querySelector('.balance-zone-bar').style.backgroundColor = colors[index];
        element.classList.remove('balance-zone-active');
    });
    if (!daysCount || !Number.isFinite(expected) || expected <= 0) {
        totalElement.textContent = '–';
        statusElement.textContent = goal > 0
            ? 'No eligible logged days yet' : 'Save a calculation to set your daily targets';
        totalElement.style.color = 'var(--muted)';
        statusElement.style.color = 'var(--muted)';
        return;
    }
    const zoneIndex = balance < limits[0] ? 0 : balance < limits[1] ? 1 : balance <= limits[2]
        ? 2 : balance <= limits[3] ? 3 : 4;
    totalElement.textContent = formatSigned(balance);
    statusElement.textContent = labels[zoneIndex];
    totalElement.style.color = colors[zoneIndex];
    statusElement.style.color = colors[zoneIndex];
    zoneElements[zoneIndex]?.classList.add('balance-zone-active');
}

function renderHomeWorkouts(date) {
    const weekStart = monday(date);
    const formatDay = day => new Date(day + 'T12:00:00').toLocaleDateString('en-GB', {
        day: 'numeric', month: 'short', year: 'numeric'
    });
    setText('#home-training-week', `Training week · ${formatDay(weekStart)} to ${formatDay(shift(weekStart, 6))}`);
    const completed = completedProgrammesThisWeek(date);
    const {groups, dashboardGroupId} = readTrainingProgrammes();
    const dashboardGroup = groups.find(group => group.id === dashboardGroupId);
    const programmes = dashboardGroup?.programmes.filter(programme => programme?.id) ?? [];
    setHTML('#home-workouts', programmes.map(programme => {
        const title = esc(programme.title || 'Training programme');
        return `<label class="workout-tile"><span class="workout-tile-copy"><strong>${title}</strong></span>
            <input class="check" type="checkbox"
            data-done="${esc(programme.id)}" ${completed.has(String(programme.id)) ? 'checked' : ''}
            aria-label="${title} completed"></label>`;
    }).join('') || (dashboardGroup
        ? '<div class="empty">This group has no programmes yet.</div>'
        : '<div class="empty">Choose a programme group on the Training page to show it here.</div>'));
    document.querySelectorAll('#home-workouts [data-done]').forEach((input, index) => input.addEventListener('change',
        () => {
        setProgrammeCompleted(programmes[index], input.checked, date);
    }));
}

function renderHome() {
    if (!document.querySelector('#kcal-stat')) return;
    const date = selectedDate();
    const t = totals(date), g = getGoals();
    const hasTargets = g.kcal > 0;
    const needsFibre = hasTargets && !fiberComplete(date);
    for (const key of ['kcal', 'protein', 'carbs', 'fat', 'fiber']) {
        const unit = key === 'kcal' ? 'kcal' : 'g', delta = g[key] - t[key];
        const incomplete = key === 'fiber' && needsFibre;
        const intake = incomplete && !fiberKnown(date) ? '–' : fmt(t[key]);
        setHTML(`#${key}-stat`,
            hasTargets ? `${intake} <small>/ ${fmt(g[key])} ${unit}</small>` : '0<small>/0</small>');
        setProgress(`#${key}-bar`, t[key], g[key], date, key);
        updateZoneDisplay(key, t[key], g[key], date, 'home');
        if (incomplete) setText('#fiber-zone', intake === '–' ? '' : 'Partial intake');
        const stat = document.querySelector(`#${key}-stat`);
        if (stat) stat.classList.toggle('cheat-over', hasTargets && !incomplete && isCheatDay(date) && t[key] > g[key]);
        setText(`#${key}-remaining`,
            !hasTargets || incomplete ? '' : isSickDay(date) ? 'Sick day – target assessment paused' : isCheatDay(date)
            ? 'Cheat day – included in balance' : `${fmt(Math.abs(delta))} ${unit} ${delta >= 0
            ? 'remaining' : 'above target'}`);
    }
    const fibreDiaryLink = document.querySelector('#fiber-diary-link');
    if (fibreDiaryLink) fibreDiaryLink.hidden = !needsFibre;
    const homeSick = document.querySelector('#home-sick-status');
    if (homeSick) homeSick.hidden = !isSickDay(date);
    const homeCheat = document.querySelector('#home-cheat-status');
    if (homeCheat) homeCheat.hidden = !isCheatDay(date);
    document.querySelector('.stats.stats-four')?.classList.toggle('cheat-day-stats',
        isCheatDay(date) && !isSickDay(date));
    renderHomeWorkouts(date);
    const items = food().filter(x => x.date === date).slice(-5).reverse();
    setHTML('#home-meals',
        items.length
        ? items.map(x =>
        `<div class="meal-row"><div><strong>${esc(x.name)}</strong><small>${fmt(x.grams)} ${x.type === 'drink'
        ? 'ml' : 'g'} · ${fmt(x.protein)} g protein · ${fmt(x.carbs
        || 0)} g carbohydrates</small></div><span class="tag">${fmt(x.kcal)} kcal</span></div>`)
        .join('') : '<div class="empty">No meals logged for this date.</div>');
    drawChart();
    renderCalorieBalance();
}


export {drawChart, renderCalorieBalance, renderHome};
