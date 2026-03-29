"use client";

import { useEffect, useState } from "react";

export function PricingSection({ plans, supportEmail, isAuthenticated = false }) {
  const [pendingPlanId, setPendingPlanId] = useState("");
  const [statusMessage, setStatusMessage] = useState(
    isAuthenticated
      ? "Choisis ton rythme et active le coach en quelques minutes."
      : "Creer un compte prend quelques secondes, puis Stripe prend le relai pour le paiement."
  );

  useEffect(() => {
    const currentUrl = new URL(window.location.href);
    const referralCode = currentUrl.searchParams.get("ref");

    if (referralCode) {
      window.localStorage.setItem("llc_referral_code", referralCode);
    }
  }, []);

  async function startCheckout(planId) {
    if (!isAuthenticated) {
      window.location.href = `/signup?next=/compte&plan=${encodeURIComponent(planId)}`;
      return;
    }

    setPendingPlanId(planId);
    setStatusMessage("Redirection vers le paiement securise...");

    try {
      const currentUrl = new URL(window.location.href);
      const affiliateCode =
        currentUrl.searchParams.get("ref")
        || window.localStorage.getItem("llc_referral_code")
        || "";

      const response = await fetch("/api/billing/checkout-session", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ planId, affiliateCode })
      });

      const payload = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(payload.detail || payload.error || "Le checkout n'a pas pu etre initialise.");
      }

      if (!payload.url) {
        throw new Error("Stripe n'a pas retourne d'URL de redirection.");
      }

      window.location.href = payload.url;
    } catch (error) {
      setPendingPlanId("");
      setStatusMessage(
        error instanceof Error
          ? error.message
          : "Le checkout n'a pas pu etre initialise."
      );
    }
  }

  return (
    <section className="pricing-section" id="pricing">
      <div className="pricing-header">
        <p className="eyebrow">Tarifs</p>
        <h2>Deux offres simples. La meme promesse des la premiere partie.</h2>
        <p className="pricing-note">{statusMessage}</p>
      </div>

      <div className="pricing-grid">
        {plans.map((plan) => (
          <article
            className={`pricing-card${plan.highlight ? " featured" : ""}`}
            key={plan.id}
          >
            <div className="pricing-card-header">
              <div>
                <h3>{plan.displayName}</h3>
                <p className="pricing-meta">{plan.headline}</p>
              </div>

              {plan.highlight ? <span className="pricing-badge">Recommande</span> : null}
            </div>

            <div>
              <p className="pricing-price">{plan.displayPrice}</p>
              <span className="pricing-period">{plan.displayPeriod}</span>
            </div>

            <ul className="pricing-feature-list">
              {plan.features.map((feature) => (
                <li key={feature}>{feature}</li>
              ))}
            </ul>

            <button
              className="button button-primary"
              disabled={!plan.checkoutEnabled || pendingPlanId === plan.id}
              onClick={() => startCheckout(plan.id)}
              type="button"
            >
              {!plan.checkoutEnabled
                ? "Paiement temporairement indisponible"
                : pendingPlanId === plan.id
                  ? "Ouverture..."
                  : isAuthenticated
                    ? plan.ctaLabel
                    : "Creer un compte pour acheter"}
            </button>
          </article>
        ))}
      </div>

      <p className="pricing-footnote">
        Besoin d'un acces equipe, d'une offre createur ou d'un accompagnement sur mesure ? Ecris a{" "}
        <a href={`mailto:${supportEmail}`}>{supportEmail}</a>.
      </p>
    </section>
  );
}
