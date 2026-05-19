import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 500 }, // Rampa de subida a 500 VUs
    { duration: '3m', target: 500 }, // Mantener 500 VUs
    { duration: '1m', target: 0 },   // Rampa de bajada
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% de peticiones en menos de 2s
    http_req_failed: ['rate<0.01'],    // Menos de 1% de fallos
  },
};

const BASE_URL = 'http://localhost:5032';
const TOKEN = 'AQUÍ_VA_UN_TOKEN_JWT_VALIDO'; // En un entorno real se inyecta o se pide en setup()

export default function () {
  // 1. Simular lectura del dashboard
  const resTrends = http.get(`${BASE_URL}/api/trends/current`, {
    headers: { 'Authorization': `Bearer ${TOKEN}` }
  });

  check(resTrends, {
    'Dashboard trends status is 200': (r) => r.status === 200,
  });

  // Pausa humana
  sleep(Math.random() * 2 + 1);

  // 2. Simular programación de campaña (10% de probabilidad)
  if (Math.random() < 0.1) {
    const payload = JSON.stringify({
      name: `K6 Load Test Campaign ${__VU}`,
      description: 'Stress testing campaign creation'
    });

    const resCampaign = http.post(`${BASE_URL}/api/campaigns`, payload, {
      headers: {
        'Authorization': `Bearer ${TOKEN}`,
        'Content-Type': 'application/json'
      }
    });

    check(resCampaign, {
      'Campaign creation status is 200 or 201': (r) => r.status === 200 || r.status === 201,
    });
  }

  sleep(Math.random() * 3 + 2);
}
