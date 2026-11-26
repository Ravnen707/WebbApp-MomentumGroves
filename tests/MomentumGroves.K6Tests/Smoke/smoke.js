import { check } from 'k6';
import http from 'k6/http';

const baseUrl = __ENV.MOMENTUMGROVES_BASE_URL || 'http://127.0.0.1:5070';

export const options = {
    vus: 1,
    iterations: 1,
    thresholds: {
        checks: ['rate==1'],
    },
};

export default function checkLogo() {
    const response = http.get(`${baseUrl.replace(/\/+$/, '')}/Logo/MomentumGrovesLogo.png`, { redirects: 0 });

    check(response, {
        'logo returns HTTP 200': (result) => result.status === 200,
        'logo is served as PNG': (result) => result.headers['Content-Type'] === 'image/png',
    });
}
