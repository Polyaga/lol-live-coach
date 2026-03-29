import Link from "next/link";
import { getSiteContent } from "../../lib/site-config";

export default async function AmbassadorPage({ searchParams }) {
  const params = await searchParams;
  const site = getSiteContent();
  const isSubmitted = params?.submitted === "1";
  const hasError = params?.error === "1";

  return (
    <main className="site-shell">
      <header className="topbar">
        <Link className="brand" href="/">
          <span className="brand-mark" />
          <span>{site.productName}</span>
        </Link>

        <nav className="topnav">
          <Link href="/">Produit</Link>
          <a href="#avantages">Pourquoi nous rejoindre</a>
          <a href="#formulaire">Candidater</a>
        </nav>
      </header>

      <section className="hero hero-compact">
        <div className="hero-copy">
          <p className="eyebrow">Programme ambassadeur</p>
          <h1>Fais decouvrir l'app a ta communaute. Si ton profil est retenu, on t'offre l'acces premium et 10% des ventes que tu generes.</h1>
          <p className="hero-lead">
            Ce programme est pense pour les streamers, createurs et coaches qui aiment
            recommander des outils utiles, beaux a montrer en live et faciles a faire aimer.
          </p>

          <div className="signal-row">
            <div className="signal-pill">
              <span className="signal-value">Acces offert</span>
              <span className="signal-label">premium offert si le profil est retenu</span>
            </div>
            <div className="signal-pill">
              <span className="signal-value">10%</span>
              <span className="signal-label">sur les ventes associees a ton lien</span>
            </div>
            <div className="signal-pill">
              <span className="signal-value">Lien personnel</span>
              <span className="signal-label">simple a partager en live, video ou bio</span>
            </div>
          </div>
        </div>

        <div className="note-box">
          <p className="eyebrow">Comment ca se passe</p>
          <ol className="simple-list">
            <li>Tu nous partages ta chaine, ton audience et la maniere dont tu presenterais le produit.</li>
            <li>Nous regardons si ton univers et ton ton collent naturellement a l'experience LoL Live Coach.</li>
            <li>Si le partenariat est valide, tu recois un acces premium et ton lien d'affiliation personnel.</li>
            <li>Tu fais decouvrir l'app a ton rythme et tu touches 10% des ventes associees a ton lien.</li>
          </ol>
        </div>
      </section>

      <section className="feature-list ambassador-benefits" id="avantages">
        <article className="feature-card">
          <p className="feature-kicker">Le bon profil</p>
          <h3>Des createurs qui aiment recommander ce qu'ils utilisent vraiment.</h3>
          <p>
            Il ne s'agit pas d'etre le plus gros. Ce qui compte, c'est une audience qui
            comprend vite la valeur du produit et un ton capable de la transmettre proprement.
          </p>
        </article>

        <article className="feature-card">
          <p className="feature-kicker">Ce que tu recois</p>
          <h3>Un acces premium offert, un lien personnel et un produit facile a montrer.</h3>
          <p>
            Si ta candidature est retenue, tu accedes a l'app sans cout, tu obtiens un
            lien personnel et tu beneficies d'une commission claire sur les ventes attribuees.
          </p>
        </article>
      </section>

      <section className="form-section" id="formulaire">
        <div className="section-copy">
          <p className="eyebrow">Candidature</p>
          <h2>Montre-nous ton univers, ton audience et la facon dont tu ferais aimer l'app.</h2>
          <p>
            Plus ta candidature est concrete, plus nous pouvons comprendre rapidement si
            LoL Live Coach a sa place dans ton contenu.
          </p>
        </div>

        {isSubmitted ? (
          <div className="success-banner">
            Candidature envoyee. Nous revenons vers toi rapidement si ton profil correspond au programme.
          </div>
        ) : null}

        {hasError ? (
          <div className="error-banner">
            Le formulaire est incomplet. Renseigne au minimum ton nom, ton email, ta chaine,
            ton lien et la maniere dont tu parlerais du produit.
          </div>
        ) : null}

        <form action="/api/ambassadors/apply" className="application-form" method="post">
          <div className="form-grid">
            <label className="field">
              <span>Nom complet</span>
              <input name="fullName" placeholder="Ton nom" required type="text" />
            </label>

            <label className="field">
              <span>Email</span>
              <input name="email" placeholder="ton@email.com" required type="email" />
            </label>

            <label className="field">
              <span>Nom de chaine ou pseudo</span>
              <input name="channelName" placeholder="Ton nom de chaine" required type="text" />
            </label>

            <label className="field">
              <span>Plateforme principale</span>
              <select name="platform" required>
                <option value="">Choisir</option>
                <option value="Twitch">Twitch</option>
                <option value="YouTube">YouTube</option>
                <option value="TikTok">TikTok</option>
                <option value="Kick">Kick</option>
                <option value="Autre">Autre</option>
              </select>
            </label>

            <label className="field field-full">
              <span>Lien de chaine / profil</span>
              <input
                name="channelUrl"
                placeholder="https://twitch.tv/tonpseudo"
                required
                type="url"
              />
            </label>

            <label className="field field-full">
              <span>Audience et rythme de contenu</span>
              <textarea
                name="audienceSummary"
                placeholder="Taille moyenne de ton audience, frequence de diffusion, type de contenu et profil des joueurs que tu touches."
                rows="3"
              />
            </label>

            <label className="field field-full">
              <span>Comment tu presenterais LoL Live Coach a ton audience</span>
              <textarea
                name="motivation"
                placeholder="Dis-nous comment tu montrerais l'app en stream ou en video, a quel moment tu en parlerais et pourquoi ton audience y serait receptive."
                required
                rows="6"
              />
            </label>
          </div>

          <button className="button button-primary" type="submit">
            Envoyer ma candidature
          </button>
        </form>
      </section>
    </main>
  );
}
