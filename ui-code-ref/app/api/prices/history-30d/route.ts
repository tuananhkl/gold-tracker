import { getHistorySeries } from "@/lib/api"
import { logger, getTraceId } from "@/lib/logger"

export async function GET(request: Request) {
  const startTime = Date.now();
  const traceId = getTraceId(request);
  const { searchParams } = new URL(request.url);
  const brand = searchParams.get("brand");
  const regionParam = searchParams.get("region");
  const daysParam = searchParams.get("days");

  if (!brand) {
    logger.warn("Missing brand parameter", { traceId, route: "/api/prices/history-30d", status: 400 });
    const response = Response.json({ error: "brand query parameter is required" }, { status: 400 });
    response.headers.set("x-trace-id", traceId);
    return response;
  }

  const days = daysParam ? Number.parseInt(daysParam, 10) : 30;

  try {
    logger.info("Fetching price history", { traceId, route: "/api/prices/history-30d", brand, region: regionParam, days });
    
    const history = await getHistorySeries({ brand, region: regionParam, days, traceId });
    const duration = Date.now() - startTime;
    
    logger.info("Price history fetched successfully", {
      traceId,
      route: "/api/prices/history-30d",
      status: 200,
      duration_ms: duration,
      brand,
      region: regionParam,
      historyPoints: history.history.length,
    });
    
    const response = Response.json(history);
    response.headers.set("x-trace-id", traceId);
    return response;
  } catch (error) {
    const duration = Date.now() - startTime;
    const err = error instanceof Error ? error : new Error(String(error));
    
    logger.error("Failed to load price history", err, {
      traceId,
      route: "/api/prices/history-30d",
      status: 500,
      duration_ms: duration,
      brand,
    });
    
    const response = Response.json({ error: "Failed to load price history" }, { status: 500 });
    response.headers.set("x-trace-id", traceId);
    return response;
  }
}
