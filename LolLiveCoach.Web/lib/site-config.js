const productName = process.env.NEXT_PUBLIC_PRODUCT_NAME || "LoL Live Coach";
const supportEmail = process.env.NEXT_PUBLIC_SUPPORT_EMAIL || "hello@lol-live-coach.com";
const downloadUrl = process.env.DESKTOP_DOWNLOAD_URL || process.env.NEXT_PUBLIC_DESKTOP_DOWNLOAD_URL || "";

const plans = [
  {
    id: "yearly",
    mode: "subscription",
    stripePriceId: process.env.STRIPE_PRICE_YEARLY || "",
    displayName: "Annuel",
    displayPrice: "89 EUR",
    displayPeriod: "/an",
    headline: "La formule la plus avantageuse pour jouer toute la saison avec le coach actif a chaque partie.",
    ctaLabel: "Passer a l'annuel",
    highlight: true,
    features: [
      "Overlay live discret et lisible",
      "Conseils de macro, tempo et reset en situation",
      "Lecture de compo ennemie et alertes build",
      "Historique des notifications en partie",
      "Le meilleur tarif pour un usage continu"
    ]
  },
  {
    id: "monthly",
    mode: "subscription",
    stripePriceId: process.env.STRIPE_PRICE_MONTHLY || "",
    displayName: "Mensuel",
    displayPrice: "9,90 EUR",
    displayPeriod: "/mois",
    headline: "La bonne facon de decouvrir l'experience, prendre tes marques et sentir la difference en jeu.",
    ctaLabel: "Commencer maintenant",
    highlight: false,
    features: [
      "Acces complet a LoL Live Coach",
      "Desktop Windows et overlay in-game",
      "Conseils contextualises sur les timings cles",
      "Activation rapide depuis ton compte client"
    ]
  }
];

export function getSiteContent() {
  return {
    productName,
    supportEmail,
    downloadUrl
  };
}

export function getPublicPlans() {
  const stripeReady = Boolean(process.env.STRIPE_SECRET_KEY);

  return plans.map(({ stripePriceId, ...plan }) => ({
    ...plan,
    checkoutEnabled: stripeReady && Boolean(stripePriceId)
  }));
}

export function getPlanById(planId) {
  return plans.find((plan) => plan.id === planId) ?? null;
}

export function getPlanByStripePriceId(priceId) {
  return plans.find((plan) => plan.stripePriceId === priceId) ?? null;
}

export function resolveSiteUrl(origin) {
  const configuredSiteUrl = process.env.SITE_URL || process.env.NEXT_PUBLIC_SITE_URL;
  const baseUrl = configuredSiteUrl || origin || "http://localhost:3000";
  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
}
