import { NextResponse } from "next/server";
import { ADMIN_PERMISSIONS } from "../../../../../lib/admin-rbac";
import { adminHasPermission, getAdminSession } from "../../../../../lib/admin-auth";
import { updateAdminAmbassadorApplication } from "../../../../../lib/admin-store";
import { buildAdminRedirectUrl, getAdminSectionParams, mapAdminErrorToQuery } from "../../../../../lib/admin-route";

export const runtime = "nodejs";

function asText(formData, key) {
  return String(formData.get(key) || "").trim();
}

function getOptionalText(formData, key) {
  return formData.has(key) ? asText(formData, key) : undefined;
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
  const id = asText(formData, "id");

  if (!id) {
    return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, error: "missing-application" }), 303);
  }

  try {
    const updates = {
      status: getOptionalText(formData, "status"),
      affiliateCode: getOptionalText(formData, "affiliateCode"),
      referredRevenue: getOptionalText(formData, "referredRevenue"),
      paidOutAmount: getOptionalText(formData, "paidOutAmount"),
      adminNotes: getOptionalText(formData, "adminNotes")
    };

    if (formData.has("accessGranted")) {
      updates.accessGranted = formData.get("accessGranted") === "on";
    }

    await updateAdminAmbassadorApplication(id, updates, session.user.id);
  } catch (error) {
    return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, error: mapAdminErrorToQuery(error) }), 303);
  }

  return NextResponse.redirect(buildAdminRedirectUrl(request, { ...redirectParams, notice: "ambassador-updated" }), 303);
}
