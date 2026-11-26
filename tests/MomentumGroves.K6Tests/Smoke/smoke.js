import { check, group, sleep } from 'k6';
import http from 'k6/http';

// Use the configured environment variable if available.
// Otherwise, use the local development URL.
const baseUrl = (__ENV.MOMENTUMGROVES_BASE_URL || 'http://127.0.0.1:5070').replace(/\/+$/, '');

// Configure how the smoke test should be executed.
export const options = {
    // Number of virtual users executing the test.
    vus: 10,

    // Total number of times the test function is executed.
    iterations: 10,

    // Define the requirements for a successful test run.
    thresholds: {
        // Every check must pass.
        checks: ['rate==1'],

        // No HTTP requests should fail.
        http_req_failed: ['rate==0'],

        // 95% of HTTP requests must complete within 1000 milliseconds.
        http_req_duration: ['p(95)<1000'],
    },
};

/**
 * Executes a smoke test against the MomentumGroves application.
 * Verifies basic availability, dashboard content and static resources.
 */
export default function smokeTest() {
    group('Dashboard availability', function () {
        // Send an HTTP GET request to the application's homepage.
        const response = http.get(`${baseUrl}/`, { responseType: 'text' });

        // Verify that the application responds correctly.
        // If the request fails without a body, report a failed check and continue to the logo group.
        check(response, {
            'Dashboard returns HTTP 200': (result) => result.status === 200,
            'Dashboard returns HTML': (result) => result.headers['Content-Type']?.includes('text/html') === true,
            'Dashboard contains expected heading': (result) => result.body?.includes('Your daily overview') === true,
            // A failed connection can report zero duration, so require a successful response first.
            'Dashboard response time is below 1000ms': (result) => result.status === 200 && result.timings.duration < 1000,
        });
    });

    group('Static resource availability', function () {
        // Request the application logo as raw bytes without following redirects.
        const response = http.get(`${baseUrl}/Logo/MomentumGrovesLogo.png`, { redirects: 0, responseType: 'binary' });

        // Verify that the logo exists and is served correctly.
        // A binary body is an ArrayBuffer, so byteLength tells us how many bytes arrived.
        check(response, {
            'Logo returns HTTP 200': (result) => result.status === 200,
            'Logo is served as PNG': (result) => result.headers['Content-Type']?.includes('image/png') === true,
            'Logo response is not empty': (result) => (result.body?.byteLength ?? 0) > 0,
            'Logo response time is below 500ms': (result) => result.status === 200 && result.timings.duration < 500,
        });
    });

    // Pause execution for one second to simulate a small user delay.
    sleep(1);
}
