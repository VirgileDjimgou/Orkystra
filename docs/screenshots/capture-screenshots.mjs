import { chromium } from 'playwright';
import { mkdirSync } from 'fs';
import { join } from 'path';

const BASE = 'http://127.0.0.1:4180';
const OUT = join(import.meta.dirname, '.');

const ROLES = [
  {
    id: '01-president',
    name: 'Président / Directeur',
    button: 'Président',
    steps: [
      { file: '01-vue-ensemble', desc: 'Vue stratégique globale : KPI, alertes, providers' },
      { file: '02-details-alertes', desc: 'Analyse des alertes critiques et flux tendus' },
      { file: '03-sante-providers', desc: 'Décision : contacter les prestataires dégradés' },
      { file: '04-decisions', desc: 'Suivi : plan d\'action validé pour la journée' },
    ],
  },
  {
    id: '02-warehouse-operator',
    name: 'Opérateur Entrepôt',
    button: 'Opérateur',
    steps: [
      { file: '01-entrepots', desc: 'Vue des entrepôts : occupation, zones, quais' },
      { file: '02-jumeau-numerique', desc: 'Jumeau numérique 3D interactif' },
      { file: '03-analyse-capacite', desc: 'Analyse des capacités et risques de congestion' },
      { file: '04-reaffectation', desc: 'Décision : réaffectation des zones de stockage' },
    ],
  },
  {
    id: '03-transport-dispatcher',
    name: 'Dispatcher Transport',
    button: 'Dispatcher',
    steps: [
      { file: '01-tableau-routes', desc: 'Vue des routes : statut, arrêts, livraisons' },
      { file: '02-route-retard', desc: 'Analyse de la route RT-412 en retard' },
      { file: '03-optimisation', desc: 'Re-routage : optimisation OR-Tools disponible' },
      { file: '04-synchronisation', desc: 'Synchro transport : plan de tournée mis à jour' },
    ],
  },
  {
    id: '04-ai-analyst',
    name: 'Analyste IA',
    button: 'Analyste',
    steps: [
      { file: '01-assistant-IA', desc: 'Assistant IA : recommandations opérationnelles' },
      { file: '02-preuves-confiance', desc: 'Détail des preuves, hypothèses, confiance HIGH' },
      { file: '03-trace-operationnelle', desc: 'Trace opérationnelle et historique IA' },
      { file: '04-workflow-ia', desc: 'Workflow IA : analyse, décision, action' },
    ],
  },
  {
    id: '05-admin',
    name: 'Administrateur',
    button: 'Administrateur',
    steps: [
      { file: '01-catalogue-providers', desc: 'Catalogue des providers connecteurs' },
      { file: '02-configuration', desc: 'Configuration des connecteurs et secrets API' },
      { file: '03-etat-connexions', desc: 'État des connexions et santé des services' },
      { file: '04-runtime-config', desc: 'Configuration runtime et déploiement' },
    ],
  },
  {
    id: '06-supervisor',
    name: 'Superviseur',
    button: 'Superviseur',
    steps: [
      { file: '01-audit', desc: 'Piste d\'audit et observabilité' },
      { file: '02-metriques', desc: 'Métriques système et backbone événementiel' },
      { file: '03-sante-systeme', desc: 'Santé du système : API, MQTT, SQLite' },
      { file: '04-tableau-bord', desc: 'Tableau de bord superviseur : vue consolidée' },
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
