import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");            // reads .env files + process env, no `process.env` typing issues
  const target = env.VITE_API_TARGET || "http://localhost:5000";
  return {
    plugins: [react()],
    server: { port: 5173, proxy: { "/api": { target, changeOrigin: true }, "/hubs": { target, changeOrigin: true, ws: true } } },
  };
});
