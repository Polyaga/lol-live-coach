import Link from "next/link";
import { redirect } from "next/navigation";
import { getCurrentUser } from "../../../lib/auth";
import { getAdminSession } from "../../../lib/admin-auth";

export default async function AdminLoginPage({ searchParams }) {
  const params = await searchParams;
  const session = await getAdminSession();
  const currentUser = await getCurrentUser();

  if (session) {
    redirect("/admin");
  }

  const hasInvalidCredentials = params?.error === "1";
  const isForbidden = params?.error === "forbidden";

  return (
    <main className="checkout-page">
      <section className="checkout-card admin-login-card">
        <p className="eyebrow">Back-office</p>
        <h1>Connexion admin.</h1>
        <p>
          Le back-office repose maintenant sur les vrais comptes du site, avec roles et
          permissions. Connectez-vous avec un compte ayant un role admin.
        </p>

        {isForbidden ? (
          <div className="error-banner">
            Ce compte n'a pas de droits suffisants pour ouvrir l'espace admin.
          </div>
        ) : null}

        {hasInvalidCredentials ? (
          <div className="error-banner">Email ou mot de passe invalide.</div>
        ) : null}

        {currentUser && !session ? (
          <div className="error-banner">
            Vous etes deja connecte avec {currentUser.email}, mais ce compte n'a pas acces au
            back-office.
          </div>
        ) : null}

        <form action="/api/auth/login" className="application-form" method="post">
          <input name="next" type="hidden" value="/admin" />

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
            Ouvrir le back-office
          </button>
        </form>

        <div className="cta-row">
          {currentUser ? (
            <form action="/api/auth/logout" method="post">
              <button className="button button-secondary" type="submit">
                Changer de compte
              </button>
            </form>
          ) : (
            <Link className="button button-secondary" href="/login">
              Connexion classique
            </Link>
          )}

          <Link className="button button-secondary" href="/">
            Retour au site
          </Link>
        </div>
      </section>
    </main>
  );
}
