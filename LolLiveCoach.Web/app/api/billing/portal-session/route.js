import { NextResponse } from "next/server";
import { createBillingPortalForUser } from "../../../../lib/billing";
import { getCurrentUser } from "../../../../lib/auth";

export const runtime = "nodejs";

export async function POST(request) {
  const user = await getCurrentUser();

  if (!user) {
    return NextResponse.json(
      {
        error: "Vous devez etre connecte pour gerer votre abonnement."
      },
      {
        status: 401
      }
    );
  }

  try {
    const url = await createBillingPortalForUser({
      user,
      origin: request.nextUrl.origin
    });

    return NextResponse.json({ url });
  } catch (error) {
    return NextResponse.json(
      {
        error: error instanceof Error ? error.message : "Le portail client n'a pas pu etre initialise."
      },
      {
        status: 503
      }
    );
  }
}
