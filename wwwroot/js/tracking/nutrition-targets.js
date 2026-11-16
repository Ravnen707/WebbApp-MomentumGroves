import {read, write, PROFILE_KEY, GOAL_KEY} from "../core/storage.js";
import {DEFAULT_PROFILE} from "../core/profile-defaults.js";
import {parseDecimal, fmt, setText, localDate} from "../core/utils.js";
import {isCheatDay} from "./cheat-days.js";
import {isSickDay, fiberActive, fiberComplete} from "./sick-days.js";
import {food} from "../food/food-diary.js";

const NUTRITION_ZONES = {
    kcal: {low: 2150, good: 2550, high: 2700},
    protein: {low: 130, good: 170, high: 190},
    carbs: {low: 200, good: 320, high: 350},
    fat: {low: 60, good: 90, high: 105},
    fiber: {low: 25, good: 35, high: Infinity}
};
const ZONE_COLORS = {low: '#F2C94C', good: '#A6DA7E', high: '#F2C94C', over: '#FF1111', cheat: '#FFA500'};

function nutritionZone(key, value) {
    const limits = NUTRITION_ZONES[key];
    if (value < limits.low) {
        return 'low';
    }
    if (value <= limits.good) {
        return 'good';
    }
    if (value <= limits.high) {
        return 'high';
    }
    return 'over';
}

function nutritionColor(key, value, goal, date) {
    if (key === 'fiber' && !fiberActive(date)) return '#8fb4ce';
    if (isSickDay(date)) {
        return '#8fb4ce';
    }
    if (isCheatDay(date)) {
        return ZONE_COLORS.cheat;
    }
    return ZONE_COLORS[nutritionZone(key, value)];
}

const zoneNames = {low: 'Low', good: 'Good', high: 'High', over: 'Above target'};

function updateZoneDisplay(key, value, goal, date, prefix) {
    const inactive = key === 'fiber' && !fiberActive(date);
    const unknown = key === 'fiber' && !fiberComplete(date);
    const stat = document.querySelector(prefix === 'home' ? `#${key}-stat` : `#food-${key}`);
    if (stat) {
        stat.style.color = inactive || unknown ? 'var(--muted)' : nutritionColor(key, value, goal, date);
        stat.title = inactive ? 'Fibre was not assessed on this date' : unknown ? 'Fibre is missing for one or more entries' : isSickDay(date) ? 'Sick day – target assessment paused' : isCheatDay(date) ? 'Cheat day – included in balance' : zoneNames[nutritionZone(key, value)];
    }
    const label = document.querySelector(prefix === 'home' ? `#${key}-zone` : `#food-${key}-zone`);
    if (label) {
        const cheat = !isSickDay(date) && isCheatDay(date);
        label.textContent = inactive ? 'Not active yet' : unknown ? 'Partially/not specified' : isSickDay(date) ? 'Sick day – targets paused' : cheat ? 'Cheat day – included in balance' : zoneNames[nutritionZone(key, value)];
        label.style.color = inactive || unknown ? 'var(--muted)' : nutritionColor(key, value, goal, date);
    }
}


const getProfile = () => ({...DEFAULT_PROFILE, ...read(PROFILE_KEY, {})});

function calculate(profile) {
    const p = {...profile};
    for (const k of ['weight', 'targetWeight', 'height', 'age', 'trainingDays', 'deficit', 'proteinFactor', 'fatPercent']) p[k] = Number(p[k]);
    if (!Number.isFinite(p.weight) || p.weight < 20 || p.weight > 500 || !Number.isFinite(p.targetWeight) || p.targetWeight < 20 || p.targetWeight > 500 || !Number.isFinite(p.height) || p.height < 100 || p.height > 250 || !Number.isInteger(p.age) || p.age < 18 || p.age > 110 || !['male', 'female'].includes(p.sex) || !Number.isInteger(p.trainingDays) || p.trainingDays < 0 || p.trainingDays > 7 || ![300, 500, 700].includes(p.deficit) || !Number.isFinite(p.proteinFactor) || p.proteinFactor < 0.8 || p.proteinFactor > 2.5 || !Number.isFinite(p.fatPercent) || p.fatPercent < 15 || p.fatPercent > 45) return null;
    const bmr = 10 * p.weight + 6.25 * p.height - 5 * p.age + (p.sex === 'male' ? 5 : -161);
    const factor = p.includeTraining ? 1.2 + p.trainingDays * 0.05 : 1.2;
    const maintenance = Math.round(bmr * factor);
    const kcal = maintenance - p.deficit;
    const protein = Math.round(p.targetWeight * p.proteinFactor);
    const fat = Math.round(kcal * p.fatPercent / 100 / 9);
    const carbs = Math.round((kcal - protein * 4 - fat * 9) / 4);
    if (kcal < 1200 || carbs < 0) return null;
    return {bmr: Math.round(bmr), factor, maintenance, kcal, protein, fat, carbs, weeklyLoss: p.deficit * 7 / 7700};
}

