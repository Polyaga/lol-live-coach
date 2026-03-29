import { config } from "dotenv";
import { defineConfig, env } from "prisma/config";

const isProduction = process.env.PRODUCTION === "true";

config({
  path: isProduction ? ".env" : ".env.local"
});

export default defineConfig({
  schema: "prisma/schema.prisma",
  migrations: {
    path: "prisma/migrations"
  },
  datasource: {
    url: env("DATABASE_URL")
  }
});
