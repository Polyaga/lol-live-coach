import Link from "next/link";
import { getCurrentUser } from "../../lib/auth";
import { syncCheckoutSessionById } from "../../lib/billing";

export default async function SuccessPage({ searchParams }) {
  const params = await searchParams;
  const sessionId = params?.session_id;
  const user = await getCurrentUser();

  if (sessionId && user) {
    await syncCheckoutSessionById(sessionId).catch(() => null);
  }

  return (
    <main className="checkout-page">
      <section className="checkout-card">
        <p className="eyebrow">Merci</p>
        <h1>Ton paiement est confirme.</h1>
        <p>
          Ton abonnement est bien pris en compte. Tu peux maintenant ouvrir ton compte
          pour retrouver ta formule, gerer la facturation et telecharger l'app desktop.
        </p>
        <p className="checkout-muted">
          {sessionId
            ? `Reference Stripe : ${sessionId}`
            : "La reference de session Stripe n'a pas ete transmise."}
        </p>

        <div className="cta-row">
          <Link className="button button-primary" href="/compte">
            Ouvrir mon compte
          </Link>
          <Link className="button button-secondary" href="/">
            Retour au site
          </Link>
        </div>
      </section>
    </main>
  );
}
