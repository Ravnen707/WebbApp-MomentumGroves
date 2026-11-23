import {DONE_KEY, read, write} from "./storage.js";
import {localDate, monday, shift} from "./utils.js";

export const PROGRAM_KEY = 'sunhed.training-programs.v3';
export const HISTORY_KEY = 'sunhed.training-history.v1';

export function readTrainingProgrammes() {
    const saved = read(PROGRAM_KEY, null) ?? read('sunhed.training-programs.v2', null);
    if (Array.isArray(saved?.groups)) return saved;

    const groups = [];
    if (Array.isArray(saved?.full) && saved.full.length) {
        groups.push({id: 'full', name: 'My programmes', programmes: saved.full});
    }
    if (Array.isArray(saved?.custom) && saved.custom.length) {
        groups.push({id: 'custom', name: 'My other programmes', programmes: saved.custom});
    }
    return {groups, dashboardGroupId: groups.find(group => group.id === 'full')?.id ?? null};
}

function weeklyCompletionKeys(date) {
    const start = monday(date);
    const nextWeek = shift(start, 7);
    return Object.entries(read(DONE_KEY, {}))
        .filter(([key, completed]) => {
            const day = key.slice(0, 10);
            return completed && /^\d{4}-\d{2}-\d{2}:/.test(key) && day >= start && day < nextWeek;
        })
        .map(([key]) => key);
}

export function completedProgrammesThisWeek(date = localDate()) {
    return new Set(weeklyCompletionKeys(date).map(key => key.slice(11)));
}

export function setProgrammeCompleted(programme, completed, date = localDate()) {
    const keys = weeklyCompletionKeys(date).filter(key => key.slice(11) === String(programme.id));
    if (completed ? keys.length > 0 : keys.length === 0) return;

    const done = {...read(DONE_KEY, {})};
    const history = {...read(HISTORY_KEY, {})};
    if (completed) {
        const key = `${date}:${programme.id}`;
        done[key] = true;
        history[key] = {
            date,
            boxId: programme.id,
            title: programme.title || '',
            exercises: (programme.exercises || []).map(({image, ...exercise}) => exercise)
        };
    } else {
        for (const key of keys) {
            delete done[key];
            delete history[key];
        }
    }
    write(DONE_KEY, done);
    write(HISTORY_KEY, history);
}
