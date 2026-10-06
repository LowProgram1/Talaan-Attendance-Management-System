import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    return [{ source: '/api/:path*', destination: `${process.env.API_PROXY_TARGET || 'http://localhost:5080'}/api/:path*` }];
  },
};

export default nextConfig;
