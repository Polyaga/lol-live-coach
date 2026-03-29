import { NextResponse } from "next/server";
import { verifyDesktopToken } from "../../../../lib/auth";
import { getPlayerProfile } from "../../../../lib/riot-profile";

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

  const riotId = request.nextUrl.searchParams.get("riotId") || "";
  const platformRegion = request.nextUrl.searchParams.get("platformRegion") || "";
  const profile = await getPlayerProfile({
    riotId,
    platformRegion
  });

  return NextResponse.json(profile);
}
