export interface PriceTableRow {
  label: string
  brand: string | null
  region?: string | null
  buyToday?: number | null
  sellToday?: number | null
  buyYesterday?: number | null
  sellYesterday?: number | null
  buyChange?: number | null
  sellChange?: number | null
}

export interface ChartData {
  date: string
  buy: number
  sell: number
}

export interface HistorySeries {
  brand: string
  region?: string | null
  history: ChartData[]
}

import { createFetchOptions, getTraceId } from "./logger";

const API_BASE_URL =
  process.env.PRICE_API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:8080"

type LatestPriceItem = {
  productId: string
  brand: string
  form: string
  karat: number | null
  region: string | null
  source: string
  priceBuy: number
  priceSell: number
  currency: string
  effectiveAt: string
  collectedAt: string
}

type LatestResponse = {
  items: LatestPriceItem[]
}

type HistoryPoint = {
  date: string
  priceBuyClose: number
  priceSellClose: number
}

type HistoryResponse = {
  brand: string
  region: string | null
  points: HistoryPoint[]
}

const BRAND_CONFIGS: Array<{
  label: string
  matchers: Array<(item: LatestPriceItem) => boolean>
}> = [
  {
    label: "SJC",
    matchers: [(item) => item.brand.trim().toUpperCase().includes("SJC")],
  },
  {
    label: "DOJI HN",
    matchers: [
      (item) =>
        item.brand.trim().toUpperCase().includes("DOJI") &&
        (item.region?.trim().toUpperCase() ?? "").includes("HANOI"),
    ],
  },
  {
    label: "BTMC SJC",
    matchers: [(item) => item.brand.trim().toUpperCase().includes("BTMC")],
  },
  {
    label: "PHÚC THÀNH",
    matchers: [
      (item) =>
        item.brand.trim().toUpperCase().includes("PHUC") ||
        item.brand.trim().toUpperCase().includes("PHÚC"),
    ],
  },
]

const PRICE_DIVISOR = 100

const convertPrice = (value?: number | null): number | null => {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return null
  }
  return Math.round(Number(value) / PRICE_DIVISOR)
}

const fetchJson = async <T>(url: string, traceId?: string): Promise<T> => {
  const headers: HeadersInit = { "cache": "no-store" };
  if (traceId) {
    headers["x-trace-id"] = traceId;
  }
  const response = await fetch(url, { cache: "no-store", headers });
  if (!response.ok) {
    throw new Error(`Failed to fetch ${url}: ${response.status}`);
  }
  return response.json() as Promise<T>;
};

const fetchLatestItems = async (traceId?: string): Promise<LatestPriceItem[]> => {
  const url = new URL("/api/prices/latest", API_BASE_URL);
  url.searchParams.set("kind", "ring");
  const data = await fetchJson<LatestResponse>(url.toString(), traceId);
  return data.items ?? [];
};

const fetchHistoryPoints = async (
  brand: string,
  region: string | null,
  days: number,
  traceId?: string,
): Promise<HistoryPoint[]> => {
  const url = new URL("/api/prices/history", API_BASE_URL);
  url.searchParams.set("kind", "ring");
  url.searchParams.set("brand", brand);
  if (region) {
    url.searchParams.set("region", region);
  }
  url.searchParams.set("days", String(days));

  const headers: HeadersInit = { "cache": "no-store" };
  if (traceId) {
    headers["x-trace-id"] = traceId;
  }

  const response = await fetch(url.toString(), { cache: "no-store", headers });
  if (!response.ok) {
    return [];
  }

  const payload = (await response.json()) as Partial<HistoryResponse>;
  const points = payload.points ?? [];
  return points.sort((a, b) => (a.date > b.date ? 1 : -1));
}

const buildRowFromItem = async (
  label: string,
  item: LatestPriceItem | undefined,
  traceId?: string,
): Promise<PriceTableRow> => {
  if (!item) {
    return {
      label,
      brand: null,
      region: null,
    };
  }

  const buyToday = convertPrice(item.priceBuy);
  const sellToday = convertPrice(item.priceSell);

  const historyPoints = await fetchHistoryPoints(item.brand, item.region, 2, traceId);
  const yesterdayPoint =
    historyPoints.length > 1
      ? historyPoints[historyPoints.length - 2]
      : historyPoints.at(0)

  const buyYesterday = convertPrice(yesterdayPoint?.priceBuyClose)
  const sellYesterday = convertPrice(yesterdayPoint?.priceSellClose)

  const buyChange =
    buyToday !== null && buyYesterday !== null ? buyToday - buyYesterday : null
  const sellChange =
    sellToday !== null && sellYesterday !== null ? sellToday - sellYesterday : null

  return {
    label,
    brand: item.brand,
    region: item.region,
    buyToday,
    sellToday,
    buyYesterday,
    sellYesterday,
    buyChange,
    sellChange,
  }
}

export const getLatestTableRows = async (traceId?: string): Promise<PriceTableRow[]> => {
  const latestItems = await fetchLatestItems(traceId);
  const unused = [...latestItems];

  const rows: PriceTableRow[] = [];
  for (const config of BRAND_CONFIGS) {
    const index = unused.findIndex((item) =>
      config.matchers.some((matcher) => matcher(item)),
    );
    const matchedItem = index >= 0 ? unused.splice(index, 1)[0] : undefined;
    rows.push(await buildRowFromItem(config.label, matchedItem, traceId));
  }

  return rows;
};

export const getHistorySeries = async (options: {
  brand: string;
  region?: string | null;
  days?: number;
  traceId?: string;
}): Promise<HistorySeries> => {
  const points = await fetchHistoryPoints(
    options.brand,
    options.region ?? null,
    options.days ?? 30,
    options.traceId,
  );
  const history = points
    .map((point) => ({
      date: point.date,
      buy: convertPrice(point.priceBuyClose) ?? 0,
      sell: convertPrice(point.priceSellClose) ?? 0,
    }))
    .filter((point) => point.buy > 0 && point.sell > 0)

  return {
    brand: options.brand,
    region: options.region ?? null,
    history,
  }
}
