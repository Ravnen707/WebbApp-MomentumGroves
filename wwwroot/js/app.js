import {initializeData, showDataError} from './core/storage.js';
import {setupDatePicker} from './core/selected-date.js';
import {drawChart, renderCalorieBalance, renderHome} from './home/dashboard.js?v=selected-date';
import {renderFood} from './food/food-diary.js?v=selected-date';
import {renderTraining} from './tracking/training-programs.js?v=selected-date';
import {renderCalculator} from './tracking/nutrition-targets.js?v=20261118-empty-calculator';
import './food/meal-editor.js';

function setupNavigation() {
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
    setupDatePicker();
    if (document.querySelector('#home-workouts, #food-date, #training-days, #weight-form')) {
        window.addEventListener('pageshow', event => {
            if (event.persisted) location.reload();
        });
    }
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
        document.addEventListener('momentum-date-change', renderHome);
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
