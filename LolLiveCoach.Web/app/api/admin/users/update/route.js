import { NextResponse } from "next/server";
import { getAdminSession } from "../../../../../lib/admin-auth";
import { buildAdminRedirectUrl, getAdminSectionParams } from "../../../../../lib/admin-route";

export const runtime = "nodejs";

export async function POST(request) {
  const session = await getAdminSession();

  if (!session) {
    return NextResponse.redirect(new URL("/admin/login", request.url), 303);
  }

  const formData = await request.formData();
  return NextResponse.redirect(buildAdminRedirectUrl(request, {
    ...getAdminSectionParams(formData, "users"),
    error: "forbidden"
  }), 303);
}
