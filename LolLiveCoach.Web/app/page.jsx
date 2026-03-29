import Link from "next/link";
import { PricingSection } from "../components/pricing-section";
import { getCurrentUser } from "../lib/auth";
import { getPublicPlans, getSiteContent } from "../lib/site-config";

export default async function HomePage() {
  const user = await getCurrentUser();
  const plans = getPublicPlans();
  const site = getSiteContent();

  return (
    <main className="site-shell">
      <header className="topbar">
        <Link className="brand" href="/">
          <span className="brand-mark" />
          <span>{site.productName}</span>
        </Link>

        <nav className="topnav">
          <a href="#features">Produit</a>
          <a href="#experience">En jeu</a>
          <a href="#pricing">Tarifs</a>
          <Link href="/ambassadeur">Ambassadeur</Link>
          <Link href={user ? "/compte" : "/login"}>{user ? "Mon compte" : "Connexion"}</Link>
        </nav>
      </header>

      <section className="hero">
        <div className="hero-copy">
          <p className="eyebrow">Coach live premium pour League of Legends</p>
          <h1>Le coach live qui t'aide a prendre la bonne decision sans quitter la partie des yeux.</h1>
          <p className="hero-lead">
            LoL Live Coach lit le contexte reel du match pour faire ressortir une seule action
            utile au bon moment: reset, tempo, vision, priorite d'objectif ou adaptation de build.
          </p>

          <div className="cta-row">
            <a className="button button-primary" href="#pricing">
              Voir les offres
            </a>
            <a className="button button-secondary" href="#features">
              Explorer le produit
            </a>
            <Link className="button button-secondary" href="/ambassadeur">
              Devenir ambassadeur
            </Link>
          </div>

          <div className="signal-row">
            <div className="signal-pill">
              <span className="signal-value">Overlay discret</span>
              <span className="signal-label">toujours lisible, jamais envahissant</span>
            </div>
            <div className="signal-pill">
              <span className="signal-value">Conseils en contexte</span>
              <span className="signal-label">tempo, reset, vision, build</span>
            </div>
            <div className="signal-pill">
              <span className="signal-value">Acces rapide</span>
              <span className="signal-label">compte, paiement securise et activation simple</span>
            </div>
          </div>
        </div>

        <div className="hero-stage">
          <div className="stage-orbit orbit-one" />
          <div className="stage-orbit orbit-two" />

          <div className="dashboard-card">
            <div className="window-chrome">
              <span />
              <span />
              <span />
            </div>

            <div className="dashboard-grid">
              <section className="dashboard-panel">
                <p className="panel-label">Conseil principal</p>
                <h2>Prends la wave, reset, reviens pour le dragon.</h2>
                <p className="panel-copy">
                  La fenetre est ouverte. Convertis ce qui est gratuit, depense ton or,
                  puis reviens avant le prochain vrai timing de carte.
                </p>
              </section>

              <section className="dashboard-panel compact">
                <p className="panel-label">Lecture du moment</p>
                <div className="chip-row">
                  <span className="metric-chip warm">Engage frontal</span>
                  <span className="metric-chip cool">2 sources AP</span>
                  <span className="metric-chip accent">Frontline solide</span>
                </div>
              </section>

              <section className="dashboard-panel compact">
                <p className="panel-label">Rappels utiles</p>
                <ul className="micro-feed">
                  <li>Recall rentable avec 1 400 gold.</li>
                  <li>Vision trop legere avant l'objectif.</li>
                  <li>Shutdown expose si tu avances seul.</li>
                </ul>
              </section>
            </div>
          </div>

          <div className="floating-overlay">
            <p className="overlay-tag">In-game overlay</p>
            <strong>Phase : Mid game</strong>
            <span>Priorite : dragon dans 58 s. Push, vision, reset, puis reprise de carte.</span>
          </div>
        </div>
      </section>

      <section className="proof-strip">
        <div>
          <p className="proof-number">Actionnable</p>
          <p className="proof-label">un conseil net quand la partie accelere</p>
        </div>
        <div>
          <p className="proof-number">Lisible</p>
          <p className="proof-label">une interface calme qui respecte ton ecran</p>
        </div>
        <div>
          <p className="proof-number">Premium</p>
          <p className="proof-label">une finition propre, rassurante et faite pour durer</p>
        </div>
      </section>

      <section className="section-grid" id="features">
        <div className="section-copy">
          <p className="eyebrow">Le produit</p>
          <h2>Concu pour les timings ou une seule bonne decision change toute la suite.</h2>
          <p>
            Le but n'est pas d'ajouter du bruit. Le but est de t'aider a lire plus vite
            ce qui compte vraiment, puis a agir avec plus de confiance.
          </p>
        </div>

        <div className="feature-list">
          <article className="feature-card">
            <p className="feature-kicker">Reset au bon moment</p>
            <h3>Quand partir, quand rester, quand convertir.</h3>
            <p>
              Avec l'or, les ressources et les derniers evenements, l'app fait remonter le
              timing rentable avant que l'erreur n'arrive.
            </p>
          </article>

          <article className="feature-card">
            <p className="feature-kicker">Lecture de composition</p>
            <h3>Ce que l'equipe adverse veut te faire subir.</h3>
            <p>
              Menaces AD ou AP, sustain, frontline, engage ou pick threat: tes alertes build
              et tes warnings suivent la vraie pression du lobby.
            </p>
          </article>

          <article className="feature-card">
            <p className="feature-kicker">Overlay de concentration</p>
            <h3>Visible quand il faut, silencieux le reste du temps.</h3>
            <p>
              L'overlay reste compact, lisible et calme. Tu gardes les yeux sur le jeu,
              pas sur une usine a gaz.
            </p>
          </article>

          <article className="feature-card">
            <p className="feature-kicker">Desktop de preparation</p>
            <h3>Tu regles tout hors partie, puis tu joues.</h3>
            <p>
              Position, preview, comportement du coach: tout se prepare avant la queue pour
              que l'experience en match reste simple.
            </p>
          </article>
        </div>
      </section>

      <section className="timeline" id="experience">
        <div className="timeline-copy">
          <p className="eyebrow">Ce que tu ressens</p>
          <h2>Moins d'hesitation. Plus de lucidite. Des parties plus propres.</h2>
        </div>

        <div className="timeline-steps">
          <article className="timeline-step">
            <span>01</span>
            <h3>Moins de bruit.</h3>
            <p>
              Tu ne combats pas un second ecran surcharge. Tu vois une priorite simple
              qui t'aide sans casser ton rythme.
            </p>
          </article>

          <article className="timeline-step">
            <span>02</span>
            <h3>Plus de confiance.</h3>
            <p>
              Quand le coach confirme un reset, une vision ou un tempo, tu engages
              l'action plus vite et avec moins de doute.
            </p>
          </article>

          <article className="timeline-step">
            <span>03</span>
            <h3>Des habitudes plus propres.</h3>
            <p>
              Partie apres partie, tu consolides de meilleurs timings au lieu de
              revivre les memes erreurs de greed, de retard ou de mauvais reset.
            </p>
          </article>
        </div>
      </section>

      <PricingSection
        isAuthenticated={Boolean(user)}
        plans={plans}
        supportEmail={site.supportEmail}
      />

      <section className="partner-strip">
        <div className="section-copy">
          <p className="eyebrow">Programme ambassadeur</p>
          <h2>Tu parles a des joueurs qui aiment les outils propres et utiles ? Fais decouvrir l'app et touche 10% des ventes.</h2>
          <p>
            Le programme est ouvert aux streamers, createurs et coaches capables de
            montrer le produit avec sincerite, exigence et envie. Si le profil colle,
            l'acces premium est offert.
          </p>
        </div>

        <div className="cta-row">
          <Link className="button button-primary" href="/ambassadeur">
            Candidater au programme
          </Link>
        </div>
      </section>

      <section className="faq-section">
        <div className="faq-header">
          <p className="eyebrow">FAQ</p>
          <h2>Ce que les joueurs veulent savoir avant de commencer.</h2>
        </div>

        <div className="faq-grid">
          <article className="faq-card">
            <h3>L'app reste-t-elle discrete pendant la partie ?</h3>
            <p>
              Oui. L'interface est volontairement courte, lisible et calme. Elle aide
              a lire la situation sans voler l'attention du match.
            </p>
          </article>

          <article className="faq-card">
            <h3>Faut-il une longue configuration avant la premiere game ?</h3>
            <p>
              Non. L'essentiel se prepare rapidement hors partie pour que l'experience
              en match reste fluide, propre et immediate.
            </p>
          </article>

          <article className="faq-card">
            <h3>Comment se passe l'achat ?</h3>
            <p>
              L'achat se fait sur le site avec compte client et paiement Stripe securise.
              Ensuite, tu retrouves ton abonnement, tes acces et le telechargement depuis ton espace.
            </p>
          </article>
        </div>
      </section>

      <footer className="site-footer">
        <div>
          <strong>{site.productName}</strong>
          <p>Produit independant, non affilie a Riot Games.</p>
        </div>

        <div className="footer-links">
          <a href={`mailto:${site.supportEmail}`}>{site.supportEmail}</a>
          <a href="#pricing">Acheter</a>
          <Link href="/ambassadeur">Ambassadeur</Link>
        </div>
      </footer>
    </main>
  );
}
