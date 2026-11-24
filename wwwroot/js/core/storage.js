import {localDate} from "./utils.js";

const FOOD_KEY = 'sunhed.food.v1', WORKOUT_KEY = 'sunhed.workouts.v1', DONE_KEY = 'sunhed.done.v1',
    PROFILE_KEY = 'sunhed.profile.v1', GOAL_KEY = 'sunhed.goal.v1', CHEAT_KEY = 'sunhed.cheat-days.v1',
    SICK_KEY = 'sunhed.sick-days.v1', FIBER_START_KEY = 'sunhed.fiber-start.v1';

const state = Object.create(null);
const trainingKey = 'sunhed.training-programs.v3';
let savedTrainingWeights = {};
function trainingWeights(programmes) {
    return Object.fromEntries((programmes?.groups ?? []).flatMap(group =>
        (group.programmes ?? []).flatMap(programme => (programme.exercises ?? [])
            .filter(exercise => exercise?.id)
            .map(exercise => [exercise.id, Number(exercise.kg) || 0]))));
}
let pendingSave = Promise.resolve();
let dataReady = false;
const read = (key, fallback) => Object.prototype.hasOwnProperty.call(state, key) ? state[key] : fallback;
const write = (key, value) => {
    if (!dataReady) {
        throw new Error('The database is not ready.');
    }
    state[key] = structuredClone(value);
    const body = JSON.stringify(state[key]);
    pendingSave = pendingSave.catch(() => {
    }).then(async () => {
        const training = key === trainingKey;
        const response = await fetch(training ? '/api/training/programmes' : '/api/momentum-data/' + encodeURIComponent(key), {
            method: 'PUT', headers: {'Content-Type': 'application/json'},
            body: training ? JSON.stringify({programmes: JSON.parse(body), originalWeights: savedTrainingWeights}) : body
        });
        if (training && response.status === 409) {
            const details = await response.json();
            showDataError(details.message);
            return false;
        }
        if (!response.ok) {
            const details = await response.json().catch(() => ({}));
            throw new Error('The database could not save the change (' + response.status + ').' +
                (details.message ? ' ' + details.message : ''));
        }
        if (training) savedTrainingWeights = trainingWeights(JSON.parse(body));
        return true;
    }).catch(error => {
        console.error('Database save failed:', error);
        showDataError('Could not save changes to the database. Check the Rider log and resolve the error before reloading the page.');
        return false;
    });
    return pendingSave;
};

function showDataError(message) {
    let box = document.querySelector('#momentum-data-error');
    if (!box) {
        box = document.createElement('div');
        box.id = 'momentum-data-error';
        box.setAttribute('role', 'alert');
        box.style.cssText = 'position:sticky;top:0;z-index:1000;background:#5a3335;color:white;padding:12px 20px;text-align:center';
        document.body.prepend(box);
    }
    box.textContent = message;
}

async function initializeData() {
    const response = await fetch('/api/momentum-data', {cache: 'no-store'});
    if (!response.ok) {
        throw new Error('Could not load data from the database (' + response.status + ').');
    }
    Object.assign(state, await response.json());
    savedTrainingWeights = trainingWeights(state[trainingKey]);
    dataReady = true;
    if (!read(FIBER_START_KEY, null)) {
        const start = localDate();
        const saved = await fetch('/api/momentum-data/' + encodeURIComponent(FIBER_START_KEY), {
            method: 'PUT', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(start)
        });
        if (!saved.ok) throw new Error('Could not save the fibre start date.');
        state[FIBER_START_KEY] = start;
    }
}


export {
    FOOD_KEY,
    state,
    pendingSave,
    dataReady,
    read,
    write,
    showDataError,
    initializeData,
    WORKOUT_KEY,
    DONE_KEY,
    PROFILE_KEY,
    GOAL_KEY,
    CHEAT_KEY,
    SICK_KEY,
    FIBER_START_KEY
};
