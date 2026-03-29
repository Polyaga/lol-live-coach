import Link from "next/link";
import { AdminAnalyticsOverview, AdminAnalyticsSection } from "./analytics-panels";
import {
  accessSourceLabel,
  actorLabel,
  formatDate,
  formatMoney,
  formatPercent,
  getVisibleAdminSections,
  grantStatus,
  matchesUserSearch,
  statusLabel
} from "./backoffice-helpers";

function adminSectionHref(section, extraParams = {}) {
  const url = new URL("/admin", "http://localhost");
  url.searchParams.set("section", section);

  for (const [key, value] of Object.entries(extraParams)) {
    if (value === undefined || value === null || value === "") {
      continue;
    }

    url.searchParams.set(key, String(value));
  }

  return `${url.pathname}${url.search}`;
}

function SectionHeading({ eyebrow, title, description }) {
  return (
    <div className="section-copy">
      <p className="eyebrow">{eyebrow}</p>
      <h2>{title}</h2>
      <p>{description}</p>
    </div>
  );
}

function FoldoutCard({ eyebrow, title, description, children }) {
  return (
    <details className="admin-card admin-foldout">
      <summary className="admin-foldout-summary">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2>{title}</h2>
          <p className="admin-subtitle">{description}</p>
        </div>
        <span className="admin-foldout-toggle">Afficher le formulaire</span>
      </summary>

      <div className="admin-foldout-content">{children}</div>
    </details>
  );
}

function AdminSubmenu({ activeSection, sections }) {
  if (!sections.length) {
    return null;
  }

  return (
    <nav className="admin-submenu">
      {sections.map((section) => (
        <Link
          className={`admin-submenu-link ${activeSection === section.id ? "is-active" : ""}`}
          href={adminSectionHref(section.id)}
          key={section.id}
        >
          <span>{section.label}</span>
          <small>{section.meta}</small>
        </Link>
      ))}
    </nav>
  );
}

function buildRoleHighlights({ capabilities, data, sections }) {
  return [
    {
      label: "Espaces visibles",
      value: sections.filter((section) => section.id !== "dashboard").length,
      detail: "module(s) accessibles"
    },
    capabilities.canViewUsers && {
      label: "Utilisateurs",
      value: data.summary.totalUsers,
      detail: `${data.summary.adminUsers} admin(s)`
    },
    capabilities.canViewAnalytics && {
      label: "Premium actif",
      value: data.summary.activePremiumUsers,
      detail: `${data.summary.activeSubscriptions} abonnement(s)`
    },
    capabilities.canViewAccess && {
      label: "Grants manuels",
      value: data.summary.activeManualGrants,
      detail: "actuellement actifs"
    },
    capabilities.canViewAmbassadors && {
      label: "Ambassadeurs",
      value: data.summary.pendingAmbassadors,
      detail: "dossier(s) a traiter"
    }
  ].filter(Boolean).slice(0, 4);
}