const getGoals = () => {
    const result = calculate(getProfile());
    return result ? {
        kcal: result.kcal,
        protein: result.protein,
        carbs: result.carbs,
        fat: result.fat, fiber: 30
    } : {kcal: 2463, protein: 153, carbs: 278, fat: 82, fiber: 30};
};

function setProgress(selector, value, goal, date = localDate(), key = 'kcal') {
    const el = document.querySelector(selector);
    if (!el) return;
    el.style.width = `${goal > 0 ? Math.min(100, Math.max(0, value / goal * 100)) : 0}%`;
    el.style.background = key === 'fiber' && (!fiberActive(date) || !fiberComplete(date)) ? 'var(--muted)' : nutritionColor(key, value, goal, date);
}


function renderCalculator() {
    const form = document.querySelector('#calculator-form');
    if (!form) return;
    const saved = getProfile();
    for (const [key, value] of Object.entries(saved)) {
        const el = form.elements.namedItem(key);
        if (!el) continue;
        if (el.type === 'checkbox') el.checked = Boolean(value); else el.value = String(value);
    }

    function current() {
        const d = new FormData(form);
        return {
            weight: parseDecimal(d.get('weight')),
            targetWeight: parseDecimal(d.get('targetWeight')),
            height: parseDecimal(d.get('height')),
            age: parseDecimal(d.get('age')),
            sex: String(d.get('sex')),
            includeTraining: form.elements.namedItem('includeTraining').checked,
            trainingDays: parseDecimal(d.get('trainingDays')),
            deficit: parseDecimal(d.get('deficit')),
            proteinFactor: parseDecimal(d.get('proteinFactor')),
            fatPercent: parseDecimal(d.get('fatPercent'))
        };
    }

    function update() {
        const p = current(), result = calculate(p);
        document.querySelector('#training-settings').hidden = !p.includeTraining;
        setText('#training-info', `You can adjust the number of training days without changing your training programmes.`);
        if (!result) {
            for (const id of ['#calc-bmr', '#calc-maintenance', '#calc-kcal', '#calc-protein', '#calc-carbs', '#calc-fat', '#calc-loss']) setText(id, '–');
            setText('#calc-explanation', 'Check your entries.');
            return;
        }
        setText('#calc-bmr', `${fmt(result.bmr)} kcal`);
        setText('#calc-maintenance', `${fmt(result.maintenance)} kcal`);
        setText('#calc-kcal', `${fmt(result.kcal)} kcal`);
        setText('#calc-protein', `${fmt(result.protein)} g`);
        setText('#calc-carbs', `${fmt(result.carbs)} g`);
        setText('#calc-fat', `${fmt(result.fat)} g`);
        setText('#calc-loss', `${result.weeklyLoss.toLocaleString('en-GB', {maximumFractionDigits: 2})} kg/week`);
        setText('#calc-explanation', `${p.includeTraining ? `${p.trainingDays} training sessions/week` : 'Sedentary'} · activity factor ${result.factor.toLocaleString('en-GB', {maximumFractionDigits: 2})} · ${p.deficit} kcal deficit`);
        const macroKcal = [result.protein * 4, result.carbs * 4, result.fat * 9],
            sum = macroKcal.reduce((a, b) => a + b, 0);
        ['protein', 'carbs', 'fat'].forEach((k, i) => {
            const el = document.querySelector(`#macro-${k}`);
            el.style.flex = String(macroKcal[i]);
            el.title = `${k}: ${macroKcal[i]} kcal (${Math.round(macroKcal[i] / sum * 100)} %)`;
        });
    }

    form.addEventListener('input', () => {
        setText('#calculator-message', 'Changes have not been saved yet.');
        update();
    });
    form.addEventListener('change', update);
    form.addEventListener('submit', e => {
        e.preventDefault();
        if (!form.reportValidity()) return;
        const p = current(), result = calculate(p);
        if (!result) {
            setText('#calculator-message', 'Invalid calculation. Check your details.');
            return;
        }
        write(PROFILE_KEY, p);
        write(GOAL_KEY, {kcal: result.kcal, protein: result.protein, carbs: result.carbs, fat: result.fat});
        setText('#calculator-message', '✓ Calculation saved. The dashboard and food diary now use your new targets.');
    });
    update();
}


export {
    NUTRITION_ZONES,
    ZONE_COLORS,
    nutritionZone,
    nutritionColor,
    zoneNames,
    updateZoneDisplay,
    getProfile,
    calculate,
    getGoals,
    setProgress,
    renderCalculator
};
