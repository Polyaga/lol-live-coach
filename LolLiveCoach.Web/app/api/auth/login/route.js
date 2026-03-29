import { NextResponse } from "next/server";
import { appendQueryToPath, getSafeRedirectPath } from "../../../../lib/navigation";
import {
  authenticateUser,
  buildSessionCookie,
  createWebSession
} from "../../../../lib/auth";
import { hasAdminAccess } from "../../../../lib/admin-rbac";

export const runtime = "nodejs";

function getRedirectTarget(formData, user) {
  const plan = String(formData.get("plan") || "").trim();
  const requestedNext = sanitizeRequestedNext(formData.get("next"), user);

  return plan ? appendQueryToPath("/compte", { plan }) : requestedNext;
}

function sanitizeRequestedNext(nextValue, user) {
  const fallback = hasAdminAccess(user) ? "/admin" : "/compte";
  return getSafeRedirectPath(nextValue, fallback);
}

export async function POST(request) {
  const formData = await request.formData();
  const email = String(formData.get("email") || "").trim();
  const password = String(formData.get("password") || "").trim();
  const requestedNext = sanitizeRequestedNext(formData.get("next"), null);
  const user = await authenticateUser(email, password);

  if (!user) {
    const errorBasePath = requestedNext.startsWith("/admin") ? "/admin/login" : "/login";

    return NextResponse.redirect(
      new URL(appendQueryToPath(errorBasePath, {
        error: 1,
        next: getSafeRedirectPath(formData.get("next"), "/compte"),
        plan: String(formData.get("plan") || "").trim()
      }), request.url),
      303
    );
  }

  const resolvedNext = sanitizeRequestedNext(formData.get("next"), user);

  if (resolvedNext.startsWith("/admin") && !hasAdminAccess(user)) {
    return NextResponse.redirect(new URL("/admin/login?error=forbidden", request.url), 303);
  }

  const redirectTarget = getRedirectTarget(formData, user);
  const sessionToken = await createWebSession(user.id);
  const response = NextResponse.redirect(new URL(redirectTarget, request.url), 303);
  response.cookies.set(buildSessionCookie(sessionToken));
  return response;
}
