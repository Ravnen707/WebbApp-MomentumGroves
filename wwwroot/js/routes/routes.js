const mapElement = document.getElementById('route-map');
const status = document.getElementById('route-status');
// Start with a world view rather than publishing a personal starting location.
const map = L.map('route-map').setView([0, 0], 2);
const osm = L.tileLayer(mapElement.dataset.osmTileUrl, {attribution: '&copy; OpenStreetMap contributors', maxZoom: 19});
osm.addTo(map);
let topo = null;
const topoKey = mapElement.dataset.tracestrackKey;
// Tracestrack's documented raster URL is configured here; a key is required.
if (topoKey) {
    topo = L.tileLayer(`${mapElement.dataset.tracestrackTileUrl}?key=${encodeURIComponent(topoKey)}`, {
        attribution: '&copy; Tracestrack &copy; OpenStreetMap contributors',
        maxZoom: 19
    });
}
const startIcon = L.divIcon({
    className: 'route-start-marker',
    html: '<div class="route-start-marker-dot"></div>',
    iconSize: [24, 24],
    iconAnchor: [12, 12]
});
const marker = L.marker(map.getCenter(), {draggable: true, icon: startIcon}).addTo(map).bindPopup('Starting point');
let line = null;
let candidates = [];
let selected = -1;
const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
const byId = id => document.getElementById(id);
const show = message => {
    status.textContent = message;
};
const settings = () => ({
    latitude: marker.getLatLng().lat,
    longitude: marker.getLatLng().lng,
    distanceKm: Number(byId('route-distance').value),
    activity: byId('route-activity').value,
    environment: byId('route-environment').value
});
const request = async (url, method, data) => {
    const response = await fetch(url, {
        method,
        headers: {'Content-Type': 'application/json', 'RequestVerificationToken': token},
        body: data ? JSON.stringify(data) : undefined
    });
    if (!response.ok) {
        const result = await response.json().catch(() => ({}));
        throw new Error(result.error || `Server error (${response.status})`);
    }
    return response.status === 204 ? null : response.json();
};

function clearPreview() {
    if (line) {
        map.removeLayer(line);
        line = null;
    }
    candidates = [];
    selected = -1;
    byId('route-save-box').hidden = true;
    byId('route-candidates').replaceChildren();
}

function setStart(lat, lon) {
    marker.setLatLng([lat, lon]);
    map.setView([lat, lon], 14);
    clearPreview();
}

map.on('click', e => setStart(e.latlng.lat, e.latlng.lng));
marker.on('dragend', () => clearPreview());
byId('route-map-style').addEventListener('change', e => {
    if (e.target.value === 'topo' && !topo) {
        show('Tracestrack Topo requires an API key (Tracestrack__ApiKey). Showing OpenStreetMap instead.');
        e.target.value = 'osm';
        return;
    }
    if (e.target.value === 'topo') {
        map.removeLayer(osm);
        topo.addTo(map);
    } else {
        if (topo) map.removeLayer(topo);
        osm.addTo(map);
    }
});
byId('route-search').addEventListener('click', async () => {
    const query = byId('route-address').value.trim();
    if (!query) {
        show('Enter an area or address.');
        return;
    }
    show('Searching for an address...');
    const container = byId('route-search-results');
    container.replaceChildren();
    try {
        const results = await request(`/routes/search?query=${encodeURIComponent(query)}`, 'GET');
        if (!results.length) {
            show('No addresses found.');
            return;
        }
        for (const result of results) {
            const button = document.createElement('button');
            button.type = 'button';
            button.textContent = result.display_name;
            button.addEventListener('click', () => {
                setStart(Number(result.lat), Number(result.lon));
                byId('route-address').value = result.display_name;
                container.replaceChildren();
                show('Starting point updated.');
            });
            container.append(button);
        }
        show('Choose an address from the list.');
    } catch (error) {
        show(error.message);
    }
});
byId('route-address').addEventListener('keydown', e => {
    if (e.key === 'Enter') {
        e.preventDefault();
        byId('route-search').click();
    }
});

