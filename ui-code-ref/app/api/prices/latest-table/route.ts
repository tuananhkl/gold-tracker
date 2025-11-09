import { getLatestTableRows } from "@/lib/api"
import { logger, getTraceId } from "@/lib/logger"

export async function GET(request: Request) {
  const startTime = Date.now();
  const traceId = getTraceId(request);
  
  try {
    logger.info("Fetching latest price table", { traceId, route: "/api/prices/latest-table" });
    
    const rows = await getLatestTableRows(traceId);
    const duration = Date.now() - startTime;
    
    logger.info("Latest price table fetched successfully", {
      traceId,
      route: "/api/prices/latest-table",
      status: 200,
      duration_ms: duration,
      itemCount: rows.length,
    });
    
    const response = Response.json(rows);
    response.headers.set("x-trace-id", traceId);
    return response;
  } catch (error) {
    const duration = Date.now() - startTime;
    const err = error instanceof Error ? error : new Error(String(error));
    
    logger.error("Failed to load latest price table", err, {
      traceId,
      route: "/api/prices/latest-table",
      status: 500,
      duration_ms: duration,
    });
    
    const response = Response.json({ error: "Failed to load price data" }, { status: 500 });
    response.headers.set("x-trace-id", traceId);
    return response;
  }
}
