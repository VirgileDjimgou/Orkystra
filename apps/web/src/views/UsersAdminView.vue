<template>
  <div class="stacked-page">
    <section class="page-hero">
      <div>
        <span class="eyebrow">Administration</span>
        <h1>User administration</h1>
        <p>
          Manage the users of your current organization. Operators can sign in,
          but only administrators can see and use this area.
        </p>
      </div>
      <span class="badge text-bg-dark">{{
        session.user?.organizationName
      }}</span>
    </section>

    <div class="row g-4">
      <div class="col-lg-7">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Current users</h2>
              <p>Users are strictly scoped to the signed-in organization.</p>
            </div>
            <button
              class="btn btn-outline-secondary"
              :disabled="isLoadingUsers"
              @click="loadUsers"
            >
              {{ isLoadingUsers ? "Refreshing..." : "Refresh" }}
            </button>
          </div>

          <div v-if="usersError" class="alert alert-danger">
            {{ usersError }}
          </div>
          <div v-else-if="isLoadingUsers" class="empty-placeholder">
            Loading users...
          </div>
          <div v-else-if="users.length === 0" class="empty-placeholder">
            No users found.
          </div>
          <div v-else class="user-list">
            <article v-for="user in users" :key="user.userId" class="user-card">
              <div>
                <strong>{{ user.fullName }}</strong>
                <div class="text-secondary small">{{ user.email }}</div>
              </div>
              <div class="user-meta">
                <span class="badge text-bg-light">{{ user.role }}</span>
                <span :class="user.isActive ? 'text-success' : 'text-danger'">
                  {{ user.isActive ? "Active" : "Inactive" }}
                </span>
              </div>
            </article>
          </div>
        </section>
      </div>

      <div class="col-lg-5">
        <section class="surface-panel">
          <div class="panel-heading">
            <div>
              <h2>Create a user</h2>
              <p>
                New users are automatically created inside the current tenant.
              </p>
            </div>
          </div>

          <form class="stack-form" @submit.prevent="createUser">
            <label class="form-label" for="fullName">Full name</label>
            <input
              id="fullName"
              v-model="form.fullName"
              class="form-control"
              required
            />

            <label class="form-label" for="email">Email</label>
            <input
              id="email"
              v-model="form.email"
              class="form-control"
              type="email"
              required
            />

            <label class="form-label" for="password">Temporary password</label>
            <input
              id="password"
              v-model="form.password"
              class="form-control"
              type="password"
              minlength="8"
              required
            />

            <label class="form-label" for="role">Role</label>
            <select id="role" v-model="form.role" class="form-select">
              <option value="Admin">Admin</option>
              <option value="Operator">Operator</option>
              <option value="Driver">Driver</option>
            </select>

            <div v-if="formError" class="alert alert-danger mb-0">
              {{ formError }}
            </div>
            <div v-if="formSuccess" class="alert alert-success mb-0">
              {{ formSuccess }}
            </div>

            <button
              class="btn btn-primary"
              type="submit"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? "Creating..." : "Create user" }}
            </button>
          </form>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { apiRequest } from "../services/api";
import { useSessionStore } from "../features/auth/store";

type UserSummary = {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  isActive: boolean;
};

const session = useSessionStore();
const users = ref<UserSummary[]>([]);
const usersError = ref("");
const isLoadingUsers = ref(false);
const isSubmitting = ref(false);
const formError = ref("");
const formSuccess = ref("");

const form = reactive({
  fullName: "",
  email: "",
  password: "Operator123!",
  role: "Operator",
});

async function loadUsers() {
  if (!session.accessToken) {
    return;
  }

  isLoadingUsers.value = true;
  usersError.value = "";
  try {
    users.value = await apiRequest<UserSummary[]>("/api/v1/admin/users", {
      token: session.accessToken,
    });
  } catch (error) {
    usersError.value =
      error instanceof Error
        ? "Unable to load organization users."
        : "Unable to load users.";
  } finally {
    isLoadingUsers.value = false;
  }
}

async function createUser() {
  if (!session.accessToken) {
    return;
  }

  isSubmitting.value = true;
  formError.value = "";
  formSuccess.value = "";
  try {
    await apiRequest<UserSummary>("/api/v1/admin/users", {
      method: "POST",
      token: session.accessToken,
      body: form,
    });
    formSuccess.value = "User created successfully.";
    form.fullName = "";
    form.email = "";
    form.password = "Operator123!";
    form.role = "Operator";
    await loadUsers();
  } catch (error) {
    formError.value =
      error instanceof Error
        ? "Unable to create the user with the provided data."
        : "Unable to create the user.";
  } finally {
    isSubmitting.value = false;
  }
}

onMounted(loadUsers);
</script>