function preview(geometry, index = -1) {
    if (line) map.removeLayer(line);
    const layer = L.geoJSON({type: 'Feature', geometry, properties: {}}, {
        style: {
            color: '#2563eb',
            weight: 5,
            opacity: 0.95
        }
    }).addTo(map);
    line = layer;
    map.fitBounds(layer.getBounds().pad(0.15));
    selected = index;
}

byId('route-settings').addEventListener('submit', async e => {
    e.preventDefault();
    clearPreview();
    const data = settings();
    if (data.distanceKm < 0.5 || data.distanceKm > 40) {
        show('Choose a distance between 0.5 and 40 km.');
        return;
    }
    show('Calculating loop routes...');
    try {
        candidates = await request('/routes/generate', 'POST', data);
        if (!candidates.length) {
            show('No routes found.');
            return;
        }
        const container = byId('route-candidates');
        candidates.forEach((candidate, index) => {
            const item = document.createElement('div');
            item.className = 'route-item';
            const label = document.createElement('strong');
            label.textContent = `Suggestion ${index + 1} · ${candidate.distanceKm.toLocaleString('en-GB')} km`;
            const button = document.createElement('button');
            button.textContent = 'View this route';
            button.addEventListener('click', () => {
                preview(JSON.parse(candidate.geoJson), index);
                byId('route-save-box').hidden = false;
                show('This route is a preview. Press Save route to save it.');
            });
            item.append(label, button);
            container.append(item);
        });
        preview(JSON.parse(candidates[0].geoJson), 0);
        byId('route-save-box').hidden = false;
        show(`${candidates.length} suggestions found. Routes are saved when you press Save route.`);
    } catch (error) {
        show(error.message);
    }
});
byId('route-save').addEventListener('click', async () => {
    if (selected < 0) return;
    const name = byId('route-name').value.trim();
    if (!name) {
        show('Give the route a name first.');
        return;
    }
    const data = settings();
    try {
        await request('/routes/saved', 'POST', {
            name,
            activity: data.activity,
            environment: data.environment,
            targetDistanceKm: data.distanceKm,
            distanceKm: candidates[selected].distanceKm,
            latitude: data.latitude,
            longitude: data.longitude,
            geoJson: candidates[selected].geoJson
        });
        show('Route saved.');
        await loadSaved();
    } catch (error) {
        show(error.message);
    }
});

async function loadSaved() {
    const container = byId('route-saved');
    container.replaceChildren();
    try {
        const routes = await request('/routes/saved', 'GET');
        if (!routes.length) {
            const p = document.createElement('p');
            p.className = 'muted';
            p.textContent = 'No saved routes yet.';
            container.append(p);
            return;
        }
        for (const route of routes) {
            const item = document.createElement('div');
            item.className = 'route-item';
            const title = document.createElement('strong');
            title.textContent = `${route.name} · ${route.distanceKm.toLocaleString('en-GB')} km`;
            const actions = document.createElement('div');
            actions.className = 'route-item-actions';
            const view = document.createElement('button');
            view.textContent = 'View';
            view.addEventListener('click', () => {
                clearPreview();
                marker.setLatLng([route.latitude, route.longitude]);
                preview(JSON.parse(route.geoJson));
                show('Showing saved route.');
            });
            const remove = document.createElement('button');
            remove.textContent = 'Delete';
            remove.addEventListener('click', async () => {
                if (!confirm(`Delete route "${route.name}"?`)) return;
                try {
                    await request(`/routes/saved/${route.id}`, 'DELETE');
                    await loadSaved();
                    show('Route deleted.');
                } catch (error) {
                    show(error.message);
                }
            });
            actions.append(view, remove);
            item.append(title, actions);
            container.append(item);
        }
    } catch (error) {
        show(error.message);
    }
}

loadSaved();
