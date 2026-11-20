import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The browser only ever talks to this dev server (port 5174; 5173 is often taken by other Vite projects). Calls to /api/* are forwarded to the
// ASP.NET API, so no CORS setup is needed. In Docker the API is reachable by its service name "api".
const apiTarget = process.env.VITE_API_TARGET ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react()],
  server: {
    host: "0.0.0.0",
    port: 5174,
    strictPort: true,
    proxy: { "/api": { target: apiTarget, changeOrigin: true } },
    // Files edited on Windows reach the Linux container without change events, so poll for changes.
    watch: process.env.CHOKIDAR_USEPOLLING ? { usePolling: true, interval: 300 } : undefined,
  },
});
