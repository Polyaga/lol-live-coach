import { appendQueryToPath } from "./navigation";
import { isAdminMutationError } from "./admin-store";

export function buildAdminRedirectUrl(request, params = {}) {
  return new URL(appendQueryToPath("/admin", params), request.url);
}

export function getAdminSectionParams(formData, fallbackSection = "dashboard") {
  const section = String(formData?.get?.("section") || fallbackSection).trim();
  return section ? { section } : {};
}

export function mapAdminErrorToQuery(error) {
  if (isAdminMutationError(error)) {
    const target = Array.isArray(error.meta?.target) ? error.meta.target.join(",") : String(error.meta?.target || "");

    if (target.includes("email")) {
      return "duplicate-email";
    }

    if (target.includes("key")) {
      return "duplicate-role-key";
    }
  }

  if (!(error instanceof Error)) {
    return "missing-fields";
  }

  switch (error.message) {
    case "USER_NOT_FOUND":
      return "missing-user";
    case "ROLE_NOT_FOUND":
      return "missing-role";
    case "ACCESS_GRANT_NOT_FOUND":
      return "missing-access-grant";
    case "EMAIL_AND_PASSWORD_REQUIRED":
    case "ROLE_NAME_REQUIRED":
    case "ACCESS_REASON_REQUIRED":
      return "missing-fields";
    case "ACCESS_END_BEFORE_START":
      return "missing-fields";
    case "SELF_ROLE_REMOVAL_FORBIDDEN":
      return "self-role-removal";
    case "Candidature ambassadeur introuvable.":
      return "missing-application";
    case "Le mot de passe doit contenir au moins 8 caracteres.":
      return "password-too-short";
    default:
      return "missing-fields";
  }
}
