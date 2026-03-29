import { NextResponse } from "next/server";
import { adminHasPermission, getAdminSession } from "../../../../../lib/admin-auth";
import { ADMIN_PERMISSIONS } from "../../../../../lib/admin-rbac";
import { createAdminAmbassadorApplication } from "../../../../../lib/admin-store";
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
  const redirectParams = getAdminSectionParams(formData, "ambassadors");

  if (!adminHasPermission(session, ADMIN_PERMISSIONS.AMBASSADORS_MANAGE)) {
    return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, error: "forbidden" }), 303);
  }

  try {
    await createAdminAmbassadorApplication({
      fullName: asText(formData, "fullName"),
      email: asText(formData, "email"),
      channelName: asText(formData, "channelName"),
      platform: asText(formData, "platform"),
      channelUrl: asText(formData, "channelUrl"),
      status: asText(formData, "status"),
      commissionRate: asText(formData, "commissionRate"),
      affiliateCode: asText(formData, "affiliateCode"),
      accessGranted: formData.get("accessGranted") === "on",
      audienceSummary: asText(formData, "audienceSummary"),
      motivation: asText(formData, "motivation"),
      adminNotes: asText(formData, "adminNotes")
    }, session.user.id);
  } catch (error) {
    return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, error: mapAdminErrorToQuery(error) }), 303);
  }

  return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, notice: "ambassador-created" }), 303);
}
