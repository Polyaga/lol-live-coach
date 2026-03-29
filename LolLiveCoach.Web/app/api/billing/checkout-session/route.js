import { NextResponse } from "next/server";
import { getCurrentUser } from "../../../../lib/auth";
import { createCheckoutSessionForUser } from "../../../../lib/billing";

export const runtime = "nodejs";

export async function POST(request) {
  const body = await request.json().catch(() => null);
  const planId = body?.planId?.trim();
  const affiliateCode = body?.affiliateCode?.trim();
  const user = await getCurrentUser();

  if (!planId) {
    return NextResponse.json(
      {
        error: "Le champ planId est requis."
      },
      {
        status: 400
      }
    );
  }

  if (!user) {
    return NextResponse.json(
      {
        error: "Vous devez creer un compte ou vous connecter avant de lancer le checkout."
      },
      {
        status: 401
      }
    );
  }

  try {
    const session = await createCheckoutSessionForUser({
      user,
      planId,
      origin: request.nextUrl.origin,
      affiliateCode
    });

    return NextResponse.json({
      sessionId: session.sessionId,
      url: session.url
    });
  } catch (error) {
    return NextResponse.json(
      {
        error: error instanceof Error ? error.message : "Le checkout n'a pas pu etre initialise."
      },
      {
        status: 503
      }
    );
  }
}
