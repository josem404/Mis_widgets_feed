import { defineConfig } from "vitest/config";

export default defineConfig({
  base: "/Mis_widgets_feed/",
  build: {
    target: "es2022",
    sourcemap: true,
  },
  test: {
    environment: "node",
    include: ["tests/**/*.test.ts"],
  },
});
