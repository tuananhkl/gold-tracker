import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { logger, getTraceId } from "./lib/logger";

export function middleware(request: NextRequest) {
  const startTime = Date.now();
  const traceId = getTraceId(request);
  
  // Add trace ID to response headers
  const response = NextResponse.next();
  response.headers.set("x-trace-id", traceId);
  response.headers.set("x-correlation-id", traceId);

  // Log request
  logger.info("HTTP request", {
    traceId,
    route: request.nextUrl.pathname,
    method: request.method,
    user_agent: request.headers.get("user-agent") || undefined,
    client_ip: request.ip || request.headers.get("x-forwarded-for") || undefined,
  });

  // Log response after completion
  response.headers.set("x-request-start-time", startTime.toString());

  return response;
}

export const config = {
  matcher: [
    "/api/:path*",
    "/((?!_next/static|_next/image|favicon.ico).*)",
  ],
};

