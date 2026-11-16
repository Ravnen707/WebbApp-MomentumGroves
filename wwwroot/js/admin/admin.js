(() => {
    'use strict';

    const byId = id => document.getElementById(id);
    const checkButton = byId('check-button');
    const testButton = byId('test-button');
    const installButton = byId('install-button');
    const cancelButton = byId('cancel-button');
    const prompt = byId('update-prompt');
    const loading = byId('loading');
    let release = null;
    let busy = false;

    function describeRelease(result) {
        if (!result.latest) {
            return result.success ? "No DTU release identified." : "Could not determine the latest DTU release.";
        }
        const latest = result.latest;
        return `Latest version found: ${latest.version}\nTitle: ${latest.title}\nFile: ${latest.fileName}\nArticle ID: ${latest.articleId}\nInstalled version: ${result.installedVersion}\nUpdate available: ${result.updateAvailable ? "Yes" : "No"}`;
    }

    function setBusy(value) {
        busy = value;
        checkButton.disabled = value;
        testButton.disabled = value;
        installButton.disabled = value;
        cancelButton.disabled = value;
    }

    function status(message, error = false) {
        byId('status').textContent = message;
        byId('status').classList.toggle('error', error);
    }

    async function getJson(url, options) {
        const response = await fetch(url, {cache: 'no-store', ...options});
        if (!response.ok) {
            throw new Error(`The server responded with HTTP ${response.status}.`);
        }
        return response.json();
    }

    async function loadStatus() {
        try {
            const data = await getJson('/Admin/api/status');
            byId('installed-version').textContent = `Version ${data.version}`;
            byId('food-count').textContent = Number(data.foodCount).toLocaleString('en-GB');
        } catch (error) {
            status(`Could not read the local database: ${error.message}`, true);
        }
    }

    checkButton.addEventListener('click', async () => {
        if (busy) {
            return;
        }
        setBusy(true);
        release = null;
        prompt.hidden = true;
        status('Checking the latest version at DTU…');
        try {
            const result = await getJson('/Admin/api/check');
            status(`${result.message}\n${describeRelease(result)}`, !result.success);
            if (result.success && result.updateAvailable && result.latest) {
                release = result.latest;
                byId('update-description').textContent = `${release.fileName} (version ${release.version}). Installed version: ${result.installedVersion}.`;
                prompt.hidden = false;
            }
        } catch (error) {
            status(`Version check failed: ${error.message}`, true);
        } finally {
            setBusy(false);
        }
    });

    testButton.addEventListener('click', async () => {
        if (busy) {
            return;
        }
        setBusy(true);
        byId('test-result').textContent = 'Testing the connection to DTU…';
        try {
            const result = await getJson('/Admin/api/test');
            byId('test-result').textContent = `${result.message}\n${describeRelease(result)}`;
            byId('test-result').classList.toggle('error', !result.success);
        } catch (error) {
            byId('test-result').textContent = `Test failed: ${error.message}`;
            byId('test-result').classList.add('error');
        } finally {
            setBusy(false);
        }
    });

    cancelButton.addEventListener('click', () => {
        release = null;
        prompt.hidden = true;
        status('Installation cancelled. The database is unchanged.');
    });

    installButton.addEventListener('click', async () => {
        if (busy || !release) {
            return;
        }
        setBusy(true);
        prompt.hidden = true;
        loading.hidden = false;
        status('Downloading and importing the DTU Frida dataset…');
        try {
            const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]').value;
            const result = await getJson('/Admin/api/install', {
                method: 'POST',
                headers: {'Content-Type': 'application/json', 'RequestVerificationToken': token},
                body: JSON.stringify({articleId: release.articleId, version: release.version})
            });
            status(result.message, !result.success);
            if (result.success) {
                release = null;
                await loadStatus();
            }
        } catch (error) {
            status(`Installation failed: ${error.message}`, true);
        } finally {
            loading.hidden = true;
            setBusy(false);
        }
    });

    loadStatus();
})();
