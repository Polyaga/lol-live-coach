const DEFAULT_PLATFORM_REGION = "EUW1";
const MIN_RECENT_MATCHES = 3;
const MAX_RECENT_MATCHES = 8;
const DEFAULT_RECENT_MATCHES = 5;
const RIOT_TIMEOUT_MS = 8000;

function createUnavailableProfile(message, { isConfigured = true, riotId = "", platformRegion = "" } = {}) {
  return {
    isConfigured,
    isAvailable: false,
    message,
    riotId,
    platformRegion,
    summonerLevel: 0,
    soloQueueTier: "Non classe",
    soloQueueLeaguePoints: 0,
    soloQueueWins: 0,
    soloQueueLosses: 0,
    recentKda: 0,
    recentWinRate: 0,
    recentCsPerMinute: 0,
    recentSampleSize: 0,
    recentMatches: []
  };
}

function getRiotApiKey() {
  return String(process.env.RIOT_API_KEY || "").trim();
}

function parseRiotId(riotId) {
  const normalized = String(riotId || "").trim();

  if (!normalized) {
    return null;
  }

  const separatorIndex = normalized.lastIndexOf("#");
  if (separatorIndex <= 0 || separatorIndex >= normalized.length - 1) {
    return null;
  }

  const gameName = normalized.slice(0, separatorIndex).trim();
  const tagLine = normalized.slice(separatorIndex + 1).trim();

  if (!gameName || !tagLine) {
    return null;
  }

  return {
    gameName,
    tagLine,
    normalized: `${gameName}#${tagLine.toUpperCase()}`
  };
}

function normalizePlatform(platformRegion) {
  const normalized = String(platformRegion || "").trim().toUpperCase();
  return normalized || null;
}

function getDefaultPlatformRegion() {
  return normalizePlatform(process.env.RIOT_DEFAULT_PLATFORM_REGION) || DEFAULT_PLATFORM_REGION;
}

function getRecentMatchesCount() {
  const parsedValue = Number.parseInt(String(process.env.RIOT_RECENT_MATCHES_COUNT || DEFAULT_RECENT_MATCHES), 10);

  if (Number.isNaN(parsedValue)) {
    return DEFAULT_RECENT_MATCHES;
  }

  return Math.min(MAX_RECENT_MATCHES, Math.max(MIN_RECENT_MATCHES, parsedValue));
}

function resolveRoutingRegion(platformRegion) {
  switch (platformRegion) {
    case "EUW1":
    case "EUN1":
    case "TR1":
    case "RU":
      return "europe";
    case "NA1":
    case "BR1":
    case "LA1":
    case "LA2":
      return "americas";
    case "KR":
    case "JP1":
      return "asia";
    case "OC1":
    case "PH2":
    case "SG2":
    case "TH2":
    case "TW2":
    case "VN2":
      return "sea";
    default:
      return null;
  }
}

function capitalize(value) {
  const lower = String(value || "").toLowerCase();
  return lower ? `${lower[0].toUpperCase()}${lower.slice(1)}` : lower;
}

function formatSoloQueue(entry) {
  if (!entry?.tier) {
    return "Non classe";
  }

  return `${capitalize(entry.tier)} ${String(entry.rank || "").trim()}`.trim();
}

function mapQueueLabel(queueId) {
  switch (queueId) {
    case 420:
      return "SoloQ";
    case 440:
      return "Flex";
    case 450:
      return "ARAM";
    case 400:
      return "Draft";
    case 430:
      return "Blind";
    case 490:
      return "Quickplay";
    case 700:
      return "Clash";
    default:
      return `Queue ${queueId}`;
  }
}

function buildRiotFetchOptions(apiKey) {
  const options = {
    headers: {
      "X-Riot-Token": apiKey
    },
    cache: "no-store"
  };

  if (typeof AbortSignal !== "undefined" && typeof AbortSignal.timeout === "function") {
    options.signal = AbortSignal.timeout(RIOT_TIMEOUT_MS);
  }

  return options;
}

async function getRiotJson(url, apiKey) {
  const response = await fetch(url, buildRiotFetchOptions(apiKey));

  if (response.status === 404) {
    return {
      status: response.status,
      payload: null
    };
  }

  if (response.status === 429) {
    throw new Error("Le profil joueur est temporairement limite par l'API Riot. Reessaie dans un instant.");
  }

  if (!response.ok) {
    throw new Error(`Le profil joueur n'a pas pu etre charge (${response.status}).`);
  }

  return {
    status: response.status,
    payload: await response.json()
  };
}

async function loadRecentMatches({ routingRegion, puuid, matchIds, apiKey }) {
  const recentMatches = await Promise.all(matchIds.map(async (matchId) => {
    const result = await getRiotJson(
      `https://${routingRegion}.api.riotgames.com/lol/match/v5/matches/${encodeURIComponent(matchId)}`,
      apiKey
    );
    const match = result.payload;
    const participant = match?.info?.participants?.find((item) => item?.puuid === puuid);

    if (!match?.info || !participant) {
      return null;
    }

    const totalCs = Number(participant.totalMinionsKilled || 0) + Number(participant.neutralMinionsKilled || 0);
    const playedAtMs = Number(match.info.gameEndTimestamp || 0) > 0
      ? Number(match.info.gameEndTimestamp)
      : Number(match.info.gameCreation || 0);
    const playedAt = playedAtMs > 0 ? new Date(playedAtMs) : null;

    return {
      matchId: match?.metadata?.matchId || matchId,
      championName: participant.championName || "Champion inconnu",
      queueLabel: mapQueueLabel(Number(match.info.queueId || 0)),
      win: Boolean(participant.win),
      kills: Number(participant.kills || 0),
      deaths: Number(participant.deaths || 0),
      assists: Number(participant.assists || 0),
      cs: totalCs,
      durationMinutes: Math.max(1, Math.round(Number(match.info.gameDuration || 0) / 60)),
      playedAt: playedAt?.toISOString() || new Date(0).toISOString()
    };
  }));

  return recentMatches
    .filter(Boolean)
    .sort((left, right) => Date.parse(right.playedAt) - Date.parse(left.playedAt));
}

