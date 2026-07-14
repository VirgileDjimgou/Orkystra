import { chromium } from 'playwright';
import { mkdirSync } from 'fs';
import { join } from 'path';

const BASE = 'http://127.0.0.1:4180';
const OUT = join(import.meta.dirname, '.');

const ROLES = [
  {
    id: '01-president',
    name: 'President / Director',
    button: 'President',
    steps: [
      { file: '01-overview', desc: 'Strategic overview: KPIs, alerts, providers' },
      { file: '02-alert-details', desc: 'Review critical alerts and tight flows' },
      { file: '03-provider-health', desc: 'Decision: contact degraded providers' },
      { file: '04-decisions', desc: 'Follow-up: action plan approved for the day' },
    ],
  },
  {
    id: '02-warehouse-operator',
    name: 'Warehouse Operator',
    button: 'Operator',
    steps: [
      { file: '01-warehouses', desc: 'Warehouse view: occupancy, zones, docks' },
      { file: '02-digital-twin', desc: 'Interactive 3D digital twin' },
      { file: '03-capacity-analysis', desc: 'Capacity analysis and congestion risks' },
      { file: '04-reassignment', desc: 'Decision: reassign storage zones' },
    ],
  },
  {
    id: '03-transport-dispatcher',
    name: 'Transport Dispatcher',
    button: 'Dispatcher',
    steps: [
      { file: '01-route-board', desc: 'Route view: status, stops, deliveries' },
      { file: '02-route-retard', desc: 'Review the delayed RT-412 route' },
      { file: '03-optimization', desc: 'Rerouting: OR-Tools optimization available' },
      { file: '04-synchronization', desc: 'Transport sync: route plan updated' },
    ],
  },
  {
    id: '04-ai-analyst',
    name: 'AI Analyst',
    button: 'Analyst',
    steps: [
      { file: '01-ai-assistant', desc: 'AI assistant: operational recommendations' },
      { file: '02-evidence-confidence', desc: 'Evidence details, assumptions, HIGH confidence' },
      { file: '03-operational-trace', desc: 'Operational trace and AI history' },
      { file: '04-ai-workflow', desc: 'AI workflow: analysis, decision, action' },
    ],
  },
  {
    id: '05-admin',
    name: 'Administrator',
    button: 'Administrator',
    steps: [
      { file: '01-provider-catalog', desc: 'Connector provider catalog' },
      { file: '02-configuration', desc: 'Connector configuration and API secrets' },
      { file: '03-connection-status', desc: 'Connection status and service health' },
      { file: '04-runtime-config', desc: 'Runtime configuration and deployment' },
    ],
  },
  {
    id: '06-supervisor',
    name: 'Supervisor',
    button: 'Supervisor',
    steps: [
      { file: '01-audit', desc: 'Audit trail and observability' },
      { file: '02-metrics', desc: 'System metrics and event backbone' },
      { file: '03-system-health', desc: 'System health: API, MQTT, SQLite' },
      { file: '04-dashboard', desc: 'Supervisor dashboard: consolidated view' },
    ],
  },
];

async function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function capture() {
  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 2,
  });
  const page = await ctx.newPage();

  for (const role of ROLES) {
    const dir = join(OUT, role.id);
    mkdirSync(dir, { recursive: true });

    for (let i = 0; i < role.steps.length; i++) {
      const step = role.steps[i];
      const filepath = join(dir, `${step.file}.png`);

      console.log(`[${role.id}] Step ${i + 1}/4: ${step.desc}`);

      await page.goto(BASE + '/demo.html', { waitUntil: 'networkidle' });

      const button = page.locator('#roleNav button', { hasText: role.button });
      await button.click();
      await sleep(1000);

      if (i > 0) {
        await page.evaluate((idx) => {
          const section = document.querySelector('.role-section.active');
          if (!section) return;
          const cards = section.querySelectorAll('.card');
          const rows = section.querySelectorAll('tr');
          const items = section.querySelectorAll('.alert-item, .event-item');
          const chatBubbles = section.querySelectorAll('.chat-bubble');

          if (cards.length >= idx && idx > 0) {
            cards[idx - 1]?.scrollIntoView({ behavior: 'instant', block: 'center' });
          } else if (rows.length > idx * 2 + 1) {
            rows[idx * 2 + 1]?.scrollIntoView({ behavior: 'instant', block: 'center' });
          }
        }, i);
        await sleep(500);
      }

      await page.screenshot({ path: filepath, fullPage: false });
      console.log(`  -> Saved ${filepath}`);
    }
  }

  await browser.close();
  console.log('\nAll screenshots captured successfully!');
}

capture().catch(e => {
  console.error('Capture failed:', e);
  process.exit(1);
});



