function formatPercentValue(value) {
  return `${Math.round(Number(value || 0) * 1000) / 10}%`;
}

function formatMetricValue(card) {
  if (card.kind === "percent") {
    return formatPercentValue(card.current);
  }

  if (card.kind === "signed") {
    return `${card.current > 0 ? "+" : ""}${card.current}`;
  }

  return String(card.current);
}

function formatMetricDelta(card) {
  if (card.difference === null || card.previous === null) {
    return card.detail;
  }

  const prefix = card.difference > 0 ? "+" : "";
  return `${prefix}${card.difference} vs periode precedente`;
}

function getMetricTone(card) {
  if (card.difference === null || card.previous === null || card.trendMode === "neutral") {
    return "neutral";
  }

  if (card.trendMode === "down-good") {
    if (card.difference < 0) {
      return "good";
    }

    if (card.difference > 0) {
      return "bad";
    }

    return "neutral";
  }

  if (card.difference > 0) {
    return "good";
  }

  if (card.difference < 0) {
    return "bad";
  }

  return "neutral";
}

function KpiGrid({ cards }) {
  return (
    <div className="admin-kpi-grid">
      {cards.map((card) => (
        <article className="admin-card admin-kpi-card" key={card.id}>
          <div className="admin-kpi-topline">
            <span className="metric-label">{card.label}</span>
            <span className={`metric-chip admin-trend-chip is-${getMetricTone(card)}`}>
              {formatMetricDelta(card)}
            </span>
          </div>

          <strong className="admin-kpi-value">{formatMetricValue(card)}</strong>
          <p className="admin-kpi-detail">{card.detail}</p>
        </article>
      ))}
    </div>
  );
}

function TrendLegend({ items }) {
  return (
    <div className="admin-chart-legend">
      {items.map((item) => (
        <span className="admin-chart-legend-item" key={item.label}>
          <span className={`admin-chart-swatch is-${item.tone}`} />
          <span>{item.label}</span>
        </span>
      ))}
    </div>
  );
}

function GroupedBarsChart({ series }) {
  const maxValue = Math.max(
    1,
    ...series.flatMap((bucket) => [
      bucket.newUsers,
      bucket.newSubscriptions,
      bucket.lostSubscriptions
    ])
  );

  return (
    <div className="admin-bar-chart">
      {series.map((bucket) => (
        <div className="admin-bar-chart-group" key={bucket.label}>
          <div className="admin-bar-chart-bars">
            <span
              className="admin-bar is-users"
              style={{ height: `${(bucket.newUsers / maxValue) * 100}%` }}
              title={`${bucket.label} - ${bucket.newUsers} nouveaux comptes`}
            />
            <span
              className="admin-bar is-subs"
              style={{ height: `${(bucket.newSubscriptions / maxValue) * 100}%` }}
              title={`${bucket.label} - ${bucket.newSubscriptions} nouveaux abonnements`}
            />
            <span
              className="admin-bar is-churn"
              style={{ height: `${(bucket.lostSubscriptions / maxValue) * 100}%` }}
              title={`${bucket.label} - ${bucket.lostSubscriptions} pertes client`}
            />
          </div>
          <span className="admin-bar-label">{bucket.label}</span>
        </div>
      ))}
    </div>
  );
}

function buildPolylinePoints(series, key, width, height, padding) {
  const maxValue = Math.max(1, ...series.map((bucket) => bucket[key]));
  const innerWidth = width - padding * 2;
  const innerHeight = height - padding * 2;

  return series
    .map((bucket, index) => {
      const x = padding + (series.length === 1 ? innerWidth / 2 : (innerWidth / (series.length - 1)) * index);
      const y = padding + innerHeight - ((bucket[key] || 0) / maxValue) * innerHeight;
      return `${x},${y}`;
    })
    .join(" ");
}