function buildProfileResponse({ account, platformRegion, summoner, soloQueueEntry, recentMatches }) {
  const totalKills = recentMatches.reduce((sum, match) => sum + match.kills, 0);
  const totalDeaths = recentMatches.reduce((sum, match) => sum + match.deaths, 0);
  const totalAssists = recentMatches.reduce((sum, match) => sum + match.assists, 0);
  const totalWins = recentMatches.filter((match) => match.win).length;
  const totalMinutes = recentMatches.reduce((sum, match) => sum + match.durationMinutes, 0);
  const totalCs = recentMatches.reduce((sum, match) => sum + match.cs, 0);
  const sampleSize = recentMatches.length;

  return {
    isConfigured: true,
    isAvailable: true,
    message: sampleSize === 0
      ? "Profil charge. Lance quelques parties pour remplir l'historique recent."
      : `Profil mis a jour sur ${sampleSize} parties recentes.`,
    riotId: `${account.gameName}#${account.tagLine}`,
    platformRegion,
    summonerLevel: Number(summoner?.summonerLevel || 0),
    soloQueueTier: formatSoloQueue(soloQueueEntry),
    soloQueueLeaguePoints: Number(soloQueueEntry?.leaguePoints || 0),
    soloQueueWins: Number(soloQueueEntry?.wins || 0),
    soloQueueLosses: Number(soloQueueEntry?.losses || 0),
    recentKda: sampleSize === 0 ? 0 : Number(((totalKills + totalAssists) / Math.max(1, totalDeaths)).toFixed(2)),
    recentWinRate: sampleSize === 0 ? 0 : Number(((totalWins / sampleSize) * 100).toFixed(1)),
    recentCsPerMinute: totalMinutes === 0 ? 0 : Number((totalCs / totalMinutes).toFixed(1)),
    recentSampleSize: sampleSize,
    recentMatches
  };
}

export function isRiotProfileConfigured() {
  return Boolean(getRiotApiKey());
}

export async function getPlayerProfile({ riotId, platformRegion }) {
  const apiKey = getRiotApiKey();
  const parsedRiotId = parseRiotId(riotId);
  const normalizedPlatform = normalizePlatform(platformRegion) || getDefaultPlatformRegion();
  const safeRiotId = parsedRiotId?.normalized || String(riotId || "").trim();

  if (!apiKey) {
    return createUnavailableProfile(
      "La cle Riot API n'est pas configuree sur la plateforme.",
      {
        isConfigured: false,
        riotId: safeRiotId,
        platformRegion: normalizedPlatform
      }
    );
  }

  if (!parsedRiotId) {
    return createUnavailableProfile(
      "Renseigne un Riot ID au format NomDeJoueur#TAG.",
      {
        riotId: safeRiotId,
        platformRegion: normalizedPlatform
      }
    );
  }

  const routingRegion = resolveRoutingRegion(normalizedPlatform);
  if (!routingRegion) {
    return createUnavailableProfile(
      "La region de jeu est invalide. Exemple : EUW1.",
      {
        riotId: parsedRiotId.normalized,
        platformRegion: normalizedPlatform
      }
    );
  }

  try {
    const accountResult = await getRiotJson(
      `https://${routingRegion}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/${encodeURIComponent(parsedRiotId.gameName)}/${encodeURIComponent(parsedRiotId.tagLine)}`,
      apiKey
    );

    if (!accountResult.payload) {
      return createUnavailableProfile(
        `Impossible de trouver ${parsedRiotId.normalized}. Verifie le Riot ID et la region.`,
        {
          riotId: parsedRiotId.normalized,
          platformRegion: normalizedPlatform
        }
      );
    }

    const account = accountResult.payload;
    const recentMatchesCount = getRecentMatchesCount();
    const [summonerResult, leagueResult, matchIdsResult] = await Promise.all([
      getRiotJson(
        `https://${normalizedPlatform.toLowerCase()}.api.riotgames.com/lol/summoner/v4/summoners/by-puuid/${encodeURIComponent(account.puuid)}`,
        apiKey
      ),
      getRiotJson(
        `https://${normalizedPlatform.toLowerCase()}.api.riotgames.com/lol/league/v4/entries/by-puuid/${encodeURIComponent(account.puuid)}`,
        apiKey
      ),
      getRiotJson(
        `https://${routingRegion}.api.riotgames.com/lol/match/v5/matches/by-puuid/${encodeURIComponent(account.puuid)}/ids?start=0&count=${recentMatchesCount}`,
        apiKey
      )
    ]);

    const recentMatches = await loadRecentMatches({
      routingRegion,
      puuid: account.puuid,
      matchIds: Array.isArray(matchIdsResult.payload) ? matchIdsResult.payload : [],
      apiKey
    });
    const soloQueueEntry = Array.isArray(leagueResult.payload)
      ? leagueResult.payload.find((entry) => String(entry?.queueType || "").toUpperCase() === "RANKED_SOLO_5X5")
      : null;

    return buildProfileResponse({
      account,
      platformRegion: normalizedPlatform,
      summoner: summonerResult.payload,
      soloQueueEntry,
      recentMatches
    });
  } catch (error) {
    return createUnavailableProfile(
      error instanceof Error ? error.message : "Le profil joueur est temporairement indisponible.",
      {
        riotId: parsedRiotId.normalized,
        platformRegion: normalizedPlatform
      }
    );
  }
}
