import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  allowedDevOrigins: [
    "5002c354-11ad-4d26-b533-6431067bd5f3-00-kjl6g6t80v42.riker.replit.dev",
    "localhost",
  ],
  serverExternalPackages: [],
  async rewrites() {
    const backendUrl = process.env.FLOW_BACKEND_URL ?? "http://localhost:5000";
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
      {
        source: "/healthz",
        destination: `${backendUrl}/healthz`,
      },
    ];
  },
};

export default nextConfig;
