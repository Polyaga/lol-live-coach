import Link from "next/link";
import { requireUser } from "../../lib/auth";
import { getAccountOverviewByUserId } from "../../lib/account-store";
import { buildAccountAccess } from "../../lib/account-access";
import { hasAdminAccess } from "../../lib/admin-rbac";
import { getPublicPlans, getSiteContent } from "../../lib/site-config";
import { AccountBillingPanel } from "../../components/account-billing-panel";

function formatMoney(amount) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: "EUR",
    maximumFractionDigits: 2
  }).format(amount || 0);
}

function formatDate(value) {
  if (!value) {
    return "Jamais";
  }

  return new Intl.DateTimeFormat("fr-FR", {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

function statusLabel(status) {
  switch (status) {
    case "approved":
      return "Approuve";
    case "rejected":
      return "Refuse";
    default:
      return "En attente";
  }
}

export default async function AccountPage({ searchParams }) {
  const params = await searchParams;
  const user = await requireUser();
  const siteOrigin = process.env.SITE_URL || process.env.NEXT_PUBLIC_SITE_URL;
  const account = await getAccountOverviewByUserId(user.id, {
    origin: siteOrigin
  });
  const site = getSiteContent();
  const plans = getPublicPlans();
  const access = buildAccountAccess({
    subscriptions: account?.subscriptions,
    ambassadorApplications: account?.ambassadorApplications,
    accessGrants: account?.accessGrants,
    origin: siteOrigin
  });
  const ambassadorApplication = account?.ambassadorApplications?.[0] || null;
  const canAccessAdmin = hasAdminAccess(account);
  const requestedPlan = plans.some((plan) => plan.id === String(params?.plan || ""))
    ? String(params?.plan || "")
    : "";

  return (
    <main className="site-shell admin-shell">
      <header className="topbar">
        <Link className="brand" href="/">
          <span className="brand-mark" />
          <span>{site.productName}</span>
        </Link>

        <div className="cta-row compact-row">
          {canAccessAdmin ? (
            <Link className="button button-secondary" href="/admin">
              Ouvrir l'admin
            </Link>
          ) : null}
          <Link className="button button-secondary" href="/">
            Retour au site
          </Link>
          <Link className="button button-secondary" href="/compte/ambassadeur">
            {ambassadorApplication ? "Espace ambassadeur" : "Programme ambassadeur"}
          </Link>
          <form action="/api/auth/logout" method="post">
            <button className="button button-secondary" type="submit">
              Deconnexion
            </button>
          </form>
        </div>
      </header>

      <section className="admin-hero">
        <div className="section-copy">
          <p className="eyebrow">Compte</p>
          <h1 className="admin-title">Bonjour {account?.displayName || account?.email}.</h1>
          <p>
            Ici, vous retrouvez votre abonnement, le telechargement du desktop et les
            sessions autorisees a verifier votre acces premium.
          </p>
        </div>
      </section>

      <section className="admin-metrics">
        <article className="metric-card">
          <span className="metric-label">Email</span>
          <strong>{account?.email}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Niveau d'acces</span>
          <strong>{access.tier}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Back-office</span>
          <strong>{canAccessAdmin ? (account?.adminRole?.name || "Admin") : "Aucun"}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Sessions desktop</span>
          <strong>{account?.desktopTokens?.length || 0}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Abonnements connus</span>
          <strong>{account?.subscriptions?.length || 0}</strong>
        </article>
      </section>

      <section className="admin-card account-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Ambassadeur</p>
            <h2>Espace ambassadeur</h2>
            <p className="admin-subtitle">
              {ambassadorApplication
                ? "Retrouve ton statut, ton lien d'affiliation, tes gains et les actions utiles depuis un espace dedie."
                : "Suivi du programme, lien d'affiliation, gains et acces premium sont regroupes dans un espace dedie."}
            </p>
          </div>

          <div className={`status-pill ${ambassadorApplication ? `status-${ambassadorApplication.status}` : "status-pending"}`}>
            {ambassadorApplication ? statusLabel(ambassadorApplication.status) : "Disponible"}
          </div>
        </div>

        <div className="admin-details">
          {ambassadorApplication ? (
            <>
              <p><strong>Chaine:</strong> {ambassadorApplication.channelName || ambassadorApplication.fullName}</p>
              <p><strong>Commission:</strong> {Math.round(ambassadorApplication.commissionRate * 100)}%</p>
              <p><strong>Gains generes:</strong> {formatMoney(ambassadorApplication.grossCommission)}</p>
              <p><strong>Code d'affiliation:</strong> {ambassadorApplication.affiliateCode || "Genere apres validation"}</p>
            </>
          ) : (
            <>
              <p><strong>Ce que tu y retrouves:</strong> statut de candidature, lien d'affiliation, revenus attribues, commission a percevoir et rappels utiles.</p>
              <p><strong>Pour qui:</strong> streamers, createurs et coaches qui veulent recommander l'app a leur audience.</p>
            </>
          )}
        </div>

        <div className="cta-row">
          <Link className="button button-primary" href="/compte/ambassadeur">
            {ambassadorApplication ? "Ouvrir mon espace ambassadeur" : "Decouvrir l'espace ambassadeur"}
          </Link>

          {!ambassadorApplication ? (
            <Link className="button button-secondary" href="/ambassadeur">
              Candidater au programme
            </Link>
          ) : null}
        </div>
      </section>

      <AccountBillingPanel
        access={access}
        downloadUrl={site.downloadUrl}
        plans={plans}
        requestedPlan={requestedPlan}
        supportEmail={site.supportEmail}
      />

      <section className="admin-card account-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Desktop</p>
            <h2>Sessions actives de l'application</h2>
            <p className="admin-subtitle">
              Chaque connexion desktop cree un jeton long terme que vous pouvez revoquer ici.
            </p>
          </div>
        </div>

        {account?.desktopTokens?.length ? (
          <div className="account-session-list">
            {account.desktopTokens.map((token) => (
              <article className="feature-card session-card" key={token.id}>
                <p className="feature-kicker">{token.name}</p>
                <h3>Derniere activite: {formatDate(token.lastUsedAt || token.createdAt)}</h3>
                <p>
                  Cree le {formatDate(token.createdAt)}
                  {token.expiresAt ? ` - expiration le ${formatDate(token.expiresAt)}` : ""}
                </p>

                <form action="/api/account/desktop-tokens/revoke" method="post">
                  <input name="tokenId" type="hidden" value={token.id} />
                  <button className="button button-secondary" type="submit">
                    Revoquer cette session
                  </button>
                </form>
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            Aucune session desktop active pour le moment. Connectez-vous depuis l'application
            une fois telechargee et elle apparaitra ici.
          </div>
        )}
      </section>
    </main>
  );
}
