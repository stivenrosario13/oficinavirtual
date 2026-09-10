import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Server-only packages that must not be bundled by Turbopack/webpack.
  serverExternalPackages: ["googleapis", "exceljs"],
};

export default nextConfig;
