import {read, write, WORKOUT_KEY, DONE_KEY} from "../core/storage.js";
import {esc, setText, localDate, shift, monday} from "../core/utils.js";

const exercise = name => ({
    name,
    sets: 2,
    reps: '10–12',
    kg: 0,
    description: '',
    image: '',
    type: 'strength',
    minutes: 0
});
const PROGRAM_KEY = 'sunhed.training-programs.v3';
const HISTORY_KEY = 'sunhed.training-history.v1';
const fullBody = [
    {
        id: 'full-a', title: 'Full Body A', subtitle: 'Full body · 6 exercises', exercises: [
            ['Chest Press', 2, '10–12', 'Chest and triceps. Press the handles forwards at chest height.'],
            ['Lat Pulldown', 2, '10–12', 'Back. Use a wide grip and pull the bar down in front of your body.'],
            ['Leg Press', 2, '10–12', 'Quads and glutes. Place your feet shoulder-width apart on the platform.'],
            ['Seated Leg Curl', 2, '12', 'Hamstrings. Curl your legs towards the seat with control.'],
            ['Hip Adductor', 2, '12–15', 'Inner thighs. Press your legs inwards with a controlled movement.'],
            ['Abdominal Crunch', 2, '12–15', 'Abs. Curl your upper body forwards with control.']
        ]
    },
    {
        id: 'full-b', title: 'Full Body B', subtitle: 'Full body · 5 exercises', exercises: [
            ['Leg Press', 2, '10–12', 'Feet shoulder-width apart. Press without locking your knees.'],
            ['Hip Abductor', 2, '12–15', 'Outer hips and glutes. Press your knees outwards.'],
            ['Seated Row', 2, '10–12', 'Back. Use a neutral vertical grip and pull towards your abdomen.'],
            ['Chest Press', 2, '10–12', 'Chest and triceps. Press forwards at chest height.'],
            ['Back Extension', 2, '12', 'Back. Extend with control without overextending.']
        ]
    },
    {
        id: 'full-c', title: 'Full Body C', subtitle: 'Arms and core · 7 exercises', exercises: [
            ['Leg Press', 2, '10–12', 'Quads and glutes. Place your feet shoulder-width apart on the platform.'],
            ['Leg Extension', 2, '12', 'Quads. Extend your legs forwards and upwards.'],
            ['Lat Pulldown', 2, '10–12', 'Back. Use a narrow neutral grip and pull towards your upper chest in front of your body.'],
            ['Chest Press', 2, '10–12', 'Chest and triceps. Press forwards at chest height.'],
            ['Seated Row', 2, '12', 'Back. Use a vertical neutral grip and pull towards your abdomen.'],
            ['Triceps Pushdown', 2, '12', 'Triceps. Use a rope grip, keep elbows close to your body and push downwards.'],
            ['Abdominal Crunch', 2, '12–15', 'Abs. Curl your upper body forwards with control.']
        ]
    }
];
// Each of the 18 exercise cards has an image; repeated exercises reuse one source URL.
// Uploaded images override these defaults without changing the shared source.
const exerciseImageSources = {
    "chest-press": "https://tokai-catv.co.jp/assets/img/column/exercise2410_3.jpg",
    "lat-pulldown-wide": "https://legionathletics.com/wp-content/uploads/2022/01/Wide-Grip-Lat-Pulldown-1.jpg",
    "leg-press": "https://www.atpress.ne.jp/attachments/jZgfnSXBbY29F0yLizZr.jpg",
    "seated-leg-curl": "https://www.institute.cl/store/1/PORTADAS%20EJERCICIOS/Captura%20de%20pantalla%202023-11-09%20191449.png",
    "hip-adductor": "https://cdn.motra.com/assets/ba5beb3c40/mediumThumbnail.jpg",
    "abdominal-crunch": "https://assets.media-platform.com/tarzan/dist/images/2019/10/02/191002_773_43-w1280.jpg",
    "hip-abductor": "https://s3.amazonaws.com/prod.skimble/assets/2423103/image_iphone.jpg",
    "seated-row": "https://www.funsportzentrum.de/images/Newsletter2026/2026-03-26-Fitundgsund-Maerz/Bilder_Artikel_Neue_Ger%C3%A4te/Compound_Row_2-yx.jpg",
    "back-extension": "https://www.johnsonfitness.com/Content/product_images/V-KEY-SL-52-K1_5.png",
    "leg-extension": "https://institute.cl/store/1/PORTADAS%20EJERCICIOS/Captura%20de%20pantalla%202023-11-09%20191536.png",
    "lat-pulldown-neutral": "https://hips.hearstapps.com/hmg-prod/images/mh-3-8-eb-swole-6408f3d22ab8a.png?crop=0.502xw%3A1.00xh%3B0.213xw%2C0&resize=1200%3A%2A",
    "triceps-pushdown": "https://www.natima.cz/user/articles/images/nejlepsi-cviky-na-triceps--jak-budovat-silne-a-tvarovane-paze.jpg"
};
const fullBodyImageKeys = {
    "full-a": ["chest-press", "lat-pulldown-wide", "leg-press", "seated-leg-curl", "hip-adductor", "abdominal-crunch"],
    "full-b": ["leg-press", "hip-abductor", "seated-row", "chest-press", "back-extension"],
    "full-c": ["leg-press", "leg-extension", "lat-pulldown-neutral", "chest-press", "seated-row", "triceps-pushdown", "abdominal-crunch"]
};
const applyDefaultImages = (box) => {
    const imageKeys = fullBodyImageKeys[box.id];
    if (!imageKeys) {
        return box;
    }
    box.exercises.forEach((item, index) => {
        if ((!item.image || Object.values(exerciseImageSources).includes(item.image) || /^\/api\/training-images\/(?:chest|latwide|legpress|legcurl|adductor|crunch|abductor|row|back|extension|latnarrow|pushdown)(?:\?|$)/.test(item.image)) && !item.imageRemoved && imageKeys[index]) {
            item.image = `/api/training-images/${imageKeys[index]}`;
            item.imageKey = imageKeys[index];
        }
    });
    return box;
};
const makeExercise = (entry) => ({
    name: entry[0],
    sets: entry[1],
    reps: entry[2],
    kg: 0,
    description: entry[3],
    image: '',
    type: 'strength',
    minutes: 0
});
const defaults = () => ({
    full: fullBody.map(box => applyDefaultImages({...box, exercises: box.exercises.map(makeExercise)})),
    custom: []
});
const getPrograms = () => {
    const saved = read(PROGRAM_KEY, null);
    const initial = defaults();
    if (saved && typeof saved === 'object') {
        if (Array.isArray(saved.full)) {
            const existing = saved.full.filter(x => ['full-a', 'full-b', 'full-c'].includes(x.id));
            initial.full = initial.full.map(box => applyDefaultImages(existing.find(x => x.id === box.id) || box));
            initial.full.push(...saved.full.filter(x => !['full-a', 'full-b', 'full-c'].includes(x.id)));
        }
        if (Array.isArray(saved.custom)) initial.custom = structuredClone(saved.custom);
    }
    return initial;
};