function DashboardSection({ capabilities, data, sections }) {
  const metricCards = [
    capabilities.canViewUsers && {
      label: "Utilisateurs",
      value: data.summary.totalUsers
    },
    capabilities.canViewUsers && {
      label: "Nouveaux comptes 30 j",
      value: data.summary.newUsersLast30Days
    },
    capabilities.canViewAnalytics && {
      label: "Premium actifs",
      value: data.summary.activePremiumUsers
    },
    capabilities.canViewAnalytics && {
      label: "Abonnements actifs",
      value: data.summary.activeSubscriptions
    },
    capabilities.canViewDashboard && {
      label: "Sessions desktop actives",
      value: data.summary.activeDesktopSessions
    },
    capabilities.canViewAccess && {
      label: "Acces premium manuels",
      value: data.summary.activeManualGrants
    },
    capabilities.canViewAmbassadors && {
      label: "Ambassadeurs en attente",
      value: data.summary.pendingAmbassadors
    },
    capabilities.canViewAmbassadors && {
      label: "CA ambassadeurs",
      value: formatMoney(data.summary.referredRevenue)
    }
  ].filter(Boolean);

  return (
    <section className="admin-section" id="dashboard">
      <SectionHeading
        description="Une vue rapide et adaptee aux permissions du role connecte."
        eyebrow="Dashboard"
        title="Vue d'ensemble"
      />

      <div className="admin-metrics">
        {metricCards.map((card) => (
          <article className="metric-card" key={card.label}>
            <span className="metric-label">{card.label}</span>
            <strong>{card.value}</strong>
          </article>
        ))}
      </div>

      {capabilities.canViewAnalytics ? <AdminAnalyticsOverview analytics={data.analytics} /> : null}

      <section className="admin-module-grid">
        {sections
          .filter((section) => section.id !== "dashboard")
          .map((section) => (
            <Link className="admin-module-card" href={adminSectionHref(section.id)} key={section.id}>
              <p className="eyebrow">{section.label}</p>
              <h3>{section.title}</h3>
              <p>{section.description}</p>
              <span className="metric-chip cool">{section.meta}</span>
            </Link>
          ))}
      </section>

      <div className="admin-list admin-split-grid">
        <article className="admin-card">
          <div className="admin-card-head">
            <div>
              <p className="eyebrow">Activite recente</p>
              <h2>Journal admin</h2>
              <p className="admin-subtitle">Les dernieres actions effectuees dans le back-office.</p>
            </div>
          </div>

          {data.recentActivity.length ? (
            <div className="stack-list">
              {data.recentActivity.map((activity) => (
                <div className="inline-record" key={activity.id}>
                  <div>
                    <strong>{activity.summary}</strong>
                    <p>{actorLabel(activity)} - {formatDate(activity.createdAt)}</p>
                  </div>
                  <span className="metric-chip">{activity.action}</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="empty-state">Aucune action admin journalisee pour le moment.</div>
          )}
        </article>

        <article className="admin-card">
          <div className="admin-card-head">
            <div>
              <p className="eyebrow">Reperes</p>
              <h2>Ce qu'il faut surveiller</h2>
              <p className="admin-subtitle">Un resume rapide des sujets visibles avec votre role.</p>
            </div>
          </div>

          <div className="stack-list">
            {capabilities.canViewUsers ? (
              <div className="inline-record">
                <div>
                  <strong>Comptes utilisateurs</strong>
                  <p>{data.summary.totalUsers} comptes, dont {data.summary.adminUsers} admins.</p>
                </div>
              </div>
            ) : null}

            {capabilities.canViewAmbassadors ? (
              <div className="inline-record">
                <div>
                  <strong>Programme ambassadeur</strong>
                  <p>{data.summary.pendingAmbassadors} candidature(s) en attente et {data.summary.approvedAmbassadors} profil(s) approuve(s).</p>
                </div>
              </div>
            ) : null}

            {capabilities.canViewAccess ? (
              <div className="inline-record">
                <div>
                  <strong>Acces premium manuels</strong>
                  <p>{data.summary.activeManualGrants} grant(s) manuel(s) actuellement actifs.</p>
                </div>
              </div>
            ) : null}

            {capabilities.canViewAnalytics ? (
              <div className="inline-record">
                <div>
                  <strong>Adoption premium</strong>
                  <p>{data.summary.activePremiumUsers} utilisateur(s) disposent d'un acces premium actif.</p>
                </div>
              </div>
            ) : null}
          </div>
        </article>
      </div>
    </section>
  );
}

function UsersSection({ data, searchQuery }) {
  const results = data.users.filter((user) => matchesUserSearch(user, searchQuery));

  return (
    <section className="admin-section" id="users">
      <SectionHeading
        description="Recherche simple par nom, prenom ou email. Cette vue est strictement en lecture seule."
        eyebrow="Utilisateurs"
        title="Annuaire utilisateurs"
      />

      <article className="admin-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Recherche</p>
            <h2>Trouver un utilisateur</h2>
            <p className="admin-subtitle">Saisissez un nom ou un email pour filtrer la liste.</p>
          </div>
          <span className="metric-chip warm">{results.length} resultat(s)</span>
        </div>

        <form action="/admin" className="admin-search-form" method="get">
          <input name="section" type="hidden" value="users" />
          <label className="field field-full">
            <span>Nom, prenom ou email</span>
            <input defaultValue={searchQuery} name="q" placeholder="Ex: Maxime, Jacquot, gmail.com..." type="search" />
          </label>

          <div className="cta-row compact-row">
            <button className="button button-primary" type="submit">
              Rechercher
            </button>
            {searchQuery ? (
              <Link className="button button-secondary" href={adminSectionHref("users")}>
                Reinitialiser
              </Link>
            ) : null}
          </div>
        </form>

        <div className="note-box admin-readonly-note">
          Les comptes utilisateurs sont consultables uniquement. Aucune edition n'est possible depuis le back-office, y compris en environnement de dev.
        </div>
      </article>

      <div className="admin-list">
        {results.length === 0 ? (
          <div className="empty-state">Aucun utilisateur ne correspond a cette recherche.</div>
        ) : (
          results.map((user) => (
            <details className="admin-card admin-user-item" key={user.id}>
              <summary className="admin-user-summary">
                <div>
                  <p className="eyebrow">Utilisateur</p>
                  <h2>{user.displayName || user.email}</h2>
                  <p className="admin-subtitle">{user.email}</p>
                </div>

                <div className="admin-user-summary-meta">
                  <span className={`status-pill ${user.access.hasPremiumAccess ? "status-approved" : "status-pending"}`}>
                    {user.adminRole?.name || accessSourceLabel(user.access)}
                  </span>
                  <span className="metric-chip">{user.desktopSessionCount} session(s)</span>
                </div>
              </summary>

              <div className="admin-user-detail-grid">
                <div className="ambassador-detail-card"><span className="metric-label">Compte cree le</span><strong>{formatDate(user.createdAt)}</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Acces premium</span><strong>{user.access.hasPremiumAccess ? "Actif" : "Aucun"}</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Source d'acces</span><strong>{accessSourceLabel(user.access)}</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Role admin</span><strong>{user.adminRole?.name || "Aucun"}</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Abonnements</span><strong>{user.subscriptionCount}</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Desktop</span><strong>{user.desktopSessionCount} session(s)</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Ambassadeur</span><strong>{user.ambassadorStatus ? statusLabel(user.ambassadorStatus) : "Aucun dossier"}</strong></div>
                <div className="ambassador-detail-card"><span className="metric-label">Grants manuels actifs</span><strong>{user.activeGrantCount}</strong></div>
              </div>
            </details>
          ))
        )}
      </div>
    </section>
  );
}

function RolesSection({ capabilities, data }) {
  return (
    <section className="admin-section" id="roles">
      <SectionHeading
        description="Roles admin systemes et personnalises. Chaque action reste dans son espace dedie."
        eyebrow="Roles"
        title="Permissions et organisation"
      />

      {capabilities.canManageRoles ? (
        <FoldoutCard
          description="Creer un role personnalise sans encombrer l'ecran principal."
          eyebrow="Nouveau role"
          title="Creer un role"
        >
          <form action="/api/admin/roles/create" className="application-form" method="post">
            <input name="section" type="hidden" value="roles" />

            <div className="form-grid">
              <label className="field">
                <span>Nom</span>
                <input name="name" required type="text" />
              </label>

              <label className="field">
                <span>Cle technique</span>
                <input name="key" placeholder="finance" type="text" />
              </label>

              <label className="field field-full">
                <span>Description</span>
                <textarea name="description" rows="3" />
              </label>
            </div>

            <div className="permission-grid">
              {data.permissionCatalog.map((group) => (
                <fieldset className="permission-group" key={group.id}>
                  <legend>{group.label}</legend>
                  {group.permissions.map((permission) => (
                    <label className="checkbox-field permission-option" key={permission.key}>
                      <input name="permissions" type="checkbox" value={permission.key} />
                      <span>
                        <strong>{permission.label}</strong>
                        <small>{permission.description}</small>
                      </span>
                    </label>
                  ))}
                </fieldset>
              ))}
            </div>

            <button className="button button-primary" type="submit">
              Creer le role
            </button>
          </form>
        </FoldoutCard>
      ) : null}

      <div className="admin-list">
        {data.roles.map((role) => (
          <article className="admin-card" key={role.id}>
            <div className="admin-card-head">
              <div>
                <p className="eyebrow">{role.isSystem ? "Role systeme" : "Role personnalise"}</p>
                <h2>{role.name}</h2>
                <p className="admin-subtitle">Cle: {role.key} - {role.memberCount} membre(s)</p>
              </div>

              <div className={`status-pill ${role.isSystem ? "status-approved" : "status-pending"}`}>
                {role.isSystem ? "Systeme" : "Custom"}
              </div>
            </div>

            <div className="chip-row">
              {role.permissions.map((permission) => (
                <span className="metric-chip" key={`${role.id}-${permission}`}>
                  {permission}
                </span>
              ))}
            </div>

            {role.description ? (
              <div className="admin-details">
                <p><strong>Description:</strong> {role.description}</p>
              </div>
            ) : null}

            {capabilities.canManageRoles ? (
              <form action="/api/admin/roles/update" className="application-form" method="post">
                <input name="id" type="hidden" value={role.id} />
                <input name="section" type="hidden" value="roles" />

                <div className="form-grid">
                  <label className="field">
                    <span>Nom</span>
                    <input defaultValue={role.name} name="name" required type="text" />
                  </label>

                  <label className="field">
                    <span>Cle technique</span>
                    <input defaultValue={role.key} disabled={role.isSystem} name="key" type="text" />
                  </label>

                  <label className="field field-full">
                    <span>Description</span>
                    <textarea defaultValue={role.description || ""} name="description" rows="3" />
                  </label>
                </div>

                <div className="permission-grid">
                  {data.permissionCatalog.map((group) => (
                    <fieldset className="permission-group" key={`${role.id}-${group.id}`}>
                      <legend>{group.label}</legend>
                      {group.permissions.map((permission) => (
                        <label className="checkbox-field permission-option" key={`${role.id}-${permission.key}`}>
                          <input
                            defaultChecked={role.permissions.includes(permission.key)}
                            disabled={role.key === "owner"}
                            name="permissions"
                            type="checkbox"
                            value={permission.key}
                          />
                          <span>
                            <strong>{permission.label}</strong>
                            <small>{permission.description}</small>
                          </span>
                        </label>
                      ))}
                    </fieldset>
                  ))}
                </div>

                {role.key === "owner" ? (
                  <div className="note-box admin-note-box">
                    Le role Owner conserve toujours toutes les permissions pour eviter de bloquer completement l'administration.
                  </div>
                ) : null}

                <button className="button button-primary" type="submit">
                  Mettre a jour le role
                </button>
              </form>
            ) : null}
          </article>
        ))}
      </div>
    </section>
  );
}

function AccessSection({ capabilities, data }) {
  return (
    <section className="admin-section" id="access">
      <SectionHeading
        description="Accorder ou revoquer un acces premium sans passer par Stripe."
        eyebrow="Acces premium"
        title="Grants manuels"
      />

      {capabilities.canManageAccess ? (
        <FoldoutCard
          description="Accorder un acces premium ponctuel ou durable."
          eyebrow="Nouvel acces"
          title="Accorder un acces premium"
        >
          <form action="/api/admin/access-grants/create" className="application-form" method="post">
            <input name="section" type="hidden" value="access" />

            <div className="form-grid">
              <label className="field">
                <span>Email utilisateur</span>
                <input name="email" required type="email" />
              </label>

              <label className="field">
                <span>Raison</span>
                <input name="reason" placeholder="Partenariat, support, geste commercial..." required type="text" />
              </label>

              <label className="field">
                <span>Debut</span>
                <input name="startsAt" type="date" />
              </label>

              <label className="field">
                <span>Fin</span>
                <input name="endsAt" type="date" />
              </label>

              <label className="field field-full">
                <span>Notes</span>
                <textarea name="notes" rows="3" />
              </label>
            </div>

            <button className="button button-primary" type="submit">
              Accorder l'acces
            </button>
          </form>
        </FoldoutCard>
      ) : null}

      <div className="admin-list">
        {data.accessGrants.length === 0 ? (
          <div className="empty-state">Aucun acces premium manuel n'a encore ete cree.</div>
        ) : (
          data.accessGrants.map((grant) => {
            const grantBadge = grantStatus(grant);

            return (
              <article className="admin-card" key={grant.id}>
                <div className="admin-card-head">
                  <div>
                    <p className="eyebrow">Grant premium</p>
                    <h2>{grant.user.displayName || grant.user.email}</h2>
                    <p className="admin-subtitle">{grant.user.email} - cree le {formatDate(grant.createdAt)}</p>
                  </div>

                  <div className={`status-pill ${grantBadge.tone}`}>{grantBadge.label}</div>
                </div>

                <div className="admin-details">
                  <p><strong>Raison:</strong> {grant.reason}</p>
                  <p><strong>Debut:</strong> {formatDate(grant.startsAt)}</p>
                  <p><strong>Fin:</strong> {grant.endsAt ? formatDate(grant.endsAt) : "Sans date de fin"}</p>
                  <p><strong>Accorde par:</strong> {grant.grantedByUser?.displayName || grant.grantedByUser?.email || "Equipe admin"}</p>
                  <p><strong>Notes:</strong> {grant.notes || "Aucune note."}</p>
                </div>

                {capabilities.canManageAccess && !grant.revokedAt ? (
                  <form action="/api/admin/access-grants/revoke" className="application-form" method="post">
                    <input name="id" type="hidden" value={grant.id} />
                    <input name="section" type="hidden" value="access" />
                    <button className="button button-secondary" type="submit">
                      Revoquer cet acces
                    </button>
                  </form>
                ) : null}
              </article>
            );
          })
        )}
      </div>
    </section>
  );
}

function AnalyticsSection({ data }) {
  return (
    <section className="admin-section" id="analytics">
      <SectionHeading
        description="KPI business lisibles pour suivre acquisition, abonnement et perte client sans quitter le back-office."
        eyebrow="Stats"
        title="Pilotage de la plateforme"
      />

      <AdminAnalyticsSection analytics={data.analytics} />
    </section>
  );
}

function AmbassadorQuickAction({ applicationId, grantAccess = false, label, tone = "secondary", status }) {
  return (
    <form action="/api/admin/ambassadors/update" className="ambassador-quick-form" method="post">
      <input name="id" type="hidden" value={applicationId} />
      <input name="section" type="hidden" value="ambassadors" />
      <input name="status" type="hidden" value={status} />
      {grantAccess ? <input name="accessGranted" type="hidden" value="on" /> : null}

      <button className={`button ${tone === "primary" ? "button-primary" : "button-secondary"}`} type="submit">
        {label}
      </button>
    </form>
  );
}

function AmbassadorApplicationCard({ application, capabilities, showQuickActions = false }) {
  return (
    <article className="admin-card" key={application.id}>
      <div className="admin-card-head">
        <div>
          <p className="eyebrow">{application.status === "pending" ? "Demande entrante" : "Dossier ambassadeur"}</p>
          <h2>{application.channelName || application.fullName}</h2>
          <p className="admin-subtitle">
            {application.fullName} - {application.email} - {application.platform || "Plateforme non renseignee"}
          </p>
        </div>

        <div className={`status-pill status-${application.status}`}>{statusLabel(application.status)}</div>
      </div>

      <div className="admin-details">
        <p><strong>Envoye le:</strong> {formatDate(application.createdAt)}</p>
        <p><strong>Lien:</strong> {application.channelUrl || "Non renseigne"}</p>
        <p><strong>Audience:</strong> {application.audienceSummary || "Non renseignee"}</p>
        <p><strong>Motivation:</strong> {application.motivation || "Aucune note"}</p>
        <p><strong>Commission:</strong> {formatPercent(application.commissionRate)}</p>
        <p><strong>Revenus attribues:</strong> {formatMoney(application.referredRevenue)}</p>
        <p><strong>Commission a payer:</strong> {formatMoney(application.commissionDue)}</p>
        <p><strong>Lien d'affiliation:</strong> {application.affiliateLink || "Genere apres validation"}</p>
      </div>

      {showQuickActions && capabilities.canManageAmbassadors ? (
        <div className="ambassador-request-actions">
          <div className="note-box admin-note-box">
            Cette candidature est en attente. Vous pouvez la valider ou la refuser immediatement depuis ici.
          </div>

          <div className="cta-row compact-row">
            <AmbassadorQuickAction
              applicationId={application.id}
              grantAccess
              label="Accepter la demande"
              status="approved"
              tone="primary"
            />
            <AmbassadorQuickAction
              applicationId={application.id}
              label="Refuser la demande"
              status="rejected"
            />
          </div>
        </div>
      ) : null}

      {capabilities.canManageAmbassadors ? (
        <form action="/api/admin/ambassadors/update" className="application-form" method="post">
          <input name="id" type="hidden" value={application.id} />
          <input name="section" type="hidden" value="ambassadors" />

          <div className="form-grid">
            <label className="field">
              <span>Statut</span>
              <select defaultValue={application.status} name="status">
                <option value="pending">En attente</option>
                <option value="approved">Approuve</option>
                <option value="rejected">Refuse</option>
              </select>
            </label>

            <label className="field">
              <span>Code affiliation</span>
              <input defaultValue={application.affiliateCode} name="affiliateCode" type="text" />
            </label>

            <label className="field">
              <span>Revenus apportes (EUR)</span>
              <input defaultValue={application.referredRevenue} name="referredRevenue" step="0.01" type="number" />
            </label>

            <label className="field">
              <span>Deja verse (EUR)</span>
              <input defaultValue={application.paidOutAmount} name="paidOutAmount" step="0.01" type="number" />
            </label>

            <label className="field checkbox-field field-full">
              <input defaultChecked={application.accessGranted} name="accessGranted" type="checkbox" />
              <span>Acces premium offert deja accorde</span>
            </label>

            <label className="field field-full">
              <span>Notes admin</span>
              <textarea defaultValue={application.adminNotes} name="adminNotes" rows="4" />
            </label>
          </div>

          <button className="button button-primary" type="submit">
            Enregistrer
          </button>
        </form>
      ) : null}
    </article>
  );
}

function AmbassadorsSection({ capabilities, data }) {
  const pendingApplications = data.ambassadorMetrics.applications.filter((application) => application.status === "pending");
  const processedApplications = data.ambassadorMetrics.applications.filter((application) => application.status !== "pending");

  return (
    <section className="admin-section" id="ambassadors">
      <SectionHeading
        description="Demandes entrantes, validations, creations manuelles, codes d'affiliation et commissions."
        eyebrow="Ambassadeurs"
        title="Programme partenaire"
      />

      <div className="admin-metrics ambassador-metrics">
        <article className="metric-card">
          <span className="metric-label">Demandes en attente</span>
          <strong>{pendingApplications.length}</strong>
        </article>
        <article className="metric-card">
          <span className="metric-label">Ambassadeurs approuves</span>
          <strong>{data.summary.approvedAmbassadors}</strong>
        </article>
        <article className="metric-card">
          <span className="metric-label">Acces premium accordes</span>
          <strong>{data.summary.grantedAmbassadorAccesses}</strong>
        </article>
        <article className="metric-card">
          <span className="metric-label">Revenus attribues</span>
          <strong>{formatMoney(data.summary.referredRevenue)}</strong>
        </article>
      </div>

      {capabilities.canManageAmbassadors ? (
        <FoldoutCard
          description="Onboarder un createur sans attendre sa candidature publique."
          eyebrow="Creation manuelle"
          title="Creer un ambassadeur"
        >
          <form action="/api/admin/ambassadors/create" className="application-form" method="post">
            <input name="section" type="hidden" value="ambassadors" />

            <div className="form-grid">
              <label className="field">
                <span>Nom complet</span>
                <input name="fullName" required type="text" />
              </label>

              <label className="field">
                <span>Email</span>
                <input name="email" required type="email" />
              </label>

              <label className="field">
                <span>Nom de chaine</span>
                <input name="channelName" required type="text" />
              </label>

              <label className="field">
                <span>Plateforme</span>
                <select defaultValue="Twitch" name="platform">
                  <option value="Twitch">Twitch</option>
                  <option value="YouTube">YouTube</option>
                  <option value="TikTok">TikTok</option>
                  <option value="Kick">Kick</option>
                  <option value="Autre">Autre</option>
                </select>
              </label>

              <label className="field field-full">
                <span>Lien de chaine</span>
                <input name="channelUrl" required type="url" />
              </label>

              <label className="field">
                <span>Statut</span>
                <select defaultValue="approved" name="status">
                  <option value="pending">En attente</option>
                  <option value="approved">Approuve</option>
                  <option value="rejected">Refuse</option>
                </select>
              </label>

              <label className="field">
                <span>Commission (%)</span>
                <input defaultValue="10" name="commissionRate" step="0.1" type="number" />
              </label>

              <label className="field">
                <span>Code d'affiliation</span>
                <input name="affiliateCode" type="text" />
              </label>

              <label className="field checkbox-field">
                <input defaultChecked name="accessGranted" type="checkbox" />
                <span>Accorder le premium si un compte utilisateur existe deja</span>
              </label>

              <label className="field field-full">
                <span>Audience</span>
                <textarea name="audienceSummary" rows="3" />
              </label>

              <label className="field field-full">
                <span>Motivation / contexte</span>
                <textarea name="motivation" required rows="4" />
              </label>

              <label className="field field-full">
                <span>Notes internes</span>
                <textarea name="adminNotes" rows="3" />
              </label>
            </div>

            <button className="button button-primary" type="submit">
              Creer l'ambassadeur
            </button>
          </form>
        </FoldoutCard>
      ) : null}

      <article className="admin-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Demandes entrantes</p>
            <h2>Candidatures recues via le formulaire</h2>
            <p className="admin-subtitle">Chaque candidature en attente remonte ici pour pouvoir etre acceptee ou refusee directement.</p>
          </div>
          <span className="metric-chip warm">{pendingApplications.length} en attente</span>
        </div>
      </article>

      <div className="admin-list">
        {pendingApplications.length === 0 ? (
          <div className="empty-state">
            Aucune demande en attente pour le moment. Les nouvelles candidatures envoyees depuis le formulaire public apparaitront ici automatiquement.
          </div>
        ) : (
          pendingApplications.map((application) => (
            <AmbassadorApplicationCard
              application={application}
              capabilities={capabilities}
              key={application.id}
              showQuickActions
            />
          ))
        )}
      </div>

      <article className="admin-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Suivi programme</p>
            <h2>Dossiers traites et ambassadeurs actifs</h2>
            <p className="admin-subtitle">Les profils approuves ou refuses restent accessibles ici pour le suivi et les ajustements.</p>
          </div>
          <span className="metric-chip cool">{processedApplications.length} dossier(s)</span>
        </div>
      </article>

      <div className="admin-list">
        {processedApplications.length === 0 ? (
          <div className="empty-state">
            Aucun dossier traite pour le moment. Une fois validee ou refusee, une candidature restera visible ici.
          </div>
        ) : (
          processedApplications.map((application) => (
            <AmbassadorApplicationCard
              application={application}
              capabilities={capabilities}
              key={application.id}
            />
          ))
        )}
      </div>
    </section>
  );
}

export function AdminBackofficeView({
  activeSection,
  capabilities,
  data,
  error,
  notice,
  searchQuery,
  session,
  site
}) {
  const sections = getVisibleAdminSections({
    capabilities,
    data
  });
  const roleHighlights = buildRoleHighlights({
    capabilities,
    data,
    sections
  });
  const visibleSectionNames = sections
    .filter((section) => section.id !== "dashboard")
    .map((section) => section.label)
    .join(", ");

  return (
    <main className="site-shell admin-shell">
      <header className="topbar">
        <Link className="brand" href="/">
          <span className="brand-mark" />
          <span>{site.productName} Back-office</span>
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
          <p className="eyebrow">Back-office</p>
          <h1 className="admin-title">Un espace par usage, un dashboard par role.</h1>
          <p>
            Le back-office est maintenant organise en sections distinctes. Vous travaillez sur un seul espace a la fois, avec une vue d'ensemble adaptee aux droits du role connecte.
          </p>
        </div>

        <div className="partner-strip admin-summary-strip">
          <div>
            <p className="eyebrow">Session</p>
            <h2>{session.user.adminRole?.name || "Admin"}</h2>
            <p>
              Connecte avec {session.user.displayName || session.user.email}. Ce role voit
              {" "}{sections.length} espace(s){visibleSectionNames ? ` : ${visibleSectionNames}.` : "."}
            </p>
          </div>

          <div className="admin-role-highlights">
            {roleHighlights.map((highlight) => (
              <article className="admin-role-highlight" key={highlight.label}>
                <span className="metric-label">{highlight.label}</span>
                <strong>{highlight.value}</strong>
                <small>{highlight.detail}</small>
              </article>
            ))}
          </div>
        </div>
      </section>

      <AdminSubmenu activeSection={activeSection} sections={sections} />

      {notice ? <div className="success-banner">{notice}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      {activeSection === "dashboard" ? <DashboardSection capabilities={capabilities} data={data} sections={sections} /> : null}
      {activeSection === "users" ? <UsersSection data={data} searchQuery={searchQuery} /> : null}
      {activeSection === "analytics" && capabilities.canViewAnalytics ? <AnalyticsSection data={data} /> : null}
      {activeSection === "roles" && capabilities.canViewRoles ? <RolesSection capabilities={capabilities} data={data} /> : null}
      {activeSection === "access" && capabilities.canViewAccess ? <AccessSection capabilities={capabilities} data={data} /> : null}
      {activeSection === "ambassadors" && capabilities.canViewAmbassadors ? <AmbassadorsSection capabilities={capabilities} data={data} /> : null}
    </main>
  );
}
