import { chromium } from 'playwright';
import { mkdirSync, renameSync, existsSync, unlinkSync, copyFileSync } from 'fs';
import { join } from 'path';

const DEMO_URL = 'http://127.0.0.1:4180/demo.html';
const ROOT = import.meta.dirname;
const VIDEO_DIR = join(ROOT, 'videos');
const POSTER_DIR = join(VIDEO_DIR, 'posters');

const ROLE_RUNS = [
  { slug: 'president', dataRole: 'president', title: 'Executive overview' },
  { slug: 'warehouse-operator', dataRole: 'warehouse', title: 'Warehouse operator' },
  { slug: 'transport-dispatcher', dataRole: 'transport', title: 'Transport dispatcher' },
  { slug: 'ai-analyst', dataRole: 'ai', title: 'AI analyst' },
  { slug: 'administrator', dataRole: 'admin', title: 'Administrator' },
  { slug: 'supervisor', dataRole: 'supervisor', title: 'Supervisor' },
  { slug: 'controller', dataRole: 'supervisor', title: 'Controller (audit-focused)' },
  { slug: 'auditor', dataRole: 'supervisor', title: 'Commissaire aux comptes (audit-focused)' }
];

function ensureDirs() {
  mkdirSync(VIDEO_DIR, { recursive: true });
  mkdirSync(POSTER_DIR, { recursive: true });
}

async function waitForDemoLoad(page) {
  await page.goto(DEMO_URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
}

async function selectRole(page, dataRole) {
  await page.click(`#roleNav button[data-role="${dataRole}"]`);
  await page.waitForTimeout(900);
}

async function animateRoleWalkthrough(page) {
  for (let i = 0; i < 4; i += 1) {
    if (i > 0) {
      await page.evaluate((idx) => {
        const section = document.querySelector('.role-section.active');
        if (!section) {
          return;
        }

        const cards = section.querySelectorAll('.card');
        const rows = section.querySelectorAll('tr');
        const alerts = section.querySelectorAll('.alert-item, .event-item, .chat-bubble');

        if (cards.length >= idx + 1) {
          cards[idx]?.scrollIntoView({ behavior: 'instant', block: 'center' });
          return;
        }

        if (rows.length >= idx + 2) {
          rows[idx + 1]?.scrollIntoView({ behavior: 'instant', block: 'center' });
          return;
        }

        if (alerts.length >= idx + 1) {
          alerts[idx]?.scrollIntoView({ behavior: 'instant', block: 'center' });
        }
      }, i);
    }

    await page.waitForTimeout(1200);
  }
}

async function recordRole(browser, role) {
  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    recordVideo: {
      dir: VIDEO_DIR,
      size: { width: 1280, height: 720 }
    }
  });

  const page = await context.newPage();
  const pageVideo = page.video();

  await waitForDemoLoad(page);
  await selectRole(page, role.dataRole);
  await animateRoleWalkthrough(page);

  const posterPath = join(POSTER_DIR, `${role.slug}.png`);
  await page.screenshot({ path: posterPath });

  await context.close();

  if (!pageVideo) {
    throw new Error(`Playwright video stream was not available for role ${role.slug}.`);
  }

  const sourceVideoPath = await pageVideo.path();
  const targetVideoPath = join(VIDEO_DIR, `${role.slug}.webm`);

  if (existsSync(targetVideoPath)) {
    unlinkSync(targetVideoPath);
  }

  renameSync(sourceVideoPath, targetVideoPath);
  console.log(`Recorded ${role.slug}: ${targetVideoPath}`);

  return targetVideoPath;
}

async function captureAll() {
  ensureDirs();

  const browser = await chromium.launch({ headless: true });
  try {
    const produced = [];
    for (const role of ROLE_RUNS) {
      console.log(`Recording role: ${role.title}`);
      const filePath = await recordRole(browser, role);
      produced.push(filePath);
    }

    const supervisorVideo = join(VIDEO_DIR, 'supervisor.webm');
    const controllerVideo = join(VIDEO_DIR, 'controller.webm');
    const auditorVideo = join(VIDEO_DIR, 'auditor.webm');

    if (!existsSync(controllerVideo) && existsSync(supervisorVideo)) {
      copyFileSync(supervisorVideo, controllerVideo);
    }

    if (!existsSync(auditorVideo) && existsSync(supervisorVideo)) {
      copyFileSync(supervisorVideo, auditorVideo);
    }

    console.log('\nCompleted role video capture. Files:');
    for (const filePath of produced) {
      console.log(` - ${filePath}`);
    }
  } finally {
    await browser.close();
  }
}

captureAll().catch((error) => {
  console.error('Role video capture failed:', error);
  process.exit(1);
});
