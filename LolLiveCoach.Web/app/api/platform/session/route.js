import { NextResponse } from "next/server";
import { authenticateUser, createDesktopToken, revokeDesktopTokenByRawToken } from "../../../../lib/auth";
import { getAccountOverviewByUserId } from "../../../../lib/account-store";
import { buildAccountAccess } from "../../../../lib/account-access";

export const runtime = "nodejs";

export async function POST(request) {
  const body = await request.json().catch(() => null);
  const email = String(body?.email || "").trim();
  const password = String(body?.password || "").trim();
  const deviceName = String(body?.deviceName || "Desktop Windows").trim();

  const user = await authenticateUser(email, password);

  if (!user) {
    return NextResponse.json(
      {
        error: "Identifiants invalides."
      },
      {
        status: 401
      }
    );
  }

  const desktopToken = await createDesktopToken(user.id, deviceName);
  const account = await getAccountOverviewByUserId(user.id, {
    origin: request.nextUrl.origin
  });
  const access = buildAccountAccess({
    subscriptions: account?.subscriptions,
    ambassadorApplications: account?.ambassadorApplications,
    accessGrants: account?.accessGrants,
    origin: request.nextUrl.origin
  });

  return NextResponse.json({
    token: desktopToken.rawToken,
    expiresAt: desktopToken.expiresAt,
    user: {
      id: user.id,
      email: user.email,
      displayName: user.displayName
    },
    access
  });
}

export async function DELETE(request) {
  const authorization = request.headers.get("authorization") || request.headers.get("x-lol-access-key");

  await revokeDesktopTokenByRawToken(authorization || "");

  return NextResponse.json({
    success: true
  });
}
