"use client";

import { useEffect, useRef, useState } from "react";

export function AccountBillingPanel({
  plans,
  access,
  requestedPlan,
  downloadUrl,
  supportEmail
}) {
  const [busyAction, setBusyAction] = useState("");
  const [statusMessage, setStatusMessage] = useState(
    access.hasPremiumAccess
      ? "Votre compte est pret. Vous pouvez gerer la facturation et telecharger l'application."
      : "Choisissez une offre pour activer le premium, puis connectez l'application a votre compte."
  );
  const hasAutoLaunched = useRef(false);

  async function startCheckout(planId) {
    setBusyAction(planId);
    setStatusMessage("Redirection vers le paiement securise...");

    try {
      const response = await fetch("/api/billing/checkout-session", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ planId })
      });

      const payload = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(payload.error || "Le checkout n'a pas pu etre initialise.");
      }

      window.location.href = payload.url;
    } catch (error) {
      setBusyAction("");
      setStatusMessage(
        error instanceof Error ? error.message : "Le checkout n'a pas pu etre initialise."
      );
    }
  }

  async function openPortal() {
    setBusyAction("portal");
    setStatusMessage("Ouverture du portail Stripe...");

    try {
      const response = await fetch("/api/billing/portal-session", {
        method: "POST"
      });

      const payload = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(payload.error || "Le portail client n'a pas pu etre initialise.");
      }

      window.location.href = payload.url;
    } catch (error) {
      setBusyAction("");
      setStatusMessage(
        error instanceof Error ? error.message : "Le portail client n'a pas pu etre initialise."
      );
    }
  }

  useEffect(() => {
    if (!requestedPlan || hasAutoLaunched.current) {
      return;
    }

    hasAutoLaunched.current = true;
    startCheckout(requestedPlan);
  }, [requestedPlan]);

  return (
    <section className="admin-card account-card">
      <div className="admin-card-head">
        <div>
          <p className="eyebrow">Facturation</p>
          <h2>Espace client et abonnement</h2>
          <p className="admin-subtitle">{statusMessage}</p>
        </div>

        <div className={`status-pill ${access.hasPremiumAccess ? "status-approved" : "status-pending"}`}>
          {access.tier}
        </div>
      </div>

      <div className="admin-details">
        <p><strong>Etat:</strong> {access.statusTitle}</p>
        <p><strong>Resume:</strong> {access.statusMessage}</p>
        <p><strong>Plan courant:</strong> {access.currentPlanKey || "Aucun plan actif"}</p>
      </div>

      <div className="pricing-grid account-pricing-grid">
        {plans.map((plan) => (
          <article className={`pricing-card${plan.highlight ? " featured" : ""}`} key={plan.id}>
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

            <button
              className="button button-primary"
              disabled={busyAction === plan.id}
              onClick={() => startCheckout(plan.id)}
              type="button"
            >
              {busyAction === plan.id ? "Redirection..." : plan.ctaLabel}
            </button>
          </article>
        ))}
      </div>

      <div className="cta-row">
        <button
          className="button button-secondary"
          disabled={busyAction === "portal" || !access.hasPremiumAccess}
          onClick={openPortal}
          type="button"
        >
          {busyAction === "portal" ? "Ouverture..." : "Gerer mon abonnement"}
        </button>

        {downloadUrl ? (
          <a className="button button-primary" href={downloadUrl}>
            Telecharger l'app desktop
          </a>
        ) : null}
      </div>

      <p className="pricing-footnote">
        Besoin d'un changement de plan, d'une facture ou d'un support humain ? Ecrivez a{" "}
        <a href={`mailto:${supportEmail}`}>{supportEmail}</a>.
      </p>
    </section>
  );
}
