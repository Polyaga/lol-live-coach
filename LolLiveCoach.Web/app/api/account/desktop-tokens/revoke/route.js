import { NextResponse } from "next/server";
import { getCurrentUser, revokeDesktopTokenById } from "../../../../../lib/auth";

export const runtime = "nodejs";

export async function POST(request) {
  const user = await getCurrentUser();

  if (!user) {
    return NextResponse.redirect(new URL("/login", request.url), 303);
  }

  const formData = await request.formData();
  const tokenId = String(formData.get("tokenId") || "").trim();

  if (tokenId) {
    await revokeDesktopTokenById(user.id, tokenId);
  }

  return NextResponse.redirect(new URL("/compte", request.url), 303);
}
