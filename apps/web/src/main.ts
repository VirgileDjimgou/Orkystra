import { createApp } from "vue";
import "bootstrap/dist/css/bootstrap.min.css";
import "leaflet/dist/leaflet.css";
import "./styles/theme.css";
import App from "./App.vue";
import { pinia } from "./pinia";
import { router } from "./router";

createApp(App).use(pinia).use(router).mount("#app");
