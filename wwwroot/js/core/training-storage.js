import {DONE_KEY, read, write} from "./storage.js";

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

export function setProgrammeCompleted(key, programme, completed) {
    const done = {...read(DONE_KEY, {}), [key]: completed};
    write(DONE_KEY, done);

    const history = {...read(HISTORY_KEY, {})};
    if (completed) {
        history[key] = {
            date: key.slice(0, 10),
            boxId: programme.id,
            title: programme.title || '',
            exercises: (programme.exercises || []).map(({image, ...exercise}) => exercise)
        };
    } else {
        delete history[key];
    }
    write(HISTORY_KEY, history);
}
