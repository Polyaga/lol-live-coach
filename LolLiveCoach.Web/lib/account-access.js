import { resolveSiteUrl } from "./site-config";

const ACTIVE_SUBSCRIPTION_STATUSES = new Set(["active", "trialing", "past_due"]);

function asDate(value) {
  return value instanceof Date ? value : value ? new Date(value) : null;
}

function hasActiveManualPremiumGrant(accessGrants = []) {
  const now = new Date();

  return accessGrants.some((grant) => {
    const startsAt = asDate(grant.startsAt) || now;
    const endsAt = asDate(grant.endsAt);

    return !grant.revokedAt
      && String(grant.type || "").toUpperCase() === "PREMIUM"
      && startsAt <= now
      && (!endsAt || endsAt >= now);
  });
}

function getPrimarySubscription(subscriptions = []) {
  return subscriptions.find((subscription) =>
    ACTIVE_SUBSCRIPTION_STATUSES.has(String(subscription.stripeStatus || "").toLowerCase()))
    || subscriptions[0]
    || null;
}

export function hasPremiumSubscription(subscriptions = []) {
  return subscriptions.some((subscription) =>
    ACTIVE_SUBSCRIPTION_STATUSES.has(String(subscription.stripeStatus || "").toLowerCase()));
}

export function hasAmbassadorAccess(applications = []) {
  return applications.some((application) =>
    application.status === "approved" && application.accessGranted);
}

export function buildAccountAccess({ subscriptions = [], ambassadorApplications = [], accessGrants = [], origin } = {}) {
  const siteUrl = resolveSiteUrl(origin);
  const premiumFromSubscription = hasPremiumSubscription(subscriptions);
  const premiumFromAmbassador = hasAmbassadorAccess(ambassadorApplications);
  const premiumFromManualGrant = hasActiveManualPremiumGrant(accessGrants);
  const hasPremiumAccess = premiumFromSubscription || premiumFromAmbassador || premiumFromManualGrant;
  const primarySubscription = getPrimarySubscription(subscriptions);
  const tier = hasPremiumAccess ? "Premium" : "Free";

  let statusTitle = hasPremiumAccess ? "Abonnement Premium actif" : "Mode Free actif";
  let statusMessage = hasPremiumAccess
    ? "L'overlay in-game, les alertes detaillees et l'historique live sont debloques."
    : "Le tableau de bord et le conseil principal restent disponibles. L'overlay in-game et les alertes detaillees sont reserves au premium.";

  if (!premiumFromSubscription && premiumFromAmbassador) {
    statusTitle = "Acces ambassadeur actif";
    statusMessage = "Votre acces premium est actuellement accorde par le programme ambassadeur.";
  }

  if (!premiumFromSubscription && !premiumFromAmbassador && premiumFromManualGrant) {
    statusTitle = "Acces premium manuel actif";
    statusMessage = "Votre acces premium est actuellement accorde manuellement par l'equipe.";
  }

  if (primarySubscription?.cancelAtPeriodEnd && primarySubscription.currentPeriodEnd) {
    statusMessage = `L'abonnement reste actif jusqu'au ${new Intl.DateTimeFormat("fr-FR", {
      dateStyle: "medium"
    }).format(asDate(primarySubscription.currentPeriodEnd))}.`;
  }

  return {
    tier,
    hasPremiumAccess,
    premiumSource: premiumFromSubscription
      ? "subscription"
      : premiumFromAmbassador
        ? "ambassador"
        : premiumFromManualGrant
          ? "manual"
          : "free",
    canUseOverlayPreview: true,
    canUseInGameOverlay: hasPremiumAccess,
    canSeeDetailedAdvice: hasPremiumAccess,
    canSeeDetailedAlerts: hasPremiumAccess,
    canUseNotificationBubbles: hasPremiumAccess,
    canUseNotificationHistory: hasPremiumAccess,
    statusTitle,
    statusMessage,
    upgradeUrl: `${siteUrl}/compte?plan=monthly`,
    manageSubscriptionUrl: `${siteUrl}/compte`,
    currentPlanKey: primarySubscription?.planKey || null,
    currentPeriodEnd: primarySubscription?.currentPeriodEnd || null,
    cancelAtPeriodEnd: Boolean(primarySubscription?.cancelAtPeriodEnd)
  };
}
