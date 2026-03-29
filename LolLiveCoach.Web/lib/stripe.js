import Stripe from "stripe";

const globalForStripe = globalThis;

function getStripeSecretKey() {
  const secretKey = process.env.STRIPE_SECRET_KEY;

  if (!secretKey) {
    throw new Error("Stripe n'est pas configure. Renseigne STRIPE_SECRET_KEY dans l'environnement du site web.");
  }

  return secretKey;
}

export function getStripeWebhookSecret() {
  const secret = process.env.STRIPE_WEBHOOK_SECRET;

  if (!secret) {
    throw new Error("Le webhook Stripe n'est pas configure. Renseigne STRIPE_WEBHOOK_SECRET.");
  }

  return secret;
}

export function getStripeClient() {
  if (!globalForStripe.stripeClient) {
    globalForStripe.stripeClient = new Stripe(getStripeSecretKey());
  }

  return globalForStripe.stripeClient;
}
