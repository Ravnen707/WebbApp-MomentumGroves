import {read, write, DONE_KEY} from "../core/storage.js";
import {esc, setText, localDate, shift, monday} from "../core/utils.js";
import {selectedDate} from "../core/selected-date.js";
import {PROGRAM_KEY, HISTORY_KEY, completedProgrammesThisWeek, readTrainingProgrammes, setProgrammeCompleted} from "../core/training-storage.js?v=weekly-completions";

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

function renderTraining() {
    const root = document.querySelector('#training-days');
    if (!root || root.dataset.initialized === 'true') return;
    root.dataset.initialized = 'true';
    const tabs = document.querySelector('#training-program-tabs');
    const addBox = document.querySelector('#training-add-box');
    const saveButton = document.querySelector('#save-workouts');
    const settings = structuredClone(readTrainingProgrammes());
    const {groups} = settings;
    groups.forEach(group => {
        group.programmes = group.programmes.filter(programme => programme?.id);
        group.programmes.forEach(programme => {
            programme.exercises ??= [];
            programme.exercises.forEach(item => {
                if (!item.image?.startsWith('/TrainingImages/')) item.image = '';
                delete item.imageKey;
            });
        });
    });
    let active = groups[0]?.id ?? null;
    const currentGroup = () => groups.find(group => group.id === active);
    const history = () => read(HISTORY_KEY, {});
    const renderProgress = () => {
        const done = read(DONE_KEY, {});
        const records = Object.entries(done).filter(([key, value]) => value && /^\d{4}-\d{2}-\d{2}:/.test(key));
        const dates = [...new Set(records.map(([key]) => key.slice(0, 10)))];
        const todayKey = localDate();
        const today = new Date(`${todayKey}T12:00:00`);
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
        const currentMonday = monday(todayKey);
        const weekly = [];
        for (let index = 7; index >= 0; index--) {
            const start = shift(currentMonday, -index * 7);
            const end = shift(start, 6);
            // The Thursday determines the ISO week-year. UTC keeps the week number independent of DST.
            const thursday = new Date(shift(start, 3));
            const yearStart = Date.UTC(thursday.getUTCFullYear(), 0, 1);
            const weekNumber = 1 + Math.floor((thursday.getTime() - yearStart) / (7 * 86400000));
            weekly.push({
                label: `W-${weekNumber}`,
                range: `${start} to ${end}`,
                count: dates.filter(date => date >= start && date <= end && date <= todayKey).length
            });
        }
        const weeks = document.querySelector('#training-progress-weeks');
        if (!weeks) return;
        const maxCount = Math.max(3, ...weekly.map(x => x.count));
        weeks.innerHTML = `<div class="training-chart-y"><span>${maxCount}</span><span>${Math.round(maxCount / 2)}</span><span>0</span></div><div class="training-chart-plot"><div class="training-chart-grid"></div>${weekly.map(x => `<div class="training-chart-column" title="${x.range} · ${x.count} training days"><strong>${x.count}</strong><div class="training-chart-bar" style="height:${x.count / maxCount * 100}%"></div><small>${x.label}</small></div>`).join('')}</div>`;
        const category = document.querySelector('#training-progress-category');
        const topic = document.querySelector('#training-progress-topic');
        const select = document.querySelector('#training-progress-exercise');
        if (!category || !topic || !select) return;
        const previousCategory = category.value || active;
        const previousTopic = topic.value;
        const previous = select.value;
        const snapshots = Object.values(history()).filter(x => x && Array.isArray(x.exercises));
        const available = groups.filter(group => group.programmes.length);
        category.replaceChildren(...available.map(group => new Option(group.name, group.id)));
        const group = available.find(item => item.id === previousCategory) || available[0];
        category.value = group?.id ?? '';
        const boxes = group?.programmes ?? [];
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

    const renameGroup = document.querySelector('#training-rename-group');
    const dashboardGroup = document.querySelector('#training-dashboard-group');
    let saveVersion = 0;
    const persist = async (successMessage = '✓ Training programmes saved.') => {
        const version = ++saveVersion;
        setText('#save-message', 'Saving training programme…');
        const saved = await write(PROGRAM_KEY, settings);
        if (version !== saveVersion) return;
        setText('#save-message', saved ? successMessage : 'Could not save the training programme.');
    };
    const collect = () => {
        const group = currentGroup();
        if (!group) return;
        root.querySelectorAll('[data-box-id]').forEach(section => {
            const box = group.programmes.find(x => x.id === section.dataset.boxId);
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
    const imageDialog = document.querySelector('#training-image-dialog');
    const imageGallery = imageDialog.querySelector('[data-image-gallery]');
    const imageStatus = imageDialog.querySelector('[data-image-status]');
    const imageInput = imageDialog.querySelector('[data-image-file]');
    const pickImage = imageDialog.querySelector('[data-pick-image]');
    const uploadImage = imageDialog.querySelector('[data-upload-image]');
    const closeImage = imageDialog.querySelector('[data-close-image]');
    let imageTarget;
    let imageBusy = false;

    const runImageAction = async action => {
        imageBusy = true;
        pickImage.disabled = uploadImage.disabled = closeImage.disabled = true;
        try {
            await action();
        } finally {
            imageBusy = false;
            pickImage.disabled = uploadImage.disabled = closeImage.disabled = false;
        }
    };
    const setExerciseImage = image => {
        imageTarget.image = image.url;
        imageTarget.imageRemoved = false;
        imageDialog.close();
        persist();
        draw();
    };

    closeImage.addEventListener('click', () => imageDialog.close());
    imageDialog.addEventListener('cancel', event => {
        if (imageBusy) event.preventDefault();
    });
    pickImage.addEventListener('click', () => runImageAction(async () => {
        imageGallery.replaceChildren();
        imageStatus.textContent = 'Loading pictures…';
        try {
            const response = await fetch('/api/training-images', {cache: 'no-store'});
            if (!response.ok) throw new Error();
            const images = await response.json();
            imageStatus.textContent = images.length ? '' : 'No pictures are available yet.';
            imageGallery.innerHTML = images.map(image => `
                <button type="button" class="btn secondary">
                    <img src="${esc(image.url)}" alt="" loading="lazy">
                    <span>${esc(image.name)}</span>
                </button>`).join('');
            [...imageGallery.children].forEach((button, index) => {
                button.addEventListener('click', () => {
                    if (!imageBusy) setExerciseImage(images[index]);
                });
            });
            imageGallery.querySelector('button')?.focus();
        } catch {
            imageStatus.textContent = 'Could not load pictures. Try again.';
        }
    }));
    uploadImage.addEventListener('click', () => imageInput.click());
    imageInput.addEventListener('change', () => {
        const file = imageInput.files?.[0];
        imageInput.value = '';
        if (!file) return;
        if (!file.size || file.size > 8_000_000) {
            imageStatus.textContent = 'Choose a JPG, PNG or WebP image no larger than 8 MB.';
            return;
        }

        runImageAction(async () => {
            imageStatus.textContent = 'Uploading picture…';
            try {
                const payload = new FormData();
                payload.append('file', file);
                const response = await fetch('/api/training-images', {method: 'POST', body: payload});
                if (response.status === 400 || response.status === 413) {
                    imageStatus.textContent = 'Choose a JPG, PNG or WebP image no larger than 8 MB.';
                    return;
                }
                if (!response.ok) throw new Error();
                setExerciseImage(await response.json());
            } catch {
                imageStatus.textContent = 'Could not upload the picture. Try again.';
            }
        });
    });
    const draw = () => {
        tabs.innerHTML = [...groups, {id: 'progress', name: 'Training progress'}].map(group => {
            const selected = group.id === active;
            return `<button type="button" class="btn secondary${selected ? ' active' : ''}" role="tab" data-program="${esc(group.id)}" aria-selected="${selected}">${esc(group.name)}</button>`;
        }).join('');
        const progressMode = active === 'progress';
        document.querySelector('#training-progress').hidden = !progressMode;
        root.hidden = progressMode;
        const group = currentGroup();
        document.querySelector('.training-program-actions').hidden = !group;
        document.querySelector('.training-footer').hidden = !group;
        if (progressMode) {
            renderProgress();
            return;
        }
        if (!group) {
            root.innerHTML = '<p class="muted">Create a programme group to get started.</p>';
            return;
        }
        const programmes = group.programmes;
        dashboardGroup.setAttribute('aria-pressed', String(group.id === settings.dashboardGroupId));
        const completed = completedProgrammesThisWeek(selectedDate());
        root.innerHTML = programmes.map((box, index) => {
            const rows = box.exercises.map((x, i) => {
                const cardio = x.type === 'cardio';
                return `<article class="training-exercise-card" data-exercise-index="${i}">
                    <div class="training-exercise-media">${x.image ? `<img src="${esc(x.image)}" alt="${esc(x.name)}" loading="lazy" referrerpolicy="no-referrer" onerror="this.style.opacity=0.25;this.title='Image could not be loaded'"><span class="training-image-empty" hidden>Image could not be loaded – upload your own image</span>` : '<div class="training-image-empty">No image</div>'}
                        <button type="button" class="training-image-upload" data-image-picker>${x.image ? 'Change image' : '+ Add image'}</button>
                        ${x.image ? '<button type="button" class="training-image-remove" data-image-remove>Remove image</button>' : ''}</div>
                    <div class="training-exercise-content"><div class="training-exercise-heading"><input data-field="name" aria-label="Exercise name" value="${esc(x.name || '')}"><div class="training-exercise-tools"><button type="button" class="btn secondary" data-exercise-move="up" title="Move up" ${i === 0 ? 'disabled' : ''}>↑</button><button type="button" class="btn secondary" data-exercise-move="down" title="Move down" ${i === box.exercises.length - 1 ? 'disabled' : ''}>↓</button><button type="button" class="btn danger" data-remove-exercise aria-label="Delete exercise">×</button></div></div>
                        <textarea data-field="description" rows="2" placeholder="Brief exercise description">${esc(x.description || '')}</textarea>
                        <div class="training-exercise-inputs ${cardio ? 'is-cardio' : ''}"><label>Type<select data-field="type"><option value="strength" ${!cardio ? 'selected' : ''}>Styrke</option><option value="cardio" ${cardio ? 'selected' : ''}>Cardio</option></select></label>
                        <label class="training-strength-field">Sets<input data-field="sets" type="number" min="0" step="1" value="${Number(x.sets) || 0}"></label>
                        <label class="training-strength-field">Reps<input data-field="reps" type="text" value="${esc(String(x.reps ?? ''))}"></label>
                        <label class="training-strength-field">Kg<input data-field="kg" type="number" min="0" step="0.5" value="${Number(x.kg) || 0}"></label>
                        <label class="training-cardio-field">Minutter<input data-field="minutes" type="number" min="0" step="1" value="${Number(x.minutes) || 0}"></label></div></div></article>`;
            }).join('');
            return `<section class="panel training-edit-box" data-box-id="${esc(box.id)}"><div class="training-title"><div class="training-box-heading"><label>Name<input data-title value="${esc(box.title)}"></label><label>Focus / description<input data-subtitle value="${esc(box.subtitle || '')}"></label></div><div class="training-box-controls"><button type="button" class="btn secondary" data-move="up" ${index === 0 ? 'disabled' : ''}>↑</button><button type="button" class="btn secondary" data-move="down" ${index === programmes.length - 1 ? 'disabled' : ''}>↓</button><button type="button" class="btn danger" data-remove-box>Delete programme</button><label><input class="check" type="checkbox" data-done ${completed.has(String(box.id)) ? 'checked' : ''}> Completed</label></div></div><div class="training-exercise-list">${rows}</div><button type="button" class="btn secondary" data-add-exercise>+ Add exercise / cardio</button></section>`;
        }).join('') || '<p class="muted">No programmes yet. Press + Add programme.</p>';
        root.querySelectorAll('[data-box-id]').forEach(section => {
            const id = section.dataset.boxId;
            const find = () => programmes.findIndex(box => box.id === id);
            section.querySelector('[data-add-exercise]').addEventListener('click', () => {
                collect();
                programmes[find()].exercises.push({
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
                const list = programmes[find()].exercises;
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
                programmes[find()].exercises.splice(Number(button.closest('[data-exercise-index]').dataset.exerciseIndex), 1);
                persist();
                draw();
            }));
            section.querySelectorAll('[data-image-picker]').forEach(button => button.addEventListener('click', () => {
                collect();
                const index = Number(button.closest('[data-exercise-index]').dataset.exerciseIndex);
                imageTarget = programmes[find()].exercises[index];
                imageStatus.textContent = '';
                imageGallery.replaceChildren();
                imageInput.value = '';
                imageDialog.showModal();
            }));
            section.querySelectorAll('[data-image-remove]').forEach(button => button.addEventListener('click', () => {
                collect();
                const selectedExercise = programmes[find()].exercises[Number(button.closest('[data-exercise-index]').dataset.exerciseIndex)];
                selectedExercise.image = '';
                selectedExercise.imageRemoved = true;
                persist();
                draw();
            }));
            section.querySelector('[data-remove-box]').addEventListener('click', () => {
                if (!confirm('Delete this training programme and its exercises?')) return;
                collect();
                programmes.splice(find(), 1);
                persist();
                draw();
            });
            section.querySelectorAll('[data-move]').forEach(button => button.addEventListener('click', () => {
                collect();
                const index = find(), other = index + (button.dataset.move === 'up' ? -1 : 1);
                if (other < 0 || other >= programmes.length) return;
                [programmes[index], programmes[other]] = [programmes[other], programmes[index]];
                persist();
                draw();
            }));
            section.querySelectorAll('[data-title],[data-subtitle],[data-field]').forEach(input => input.addEventListener('change', () => {
                collect();
                persist();
            }));
            section.querySelectorAll('[data-done]').forEach(input => input.addEventListener('change', () => {
                collect();
                const programme = programmes.find(item => item.id === id);
                setProgrammeCompleted(programme, input.checked, selectedDate());
                renderProgress();
            }));
        });
    };
    tabs.addEventListener('click', event => {
        const button = event.target.closest('[data-program]');
        if (!button || button.dataset.program === active) return;
        if (currentGroup()) {
            collect();
            persist();
        }
        active = button.dataset.program;
        draw();
        tabs.querySelector('[aria-selected="true"]')?.focus();
    });

    addBox.addEventListener('click', () => {
        collect();
        currentGroup().programmes.push({
            id: crypto.randomUUID(),
            title: 'New programme',
            subtitle: '',
            exercises: []
        });
        persist();
        draw();
    });

    const groupDialog = document.querySelector('#training-group-dialog');
    const groupForm = groupDialog.querySelector('form');
    const groupName = groupForm.elements.groupName;
    let groupToRename;
    const openGroupDialog = (group = null) => {
        groupToRename = group;
        groupDialog.querySelector('h2').textContent = group ? 'Rename programme group' : 'New programme group';
        groupName.value = group?.name ?? '';
        groupName.setCustomValidity('');
        groupDialog.showModal();
        groupName.select();
    };
    document.querySelector('#training-add-group').addEventListener('click', () => openGroupDialog());
    renameGroup.addEventListener('click', () => openGroupDialog(currentGroup()));
    groupDialog.querySelector('[data-cancel-group]').addEventListener('click', () => groupDialog.close());
    groupName.addEventListener('input', () => groupName.setCustomValidity(''));
    groupForm.addEventListener('submit', event => {
        event.preventDefault();
        const name = groupName.value.trim();
        if (!name) {
            groupName.setCustomValidity('Enter a group name.');
            groupName.reportValidity();
            return;
        }
        collect();
        if (groupToRename) {
            groupToRename.name = name;
        } else {
            const group = {id: crypto.randomUUID(), name, programmes: []};
            groups.push(group);
            active = group.id;
        }
        groupDialog.close();
        persist();
        draw();
    });

    dashboardGroup.addEventListener('click', () => {
        collect();
        const group = currentGroup();
        settings.dashboardGroupId = group.id;
        dashboardGroup.setAttribute('aria-pressed', 'true');
        persist(`“${group.name}” will appear on the dashboard.`);
    });
    saveButton.addEventListener('click', () => {
        collect();
        persist();
    });
    document.addEventListener('momentum-date-change', () => {
        const completed = completedProgrammesThisWeek(selectedDate());
        root.querySelectorAll('[data-box-id]').forEach(section => {
            section.querySelector('[data-done]').checked = completed.has(section.dataset.boxId);
        });
    });
    draw();
    renderProgress();
}

export {renderTraining};
