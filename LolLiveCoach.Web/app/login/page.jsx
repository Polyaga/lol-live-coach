import Link from "next/link";
import { redirect } from "next/navigation";
import { hasAdminAccess } from "../../lib/admin-rbac";
import { appendQueryToPath, getSafeRedirectPath } from "../../lib/navigation";
import { getCurrentUser } from "../../lib/auth";

export default async function LoginPage({ searchParams }) {
  const params = await searchParams;
  const user = await getCurrentUser();

  const next = getSafeRedirectPath(params?.next, "/compte");
  const plan = String(params?.plan || "");

  if (user) {
    const fallback = hasAdminAccess(user) && !params?.next && !plan ? "/admin" : next;
    redirect(plan ? appendQueryToPath("/compte", { plan }) : fallback);
  }

  const hasError = params?.error === "1";
  const created = params?.created === "1";

  return (
    <main className="checkout-page">
      <section className="checkout-card admin-login-card">
        <p className="eyebrow">Compte client</p>
        <h1>Connexion.</h1>
        <p>
          Connectez-vous pour gerer votre abonnement, telecharger l'app et autoriser le
          desktop a verifier votre acces premium.
        </p>

        {created ? (
          <div className="success-banner">Compte cree. Vous pouvez maintenant vous connecter.</div>
        ) : null}

        {hasError ? (
          <div className="error-banner">Email ou mot de passe invalide.</div>
        ) : null}

        <form action="/api/auth/login" className="application-form" method="post">
          <input name="next" type="hidden" value={next} />
          <input name="plan" type="hidden" value={plan} />

          <div className="form-grid">
            <label className="field field-full">
              <span>Email</span>
              <input autoComplete="email" name="email" required type="email" />
            </label>

            <label className="field field-full">
              <span>Mot de passe</span>
              <input autoComplete="current-password" name="password" required type="password" />
            </label>
          </div>

          <button className="button button-primary" type="submit">
            Ouvrir mon compte
          </button>
        </form>

        <div className="cta-row">
          <Link className="button button-secondary" href={appendQueryToPath("/signup", { next, plan })}>
            Creer un compte
          </Link>
          <Link className="button button-secondary" href="/">
            Retour au site
          </Link>
        </div>
      </section>
    </main>
  );
}
