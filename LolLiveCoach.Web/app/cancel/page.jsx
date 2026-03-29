import Link from "next/link";

export default function CancelPage() {
  return (
    <main className="checkout-page">
      <section className="checkout-card">
        <p className="eyebrow">Pas de souci</p>
        <h1>Ton paiement n'a pas ete finalise.</h1>
        <p>
          Aucun debit n'a ete effectue. Tu peux reprendre quand tu veux, comparer les
          formules et revenir au moment qui te convient.
        </p>

        <div className="cta-row">
          <Link className="button button-primary" href="/#pricing">
            Retour aux offres
          </Link>
          <Link className="button button-secondary" href="/compte">
            Mon compte
          </Link>
          <Link className="button button-secondary" href="/">
            Accueil
          </Link>
        </div>
      </section>
    </main>
  );
}
