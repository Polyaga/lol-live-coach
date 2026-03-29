import crypto from "node:crypto";
import { promisify } from "node:util";

const scryptAsync = promisify(crypto.scrypt);
const PASSWORD_KEY_LENGTH = 64;

export function normalizeEmail(value) {
  return String(value || "").trim().toLowerCase();
}

export function createOpaqueToken(bytes = 32) {
  return crypto.randomBytes(bytes).toString("base64url");
}

export function sha256(value) {
  return crypto.createHash("sha256").update(value).digest("hex");
}

export function assertPasswordPolicy(password) {
  if (String(password || "").length < 8) {
    throw new Error("Le mot de passe doit contenir au moins 8 caracteres.");
  }
}

export async function hashPassword(password) {
  assertPasswordPolicy(password);

  const salt = crypto.randomBytes(16).toString("hex");
  const derivedKey = await scryptAsync(password, salt, PASSWORD_KEY_LENGTH);

  return `scrypt:${salt}:${Buffer.from(derivedKey).toString("hex")}`;
}

export async function verifyPassword(password, storedHash) {
  const [algorithm, salt, digest] = String(storedHash || "").split(":");

  if (algorithm !== "scrypt" || !salt || !digest) {
    return false;
  }

  const derivedKey = await scryptAsync(password, salt, PASSWORD_KEY_LENGTH);
  const computedDigest = Buffer.from(derivedKey).toString("hex");
  const expected = Buffer.from(digest, "hex");
  const actual = Buffer.from(computedDigest, "hex");

  return expected.length === actual.length && crypto.timingSafeEqual(expected, actual);
}
