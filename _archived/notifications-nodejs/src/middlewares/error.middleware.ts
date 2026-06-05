import { Request, Response, NextFunction } from "express";
import { logger } from "../shared/logger";

export interface AppError extends Error {
  statusCode?: number;
  code?: string;
}

export function errorMiddleware(
  err: AppError,
  _req: Request,
  res: Response,
  _next: NextFunction
): void {
  const statusCode = err.statusCode ?? 500;
  const code = err.code ?? "INTERNAL_SERVER_ERROR";

  logger.error("Unhandled error", { code, message: err.message, stack: err.stack });

  res.status(statusCode).json({
    error: {
      code,
      message: err.message || "An unexpected error occurred",
    },
  });
}

export function notFoundMiddleware(req: Request, res: Response): void {
  res.status(404).json({
    error: {
      code: "NOT_FOUND",
      message: `Route not found: ${req.method} ${req.path}`,
    },
  });
}
