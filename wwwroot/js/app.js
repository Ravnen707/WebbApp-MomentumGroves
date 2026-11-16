import {initializeData, showDataError} from './core/storage.js';
import {localDate} from './core/utils.js';
import {drawChart, renderCalorieBalance, renderHome} from './home/dashboard.js?v=20261113-kiss-import-fix2';
import {renderFood} from './food/food-diary.js';
import {renderTraining} from './tracking/training-programs.js';
import {renderCalculator} from './tracking/nutrition-targets.js?v=20261113-kiss-import-fix2';
import './food/meal-editor.js';

function setupNavigation() {
    const today = document.querySelector('#today');
    if (today) {
        today.textContent = new Date().toLocaleDateString('en-GB', {
            weekday: 'long', day: 'numeric', month: 'long', year: 'numeric'
        });
    }
    document.querySelectorAll('nav a').forEach(link => {
        if (link.pathname === location.pathname) {
            link.classList.add('active');
        }
    });
}

function setupInfoDialogs() {
    document.querySelectorAll('[data-open-nutrition-zones]').forEach(button => {
        button.addEventListener('click', () => document.querySelector('#nutrition-zones-dialog')?.showModal());
    });
    document.querySelectorAll('[data-open-meals]').forEach(button => {
        button.addEventListener('click', () => document.querySelector('#home-meals-dialog')?.showModal());
    });
    document.querySelectorAll('[data-close-dialog]').forEach(button => {
        button.addEventListener('click', () => button.closest('dialog')?.close());
    });
}

async function start() {
    setupNavigation();
    try {
        await initializeData();
    } catch (error) {
        console.error('Could not initialise SQLite:', error);
        showDataError('Could not load SQLite data. No entries have been changed. ' + error.message);
        return;
    }
    try {
        document.querySelector('#week-chart-metric')?.addEventListener('change', drawChart);
        document.querySelector('#balance-metric')?.addEventListener('change', renderCalorieBalance);
        renderHome();
        renderFood();
        setupInfoDialogs();
        renderTraining();
        renderCalculator();
    } catch (error) {
        console.error('Could not display the page:', error);
        showDataError('Error displaying the page: ' + error.message);
    }
}

start();
