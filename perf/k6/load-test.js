// k6 load test for SellingNewProduct API.
// Measures the three core signals — Latency (p95/p99), Throughput (RPS), Error Rate — and fails the
// run if thresholds are breached (so it doubles as a CI performance gate).
//
// Run:
//   k6 run perf/k6/load-test.js
//   k6 run -e BASE_URL=http://localhost:5044 -e EMAIL=admin@shop.local -e PASSWORD=Secret123! load-test.js
//   k6 run --out json=perf/k6/results.json --summary-export=perf/k6/summary.json load-test.js
//
// Prereqs: API running (http profile = http://localhost:5044) and reachable; a valid login.
// NOTE: adjust the token field (data.accessToken) and product payload to match your API if changed.

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5044';
const EMAIL = __ENV.EMAIL || 'admin@shop.local';
const PASSWORD = __ENV.PASSWORD || 'Secret123!';

// A custom trend so we can see read-path latency separately from the login/setup calls.
const listLatency = new Trend('product_list_latency', true);

export const options = {
  scenarios: {
    // Ramp read traffic: warm up, hold steady, cool down. Find the breaking point by raising `rate`.
    steady_read_load: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 300,
      stages: [
        { target: 100, duration: '30s' }, // warm up to 100 rps
        { target: 100, duration: '1m' },  // steady
        { target: 300, duration: '1m' },  // push toward the breaking point
        { target: 0, duration: '20s' },   // cool down
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1000'], // latency budget
    http_req_failed: ['rate<0.01'],                  // < 1% error rate
    product_list_latency: ['p(95)<400'],
  },
};

// Log in once per VU init and reuse the bearer token for the iterations.
export function setup() {
  const res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ email: EMAIL, password: PASSWORD }), {
    headers: { 'Content-Type': 'application/json' },
  });
  check(res, { 'login 200': (r) => r.status === 200 });

  let token = '';
  try {
    const body = res.json();
    token = (body && body.data && (body.data.accessToken || body.data.token)) || '';
  } catch (_) {
    token = '';
  }
  return { token };
}

export default function (data) {
  const headers = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${data.token}`,
  };

  // Read path — product listing (the hot, cacheable endpoint).
  const list = http.get(`${BASE_URL}/api/products?thePage=1&thePageSize=20`, { headers });
  listLatency.add(list.timings.duration);
  check(list, {
    'products status ok': (r) => r.status === 200 || r.status === 401,
    'products fast': (r) => r.timings.duration < 500,
  });

  // Report path — heavier, cross-store aggregation (SQL + Mongo).
  const report = http.get(`${BASE_URL}/api/reports/best-selling-products?thePage=1&thePageSize=10`, { headers });
  check(report, { 'report status ok': (r) => r.status === 200 || r.status === 401 || r.status === 403 });

  sleep(0.1);
}
