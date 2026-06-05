import { Request, Response } from "express";

export function healthCheck(_req: Request, res: Response): void {
  res.status(200).json({
    status: "ok",
    service: "notifications",
    environment: process.env["NODE_ENV"] ?? "development",
    timestamp: new Date().toISOString(),
  });
}
