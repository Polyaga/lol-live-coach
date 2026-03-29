import { NextResponse } from "next/server";
import { verifyDesktopToken } from "../../../../lib/auth";
import { buildAccountAccess } from "../../../../lib/account-access";

export const runtime = "nodejs";

export async function GET(request) {
  const rawToken =
    request.headers.get("authorization")
    || request.headers.get("x-lol-access-key")
    || request.nextUrl.searchParams.get("token")
    || "";

  const desktopToken = await verifyDesktopToken(rawToken);

  if (!desktopToken) {
    return NextResponse.json(
      {
        error: "Jeton desktop invalide ou expire."
      },
      {
        status: 401
      }
    );
  }

  const access = buildAccountAccess({
    subscriptions: desktopToken.user.subscriptions,
    ambassadorApplications: desktopToken.user.ambassadorApplications,
    accessGrants: desktopToken.user.accessGrants,
    origin: request.nextUrl.origin
  });

  return NextResponse.json({
    user: {
      id: desktopToken.user.id,
      email: desktopToken.user.email,
      displayName: desktopToken.user.displayName
    },
    access
  });
}