function renderTraining() {
    const root = document.querySelector('#training-days');
    if (!root || root.dataset.initialized === 'true') return;
    root.dataset.initialized = 'true';
    const tabs = document.querySelector('#training-program-tabs');
    const addBox = document.querySelector('#training-add-box');
    const saveButton = document.querySelector('#save-workouts');
    let active = 'full';
    let programs = getPrograms();
    const history = () => read(HISTORY_KEY, {});
    const renderProgress = () => {
        const done = read(DONE_KEY, {});
        const records = Object.entries(done).filter(([key, value]) => value && /^\d{4}-\d{2}-\d{2}:/.test(key));
        const dates = [...new Set(records.map(([key]) => key.slice(0, 10)))];
        const today = new Date(`${localDate()}T12:00:00`);
        const monthAgo = new Date(today);
        monthAgo.setDate(today.getDate() - 29);
        const monthly = dates.filter(date => new Date(`${date}T12:00:00`) >= monthAgo && new Date(`${date}T12:00:00`) <= today).length;
        const stats = document.querySelector('#training-progress-stats');
        if (!stats) return;
        const statsData = [
            [monthly, 'Training days · 30 days', 'calendar-check'],
            [dates.length, 'Total training days', 'calendar-days'],
            [records.length, 'Completed workouts', 'check-circle']
        ];
        stats.innerHTML = statsData.map(([value, label]) => `<div class="training-stat"><strong>${value}</strong><span>${label}</span></div>`).join('');
        const weekly = [];
        for (let index = 7; index >= 0; index--) {
            const end = new Date(today);
            end.setDate(today.getDate() - index * 7);
            const start = new Date(end);
            start.setDate(end.getDate() - 6);
            weekly.push({
                label: `${start.getDate()}/${start.getMonth() + 1}`,
                count: dates.filter(date => {
                    const value = new Date(`${date}T12:00:00`);
                    return value >= start && value <= end;
                }).length
            });
        }
        const weeks = document.querySelector('#training-progress-weeks');
        if (!weeks) return;
        const maxCount = Math.max(3, ...weekly.map(x => x.count));
        weeks.innerHTML = `<div class="training-chart-y"><span>${maxCount}</span><span>${Math.round(maxCount / 2)}</span><span>0</span></div><div class="training-chart-plot"><div class="training-chart-grid"></div>${weekly.map(x => `<div class="training-chart-column"><strong>${x.count}</strong><div class="training-chart-bar" style="height:${x.count / maxCount * 100}%" title="${x.count} training days"></div><small>${x.label}</small></div>`).join('')}</div>`;
        const category = document.querySelector('#training-progress-category');
        const topic = document.querySelector('#training-progress-topic');
        const select = document.querySelector('#training-progress-exercise');
        if (!category || !topic || !select) return;
        const previousCategory = category.value || 'full';
        const previousTopic = topic.value;
        const previous = select.value;
        const snapshots = Object.values(history()).filter(x => x && Array.isArray(x.exercises));
        const groups = [
            ['full', 'Full Body'], ['custom', 'My other programmes']
        ];
        category.replaceChildren(...groups.filter(([key]) => programs[key]?.length).map(([key, title]) => new Option(title, key)));
        category.value = groups.some(([key]) => key === previousCategory && programs[key]?.length)
            ? previousCategory : category.options[0]?.value || '';
        const boxes = programs[category.value] || [];
        topic.replaceChildren(...boxes.map(box => new Option(box.title, box.id)));
        if (boxes.some(box => box.id === previousTopic)) topic.value = previousTopic;
        const box = boxes.find(item => item.id === topic.value);
        const names = (box?.exercises || []).map((item, index) => ({name: item.name, index})).filter(item => item.name);
        select.replaceChildren(new Option('Choose an exercise', ''), ...names.map(item => new Option(item.name, String(item.index))));
        if (names.some(item => String(item.index) === previous)) select.value = previous;
        const strength = document.querySelector('#training-progress-strength');
        const selectedIndex = Number(select.value);
        const selected = select.value !== '' ? box?.exercises?.[selectedIndex]?.name : null;
        strength.replaceChildren();
        if (!selected) {
            strength.textContent = 'Choose an exercise from your training programme.';
            return;
        }
        const values = snapshots.filter(x => x.date && x.boxId === box?.id)
            .flatMap(x => x.exercises.filter(e => e.name === selected && Number(e.kg) > 0).map(e => ({date: x.date, ...e})))
            .sort((a, b) => a.date.localeCompare(b.date));
        if (!values.length) {
            const matching = box?.exercises?.[selectedIndex];
            const note = document.createElement('p');
            note.className = 'muted';
            note.textContent = 'No training history for this exercise yet. New completed workouts will be logged here.';
            strength.append(note);
            if (matching) {
                const current = document.createElement('p');
                current.className = 'training-current-exercise';
                current.textContent = `Current programme: ${matching.sets || 0} sets × ${matching.reps || 0} reps · ${matching.kg || 0} kg`;
                strength.append(current);
            }
            return;
        }
        const series = values.slice(-16);
        const width = 740, height = 245, left = 48, right = 18, top = 18, bottom = 42;
        const plotW = width - left - right, plotH = height - top - bottom;
        const maximum = Math.max(5, ...series.map(x => Number(x.kg)));
        const yMax = Math.ceil(maximum / 5) * 5;
        const points = series.map((item, index) => ({
            x: left + (series.length === 1 ? plotW / 2 : index / (series.length - 1) * plotW),
            y: top + plotH * (1 - Number(item.kg) / yMax),
            item
        }));
        const svgNs = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(svgNs, 'svg');
        svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
        svg.setAttribute('role', 'img');
        svg.setAttribute('aria-label', `Progress in ${selected}, weight in kilograms`);
        const add = (tag, attrs, content) => {
            const el = document.createElementNS(svgNs, tag);
            for (const [key, value] of Object.entries(attrs)) el.setAttribute(key, String(value));
            if (content != null) el.textContent = String(content);
            svg.append(el);
            return el;
        };
        for (let i = 0; i <= 4; i++) {
            const y = top + i * plotH / 4;
            add('line', {x1: left, y1: y, x2: width - right, y2: y, class: 'training-line-grid'});
            add('text', {
                x: left - 10,
                y: y + 4,
                'text-anchor': 'end',
                class: 'training-line-label'
            }, Math.round(yMax * (1 - i / 4)));
        }
        if (points.length > 1) add('polyline', {
            points: points.map(p => `${p.x},${p.y}`).join(' '),
            class: 'training-line-path'
        });
        for (const p of points) {
            const circle = add('circle', {cx: p.x, cy: p.y, r: 5, class: 'training-line-point'});
            const title = document.createElementNS(svgNs, 'title');
            title.textContent = `${p.item.date}: ${p.item.kg} kg · ${p.item.sets} sets × ${p.item.reps} reps`;
            circle.append(title);
        }
        const labels = [...new Set([0, Math.floor((points.length - 1) / 2), points.length - 1])];
        for (const index of labels) {
            const p = points[index];
            add('text', {
                x: p.x,
                y: height - 12,
                'text-anchor': 'middle',
                class: 'training-line-label'
            }, p.item.date.slice(5));
        }
        const chart = document.createElement('div');
        chart.className = 'training-line-chart';
        chart.append(svg);
        const description = document.createElement('p');
        description.className = 'muted tiny';
        description.textContent = `${series.length} entries · latest ${series[series.length - 1].item.kg} kg · progress based on completed workouts`;
        strength.append(chart, description);
    };
    document.querySelector('#training-progress-category')?.addEventListener('change', () => {
        if (document.querySelector('#training-progress-topic')) document.querySelector('#training-progress-topic').value = '';
        if (document.querySelector('#training-progress-exercise')) document.querySelector('#training-progress-exercise').value = '';
        renderProgress();
    });
    document.querySelector('#training-progress-topic')?.addEventListener('change', () => {
        if (document.querySelector('#training-progress-exercise')) document.querySelector('#training-progress-exercise').value = '';
        renderProgress();
    });
    document.querySelector('#training-progress-exercise')?.addEventListener('change', renderProgress);

    const selectProgram = document.querySelector('#training-program-tabs');
    const addProgram = document.querySelector('#training-add-program');
    let saveVersion = 0;
    const persist = async () => {
        const version = ++saveVersion;
        setText('#save-message', 'Saving training programme…');
        const saved = await write(PROGRAM_KEY, programs);
        if (version !== saveVersion) return;
        setText('#save-message', saved ? '✓ Training programme saved to SQLite.' : 'Could not save the training programme.');
    };
    const collect = () => {
        if (active === 'progress') return;
        root.querySelectorAll('[data-box-id]').forEach(section => {
            const box = programs[active]?.find(x => x.id === section.dataset.boxId);
            if (!box) return;
            box.title = section.querySelector('[data-title]').value.trim() || 'New programme';
            box.subtitle = section.querySelector('[data-subtitle]').value.trim();
            box.exercises = [...section.querySelectorAll('[data-exercise-index]')].map(row => {
                const old = box.exercises[Number(row.dataset.exerciseIndex)] || {};
                const get = key => row.querySelector(`[data-field="${key}"]`)?.value || '';
                return {
                    ...old, name: get('name').trim(), description: get('description').trim(), type: get('type'),
                    sets: Math.max(0, Number(get('sets')) || 0), reps: get('reps').trim(),
                    kg: Math.max(0, Number(get('kg')) || 0), minutes: Math.max(0, Number(get('minutes')) || 0)
                };
            });
        });
    };
    const draw = () => {
        selectProgram.querySelectorAll('[data-program]').forEach(button => {
            const selected = button.dataset.program === active;
            button.classList.toggle('active', selected);
            button.setAttribute('aria-selected', String(selected));
        });
        const progressMode = active === 'progress';
        document.querySelector('#training-progress').hidden = !progressMode;
        root.hidden = progressMode;
        document.querySelector('.training-program-actions').hidden = progressMode;
        document.querySelector('.training-footer').hidden = progressMode;
        if (progressMode) {
            renderProgress();
            return;
        }
        const done = read(DONE_KEY, {});
        root.innerHTML = programs[active].map((box, index) => {
            const date = localDate();
            const doneKey = `${date}:${box.id}`;
            const rows = box.exercises.map((x, i) => {
                const cardio = x.type === 'cardio';
                return `<article class="training-exercise-card" data-exercise-index="${i}">
                    <div class="training-exercise-media">${x.image ? `<img src="${esc(x.image)}" alt="${esc(x.name)}" loading="lazy" referrerpolicy="no-referrer" onerror="this.style.opacity=0.25;this.title='Image could not be loaded'"><span class="training-image-empty" hidden>Image could not be loaded – upload your own image</span>` : '<div class="training-image-empty">No image</div>'}
                        <label class="training-image-upload">${x.image ? 'Change image' : '+ Add image'}<input type="file" accept="image/jpeg,image/png,image/webp" data-image-upload hidden></label>
                        ${x.image ? '<button type="button" class="training-image-remove" data-image-remove>Remove image</button>' : ''}</div>
                    <div class="training-exercise-content"><div class="training-exercise-heading"><input data-field="name" aria-label="Exercise name" value="${esc(x.name || '')}"><div class="training-exercise-tools"><button type="button" class="btn secondary" data-exercise-move="up" title="Move up" ${i === 0 ? 'disabled' : ''}>↑</button><button type="button" class="btn secondary" data-exercise-move="down" title="Move down" ${i === box.exercises.length - 1 ? 'disabled' : ''}>↓</button><button type="button" class="btn danger" data-remove-exercise aria-label="Delete exercise">×</button></div></div>
                        <textarea data-field="description" rows="2" placeholder="Brief exercise description">${esc(x.description || '')}</textarea>
                        <div class="training-exercise-inputs ${cardio ? 'is-cardio' : ''}"><label>Type<select data-field="type"><option value="strength" ${!cardio ? 'selected' : ''}>Styrke</option><option value="cardio" ${cardio ? 'selected' : ''}>Cardio</option></select></label>
                        <label class="training-strength-field">Sets<input data-field="sets" type="number" min="0" step="1" value="${Number(x.sets) || 0}"></label>
                        <label class="training-strength-field">Reps<input data-field="reps" type="text" value="${esc(String(x.reps ?? ''))}"></label>
                        <label class="training-strength-field">Kg<input data-field="kg" type="number" min="0" step="0.5" value="${Number(x.kg) || 0}"></label>
                        <label class="training-cardio-field">Minutter<input data-field="minutes" type="number" min="0" step="1" value="${Number(x.minutes) || 0}"></label></div></div></article>`;
            }).join('');
            return `<section class="panel training-edit-box" data-box-id="${esc(box.id)}"><div class="training-title"><div class="training-box-heading"><label>Name<input data-title value="${esc(box.title)}"></label><label>Focus / description<input data-subtitle value="${esc(box.subtitle || '')}"></label></div><div class="training-box-controls"><button type="button" class="btn secondary" data-move="up" ${index === 0 ? 'disabled' : ''}>↑</button><button type="button" class="btn secondary" data-move="down" ${index === programs[active].length - 1 ? 'disabled' : ''}>↓</button><button type="button" class="btn danger" data-remove-box>Delete programme</button><label><input class="check" type="checkbox" data-done="${doneKey}" ${done[doneKey] ? 'checked' : ''}> Completed</label></div></div><div class="training-exercise-list">${rows}</div><button type="button" class="btn secondary" data-add-exercise>+ Add exercise / cardio</button></section>`;
        }).join('') || '<p class="muted">No programmes yet. Press + Add programme.</p>';
        root.querySelectorAll('[data-box-id]').forEach(section => {
            const id = section.dataset.boxId;
            const find = () => programs[active].findIndex(box => box.id === id);
            section.querySelector('[data-add-exercise]').addEventListener('click', () => {
                collect();
                programs[active][find()].exercises.push({
                    ...exercise('New exercise'),
                    description: '',
                    image: '',
                    type: 'strength',
                    minutes: 0
                });
                persist();
                draw();
            });
            section.querySelectorAll('[data-exercise-move]').forEach(button => button.addEventListener('click', () => {
                collect();
                const list = programs[active][find()].exercises;
                const index = Number(button.closest('[data-exercise-index]').dataset.exerciseIndex);
                const other = index + (button.dataset.exerciseMove === 'up' ? -1 : 1);
                if (other < 0 || other >= list.length) return;
                [list[index], list[other]] = [list[other], list[index]];
                persist();
                draw();
            }));
            section.querySelectorAll('[data-field="type"]').forEach(input => input.addEventListener('change', () => {
                const group = input.closest('.training-exercise-inputs');
                group.classList.toggle('is-cardio', input.value === 'cardio');
                collect();
                persist();
            }));
            section.querySelectorAll('[data-remove-exercise]').forEach(button => button.addEventListener('click', () => {
                collect();
                programs[active][find()].exercises.splice(Number(button.closest('[data-exercise-index]').dataset.exerciseIndex), 1);
                persist();
                draw();
            }));
            section.querySelectorAll('[data-image-upload]').forEach(input => input.addEventListener('change', async () => {
                const file = input.files?.[0];
                if (!file) return;
                if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 8 * 1024 * 1024) {
                    alert('Choose a JPG, PNG or WebP image no larger than 8 MB.');
                    return;
                }
                const img = new Image();
                const url = URL.createObjectURL(file);
                try {
                    await new Promise((resolve, reject) => {
                        img.onload = resolve;
                        img.onerror = reject;
                        img.src = url;
                    });
                    const canvas = document.createElement('canvas');
                    const scale = Math.min(1, 520 / Math.max(img.naturalWidth, img.naturalHeight));
                    canvas.width = Math.max(1, Math.round(img.naturalWidth * scale));
                    canvas.height = Math.max(1, Math.round(img.naturalHeight * scale));
                    canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
                    const data = canvas.toDataURL('image/webp', .75);
                    collect();
                    const selectedExercise = programs[active][find()].exercises[Number(input.closest('[data-exercise-index]').dataset.exerciseIndex)];
                    const imageKey = `user-${crypto.randomUUID()}`;
                    const blob = await (await fetch(data)).blob();
                    const payload = new FormData();
                    payload.append('file', blob, 'exercise.webp');
                    const upload = await fetch(`/api/training-images/${imageKey}`, {method: 'POST', body: payload});
                    if (!upload.ok) throw new Error('Could not save the image to SQLite.');
                    selectedExercise.image = `/api/training-images/${imageKey}`;
                    selectedExercise.imageKey = imageKey;
                    selectedExercise.imageRemoved = false;
                    await persist();
                    draw();
                } catch (error) {
                    console.error(error);
                    alert('Image could not be loaded.');
                } finally {
                    URL.revokeObjectURL(url);
                }
            }));
            section.querySelectorAll('[data-image-remove]').forEach(button => button.addEventListener('click', () => {
                collect();
                const selectedExercise = programs[active][find()].exercises[Number(button.closest('[data-exercise-index]').dataset.exerciseIndex)];
                selectedExercise.image = '';
                selectedExercise.imageRemoved = true;
                persist();
                draw();
            }));
            section.querySelector('[data-remove-box]').addEventListener('click', () => {
                if (!confirm('Delete this training programme and its exercises?')) return;
                collect();
                programs[active].splice(find(), 1);
                persist();
                draw();
            });
            section.querySelectorAll('[data-move]').forEach(button => button.addEventListener('click', () => {
                collect();
                const index = find(), other = index + (button.dataset.move === 'up' ? -1 : 1);
                if (other < 0 || other >= programs[active].length) return;
                [programs[active][index], programs[active][other]] = [programs[active][other], programs[active][index]];
                persist();
                draw();
            }));
            section.querySelectorAll('[data-title],[data-subtitle],[data-field]').forEach(input => input.addEventListener('change', () => {
                collect();
                persist();
            }));
            section.querySelectorAll('[data-done]').forEach(input => input.addEventListener('change', () => {
                collect();
                const all = read(DONE_KEY, {});
                all[input.dataset.done] = input.checked;
                write(DONE_KEY, all);
                const snapshots = history();
                if (input.checked) {
                    const box = programs[active].find(x => x.id === id);
                    snapshots[input.dataset.done] = {
                        date: input.dataset.done.slice(0, 10),
                        boxId: id,
                        title: box?.title || '',
                        exercises: structuredClone(box?.exercises || []).map(({image, ...rest}) => rest)
                    };
                } else {
                    delete snapshots[input.dataset.done];
                }
                write(HISTORY_KEY, snapshots);
                renderProgress();
            }));
        });
    };
    selectProgram.querySelectorAll('[data-program]').forEach(button => button.addEventListener('click', () => {
        if (active !== 'progress') {
            collect();
            persist();
        }
        active = button.dataset.program;
        if (!['full', 'custom', 'progress'].includes(active)) active = 'full';
        draw();
    }));
    const createProgram = () => {
        if (active !== 'progress') collect();
        if (active === 'progress') active = 'custom';
        programs[active].push({
            id: `custom-${Date.now()}-${Math.floor(Math.random() * 100000)}`,
            title: 'New programme',
            subtitle: '',
            exercises: []
        });
        persist();
        draw();
    };
    addBox.addEventListener('click', createProgram);
    addProgram?.addEventListener('click', () => {
        if (active !== 'progress') {
            collect();
            persist();
        }
        active = 'custom';
        createProgram();
    });
    saveButton.addEventListener('click', () => {
        collect();
        persist();
    });
    draw();
    renderProgress();
    // Store the initial A/B/C program and its image URLs in momentum.db.
    if (!read(PROGRAM_KEY, null)) {
        persist();
    }
}

export {renderTraining};
