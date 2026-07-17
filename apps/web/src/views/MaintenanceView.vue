<template>
  <main class="page-stack">
    <section class="page-heading">
      <p class="eyebrow">Fleet availability</p>
      <h1>Maintenance work orders</h1>
      <p>
        Schedule repairs, track costs and return vehicles to service with an
        auditable history.
      </p>
    </section>
    <p v-if="error" class="notice notice-error" role="alert">{{ error }}</p>
    <section class="panel">
      <div class="panel-heading">
        <h2>Open backlog</h2>
        <button type="button" :disabled="loading" @click="load">Refresh</button>
      </div>
      <p v-if="loading">Loading work orders…</p>
      <p v-else-if="orders.length === 0">
        No maintenance work orders are open.
      </p>
      <div v-else class="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Vehicle</th>
              <th>Work order</th>
              <th>Due</th>
              <th>Status</th>
              <th>Availability</th>
              <th>Cost</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="order in orders" :key="order.id">
              <td>{{ order.vehicleRegistrationNumber }}</td>
              <td>
                <div>
                  <strong>{{ order.title }}</strong>
                </div>
                <small>{{ order.sourceKey }}</small>
              </td>
              <td>{{ formatDate(order.dueAtUtc) }}</td>
              <td>{{ order.status }}</td>
              <td>
                {{ order.isVehicleUnavailable ? "Unavailable" : "Available" }}
              </td>
              <td>{{ formatMoney(order.totalCost, order.currencyCode) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </main>
</template>
<script setup lang="ts">
import { onMounted, ref } from "vue";
import { apiRequest } from "../services/api";
type WorkOrder = {
  id: string;
  vehicleRegistrationNumber: string;
  title: string;
  sourceKey: string;
  dueAtUtc: string;
  status: string;
  isVehicleUnavailable: boolean;
  totalCost: number;
  currencyCode: string;
};
const orders = ref<WorkOrder[]>([]);
const loading = ref(false);
const error = ref("");
async function load() {
  loading.value = true;
  error.value = "";
  try {
    orders.value = await apiRequest<WorkOrder[]>(
      "/api/v1/maintenance/work-orders/",
    );
  } catch {
    error.value = "Maintenance work orders could not be loaded.";
  } finally {
    loading.value = false;
  }
}
function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
    new Date(value),
  );
}
function formatMoney(value: number, currency: string) {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency,
  }).format(value);
}
onMounted(load);
</script>
