import type { PlayniteGame } from "../client.js";

/** Format seconds as "Xh Ym" */
export function formatPlaytime(seconds?: number): string {
  if (!seconds || seconds <= 0) return "Never played";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  if (hours === 0) return `${minutes}m`;
  if (minutes === 0) return `${hours}h`;
  return `${hours}h ${minutes}m`;
}

/** Format a date string to a short readable form */
export function formatDate(dateStr?: string): string {
  if (!dateStr) return "Unknown";
  try {
    const d = new Date(dateStr);
    return d.toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return dateStr;
  }
}

/** Get Steam header image URL if the game is from Steam */
export function getSteamImageUrl(game: PlayniteGame): string | null {
  if (game.source !== "Steam") return null;
  const steamLink = game.links?.find(
    (l) => l.url?.includes("store.steampowered.com") || l.url?.includes("steamcommunity.com")
  );
  if (steamLink) {
    const match = steamLink.url.match(/\/app\/(\d+)/);
    if (match) {
      return `https://steamcdn-a.akamaihd.net/steam/apps/${match[1]}/header.jpg`;
    }
  }
  // Try gameId field
  if (game.gameId && /^\d+$/.test(game.gameId)) {
    return `https://steamcdn-a.akamaihd.net/steam/apps/${game.gameId}/header.jpg`;
  }
  return null;
}

/** Format a game to a concise text line */
export function formatGameLine(game: PlayniteGame): string {
  const parts = [game.name];
  if (game.source) parts.push(`[${game.source}]`);
  if (game.playtime) parts.push(formatPlaytime(game.playtime));
  if (game.isInstalled) parts.push("✓ installed");
  if (game.favorite) parts.push("★");
  return parts.join(" — ");
}

/** Format a full game detail block */
export function formatGameDetail(game: PlayniteGame): string {
  const lines: string[] = [];
  lines.push(`# ${game.name}`);
  lines.push("");

  if (game.source) lines.push(`**Source:** ${game.source}`);
  if (game.platforms?.length) lines.push(`**Platforms:** ${game.platforms.join(", ")}`);
  if (game.genres?.length) lines.push(`**Genres:** ${game.genres.join(", ")}`);
  if (game.developers?.length) lines.push(`**Developers:** ${game.developers.join(", ")}`);
  if (game.publishers?.length) lines.push(`**Publishers:** ${game.publishers.join(", ")}`);
  if (game.releaseDate) lines.push(`**Release:** ${formatDate(game.releaseDate)}`);
  lines.push(`**Playtime:** ${formatPlaytime(game.playtime)}`);
  if (game.playCount) lines.push(`**Play count:** ${game.playCount}`);
  if (game.lastActivity) lines.push(`**Last played:** ${formatDate(game.lastActivity)}`);
  if (game.completionStatus) lines.push(`**Status:** ${game.completionStatus}`);
  if (game.userScore != null) lines.push(`**User score:** ${game.userScore}/100`);
  if (game.communityScore != null) lines.push(`**Community score:** ${game.communityScore}/100`);
  if (game.criticScore != null) lines.push(`**Critic score:** ${game.criticScore}/100`);
  if (game.categories?.length) lines.push(`**Categories:** ${game.categories.join(", ")}`);
  if (game.tags?.length) lines.push(`**Tags:** ${game.tags.join(", ")}`);
  if (game.features?.length) lines.push(`**Features:** ${game.features.join(", ")}`);
  if (game.series?.length) lines.push(`**Series:** ${game.series.join(", ")}`);
  if (game.description) {
    lines.push("");
    lines.push("## Description");
    // Strip HTML tags for text output
    lines.push(game.description.replace(/<[^>]+>/g, "").substring(0, 500));
  }
  if (game.notes) {
    lines.push("");
    lines.push("## Notes");
    lines.push(game.notes);
  }
  return lines.join("\n");
}

/** Source badge color mapping */
export const SOURCE_COLORS: Record<string, string> = {
  Steam: "#1b2838",
  "Epic Games": "#2a2a2a",
  "Xbox Game Pass": "#107c10",
  Xbox: "#107c10",
  "EA Play": "#ff4747",
  "EA app": "#ff4747",
  Ubisoft: "#0070ff",
  GOG: "#86328a",
  PlayStation: "#003087",
  Nintendo: "#e60012",
  "Amazon Games": "#ff9900",
  "Humble Bundle": "#cc2929",
  "Battle.net": "#00aeff",
  "itch.io": "#fa5c5c",
  Default: "#555555",
};

export function getSourceColor(source?: string): string {
  if (!source) return SOURCE_COLORS.Default;
  return SOURCE_COLORS[source] || SOURCE_COLORS.Default;
}
