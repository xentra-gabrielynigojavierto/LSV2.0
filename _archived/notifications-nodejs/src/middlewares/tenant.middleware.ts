import { Request, Response, NextFunction } from "express";

const TENANT_HEADER = "x-tenant-id";

export function tenantMiddleware(req: Request, res: Response, next: NextFunction): void {
  if (req.path === "/v1/health" || req.path.startsWith("/v1/health/")) {
    return next();
  }

  const raw = req.headers[TENANT_HEADER];
  const tenantId = Array.isArray(raw) ? raw[0] : raw;

  req.tenantId = tenantId && tenantId.trim() !== "" ? tenantId.trim() : undefined;
  next();
}
