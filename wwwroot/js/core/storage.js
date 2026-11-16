import {localDate} from "./utils.js";

const FOOD_KEY = 'sunhed.food.v1', WORKOUT_KEY = 'sunhed.workouts.v1', DONE_KEY = 'sunhed.done.v1',
    PROFILE_KEY = 'sunhed.profile.v1', GOAL_KEY = 'sunhed.goal.v1', CHEAT_KEY = 'sunhed.cheat-days.v1',
    SICK_KEY = 'sunhed.sick-days.v1', FIBER_START_KEY = 'sunhed.fiber-start.v1';

const state = Object.create(null);
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
        const response = await fetch('/api/momentum-data/' + encodeURIComponent(key), {
            method: 'PUT', headers: {'Content-Type': 'application/json'}, body
        });
        if (!response.ok) {
            const details = await response.json().catch(() => ({}));
            throw new Error('SQLite could not save the change (' + response.status + ').' +
                (details.message ? ' ' + details.message : ''));
        }
        return true;
    }).catch(error => {
        console.error('SQLite-lagring fejlede:', error);
        showDataError('Could not save changes to SQLite. Check the Rider log and resolve the error before reloading the page.');
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
        throw new Error('Could not load data from SQLite (' + response.status + ').');
    }
    Object.assign(state, await response.json());
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
