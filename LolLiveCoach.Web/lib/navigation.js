export function getSafeRedirectPath(value, fallback = "/compte") {
  const candidate = String(value || "").trim();

  if (!candidate.startsWith("/") || candidate.startsWith("//")) {
    return fallback;
  }

  return candidate;
}

export function appendQueryToPath(path, params) {
  const url = new URL(path, "http://localhost");

  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") {
      continue;
    }

    url.searchParams.set(key, String(value));
  }

  return `${url.pathname}${url.search}`;
}
