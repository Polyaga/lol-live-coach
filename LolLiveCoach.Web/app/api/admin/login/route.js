import { NextResponse } from "next/server";
import {
  authenticateUser,
  buildSessionCookie,
  createWebSession
} from "../../../../lib/auth";
import { hasAdminAccess } from "../../../../lib/admin-rbac";

export const runtime = "nodejs";

export async function POST(request) {
  const formData = await request.formData();
  const email = String(formData.get("email") || "").trim();
  const password = String(formData.get("password") || "").trim();
  const user = await authenticateUser(email, password);

  if (!user) {
    return NextResponse.redirect(new URL("/admin/login?error=1", request.url), 303);
  }

  if (!hasAdminAccess(user)) {
    return NextResponse.redirect(new URL("/admin/login?error=forbidden", request.url), 303);
  }

  const sessionToken = await createWebSession(user.id);
  const response = NextResponse.redirect(new URL("/admin", request.url), 303);
  response.cookies.set(buildSessionCookie(sessionToken));
  return response;
}
