import { NextResponse } from "next/server";
import { handleStripeEvent } from "../../../../lib/billing";
import { getStripeClient, getStripeWebhookSecret } from "../../../../lib/stripe";

export const runtime = "nodejs";

export async function POST(request) {
  const stripe = getStripeClient();
  const signature = request.headers.get("stripe-signature");

  if (!signature) {
    return NextResponse.json(
      {
        error: "Signature Stripe manquante."
      },
      {
        status: 400
      }
    );
  }

  try {
    const payload = await request.text();
    const event = stripe.webhooks.constructEvent(payload, signature, getStripeWebhookSecret());
    await handleStripeEvent(event);

    return NextResponse.json({
      received: true
    });
  } catch (error) {
    return NextResponse.json(
      {
        error: error instanceof Error ? error.message : "Webhook Stripe invalide."
      },
      {
        status: 400
      }
    );
  }
}
