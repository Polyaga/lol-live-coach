import Link from "next/link";
import { AmbassadorAffiliateTools } from "../../../components/ambassador-affiliate-tools";
import { requireUser } from "../../../lib/auth";
import { getAccountOverviewByUserId } from "../../../lib/account-store";
import { buildAccountAccess } from "../../../lib/account-access";
import { getSiteContent } from "../../../lib/site-config";

function formatMoney(amount) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: "EUR",
    maximumFractionDigits: 2
  }).format(amount || 0);
}

function formatDate(value, fallback = "Non renseigne") {
  if (!value) {
    return fallback;
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

function buildHeroContent(application) {
  if (!application) {
    return {
      title: "Ton espace ambassadeur commencera ici.",
      description:
        "Une fois ta candidature envoyee, tu retrouveras ici ton statut, ton lien d'affiliation, tes gains et les actions utiles pour faire vivre le partenariat."
    };
  }

  if (application.status === "approved") {
    return {
      title: "Ton espace ambassadeur est pret.",
      description:
        "Retrouve ton lien d'affiliation, les revenus qui te sont attribues, ce que tu as deja touche et ce qu'il reste a percevoir."
    };
  }

  if (application.status === "rejected") {
    return {
      title: "Ta candidature a ete examinee.",
      description:
        "Le dossier n'est pas actif pour le moment. Tu peux reprendre contact avec nous si ton positionnement ou ton audience ont evolue."
    };
  }

  return {
    title: "Ta candidature est en cours d'examen.",
    description:
      "Apres validation, ton lien personnel, tes stats d'affiliation et ton acces premium apparaitront ici automatiquement."
  };
}

export default async function AmbassadorDashboardPage() {
  const user = await requireUser();
  const site = getSiteContent();
  const siteOrigin = process.env.SITE_URL || process.env.NEXT_PUBLIC_SITE_URL;
  const account = await getAccountOverviewByUserId(user.id, {
    origin: siteOrigin
  });
  const access = buildAccountAccess({
    subscriptions: account?.subscriptions,
    ambassadorApplications: account?.ambassadorApplications,
    accessGrants: account?.accessGrants,
    origin: siteOrigin
  });
  const application = account?.ambassadorApplications?.[0] || null;
  const hero = buildHeroContent(application);
  const affiliateLink = application?.affiliateLink || "";

  return (
    <main className="site-shell admin-shell">
      <header className="topbar">
        <Link className="brand" href="/">
          <span className="brand-mark" />
          <span>{site.productName}</span>
        </Link>

        <div className="cta-row compact-row">
          <Link className="button button-secondary" href="/compte">
            Mon compte
          </Link>
          <Link className="button button-secondary" href="/">
            Retour au site
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
          <p className="eyebrow">Ambassadeur</p>
          <h1 className="admin-title">{hero.title}</h1>
          <p>{hero.description}</p>
        </div>

        <div className={`status-pill ${application ? `status-${application.status}` : "status-pending"}`}>
          {application ? statusLabel(application.status) : "A completer"}
        </div>
      </section>

      <section className="admin-metrics">
        <article className="metric-card">
          <span className="metric-label">Statut</span>
          <strong>{application ? statusLabel(application.status) : "Aucune candidature"}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Commission</span>
          <strong>{application ? `${Math.round(application.commissionRate * 100)}%` : "10%"}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Revenus attribues</span>
          <strong>{formatMoney(application?.referredRevenue)}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Gains generes</span>
          <strong>{formatMoney(application?.grossCommission)}</strong>
        </article>

        <article className="metric-card">
          <span className="metric-label">Restant a percevoir</span>
          <strong>{formatMoney(application?.commissionDue)}</strong>
        </article>
      </section>

      {!application ? (
        <section className="ambassador-dashboard-grid">
          <article className="admin-card">
            <div className="admin-card-head">
              <div>
                <p className="eyebrow">Commencer</p>
                <h2>Envoie ta candidature</h2>
                <p className="admin-subtitle">
                  Renseigne ta chaine, ton audience et la facon dont tu aimerais presenter
                  LoL Live Coach. Si le profil colle, tout se debloquera ici.
                </p>
              </div>
            </div>

            <div className="admin-details">
              <p><strong>Ce que tu verras ici:</strong> ton statut, ton lien d'affiliation, tes gains, tes versements et ton acces premium.</p>
              <p><strong>Ce que l'on attend:</strong> une recommandation sincere, claire et utile pour des joueurs qui peuvent aimer le produit.</p>
            </div>

            <div className="cta-row">
              <Link className="button button-primary" href="/ambassadeur">
                Candidater au programme
              </Link>
              <Link className="button button-secondary" href="/compte">
                Revenir au compte
              </Link>
            </div>
          </article>

          <article className="admin-card">
            <p className="eyebrow">Ce qui est prevu</p>
            <h2>Un cockpit simple pour ton partenariat</h2>
            <ol className="simple-list">
              <li>Ton lien d'affiliation personnel apparaitra ici des qu'il sera actif.</li>
              <li>Les revenus attribues et ta commission seront mis a jour dans ce tableau de bord.</li>
              <li>Ton acces premium offert sera visible au meme endroit, sans aller-retour avec l'admin.</li>
            </ol>
          </article>
        </section>
      ) : (
        <>
          <section className="ambassador-dashboard-grid">
            <article className="admin-card">
              <div className="admin-card-head">
                <div>
                  <p className="eyebrow">Affiliation</p>
                  <h2>Ton lien et ton code</h2>
                  <p className="admin-subtitle">
                    {affiliateLink
                      ? "Le lien ouvre le site avec ton code deja attache au parcours d'achat."
                      : "Ton lien apparaitra ici des que la candidature sera validee et qu'un code sera attribue."}
                  </p>
                </div>
              </div>

              <AmbassadorAffiliateTools
                affiliateCode={application.affiliateCode}
                affiliateLink={affiliateLink}
                productName={site.productName}
              />
            </article>

            <article className="admin-card">
              <p className="eyebrow">Suivi</p>
              <h2>Vue d'ensemble du partenariat</h2>

              <div className="ambassador-detail-grid">
                <div className="ambassador-detail-card">
                  <span className="metric-label">Chaine</span>
                  <strong>{application.channelName || application.fullName}</strong>
                </div>

                <div className="ambassador-detail-card">
                  <span className="metric-label">Plateforme</span>
                  <strong>{application.platform || "Non renseignee"}</strong>
                </div>

                <div className="ambassador-detail-card">
                  <span className="metric-label">Candidature envoyee</span>
                  <strong>{formatDate(application.createdAt)}</strong>
                </div>

                <div className="ambassador-detail-card">
                  <span className="metric-label">Derniere attribution</span>
                  <strong>{formatDate(application.lastReferralAt, "Pas encore de vente attribuee")}</strong>
                </div>

                <div className="ambassador-detail-card">
                  <span className="metric-label">Deja verse</span>
                  <strong>{formatMoney(application.paidOutAmount)}</strong>
                </div>

                <div className="ambassador-detail-card">
                  <span className="metric-label">Acces premium</span>
                  <strong>{access.hasPremiumAccess ? "Actif" : "En attente"}</strong>
                </div>
              </div>

              <div className="admin-details">
                <p><strong>Lien de chaine:</strong> <a href={application.channelUrl}>{application.channelUrl}</a></p>
                <p><strong>Resume audience:</strong> {application.audienceSummary || "Non renseigne"}</p>
                <p><strong>Ce que tu gagnes:</strong> {Math.round(application.commissionRate * 100)}% des ventes que nous avons rattachees a ton lien.</p>
              </div>
            </article>
          </section>

          <section className="ambassador-dashboard-grid">
            <article className="admin-card">
              <p className="eyebrow">Actions utiles</p>
              <h2>Tout ce qu'il te faut pour activer le partenariat</h2>

              <ul className="ambassador-help-list">
                <li>Teste ton lien toi-meme avant de le partager pour verifier le parcours.</li>
                <li>Place ton lien en bio, description, message epingle ou commande de stream.</li>
                <li>Garde ton code d'affiliation a portee de main si tu veux aussi le citer en live.</li>
                <li>Les chiffres affiches ici correspondent aux ventes que nous avons rattachees a ton lien.</li>
              </ul>

              <div className="cta-row">
                {affiliateLink ? (
                  <a className="button button-primary" href={affiliateLink}>
                    Ouvrir mon lien
                  </a>
                ) : null}

                {site.downloadUrl && access.hasPremiumAccess ? (
                  <a className="button button-secondary" href={site.downloadUrl}>
                    Telecharger l'app desktop
                  </a>
                ) : null}
              </div>
            </article>

            <article className="admin-card">
              <p className="eyebrow">Support</p>
              <h2>Besoin d'un coup de main ?</h2>

              <div className="admin-details">
                <p><strong>Email:</strong> <a href={`mailto:${site.supportEmail}`}>{site.supportEmail}</a></p>
                <p><strong>Paiements:</strong> si un versement manque ou si une attribution semble incorrecte, ecris-nous et on verifie avec toi.</p>
                <p><strong>Produit:</strong> si ton acces premium est actif, tu peux recuperer la derniere version desktop depuis ton compte.</p>
              </div>

              <div className="cta-row">
                <a className="button button-secondary" href={`mailto:${site.supportEmail}`}>
                  Contacter le support
                </a>
                <Link className="button button-secondary" href="/compte">
                  Retour a mon compte
                </Link>
              </div>
            </article>
          </section>
        </>
      )}
    </main>
  );
}
