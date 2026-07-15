<template>
  <div class="app-shell">
    <aside class="sidebar">
      <div class="brand">Zynro Fleet</div>
      <nav class="nav flex-column gap-1">
        <RouterLink class="nav-link" to="/">Vue générale</RouterLink>
        <RouterLink class="nav-link" to="/map">Carte flotte</RouterLink>
      </nav>
    </aside>
    <main class="main-content">
      <header class="topbar">
        <div>
          <strong>Centre d'opérations</strong>
          <div class="text-secondary small">
            Environnement de démonstration Zynro
          </div>
        </div>
        <span class="badge text-bg-success">Système disponible</span>
      </header>
      <section class="content"><RouterView /></section>
    </main>
  </div>
</template>
