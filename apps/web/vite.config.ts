import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

const apiTarget = process.env.VITE_API_BASE_URL ?? "http://localhost:5080";

export default defineConfig({
  plugins: [vue()],
  test: {
    globals: true,
    environment: "jsdom",
    include: ["src/**/*.spec.ts"],
    exclude: ["e2e/**", "playwright.config.ts"],
  },
  server: {
    port: 5183,
    proxy: {
      "/api": { target: apiTarget, changeOrigin: true },
      "/hubs": {
        target: apiTarget,
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
