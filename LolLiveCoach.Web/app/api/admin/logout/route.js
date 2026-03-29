import { NextResponse } from "next/server";
import { buildClearedSessionCookie, revokeCurrentWebSession } from "../../../../lib/auth";

export const runtime = "nodejs";

export async function POST(request) {
  await revokeCurrentWebSession();
  const response = NextResponse.redirect(new URL("/admin/login", request.url), 303);
  response.cookies.set(buildClearedSessionCookie());
  return response;
}
