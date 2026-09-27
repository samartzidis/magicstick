import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// Tauri expects a fixed dev-server port and serves the production build from ./dist.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  clearScreen: false,
  server: {
    port: 1420,
    strictPort: true,
    watch: {
      // Rust and .NET rebuilds must not trigger frontend reloads.
      ignored: ["**/src-tauri/**", "**/src-dotnet/**"],
    },
  },
});
