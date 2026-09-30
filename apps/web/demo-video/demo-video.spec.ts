import type { Locator, Page } from "@playwright/test";
import { expect, test } from "@playwright/test";

const pace = Number(process.env.DEMO_PACE ?? "1");
const recipientStatusUrl = process.env.DEMO_RECIPIENT_URL ?? "";

test.describe.configure({ mode: "serial" });

test("00 — Introduction", async ({ page }) => {
  await titleCard(
    page,
    "Orkystra FleetOps",
    `Démonstration développeur complète — rôles, temps réel et preuves simulées

• Plateforme SaaS de gestion de flotte pour PME de livraison et de terrain
• Monolithe modulaire ASP.NET Core · Vue 3 · SQL Server · SignalR · Android natif
• Parcours : mission → workflow conducteur hors ligne → inspection/preuve → exception → action opérateur

Toutes les données et captures sont SIMULATED DEVELOPMENT EVIDENCE — NOT PILOT OR COMMERCIAL PROOF.

Sommaire : Demo publique · Cockpit temps réel · Exceptions · Flotte · Dispatch · Planning · Alertes · Maintenance · Conformité · Administration · Isolation multi-tenant · Application Android conducteur`,
    14,
  );
});

test("01 — Demo publique", async ({ page }) => {
  await prepare(page);
  await goto(page, "/demo");
  await scene(
    page,
    "Portail de demonstration publique",
    "Un visiteur lance une session éphémère, en lecture seule, sur un tenant synthétique. Aucun mot de passe, aucune donnée client.",
    6,
  );
  await page.getByRole("button", { name: "Launch Live Demo" }).click();
  await expect(
    page.getByRole("heading", { name: "Operations cockpit" }),
  ).toBeVisible();
  await scene(
    page,
    "Bandeau SIMULATED DEMO",
    "Le serveur émet une identité de démonstration (isDemo=true) ; l'interface affiche en permanence le bandeau SIMULATED DEMO et les contrôles de scénario privés à la session.",
    7,
  );
  await softWait(page, page.locator('[aria-label^="DEMO-100:"]'), 20_000);
  await scene(
    page,
    "Flotte synthétique animée en temps réel",
    "Le moteur Demo du Worker alimente la télémétrie canonique : les véhicules DEMO bougent sur la carte, avec qualité, vitesse et exception de mission retardée.",
    8,
  );
  const controls = page.getByLabel("Simulated demo controls");
  await controls.getByLabel("Scenario").selectOption("LATE_DELIVERY");
  await controls.getByRole("button", { name: "Start" }).click();
  await scene(
    page,
    "Contrôles de scénario",
    "Le visiteur choisit un scénario déterministe (ici LATE DELIVERY) et pilote son état privé ; une session publique ne peut jamais déclencher d'effet externe.",
    6,
  );
  await page
    .getByRole("button", { name: /Mission DEMO-M-100 delayed/ })
    .click();
  await scene(
    page,
    "Contexte inspecté",
    "Exception sélectionnée : véhicule DEMO-100, mission DEMO-M-100, conducteur synthétique et retard contrôlé de 15 minutes.",
    8,
  );
  await page.getByRole("tab", { name: "Virtual drivers" }).click();
  await scene(
    page,
    "Activité des pilotes virtuels",
    "Dans le profil hébergé, l'activité agent est observée dans l'espace opérateur (chapitre suivant). Ici la vue publique reste volontairement limitée.",
    6,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
  await softWait(page, page.getByRole("button", { name: "Launch Live Demo" }));
});

test("02 — Cockpit operateur temps reel", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await scene(
    page,
    "Cockpit opérations — Northwind Logistics",
    "Carte temps réel, indicateurs de flotte, file d'exceptions, missions et activité des agents. Les mises à jour arrivent par SignalR.",
    6,
  );
  await softWait(page, page.locator(".leaflet-interactive"), 25_000);
  await scene(
    page,
    "Suivi temps réel des véhicules",
    "Le moteur Demo anime 20 véhicules Northwind ; les marqueurs portent état, qualité et cap, et la carte se met à jour sans rechargement.",
    10,
  );
  await page.getByRole("tab", { name: "Virtual drivers" }).click();
  await scene(
    page,
    "Pilotes virtuels autonomes",
    "Chaque décision est tracée par un contrat observable : état observé, politique, action, code et message — sans raisonnement privé.",
    10,
  );
  await page.getByRole("tab", { name: "Exceptions" }).click();
  await scene(
    page,
    "Exceptions opérationnelles en direct",
    "Les pilotes virtuels signalent des retards contrôlés ; les exceptions alimentent la file de l'opérateur en temps réel.",
    8,
  );
  const firstException = page
    .getByRole("button", { name: /Mission .* delayed/ })
    .first();
  if (await softWait(page, firstException, 15_000)) {
    await firstException.click();
    await scene(
      page,
      "Inspecteur de contexte",
      "Véhicule, vitesse, qualité, appareil, mission, conducteur et exception réunis dans un seul panneau de décision.",
      9,
    );
  }
  await page.getByRole("tab", { name: "Missions" }).click();
  await scene(
    page,
    "Missions en cours",
    "Les missions actives sont corrélées aux véhicules de la carte ; un clic recentre le contexte.",
    6,
  );
  await page.getByRole("tab", { name: "Timeline" }).click();
  await scene(
    page,
    "Chronologie opérationnelle",
    "La timeline restitue les événements auditables de la mission sélectionnée.",
    7,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("03 — File d'exceptions", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await goto(page, "/operations");
  await scene(
    page,
    "Operations center",
    "File unifiée : alertes, retards de mission, défauts critiques et blocages de synchronisation conducteur, avec filtres et vues sauvegardées.",
    8,
  );
  const target = page
    .locator(".operations-card")
    .filter({ hasText: "NW-VIDEO-DELAY" })
    .first();
  if (await softWait(target, 15_000)) {
    await target.getByLabel("Assign owner").selectOption({ index: 1 });
    await target.getByRole("button", { name: "Assign" }).click();
    await scene(
      page,
      "Affectation d'une exception",
      "L'opérateur s'affecte l'exception ; l'action est autorisée côté serveur puis auditée.",
      6,
    );
    await target.getByRole("button", { name: "Acknowledge" }).click();
    await scene(
      page,
      "Prise en compte",
      "L'accusé de réception horodaté sort l'exception du flux non traité.",
      5,
    );
    await target.getByRole("button", { name: "Resolve" }).click();
    await scene(
      page,
      "Résolution motivée",
      "La résolution exige un motif : la décision reste traçable dans l'audit et la chronologie.",
      7,
    );
  } else {
    await scene(
      page,
      "File unifiée",
      "Les exceptions proviennent des alertes, retards, défauts et blocages de synchronisation.",
      8,
    );
  }
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("04 — Registre de flotte", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await goto(page, "/fleet/vehicles");
  await scene(
    page,
    "Registre des véhicules",
    "Vingt véhicules Northwind avec statut et cycle de vie ; l'opérateur dispose d'un accès en lecture, l'administrateur des mutations.",
    7,
  );
  await goto(page, "/fleet/drivers");
  await scene(
    page,
    "Conducteurs",
    "Profils avec permis et statut, socle des affectations et des campagnes de conformité.",
    6,
  );
  await goto(page, "/fleet/devices");
  await scene(
    page,
    "Appareils GPS",
    "Chaque traceur peut être affecté à un véhicule ; une seule affectation active par appareil est autorisée.",
    7,
  );
  const deviceCard = page
    .locator(".user-card")
    .filter({ hasText: "NW-GPS-900" })
    .first();
  if (await softWait(page, deviceCard, 10_000)) {
    const select = deviceCard.locator("select");
    await select.selectOption({ index: 1 });
    await deviceCard.getByRole("button", { name: "Assign" }).click();
    await scene(
      page,
      "Affectation d'un traceur",
      "L'affectation crée un historique : la traçabilité du couple véhicule/appareil est conservée.",
      7,
    );
  }
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("05 — Dispatch et missions", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await goto(page, "/dispatch/missions");
  await scene(
    page,
    "Mission control board",
    "Backlog des missions avec conducteur, véhicule, statut et retard simulé.",
    7,
  );
  const reference = `NW-VIDEO-UI-${Date.now().toString().slice(-6)}`;
  const schedule = buildSchedule(reference);
  await page.getByPlaceholder("Mission reference").fill(reference);
  await page.getByPlaceholder("Mission title").fill("Tournée démonstration");
  const scheduleInputs = page.locator('input[type="datetime-local"]');
  await scheduleInputs.nth(0).fill(schedule.startLocal);
  await scheduleInputs.nth(1).fill(schedule.endLocal);
  await scheduleInputs.nth(2).fill(schedule.firstStopLocal);
  await scene(
    page,
    "Création d'une mission",
    "Référence unique, fenêtre planifiée et arrêts : la validation domaine impose des bornes cohérentes.",
    7,
  );
  await page.getByRole("button", { name: "Create mission" }).click();
  await softWait(
    page,
    page.getByText(`Mission ${reference} created in Draft.`),
  );
  await scene(
    page,
    "Brouillon créé",
    "La mission naît en Draft ; chaque transition suivante est contrôlée par la machine à états et auditée.",
    6,
  );
  const showcase = page
    .locator(".user-card[role='button']")
    .filter({ hasText: "NW-VIDEO-SHOW" })
    .first();
  if (await softWait(page, showcase, 10_000)) {
    await showcase.click();
    await scene(
      page,
      "Mission avec preuve de livraison",
      "Inspection pré-départ validée, puis preuve signée : photo et signature stockées dans le média privé, accessibles via URL signée.",
      9,
    );
    await scene(
      page,
      "Chronologie auditée et preuves",
      "La chronologie et les liens de preuve documentent la chaîne mission → inspection → livraison.",
      8,
    );
  }
  const delayed = page
    .locator(".user-card[role='button']")
    .filter({ hasText: "NW-VIDEO-DELAY" })
    .first();
  if (await softWait(page, delayed, 8_000)) {
    await delayed.click();
    await scene(
      page,
      "Retard contrôlé",
      "La simulation de retard crée une exception opérationnelle sans modifier les tables métier à la main.",
      7,
    );
  }
  if (recipientStatusUrl) {
    await page.goto(recipientStatusUrl);
    await installOverlay(page);
    await scene(
      page,
      "Statut destinataire partagé",
      "Lien public opaque, expirable et révocable : le destinataire suit sa livraison sans exposer les données internes.",
      9,
    );
  }
});

test("06 — Planning quotidien", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await goto(page, "/dispatch/productivity");
  await scene(
    page,
    "Espace de planification quotidienne",
    "Board jour/semaine, modèles de tournée, duplication et import idempotent.",
    7,
  );
  await page.getByPlaceholder("Template name").fill("Tournée du matin");
  await page.getByPlaceholder("Mission title").fill("Livraisons matinales");
  await page.getByPlaceholder("First stop name").fill("Dépôt");
  await page.getByPlaceholder("First stop address").fill("1 Dispatch Way");
  await page.getByRole("button", { name: "Save template" }).click();
  await scene(
    page,
    "Modèle de tournée réutilisable",
    "Le modèle appartient au tenant et reste réutilisable par l'équipe planning.",
    6,
  );
  const template = page
    .locator(".list-group-item")
    .filter({ hasText: "Tournée du matin" })
    .first();
  if (await softWait(page, template, 8_000)) {
    await template.getByRole("button", { name: "Duplicate" }).click();
    await scene(
      page,
      "Duplication en brouillon",
      "La duplication prépare une mission datée sans affecter la tournée d'origine.",
      6,
    );
  }
  await page
    .getByPlaceholder("Unique import key")
    .fill(`demo-${Date.now().toString().slice(-6)}`);
  const row = JSON.stringify([
    {
      reference: "NW-PLAN-1",
      title: "Import planifié",
      scheduledStartUtc: iso(26),
      scheduledEndUtc: iso(28),
      stopName: "Dépôt",
      stopAddress: "1 Dispatch Way",
      plannedArrivalUtc: iso(26.5),
    },
  ]);
  await page.getByLabel("Import rows JSON").fill(row);
  await page.getByRole("button", { name: "Preview" }).click();
  await scene(
    page,
    "Prévisualisation d'import",
    "Erreurs détaillées avant application ; la confirmation ne rejoue jamais deux fois la même clé.",
    6,
  );
  await page.getByRole("button", { name: "Confirm import" }).click();
  await scene(
    page,
    "Import confirmé",
    "La clé d'import rend l'opération idempotente, même en cas de rejeu.",
    6,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("07 — Centre d'alertes", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await goto(page, "/alerts");
  await scene(
    page,
    "Alert center",
    "Alertes de conformité, maintenance et véhicules silencieux, calculées par un scan déterministe et le Worker.",
    7,
  );
  await page.getByRole("button", { name: "Run scan" }).click();
  await softWait(page, page.locator(".alert-card"), 20_000);
  await scene(
    page,
    "Scan déterministe",
    "Chaque règle produit une clé stable : rejouer le scan ne duplique jamais une alerte.",
    8,
  );
  const alertCard = page.locator(".alert-card").first();
  if (await softWait(page, alertCard, 10_000)) {
    const select = alertCard.locator(".assignment-select");
    await select.selectOption({ index: 1 });
    await alertCard.getByRole("button", { name: "Assign" }).click();
    await alertCard.getByRole("button", { name: "Acknowledge" }).click();
    await scene(
      page,
      "Affectation et accusation de réception",
      "Les actions sont réservées aux rôles autorisés et journalisées.",
      6,
    );
  }
  await scene(
    page,
    "Notifications",
    "Un outbox dédupliqué garde une trace des notifications ; le canal e-mail reste un adaptateur de développement.",
    6,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("08 — Maintenance et conformité", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@northwind.local", "Operator123!");
  await goto(page, "/maintenance");
  await scene(
    page,
    "Ordres de travail de maintenance",
    "Le scan de maintenance crée des ordres bornés depuis les plans véhicule : échéance, disponibilité et coût.",
    8,
  );
  await goto(page, "/compliance");
  await scene(
    page,
    "Espace conformité",
    "Politique d'affectation, matrice de couverture par sujet et campagnes d'inspection avec taux de soumission.",
    8,
  );
  await page.getByRole("button", { name: "Export audit CSV" }).click();
  await scene(
    page,
    "Export d'audit",
    "L'export CSV est filtré par organisation et destiné aux audits, pas à la production de PII.",
    6,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("09 — Administration", async ({ page }) => {
  await prepare(page);
  await login(page, "admin@northwind.local", "Admin123!");
  await goto(page, "/admin/onboarding");
  await scene(
    page,
    "Guided setup administrateur",
    "Checklist d'activation : MFA, comptes, véhicules, appareils, documents, missions et première valeur.",
    7,
  );
  await page.getByLabel("Data type").selectOption("drivers");
  await page
    .getByLabel("CSV content")
    .fill(
      `fullName,licenseNumber,phoneNumber\nTaylor Video,NV-${Date.now().toString().slice(-6)},+1-555-0300`,
    );
  await page.getByRole("button", { name: "Preview import" }).click();
  await softWait(page, page.getByText("Preview result"));
  await scene(
    page,
    "Import CSV validé",
    "Aperçu ligne à ligne, erreurs explicites, puis confirmation explicite : aucun import silencieux.",
    7,
  );
  await page.getByRole("button", { name: "Confirm validated import" }).click();
  await scene(
    page,
    "Import confirmé",
    "Les créations et mises à jour sont idempotentes et tracées.",
    6,
  );
  const driverSelect = page.getByLabel("Activated driver account");
  if (await softWait(page, driverSelect, 8_000)) {
    await driverSelect.selectOption({ index: 1 });
    await page.getByRole("button", { name: "Create pairing code" }).click();
    await scene(
      page,
      "Appairage d'un appareil conducteur",
      "Code à six chiffres, dix minutes de validité : l'appareil reçoit une session liée au conducteur.",
      7,
    );
  }
  await goto(page, "/admin/users");
  await scene(
    page,
    "Administration des utilisateurs",
    "Rôles Admin, Operator, Driver ; un compte conducteur est relié à un profil métier.",
    6,
  );
  await page.getByLabel("Full name").fill("Video Viewer");
  await page
    .getByLabel("Email")
    .fill(`video-${Date.now().toString().slice(-6)}@northwind.local`);
  await page.getByRole("button", { name: "Create user" }).click();
  await scene(
    page,
    "Création d'un utilisateur",
    "Mot de passe temporaire et rôle explicite ; l'autorisation reste côté serveur.",
    6,
  );
  await goto(page, "/admin/security");
  await scene(
    page,
    "Sécurité et cycle de vie des données",
    "MFA administrateur, rétention télémétrie, export de tenant et purge contrôlée avec confirmation par slug.",
    9,
  );
  await page.getByRole("button", { name: "Download tenant export" }).click();
  await scene(
    page,
    "Export de tenant",
    "Export JSON tenant-scoped : preuves et données sans fuite inter-organisations.",
    5,
  );
  await goto(page, "/admin/pilot");
  await scene(
    page,
    "Pilot review",
    "Mesure pilote : consentement explicite, agrégats quotidiens sans PII, incidents et décision de niche.",
    7,
  );
  await page.getByText("I have informed the organization").click();
  await scene(
    page,
    "Consentement explicite",
    "Sans consentement, la collecte d'agrégats est refusée (409) ; le consentement est révocable.",
    6,
  );
  await page.getByRole("button", { name: "Record daily aggregate" }).click();
  await softWait(page, page.getByText("Daily aggregate recorded."));
  await page.getByLabel("Incident severity").selectOption({ index: 1 });
  await page.getByPlaceholder("Category").fill("sync-block");
  await page
    .getByPlaceholder("Summary without personal data")
    .fill("Blocage de synchronisation signalé pendant la démonstration.");
  await page.getByRole("button", { name: "Record incident" }).click();
  await scene(
    page,
    "Incident de support",
    "Les incidents sont décrits sans données personnelles et suivis jusqu'à résolution.",
    6,
  );
  await page.getByRole("button", { name: "Resolve" }).first().click();
  await page.getByPlaceholder("Primary segment").fill("logistique régionale");
  await page
    .getByPlaceholder("Evidence-based rationale")
    .fill(
      "Trois organisations fictives démontrent la chaîne mission-preuve-exception.",
    );
  await page.getByRole("button", { name: "Record decision" }).click();
  await scene(
    page,
    "Décision de niche",
    "GO / SIMPLIFY / PIVOT / STOP : la décision est fondée sur des preuves et reste auditable.",
    6,
  );
  await page.getByRole("button", { name: "Export evidence" }).click();
  await goto(page, "/admin/integrations");
  await scene(
    page,
    "Intégrations et audit",
    "Fournisseur télématique virtuel, clés API à scopes bornés, webhooks signés, catalogue de contrats et outbox.",
    9,
  );
  const connectionName = page.getByLabel("Sandbox connection name");
  await connectionName.fill("FleetOps video provider");
  await page.getByRole("button", { name: "Create connection" }).click();
  await scene(
    page,
    "Connexion télématique en bac à sable",
    "L'intégration virtuelle est explicitement sandbox : elle ne constitue pas une intégration commerciale.",
    6,
  );
  await page.locator("#webhookName").fill("Portfolio observer");
  await page.locator("#webhookEvent").selectOption({ index: 1 });
  await page.locator("#signingSecret").fill("demo-signing-secret");
  await page.getByLabel("Use FleetOps sandbox receiver").check();
  await page.getByRole("button", { name: "Create webhook" }).click();
  await scene(
    page,
    "Webhook signé",
    "Chaque livraison est signée, rejouable et suivie dans l'outbox avec dead-letter.",
    7,
  );
  await scene(
    page,
    "Catalogue de contrats",
    "Les contrats publiés documentent chaque événement et son exemple de charge utile.",
    6,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("10 — Isolation multi-tenant", async ({ page }) => {
  await prepare(page);
  await login(page, "operator@southridge.local", "Operator123!");
  await scene(
    page,
    "Un autre tenant",
    "Southridge Transport possède ses propres véhicules SR-200 et ses données ; l'organisation vient exclusivement de l'identité authentifiée.",
    8,
  );
  await goto(page, "/dispatch/missions");
  await scene(
    page,
    "Aucune fuite inter-organisations",
    "Les missions Northwind sont invisibles et une mutation croisée retournerait 404, pas 403 : aucune existence n'est révélée.",
    8,
  );
  await page.getByRole("button", { name: "Sign out" }).click();
});

test("11 — Chapitre conducteur: application Android", async ({ page }) => {
  await titleCard(
    page,
    "Application Android Conducteur",
    `Le conducteur travaille sur une application native Compose / Material 3 :

• file de missions hors ligne (Room) et synchronisation fiable (WorkManager)
• inspection pré-départ obligatoire avant le départ
• départ, arrivée, preuve de livraison (photo + signature manuscrite)
• campagnes d'inspection assignées, appairage d'appareil à six chiffres
• toute commande synchronisée porte un identifiant idempotent généré sur l'appareil

La séquence suivante est filmée sur un émulateur Android réel.`,
    12,
  );
});

test("12 — Conclusion et preuves", async ({ page }) => {
  await titleCard(
    page,
    "Preuves et limites honnêtes",
    `Chaque capacité montrée renvoie à une preuve technique versionnée :

• Gate complète et smoke hébergé : .runtime/sprint32-quality-gate.log · .runtime/sprint32-demo-smoke.log
• Fiabilité mesurée : docs/02-engineering/RELIABILITY_REPORT.md
• Sécurité Demo : docs/01-architecture/DEMO_MODE_SECURITY.md
• Moteur Demo et agents virtuels : docs/01-architecture/HOSTED_DEMO_ENGINE.md · VIRTUAL_DRIVER_AGENTS.md
• Release checklist : docs/02-engineering/RELEASE_CHECKLIST.md

Limites assumées : pilotes virtuels non provisionnés dans le profil hébergé ; charge 50 véhicules non testée ; stores de session process-locaux ; tuiles cartographiques et hébergement à décider.

SIMULATED DEVELOPMENT EVIDENCE — NOT PILOT OR COMMERCIAL PROOF.`,
    14,
  );
});

function iso(hoursFromNow: number) {
  return new Date(Date.now() + hoursFromNow * 60 * 60 * 1000).toISOString();
}

function buildSchedule(reference: string) {
  const seed = Number(reference.replace(/\D/g, "").slice(-5)) || 0;
  const start = new Date(Date.now() + (48 + (seed % 48)) * 60 * 60 * 1000);
  const end = new Date(start.getTime() + 2 * 60 * 60 * 1000);
  return {
    startLocal: toLocalInput(start),
    endLocal: toLocalInput(end),
    firstStopLocal: toLocalInput(new Date(start.getTime() + 30 * 60 * 1000)),
    secondStopLocal: toLocalInput(new Date(start.getTime() + 90 * 60 * 1000)),
  };
}

function toLocalInput(value: Date) {
  return new Date(value.getTime() - value.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
}

async function prepare(page: Page) {
  page.on("dialog", async (dialog) => {
    if (dialog.type() === "prompt") {
      await dialog.accept("Résolu pendant la démonstration guidée");
    } else {
      await dialog.accept();
    }
  });
}

async function login(page: Page, email: string, password: string) {
  await goto(page, "/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(
    page.getByRole("heading", { name: "Operations cockpit" }),
  ).toBeVisible({ timeout: 30_000 });
  await installOverlay(page);
}

async function goto(page: Page, route: string) {
  await page.goto(route);
  await installOverlay(page);
}

async function scene(
  page: Page,
  title: string,
  text: string,
  holdSeconds: number,
) {
  await installOverlay(page);
  await page.evaluate(
    ([captionTitle, captionText]) => {
      const caption = document.getElementById("fleetops-video-caption");
      if (!caption) return;
      const strong = caption.querySelector("strong");
      const span = caption.querySelector("span");
      if (strong) strong.textContent = captionTitle;
      if (span) span.textContent = captionText;
    },
    [title, text] as const,
  );
  await page.waitForTimeout(
    Math.max(150, Math.round(holdSeconds * 1000 * pace)),
  );
}

async function titleCard(
  page: Page,
  title: string,
  body: string,
  holdSeconds: number,
) {
  const safeTitle = escapeHtml(title);
  const safeBody = escapeHtml(body).replaceAll("\n", "<br/>");
  await page.setContent(`<!doctype html>
<html lang="fr"><head><meta charset="utf-8"/><title>${safeTitle}</title></head>
<body style="margin:0;background:linear-gradient(135deg,#0b1c24 0%,#0b6b5d 100%);color:#f8fafc;font-family:'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;">
<main style="max-width:1100px;padding:64px;">
<div style="letter-spacing:.24em;font-size:14px;color:#8ce0c7;margin-bottom:18px;">FLEETOPS — DÉMONSTRATION DÉVELOPPEUR</div>
<h1 style="font-size:52px;margin:0 0 24px 0;">${safeTitle}</h1>
<div style="font-size:19px;line-height:1.6;color:#dbe7ee;">${safeBody}</div>
</main></body></html>`);
  await page.waitForTimeout(
    Math.max(150, Math.round(holdSeconds * 1000 * pace)),
  );
}

function escapeHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function installOverlay(page: Page) {
  await page.evaluate(() => {
    if (document.getElementById("fleetops-video-caption")) return;
    const style = document.createElement("style");
    style.textContent = `
      #fleetops-video-caption { position: fixed; left: 0; right: 0; bottom: 0; z-index: 2147483647; background: rgba(10, 18, 24, 0.93); color: #f8fafc; padding: 14px 26px 18px 26px; font-family: 'Segoe UI', sans-serif; pointer-events: none; border-top: 3px solid #0b6b5d; }
      #fleetops-video-caption strong { display: block; font-size: 20px; margin-bottom: 4px; color: #8ce0c7; }
      #fleetops-video-caption span { display: block; font-size: 15px; line-height: 1.35; }
      #fleetops-video-badge { position: fixed; bottom: 92px; right: 16px; z-index: 2147483647; background: rgba(11, 107, 93, 0.95); color: #ffffff; padding: 6px 12px; border-radius: 999px; font-family: 'Segoe UI', sans-serif; font-size: 11px; letter-spacing: 0.08em; pointer-events: none; }
    `;
    document.head.appendChild(style);
    const caption = document.createElement("div");
    caption.id = "fleetops-video-caption";
    caption.innerHTML = "<strong></strong><span></span>";
    document.body.appendChild(caption);
    const badge = document.createElement("div");
    badge.id = "fleetops-video-badge";
    badge.textContent = "FLEETOPS — DÉMONSTRATION DÉVELOPPEUR";
    document.body.appendChild(badge);
  });
}

async function softWait(page: Page, locator: Locator, timeout = 20_000) {
  void page;
  try {
    await locator.first().waitFor({ state: "visible", timeout });
    return true;
  } catch {
    return false;
  }
}
