import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig({
  plugins: [vue()],
  test: { globals: true, environment: "jsdom" },
  server: {
    port: 5183,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true },
      "/hubs": {
        target: "http://localhost:5080",
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
