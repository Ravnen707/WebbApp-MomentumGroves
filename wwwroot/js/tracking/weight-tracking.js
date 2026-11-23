import {selectDate, selectedDate} from '../core/selected-date.js';
import {localDate} from '../core/utils.js';

(() => {
    'use strict';
    const HISTORY_KEY = 'sunhed.weight-history.v1';
    const PROFILE_KEY = 'sunhed.profile.v1';
    const GOALS_KEY = 'sunhed.weight-goals.v1';
    const form = document.querySelector('#weight-form');
    if (!form) return;
    const byId = id => document.getElementById(id);
    const dateInput = form.elements.namedItem('date');
    const daysBetween = (a, b) => (Date.parse(b + 'T12:00:00Z') - Date.parse(a + 'T12:00:00Z')) / 86400000;
    const fmt = n => Number(n).toLocaleString('en-GB', {maximumFractionDigits: 2});
    const displayDate = d => new Date(d + 'T12:00:00').toLocaleDateString('en-GB', {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
    });
    const validDate = d => /^\d{4}-\d{2}-\d{2}$/.test(d) && !Number.isNaN(Date.parse(d));
    let history = {};
    let profile = {};
    let goals = {mode: 'final', milestones: []};
    let saveQueue = Promise.resolve();
    let monthOffset = 0;
    let historyVisibleCount = 7;

    // Calculates a time-adjusted exponential weight trend from recorded weigh-ins.
    function trendPoints(points) {
        let trend = null;
        let previousDate = null;
        return points.map(point => {
            const elapsed = previousDate === null ? 0 : Math.max(0, daysBetween(previousDate, point.date));
            trend = trend === null ? point.weight : trend + (1 - Math.pow(0.9, elapsed)) * (point.weight - trend);
            previousDate = point.date;
            return {...point, trend};
        });
    }

    // Estimates a target month from a regression of the last 60 days of measured trend.
    function forecast(points, target) {
        if (points.length < 6 || !Number.isFinite(target)) return null;
        const latest = points[points.length - 1];
        const recent = points.filter(p => daysBetween(p.date, latest.date) <= 60);
        if (recent.length < 6 || daysBetween(recent[0].date, latest.date) < 14) return null;
        const xs = recent.map(p => daysBetween(recent[0].date, p.date));
        const xm = xs.reduce((a, b) => a + b, 0) / xs.length;
        const ym = recent.reduce((sum, p) => sum + p.trend, 0) / recent.length;
        const denominator = xs.reduce((sum, x) => sum + (x - xm) ** 2, 0);
        const slope = denominator ? xs.reduce((sum, x, i) => sum + (x - xm) * (recent[i].trend - ym), 0) / denominator : 0;
        if (target >= latest.trend) return {reached: true};
        if (slope >= -0.01) return {stalled: true};
        const days = (target - latest.trend) / slope;
        if (!Number.isFinite(days) || days < 0 || days > 730) return null;
        const date = new Date(latest.date + 'T12:00:00');
        date.setDate(date.getDate() + Math.ceil(days));
        return {date, weeklyRate: slope * 7, days};
    }

    // Converts validated saved entries into chronological measured data points.
    function measuredPoints() {
        return Object.entries(history).filter(([date, item]) => validDate(date) && item?.status === 'weighed' && Number.isFinite(Number(item.weight)) && Number(item.weight) >= 20 && Number(item.weight) <= 500)
            .map(([date, item]) => ({date, weight: Number(item.weight)})).sort((a, b) => a.date.localeCompare(b.date));
    }

    // Chooses the next unreached milestone or falls back to the final BMI target.
    function activeGoal(points) {
        const current = points.at(-1)?.weight;
        const final = Number(profile.targetWeight);
        const validFinal = Number.isFinite(final) && final >= 20 && final <= 500 ? final : null;
        const milestones = [...new Set((Array.isArray(goals.milestones) ? goals.milestones : [])
            .map(Number).filter(n => Number.isFinite(n) && n >= 20 && n <= 500))].sort((a, b) => b - a);
        const isAhead = n => current === undefined || n < current - 0.00001;
        const remaining = milestones.filter(isAhead);
        if (goals.mode === 'milestone' && remaining.length) {
            return {value: remaining[0], type: 'milestone', finished: false};
        }
        if (validFinal !== null && isAhead(validFinal)) {
            return {value: validFinal, type: 'final', finished: false};
        }
        return {
            value: null,
            type: null,
            finished: current !== undefined && (milestones.length > 0 || validFinal !== null)
        };
    }

    // Updates compact goal cards without changing the stored weigh-ins.
    function renderGoals(points) {
        const active = activeGoal(points);
        const current = points.at(-1)?.weight;
        const values = [...new Set((goals.milestones || []).map(Number))].filter(n => Number.isFinite(n) && n >= 20 && n <= 500).sort((a, b) => b - a);
        const nextMilestone = values.find(n => current === undefined || n < current - 0.00001);
        const input = byId('weight-milestone-form').elements.namedItem('milestone');
        if (document.activeElement !== input) input.value = nextMilestone ?? values.at(-1) ?? '';
        byId('weight-milestone-enabled').checked = goals.mode === 'milestone';
        byId('weight-milestone-detail').textContent = active.type === 'milestone'
            ? `${fmt(Math.max(0, (current ?? active.value) - active.value))} kg remaining to milestone`
            : values.length && !nextMilestone ? 'Milestone reached' : values.length ? 'Enable to track your milestone' : 'Set a milestone';
        const final = Number(profile.targetWeight);
        const validFinal = Number.isFinite(final) && final >= 20 && final <= 500 ? final : null;
        byId('weight-final-goal').textContent = validFinal === null ? 'Not set' : `${fmt(validFinal)} kg`;
        byId('weight-final-selected').textContent = active.type === 'final' ? 'Tracking' : active.type === null ? 'Completed' : 'After milestone';
        byId('weight-final-detail').textContent = validFinal === null ? 'Set in Health & Nutrition Calculator' : active.type === 'final' ? 'Goal tracked automatically' : 'From Health & Nutrition Calculator';
        return active;
    }

    // Persists milestone settings without changing the BMI profile or weigh-ins.
    function updateGoals(patch) {
        const next = {...goals, ...patch};
        byId('weight-goal-message').textContent = 'Saving goals…';
        queueUpdate(async () => {
            await save(GOALS_KEY, next);
            goals = next;
            render();
        }).then(() => {
            byId('weight-goal-message').textContent = '✓ Goals saved to SQLite.';
        })
            .catch(error => {
                byId('weight-goal-message').textContent = `Could not save goals: ${error.message}`;
            });
    }

    // Draws actual measurements and computed trend without inventing measurements on skipped days.
    function drawChart(points, target) {
        const host = byId('weight-chart');
        const today = new Date();
        today.setHours(12, 0, 0, 0);
        const endDate = new Date(today.getFullYear(), today.getMonth() + monthOffset + 1, 0, 12);
        if (monthOffset === 0) endDate.setTime(today.getTime());
        const startDate = new Date(endDate.getFullYear(), endDate.getMonth() - 1, Math.min(endDate.getDate(), new Date(endDate.getFullYear(), endDate.getMonth(), 0).getDate()), 12);
        const first = localDate(startDate);
        const last = localDate(endDate);
        const visible = points.filter(p => p.date >= first && p.date <= last);
        byId('weight-period-label').textContent = `${displayDate(first)} – ${displayDate(last)}`;
        byId('weight-period-next').disabled = monthOffset >= 0;
        byId('weight-period-today').disabled = monthOffset === 0;
        if (!visible.length) {
            host.textContent = 'No weigh-ins in the selected month.';
            return;
        }
        const W = 960, H = 340, left = 55, right = 18, top = 20, bottom = 45;
        const values = visible.flatMap(p => [p.weight, p.trend]);

        const latestWeight = visible.at(-1).weight;
        const lo = Math.floor(Math.min(latestWeight - 5, ...values));
        const hi = Math.ceil(Math.max(latestWeight + 5, ...values));
        const span = Math.max(1, hi - lo);
        const totalDays = Math.max(1, daysBetween(first, last));
        const x = p => left + daysBetween(first, p.date) / totalDays * (W - left - right);
        const y = value => top + (hi - value) / span * (H - top - bottom);
        const path = key => visible.map((p, i) => `${i ? 'L' : 'M'}${x(p).toFixed(1)},${y(p[key]).toFixed(1)}`).join(' ');
        // Uses whole-kilogram grid values and evenly spaced date labels.
        const tickStep = Math.max(1, Math.ceil(span / 5));
        const firstTick = Math.ceil(lo / tickStep) * tickStep;
        const grid = Array.from({length: Math.floor((hi - firstTick) / tickStep) + 1}, (_, i) => {
            const v = firstTick + i * tickStep;
            const yy = y(v);
            return `<line x1="${left}" y1="${yy}" x2="${W - right}" y2="${yy}" stroke="currentColor" opacity=".12"/><text x="${left - 9}" y="${yy + 4}" fill="currentColor" opacity=".7" font-size="12" text-anchor="end">${v}</text>`;
        }).join('');
        const labels = [0, 0.25, 0.5, 0.75, 1].map(fraction => {
            const day = Math.round(totalDays * fraction);
            const date = new Date(startDate);
            date.setDate(date.getDate() + day);
            const text = date.toLocaleDateString('en-GB', {day: 'numeric', month: 'short'});
            const xx = left + day / totalDays * (W - left - right);
            return `<text x="${xx}" y="${H - 12}" fill="currentColor" opacity=".7" font-size="12" text-anchor="middle">${text}</text>`;
        }).join('');
        const targetLine = Number.isFinite(target) && target > 0 && target >= lo && target <= hi
            ? `<line x1="${left}" y1="${y(target)}" x2="${W - right}" y2="${y(target)}" stroke="#b8dd9d" stroke-width="1.8" stroke-dasharray="7 5"/><text x="${W - right - 5}" y="${Math.max(14, y(target) - 8)}" text-anchor="end" class="weight-chart-goal-label">Target: ${fmt(target)} kg</text>`
            : '';
        const goalNote = Number.isFinite(target) && target > 0 && (target < lo || target > hi)
            ? `<div class="weight-chart-target-note">Target: ${fmt(target)} kg · outside the chart's displayed weight range</div>` : '';
        const circles = visible.map((p, i) => `
            <circle class="weight-chart-point weight-chart-measured" data-index="${i}" data-kind="weight" cx="${x(p)}" cy="${y(p.weight)}" r="5.5" fill="#5ca8ed" stroke="#172026" stroke-width="1.5" tabindex="0" aria-label="${displayDate(p.date)}, measured ${fmt(p.weight)} kilogram"/>
            <circle class="weight-chart-point weight-chart-trend" data-index="${i}" data-kind="trend" cx="${x(p)}" cy="${y(p.trend)}" r="5" fill="#f5b66b" stroke="#172026" stroke-width="1.5" tabindex="0" aria-label="${displayDate(p.date)}, estimated weight trend ${fmt(p.trend)} kilogram"/>`).join('');
        host.innerHTML = `<svg viewBox="0 0 ${W} ${H}" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="Actual weigh-ins and calculated weight trend">${grid}${targetLine}<path d="${path('weight')}" fill="none" stroke="#5ca8ed" stroke-width="2.5" stroke-linejoin="round"/>${visible.length > 1 ? `<path d="${path('trend')}" fill="none" stroke="#f5b66b" stroke-width="3" stroke-linejoin="round"/>` : ''}${circles}${labels}</svg>${goalNote}<div class="weight-chart-tooltip" role="status" hidden></div>`;
        const tooltip = host.querySelector('.weight-chart-tooltip');
        const showTooltip = (circle, event) => {
            const p = visible[Number(circle.dataset.index)];
            const kind = circle.dataset.kind;
            tooltip.replaceChildren();
            const title = document.createElement('strong');
            title.textContent = displayDate(p.date);
            const value = document.createElement('span');
            value.textContent = `${kind === 'weight' ? 'Measured weight' : 'Estimated weight trend'}: ${fmt(kind === 'weight' ? p.weight : p.trend)} kg`;
            const other = document.createElement('span');
            other.textContent = `${kind === 'weight' ? 'Estimated weight trend' : 'Measured weight'}: ${fmt(kind === 'weight' ? p.trend : p.weight)} kg`;
            tooltip.append(title, value, other);
            tooltip.hidden = false;
            const rect = host.getBoundingClientRect();
            const clientX = event?.clientX || rect.left + (Number(circle.getAttribute('cx')) / W) * rect.width;
            const clientY = event?.clientY || rect.top + (Number(circle.getAttribute('cy')) / H) * rect.width * H / W;
            tooltip.style.left = `${Math.max(8, Math.min(clientX - rect.left + 12, rect.width - tooltip.offsetWidth - 8))}px`;
            tooltip.style.top = `${Math.max(8, clientY - rect.top - tooltip.offsetHeight - 12)}px`;
        };
        host.querySelectorAll('.weight-chart-point').forEach(circle => {
            circle.addEventListener('pointerenter', event => showTooltip(circle, event));
            circle.addEventListener('pointermove', event => showTooltip(circle, event));
            circle.addEventListener('pointerleave', () => {
                tooltip.hidden = true;
            });
            circle.addEventListener('focus', () => showTooltip(circle));
            circle.addEventListener('blur', () => {
                tooltip.hidden = true;
            });
        });
    }

    // Renders summary, target forecast, and history from the saved measurements.
    function render() {
        const points = trendPoints(measuredPoints());
        const last = points.at(-1);
        byId('weight-latest').textContent = last ? `${fmt(last.weight)} kg` : '–';
        byId('weight-latest-date').textContent = last ? displayDate(last.date) : 'No weigh-ins yet';
        byId('weight-trend').textContent = last ? `${fmt(last.trend)} kg` : '–';
        const active = renderGoals(points);
        const target = active.value;
        const estimate = target === null ? null : forecast(points, target);
        byId('weight-forecast').textContent = target === null ? active.finished ? 'All goals reached' : 'No active goal' : estimate?.date ? estimate.date.toLocaleDateString('en-GB', {
            month: 'long',
            year: 'numeric'
        }) : estimate?.reached ? 'Goal reached' : 'Cannot estimate yet';
        byId('weight-forecast-detail').textContent = target === null ? 'The weight chart and weigh-ins continue as usual without a goal or forecast.' : estimate?.date ? `Historical weight trend: ${fmt(estimate.weeklyRate)} kg/week. Highly uncertain estimate; allow for a difference of at least 1–2 months.` : estimate?.stalled ? 'The recent trend does not show clear weight loss.' : 'Requires at least 6 weigh-ins over 14 days. The forecast uses up to 60 days of history.';
        drawChart(points, target);
        const entries = Object.entries(history).filter(([date]) => validDate(date)).sort(([a], [b]) => b.localeCompare(a));
        const host = byId('weight-history');
        if (!entries.length) {
            host.textContent = 'No entries yet.';
            return;
        }
        const table = document.createElement('table');
        table.innerHTML = '<thead><tr><th>Date</th><th>Registrering</th><th></th></tr></thead>';
        const body = document.createElement('tbody');
        for (const [date, entry] of entries.slice(0, historyVisibleCount)) {
            const row = document.createElement('tr');
            const dateCell = document.createElement('td');
            dateCell.textContent = displayDate(date);
            const weightCell = document.createElement('td');
            weightCell.textContent = entry.status === 'weighed' ? `${fmt(entry.weight)} kg` : 'Not weighed';
            const actionCell = document.createElement('td');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn secondary';
            button.textContent = 'Edit';
            button.addEventListener('click', () => {
                selectDate(date);
                form.scrollIntoView({behavior: 'smooth', block: 'center'});
            });
            actionCell.append(button);
            row.append(dateCell, weightCell, actionCell);
            body.append(row);
        }
        table.append(body);
        host.replaceChildren(table);
        if (entries.length > historyVisibleCount) {
            const more = document.createElement('button');
            more.type = 'button';
            more.className = 'btn secondary weight-history-more';
            more.textContent = `Show ${Math.min(7, entries.length - historyVisibleCount)} more`;
            more.addEventListener('click', () => {
                historyVisibleCount += 7;
                render();
            });
            host.append(more);
        }
        if (historyVisibleCount > 7) {
            const less = document.createElement('button');
            less.type = 'button';
            less.className = 'btn secondary weight-history-more';
            less.textContent = 'Show fewer';
            less.addEventListener('click', () => {
                historyVisibleCount = 7;
                render();
            });
            host.append(less);
        }
    }

    // Enables the weight field only for actual measurements.
    function toggleWeight() {
        const weighed = form.querySelector('input[name="status"]:checked').value === 'weighed';
        byId('weight-input-wrap').hidden = !weighed;
        form.elements.namedItem('weight').required = weighed;
        form.elements.namedItem('weight').disabled = !weighed;
    }

    function showSelectedEntry() {
        const date = selectedDate();
        const entry = history[date];
        dateInput.value = date;
        form.elements.namedItem('status').value = entry?.status === 'skipped' ? 'skipped' : 'weighed';
        form.elements.namedItem('weight').value = entry?.status === 'weighed' ? entry.weight : '';
        toggleWeight();
    }

    // Saves a JSON value through the existing SQLite API and reports server errors.
    async function save(key, value) {
        const response = await fetch('/api/momentum-data/' + encodeURIComponent(key), {
            method: 'PUT',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify(value)
        });
        if (!response.ok) throw new Error(`SQLite afviste gemningen (${response.status}): ${await response.text()}`);
    }

    // Serializes edits to prevent out-of-order updates to the history document.
    function queueUpdate(operation) {
        saveQueue = saveQueue.catch(() => {
        }).then(operation);
        return saveQueue;
    }

    // Persists a measurement or a skipped day without changing the nutrition targets.
    async function updateEntry(date, entry) {
        const next = {...history};
        if (entry) next[date] = entry; else delete next[date];
        await save(HISTORY_KEY, next);
        history = next;
        render();
    }

    // Loads the shared profile and weight history from the existing database.
    async function initialize() {
        try {
            const response = await fetch('/api/momentum-data', {cache: 'no-store'});
            if (!response.ok) throw new Error(`Could not load data (${response.status}).`);
            const state = await response.json();
            profile = state[PROFILE_KEY] ?? {};
            const savedGoals = state[GOALS_KEY];
            goals = savedGoals && typeof savedGoals === 'object' && !Array.isArray(savedGoals) ? {
                mode: savedGoals.mode === 'milestone' ? 'milestone' : 'final',
                milestones: Array.isArray(savedGoals.milestones) ? savedGoals.milestones : []
            } : {mode: 'final', milestones: []};
            history = state[HISTORY_KEY] && typeof state[HISTORY_KEY] === 'object' && !Array.isArray(state[HISTORY_KEY]) ? state[HISTORY_KEY] : {};
            showSelectedEntry();
            render();
        } catch (error) {
            byId('weight-message').textContent = `Could not load data: ${error.message}`;
            form.querySelector('button[type="submit"]').disabled = true;
        }
    }

    dateInput.value = selectedDate();
    dateInput.addEventListener('change', () => {
        if (dateInput.value && dateInput.validity.valid) selectDate(dateInput.value);
        else dateInput.value = selectedDate();
    });
    document.addEventListener('momentum-date-change', showSelectedEntry);
    form.querySelectorAll('input[name="status"]').forEach(radio => radio.addEventListener('change', toggleWeight));
    form.addEventListener('submit', event => {
        event.preventDefault();
        if (!form.reportValidity()) return;
        const date = form.elements.namedItem('date').value;
        if (!validDate(date)) return;
        const status = form.querySelector('input[name="status"]:checked').value;
        const weight = Number(form.elements.namedItem('weight').value.replace(',', '.'));
        if (status === 'weighed' && (!Number.isFinite(weight) || weight < 20 || weight > 500)) return;
        byId('weight-message').textContent = 'Saving…';
        queueUpdate(() => updateEntry(date, status === 'weighed' ? {status, weight} : {status})).then(() => {
            byId('weight-message').textContent = '✓ Saved to SQLite.';
        }).catch(error => {
            console.error(error);
            byId('weight-message').textContent = `Could not save: ${error.message}`;
        });
    });
    byId('weight-delete').addEventListener('click', () => {
        const date = form.elements.namedItem('date').value;
        if (!history[date] || !confirm(`Delete entry for ${displayDate(date)}?`)) return;
        queueUpdate(() => updateEntry(date, null)).then(() => {
            if (selectedDate() === date) showSelectedEntry();
            byId('weight-message').textContent = 'Entry deleted.';
        }).catch(error => {
            byId('weight-message').textContent = `Could not delete: ${error.message}`;
        });
    });
    byId('weight-milestone-enabled').addEventListener('change', event => {
        updateGoals({mode: event.currentTarget.checked ? 'milestone' : 'final'});
    });
    byId('weight-milestone-form').addEventListener('submit', event => {
        event.preventDefault();
        const input = event.currentTarget.elements.namedItem('milestone');
        const value = Number(input.value.replace(',', '.'));
        if (!Number.isFinite(value) || value < 20 || value > 500) return;
        updateGoals({milestones: [value], mode: 'milestone'});
    });
    byId('weight-period-prev').addEventListener('click', () => {
        monthOffset -= 1;
        render();
    });
    byId('weight-period-next').addEventListener('click', () => {
        monthOffset = Math.min(0, monthOffset + 1);
        render();
    });
    byId('weight-period-today').addEventListener('click', () => {
        monthOffset = 0;
        render();
    });
    toggleWeight();
    initialize();
})();
