import Link from "next/link";
import { redirect } from "next/navigation";
import { appendQueryToPath, getSafeRedirectPath } from "../../lib/navigation";
import { getCurrentUser } from "../../lib/auth";

export default async function SignupPage({ searchParams }) {
  const params = await searchParams;
  const user = await getCurrentUser();

  const next = getSafeRedirectPath(params?.next, "/compte");
  const plan = String(params?.plan || "");

  if (user) {
    redirect(plan ? appendQueryToPath("/compte", { plan }) : next);
  }

  const hasError = params?.error === "1";
  const duplicate = params?.duplicate === "1";

  return (
    <main className="checkout-page">
      <section className="checkout-card admin-login-card">
        <p className="eyebrow">Compte client</p>
        <h1>Creation de compte.</h1>
        <p>
          Le compte client vit sur le site. Il sert pour la facturation, le telechargement
          et la connexion de l'app desktop a vos droits d'acces.
        </p>

        {duplicate ? (
          <div className="error-banner">Un compte existe deja avec cet email.</div>
        ) : hasError ? (
          <div className="error-banner">Le formulaire est invalide ou le mot de passe est trop court.</div>
        ) : null}

        <form action="/api/auth/signup" className="application-form" method="post">
          <input name="next" type="hidden" value={next} />
          <input name="plan" type="hidden" value={plan} />

          <div className="form-grid">
            <label className="field field-full">
              <span>Nom affiche</span>
              <input autoComplete="name" name="displayName" type="text" />
            </label>

            <label className="field field-full">
              <span>Email</span>
              <input autoComplete="email" name="email" required type="email" />
            </label>

            <label className="field field-full">
              <span>Mot de passe</span>
              <input autoComplete="new-password" minLength="8" name="password" required type="password" />
            </label>
          </div>

          <button className="button button-primary" type="submit">
            Creer mon compte
          </button>
        </form>

        <div className="cta-row">
          <Link className="button button-secondary" href={appendQueryToPath("/login", { next, plan })}>
            J'ai deja un compte
          </Link>
          <Link className="button button-secondary" href="/">
            Retour au site
          </Link>
        </div>
      </section>
    </main>
  );
}
