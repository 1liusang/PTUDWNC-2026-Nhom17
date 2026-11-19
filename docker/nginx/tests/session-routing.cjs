// Requires frontend dependencies and a running stack with a test Author account.
// Set DEMO_BASE_URL, DEMO_EMAIL and DEMO_PASSWORD; do not commit credentials.
const assert = require('node:assert/strict');
const path = require('node:path');
const { chromium } = require(path.join(__dirname, '../../../src/Frontend/node_modules/@playwright/test'));

async function main() {
  const base = process.env.DEMO_BASE_URL;
  const email = process.env.DEMO_EMAIL;
  const password = process.env.DEMO_PASSWORD;
  assert(base && email && password, 'Set DEMO_BASE_URL, DEMO_EMAIL and DEMO_PASSWORD');
  const login = await fetch(`${base}/api/v1/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  assert.equal(login.status, 200, 'Bearer client can log in');
  const { accessToken } = await login.json();
  const headers = { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' };
  const created = await fetch(`${base}/api/v1/recipes`, {
    method: 'POST', headers,
    body: JSON.stringify({
      title: `Kiểm tra phiên Nginx ${Date.now()}`, description: 'Công thức kiểm tra định tuyến phiên đăng nhập.',
      prepTimeMinutes: 5, cookTimeMinutes: 5, servings: 1, difficulty: 0,
    }),
  });
  assert.equal(created.status, 201, 'Bearer client can create a test recipe');
  const { id } = await created.json();
  const imagesUrl = `${base}/api/v1/recipes/${id}/images`;
  let browser;
  let imageId;
  try {
    assert.equal((await fetch(imagesUrl)).status, 401, 'Anonymous requests remain unauthorized');
    assert.equal((await fetch(imagesUrl, { headers })).status, 200, 'Bearer requests reach backend');
    browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await page.goto(`${base}/auth/login`);
    await page.getByRole('textbox', { name: 'Email', exact: true }).fill(email);
    await page.locator('input[type=password]').fill(password);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await page.waitForURL(url => !url.pathname.startsWith('/auth/login'));
    const read = await page.request.get(imagesUrl);
    assert.equal(read.status(), 200, 'Cookie session reads images through Next.js');
    // Valid 1x1 PNG fixture; tests multipart forwarding as well as session routing.
    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aB9sAAAAASUVORK5CYII=', 'base64');
    const uploaded = await page.request.post(imagesUrl, {
      multipart: { file: { name: 'session.png', mimeType: 'image/png', buffer: png } },
    });
    assert.equal(uploaded.status(), 201, 'Cookie session uploads images through Next.js');
    imageId = (await uploaded.json()).imageId;
    const listed = await fetch(imagesUrl, { headers });
    assert((await listed.json()).some(image => image.imageId === imageId), 'Bearer client sees the session upload');
    const removed = await page.request.delete(`${imagesUrl}/${imageId}`);
    assert.equal(removed.status(), 204, 'Cookie session deletes images through Next.js');
    imageId = undefined;
    console.log('Nginx regression passed: anonymous 401, Bearer API, cookie read/upload/delete');
  } finally {
    if (browser) await browser.close();
    if (imageId) await fetch(`${imagesUrl}/${imageId}`, { method: 'DELETE', headers });
    await fetch(`${base}/api/v1/recipes/${id}`, { method: 'DELETE', headers });
  }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
