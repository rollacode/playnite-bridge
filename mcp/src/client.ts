import { readFileSync } from "node:fs";

const DEFAULT_AUTH_PATH =
  "C:/Games/Playnite/ExtensionsData/f47ac10b-58cc-4372-a567-0e02b2c3d479/auth.json";

export interface PlayniteGame {
  id: string;
  name: string;
  source?: string;
  genres?: string[];
  categories?: string[];
  tags?: string[];
  features?: string[];
  platforms?: string[];
  completionStatus?: string;
  isInstalled?: boolean;
  favorite?: boolean;
  hidden?: boolean;
  playtime?: number;
  playCount?: number;
  lastActivity?: string;
  userScore?: number;
  description?: string;
  notes?: string;
  developers?: string[];
  publishers?: string[];
  series?: string[];
  releaseDate?: string;
  communityScore?: number;
  criticScore?: number;
  links?: Array<{ name: string; url: string }>;
  gameId?: string; // Steam app ID from links
  coverImage?: string;
  backgroundImage?: string;
}

export interface GamesResponse {
  total: number;
  offset: number;
  limit: number;
  games: PlayniteGame[];
}

export interface CollectionItem {
  id: string;
  name: string;
}

export interface StatsResponse {
  totalGames: number;
  installed: number;
  favorites: number;
  totalPlaytime: number;
  bySource: Record<string, number>;
  byCompletionStatus: Record<string, number>;
  topGenres: Array<{ name: string; count: number }>;
  recentlyPlayed: PlayniteGame[];
}

export function resolveToken(): string {
  const envToken = process.env.PLAYNITE_TOKEN;
  if (envToken) return envToken;

  try {
    const raw = readFileSync(DEFAULT_AUTH_PATH, "utf-8");
    const data = JSON.parse(raw) as { token?: string };
    if (data.token) return data.token;
  } catch {
    // Fall through
  }

  throw new Error(
    "No Playnite token found. Set PLAYNITE_TOKEN env var or ensure auth.json exists at " +
      DEFAULT_AUTH_PATH
  );
}

export class PlayniteClient {
  readonly baseUrl: string;
  readonly token: string;

  constructor(
    baseUrl?: string,
    token?: string
  ) {
    this.baseUrl = baseUrl || process.env.PLAYNITE_URL || "http://localhost:19821";
    this.token = token || resolveToken();
  }

  async get<T = unknown>(path: string): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      headers: { Authorization: `Bearer ${this.token}` },
    });
    if (!res.ok) {
      const body = await res.text();
      throw new Error(`Playnite API ${res.status}: ${body}`);
    }
    return res.json() as Promise<T>;
  }

  async post<T = unknown>(path: string, body?: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${this.token}`,
        "Content-Type": "application/json",
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Playnite API ${res.status}: ${text}`);
    }
    return res.json() as Promise<T>;
  }

  async put<T = unknown>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: "PUT",
      headers: {
        Authorization: `Bearer ${this.token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Playnite API ${res.status}: ${text}`);
    }
    return res.json() as Promise<T>;
  }

  async delete(path: string): Promise<void> {
    const res = await fetch(`${this.baseUrl}${path}`, {
      method: "DELETE",
      headers: { Authorization: `Bearer ${this.token}` },
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`Playnite API ${res.status}: ${text}`);
    }
  }

  // Convenience methods

  async searchGames(params: Record<string, string | number | boolean> = {}): Promise<GamesResponse> {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) {
      if (v !== undefined && v !== null && v !== "") {
        qs.set(k, String(v));
      }
    }
    const query = qs.toString();
    return this.get<GamesResponse>(`/api/games${query ? "?" + query : ""}`);
  }

  async getGame(id: string): Promise<PlayniteGame> {
    return this.get<PlayniteGame>(`/api/games/${id}`);
  }

  async updateGame(id: string, fields: Record<string, unknown>): Promise<PlayniteGame> {
    return this.put<PlayniteGame>(`/api/games/${id}`, fields);
  }

  async getStats(): Promise<StatsResponse> {
    return this.get<StatsResponse>("/api/stats");
  }

  async getCollection(name: string): Promise<CollectionItem[]> {
    return this.get<CollectionItem[]>(`/api/${name}`);
  }

  async setCollection(
    gameId: string,
    field: string,
    values: string[],
    action: "set" | "add" = "set"
  ): Promise<unknown> {
    const method = action === "add" ? "post" : "put";
    const body = { [field]: values };
    if (method === "post") {
      return this.post(`/api/games/${gameId}/${field}`, body);
    }
    return this.put(`/api/games/${gameId}/${field}`, body);
  }
}
