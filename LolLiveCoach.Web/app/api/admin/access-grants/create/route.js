import { NextResponse } from "next/server";
import { adminHasPermission, getAdminSession } from "../../../../../lib/admin-auth";
import { ADMIN_PERMISSIONS } from "../../../../../lib/admin-rbac";
import { createManualAccessGrant } from "../../../../../lib/admin-store";
import { buildAdminRedirectUrl, getAdminSectionParams, mapAdminErrorToQuery } from "../../../../../lib/admin-route";

export const runtime = "nodejs";

function asText(formData, key) {
  return String(formData.get(key) || "").trim();
}

export async function POST(request) {
  const session = await getAdminSession();

  if (!session) {
    return NextResponse.redirect(new URL("/admin/login", request.url), 303);
  }

  const formData = await request.formData();
  const redirectParams = getAdminSectionParams(formData, "access");

  if (!adminHasPermission(session, ADMIN_PERMISSIONS.ACCESS_MANAGE)) {
    return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, error: "forbidden" }), 303);
  }

  try {
    await createManualAccessGrant({
      email: asText(formData, "email"),
      reason: asText(formData, "reason"),
      startsAt: asText(formData, "startsAt"),
      endsAt: asText(formData, "endsAt"),
      notes: asText(formData, "notes")
    }, session.user.id);
  } catch (error) {
    return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, error: mapAdminErrorToQuery(error) }), 303);
  }

  return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, notice: "access-created" }), 303);
}