function FlowLineChart({ series }) {
  const width = 720;
  const height = 220;
  const padding = 18;
  const subscriptionPoints = buildPolylinePoints(series, "newSubscriptions", width, height, padding);
  const churnPoints = buildPolylinePoints(series, "lostSubscriptions", width, height, padding);

  return (
    <div className="admin-line-chart-shell">
      <svg
        aria-label="Evolution quotidienne acquisition et perte client"
        className="admin-line-chart"
        role="img"
        viewBox={`0 0 ${width} ${height}`}
      >
        <line className="admin-line-grid" x1={padding} x2={width - padding} y1={height - padding} y2={height - padding} />
        <line className="admin-line-grid" x1={padding} x2={width - padding} y1={height / 2} y2={height / 2} />
        <line className="admin-line-grid" x1={padding} x2={width - padding} y1={padding} y2={padding} />
        <polyline className="admin-line-path is-subs" fill="none" points={subscriptionPoints} />
        <polyline className="admin-line-path is-churn" fill="none" points={churnPoints} />
      </svg>

      <div className="admin-line-chart-labels">
        <span>{series[0]?.label}</span>
        <span>{series[Math.floor(series.length / 2)]?.label}</span>
        <span>{series[series.length - 1]?.label}</span>
      </div>
    </div>
  );
}

export function AdminAnalyticsOverview({ analytics }) {
  return (
    <div className="admin-analytics-stack">
      <KpiGrid cards={analytics.overviewCards.slice(0, 4)} />

      <article className="admin-card admin-chart-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Flux 30 jours</p>
            <h2>Acquisition vs perte client</h2>
            <p className="admin-subtitle">Lecture rapide des entrees et sorties du premium sur les 30 derniers jours.</p>
          </div>
        </div>

        <TrendLegend
          items={[
            { label: "Nouveaux abonnements", tone: "subs" },
            { label: "Perte client", tone: "churn" }
          ]}
        />

        <FlowLineChart series={analytics.recentFlow} />
      </article>
    </div>
  );
}

export function AdminAnalyticsSection({ analytics }) {
  return (
    <section className="admin-analytics-stack">
      <KpiGrid cards={analytics.overviewCards} />

      <div className="admin-split-grid admin-list">
        <article className="admin-card admin-chart-card">
          <div className="admin-card-head">
            <div>
              <p className="eyebrow">Tendance 6 mois</p>
              <h2>Evolution business</h2>
              <p className="admin-subtitle">Comptes, abonnements et pertes client compares mois par mois.</p>
            </div>
          </div>

          <TrendLegend
            items={[
              { label: "Nouveaux comptes", tone: "users" },
              { label: "Nouveaux abonnements", tone: "subs" },
              { label: "Perte client", tone: "churn" }
            ]}
          />

          <GroupedBarsChart series={analytics.monthSeries} />
        </article>

        <article className="admin-card admin-chart-card">
          <div className="admin-card-head">
            <div>
              <p className="eyebrow">Flux 30 jours</p>
              <h2>Acquisition vs churn</h2>
              <p className="admin-subtitle">Vue glissante utile pour lire la dynamique recente.</p>
            </div>
          </div>

          <TrendLegend
            items={[
              { label: "Nouveaux abonnements", tone: "subs" },
              { label: "Perte client", tone: "churn" }
            ]}
          />

          <FlowLineChart series={analytics.recentFlow} />
        </article>
      </div>

      <article className="admin-card">
        <div className="admin-card-head">
          <div>
            <p className="eyebrow">Lecture metier</p>
            <h2>Notes d'interpretation</h2>
            <p className="admin-subtitle">Pour garder les KPI utiles sans sur-promettre sur la precision historique.</p>
          </div>
        </div>

        <div className="stack-list">
          <div className="inline-record">
            <div>
              <strong>Perte client</strong>
              <p>{analytics.inferenceNote}</p>
            </div>
          </div>

          <div className="inline-record">
            <div>
              <strong>Gain client net</strong>
              <p>{analytics.totals.netGrowth > 0 ? "La base abonnee progresse sur 30 jours." : analytics.totals.netGrowth < 0 ? "La base abonnee recule sur 30 jours." : "Croissance nette stable sur 30 jours."}</p>
            </div>
            <span className={`metric-chip admin-trend-chip is-${analytics.totals.netGrowth > 0 ? "good" : analytics.totals.netGrowth < 0 ? "bad" : "neutral"}`}>
              {analytics.totals.netGrowth > 0 ? "+" : ""}{analytics.totals.netGrowth}
            </span>
          </div>

          <div className="inline-record">
            <div>
              <strong>Clients a risque</strong>
              <p>{analytics.totals.pendingCancellations} abonnement(s) actifs ont deja une resiliation planifiee.</p>
            </div>
          </div>
        </div>
      </article>
    </section>
  );
}
