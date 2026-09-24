import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

export default defineConfig(async ({ command }) => ({
  base: command === "build" ? "/addin/" : "/",
  plugins: [react()],
  server: {
    host: "0.0.0.0",
    port: 3000,
    strictPort: true,
    https: command === "serve"
      ? {
          pfx: readFileSync(resolve(".certs/localhost.pfx")),
          passphrase: "outlook-mail-adviser-dev",
        }
      : undefined,
  },
  preview: {
    port: 3000,
    strictPort: true,
  },
}));
