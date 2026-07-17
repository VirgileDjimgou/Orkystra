<template>
  <div class="auth-page" :class="{ 'auth-page-single': !showDemoAccounts }">
    <section class="auth-panel">
      <div class="auth-copy">
        <span class="eyebrow">Fleet operations control</span>
        <h1>Secure access for every organization.</h1>
        <p>
          Sign in to coordinate missions, fleet readiness and operational
          exceptions for your organization.
        </p>
      </div>

      <form class="auth-form" @submit.prevent="submit">
        <label class="form-label" for="email">Email</label>
        <input
          id="email"
          v-model="form.email"
          class="form-control"
          type="email"
          autocomplete="username"
          required
        />

        <label class="form-label mt-3" for="password">Password</label>
        <input
          id="password"
          v-model="form.password"
          class="form-control"
          type="password"
          autocomplete="current-password"
          required
        />

        <template v-if="requiresTwoFactor">
          <label class="form-label mt-3" for="twoFactorCode">
            Authenticator code
          </label>
          <input
            id="twoFactorCode"
            v-model="form.twoFactorCode"
            class="form-control"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="8"
            placeholder="123456"
            required
          />
        </template>

        <div v-if="session.error" class="alert alert-danger mt-3 mb-0">
          {{ session.error }}
        </div>

        <button
          class="btn btn-primary auth-submit mt-4"
          :disabled="isBusy"
          type="submit"
        >
          {{
            isBusy
              ? "Signing in..."
              : requiresTwoFactor
                ? "Verify and sign in"
                : "Sign in"
          }}
        </button>
      </form>
    </section>

    <aside v-if="showDemoAccounts" class="demo-panel">
      <h2>Demo accounts</h2>
      <div
        v-for="account in demoAccounts"
        :key="account.email"
        class="demo-card"
      >
        <strong>{{ account.role }}</strong>
        <span>{{ account.organization }}</span>
        <code>{{ account.email }}</code>
        <code>{{ account.password }}</code>
        <button
          class="btn btn-outline-secondary btn-sm mt-2"
          @click="fill(account)"
        >
          Use this account
        </button>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive } from "vue";
import { useRouter } from "vue-router";
import { useSessionStore } from "../features/auth/store";

const router = useRouter();
const session = useSessionStore();
const showDemoAccounts = import.meta.env.DEV;
const form = reactive({
  email: showDemoAccounts ? "admin@northwind.local" : "",
  password: showDemoAccounts ? "Admin123!" : "",
  twoFactorCode: "",
});

const demoAccounts = [
  {
    role: "Admin",
    organization: "Northwind Logistics",
    email: "admin@northwind.local",
    password: "Admin123!",
  },
  {
    role: "Operator",
    organization: "Northwind Logistics",
    email: "operator@northwind.local",
    password: "Operator123!",
  },
  {
    role: "Admin",
    organization: "Southridge Transport",
    email: "admin@southridge.local",
    password: "Admin123!",
  },
];

const isBusy = computed(() => session.status === "authenticating");
const requiresTwoFactor = computed(
  () => session.status === "twoFactorRequired",
);

async function submit() {
  try {
    await session.login({
      email: form.email,
      password: form.password,
      twoFactorCode: form.twoFactorCode.trim() || undefined,
    });
    if (session.isAuthenticated) {
      await router.push("/");
    }
  } catch {
    // The store already exposes a user-friendly message.
  }
}

function fill(account: { email: string; password: string }) {
  form.email = account.email;
  form.password = account.password;
  form.twoFactorCode = "";
}
</script>
