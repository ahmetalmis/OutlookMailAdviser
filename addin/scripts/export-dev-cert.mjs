import { mkdirSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const certificateDirectory = join(root, ".certs");
const certificatePath = join(certificateDirectory, "localhost.pfx");

mkdirSync(certificateDirectory, { recursive: true });

const result = spawnSync("dotnet", [
  "dev-certs",
  "https",
  "--trust",
  "--export-path",
  certificatePath,
  "--password",
  "outlook-mail-adviser-dev",
], { stdio: "inherit", shell: process.platform === "win32" });

if (result.error) {
  throw result.error;
}

process.exit(result.status ?? 1);
