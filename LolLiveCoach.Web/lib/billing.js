import { prisma } from "./prisma";
import { getPlanById, getPlanByStripePriceId, resolveSiteUrl } from "./site-config";
import { getStripeClient } from "./stripe";

function normalizeMode(mode) {
  return mode === "payment" ? "payment" : "subscription";
}

function toDate(unixTimestamp) {
  return unixTimestamp ? new Date(unixTimestamp * 1000) : null;
}

async function attachStripeCustomerToUser(userId, stripeCustomerId) {
  if (!userId || !stripeCustomerId) {
    return;
  }

  await prisma.user.update({
    where: {
      id: userId
    },
    data: {
      stripeCustomerId
    }
  }).catch(() => null);
}

async function resolveUserForStripe({ userId, stripeCustomerId }) {
  if (userId) {
    const user = await prisma.user.findUnique({
      where: {
        id: userId
      }
    });

    if (user) {
      return user;
    }
  }

  if (!stripeCustomerId) {
    return null;
  }

  return prisma.user.findUnique({
    where: {
      stripeCustomerId
    }
  });
}

export async function createCheckoutSessionForUser({ user, planId, origin, affiliateCode = "" }) {
  const plan = getPlanById(planId);

  if (!plan) {
    throw new Error("L'offre demandee n'existe pas.");
  }

  if (!plan.stripePriceId) {
    throw new Error(`Le plan "${planId}" n'a pas de Price ID Stripe configure.`);
  }

  const stripe = getStripeClient();
  const siteUrl = resolveSiteUrl(origin);
  const mode = normalizeMode(plan.mode);

  const session = await stripe.checkout.sessions.create({
    success_url: `${siteUrl}/success?session_id={CHECKOUT_SESSION_ID}`,
    cancel_url: `${siteUrl}/cancel`,
    line_items: [
      {
        price: plan.stripePriceId,
        quantity: 1
      }
    ],
    mode,
    allow_promotion_codes: true,
    billing_address_collection: "auto",
    locale: "fr",
    tax_id_collection: {
      enabled: true
    },
    customer: user.stripeCustomerId || undefined,
    customer_email: user.stripeCustomerId ? undefined : user.email,
    client_reference_id: user.id,
    metadata: {
      user_id: user.id,
      plan_id: plan.id,
      product_name: process.env.NEXT_PUBLIC_PRODUCT_NAME || "LoL Live Coach",
      affiliate_code: affiliateCode || undefined
    },
    subscription_data: mode === "subscription"
      ? {
        metadata: {
          user_id: user.id,
          plan_id: plan.id,
          affiliate_code: affiliateCode || undefined
        }
      }
      : undefined,
    customer_creation: mode === "payment" ? "always" : undefined
  });

  if (!session.url || !session.id) {
    throw new Error("Stripe n'a pas retourne d'URL de redirection exploitable.");
  }

  return {
    sessionId: session.id,
    url: session.url
  };
}

export async function createBillingPortalForUser({ user, origin }) {
  const freshUser = await prisma.user.findUnique({
    where: {
      id: user.id
    },
    include: {
      subscriptions: {
        orderBy: {
          createdAt: "desc"
        },
        take: 1
      }
    }
  });

  const stripeCustomerId =
    freshUser?.stripeCustomerId
    || freshUser?.subscriptions?.[0]?.stripeCustomerId
    || user.stripeCustomerId;

  if (!stripeCustomerId) {
    throw new Error("Aucun compte Stripe n'est encore rattache a cet utilisateur.");
  }

  if (!freshUser?.stripeCustomerId && freshUser?.id) {
    await attachStripeCustomerToUser(freshUser.id, stripeCustomerId);
  }

  const stripe = getStripeClient();
  const session = await stripe.billingPortal.sessions.create({
    customer: stripeCustomerId,
    return_url: `${resolveSiteUrl(origin)}/compte`
  });

  return session.url;
}

export async function syncSubscriptionFromStripeSubscription(subscription) {
  const stripeCustomerId = String(subscription.customer || "");
  const userIdFromMetadata = subscription.metadata?.user_id || null;
  const user = await resolveUserForStripe({
    userId: userIdFromMetadata,
    stripeCustomerId
  });

  if (!user) {
    return null;
  }

  if (stripeCustomerId && user.stripeCustomerId !== stripeCustomerId) {
    await attachStripeCustomerToUser(user.id, stripeCustomerId);
  }

  const primaryItem = subscription.items?.data?.[0];
  const stripePriceId = primaryItem?.price?.id || "";
  const plan = getPlanByStripePriceId(stripePriceId);

  return prisma.subscription.upsert({
    where: {
      stripeSubscriptionId: subscription.id
    },
    create: {
      userId: user.id,
      stripeSubscriptionId: subscription.id,
      stripeCustomerId,
      stripePriceId,
      stripeStatus: subscription.status,
      planKey: plan?.id || subscription.metadata?.plan_id || null,
      billingInterval: primaryItem?.price?.recurring?.interval || null,
      currentPeriodEnd: toDate(subscription.current_period_end),
      cancelAtPeriodEnd: Boolean(subscription.cancel_at_period_end)
    },
    update: {
      userId: user.id,
      stripeCustomerId,
      stripePriceId,
      stripeStatus: subscription.status,
      planKey: plan?.id || subscription.metadata?.plan_id || null,
      billingInterval: primaryItem?.price?.recurring?.interval || null,
      currentPeriodEnd: toDate(subscription.current_period_end),
      cancelAtPeriodEnd: Boolean(subscription.cancel_at_period_end)
    }
  });
}

export async function handleCheckoutSessionCompleted(session) {
  const stripeCustomerId = String(session.customer || "");
  const userId = session.client_reference_id || session.metadata?.user_id || null;
  const user = await resolveUserForStripe({
    userId,
    stripeCustomerId
  });

  if (!user) {
    return null;
  }

  if (stripeCustomerId && user.stripeCustomerId !== stripeCustomerId) {
    await attachStripeCustomerToUser(user.id, stripeCustomerId);
  }

  if (!session.subscription) {
    return null;
  }

  const stripe = getStripeClient();
  const subscription = typeof session.subscription === "string"
    ? await stripe.subscriptions.retrieve(session.subscription)
    : session.subscription;

  return syncSubscriptionFromStripeSubscription(subscription);
}

export async function syncCheckoutSessionById(sessionId) {
  const stripe = getStripeClient();
  const session = await stripe.checkout.sessions.retrieve(sessionId, {
    expand: ["subscription"]
  });

  await handleCheckoutSessionCompleted(session);
  return session;
}

export async function handleStripeEvent(event) {
  switch (event.type) {
    case "checkout.session.completed":
      await handleCheckoutSessionCompleted(event.data.object);
      break;
    case "customer.subscription.created":
    case "customer.subscription.updated":
    case "customer.subscription.deleted":
      await syncSubscriptionFromStripeSubscription(event.data.object);
      break;
    default:
      break;
  }
}
