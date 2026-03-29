"use client";

import { useState } from "react";

export function AmbassadorAffiliateTools({
  affiliateLink = "",
  affiliateCode = "",
  productName = "LoL Live Coach"
}) {
  const [feedback, setFeedback] = useState("");

  async function copyValue(value, label) {
    if (!value) {
      return;
    }

    try {
      await navigator.clipboard.writeText(value);
      setFeedback(`${label} copie.`);
    } catch (error) {
      setFeedback(
        error instanceof Error
          ? error.message
          : `Impossible de copier ${label.toLowerCase()}.`
      );
    }
  }

  const shareMessage = affiliateLink
    ? `Je recommande ${productName} aux joueurs qui veulent un coach live plus lisible en partie. Decouvre-le ici : ${affiliateLink}`
    : "";

  return (
    <div className="ambassador-copy-stack">
      <label className="field field-full">
        <span>Lien d'affiliation</span>
        <div className="copy-field">
          <input readOnly value={affiliateLink || "Disponible apres validation"} />
          <button
            className="button button-secondary"
            disabled={!affiliateLink}
            onClick={() => copyValue(affiliateLink, "Lien d'affiliation")}
            type="button"
          >
            Copier
          </button>
        </div>
      </label>

      <label className="field field-full">
        <span>Code d'affiliation</span>
        <div className="copy-field">
          <input readOnly value={affiliateCode || "Genere apres validation"} />
          <button
            className="button button-secondary"
            disabled={!affiliateCode}
            onClick={() => copyValue(affiliateCode, "Code d'affiliation")}
            type="button"
          >
            Copier
          </button>
        </div>
      </label>

      <div className="cta-row compact-row">
        {affiliateLink ? (
          <a className="button button-primary" href={affiliateLink}>
            Ouvrir ma landing
          </a>
        ) : null}

        <button
          className="button button-secondary"
          disabled={!shareMessage}
          onClick={() => copyValue(shareMessage, "Message de partage")}
          type="button"
        >
          Copier un texte de partage
        </button>
      </div>

      {feedback ? <p className="copy-feedback">{feedback}</p> : null}
    </div>
  );
}
