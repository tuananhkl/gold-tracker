/**
 * Structured JSON logger for Next.js
 * Emits logs to stdout in JSON format for Fluent Bit collection
 */

interface LogContext {
  traceId?: string;
  route?: string;
  status?: number;
  duration_ms?: number;
  user_agent?: string;
  client_ip?: string;
  userId?: string;
  [key: string]: unknown;
}

class Logger {
  private app: string;
  private env: string;
  private serviceVersion: string;

  constructor() {
    this.app = process.env.APP_NAME || "gold-tracker-ui";
    this.env = process.env.NODE_ENV || "production";
    this.serviceVersion = process.env.SERVICE_VERSION || process.env.BUILD_VERSION || "unknown";
  }

  private formatLog(level: string, message: string, context?: LogContext, error?: Error): string {
    const log: Record<string, unknown> = {
      ts: new Date().toISOString(),
      level,
      message,
      app: this.app,
      env: this.env,
      service_version: this.serviceVersion,
    };

    // Add context fields
    if (context) {
      Object.assign(log, context);
    }

    // Add error information if present
    if (error) {
      log.error = {
        name: error.name,
        message: error.message,
        stack: error.stack,
      };
    }

    return JSON.stringify(log);
  }

  info(message: string, context?: LogContext): void {
    console.log(this.formatLog("info", message, context));
  }

  warn(message: string, context?: LogContext): void {
    console.warn(this.formatLog("warn", message, context));
  }

  error(message: string, error?: Error, context?: LogContext): void {
    console.error(this.formatLog("error", message, context, error));
  }

  debug(message: string, context?: LogContext): void {
    if (this.env === "development") {
      console.debug(this.formatLog("debug", message, context));
    }
  }
}

export const logger = new Logger();

/**
 * Middleware to extract trace ID from request headers
 */
export function getTraceId(request: Request): string {
  return (
    request.headers.get("x-trace-id") ||
    request.headers.get("x-correlation-id") ||
    crypto.randomUUID()
  );
}

/**
 * Helper to forward trace ID in fetch requests
 */
export function createFetchOptions(traceId: string, init?: RequestInit): RequestInit {
  return {
    ...init,
    headers: {
      ...init?.headers,
      "x-trace-id": traceId,
    },
  };
}

