/** @type {import('next').NextConfig} */
const nextConfig = {
  typescript: {
    ignoreBuildErrors: true,
  },
  images: {
    unoptimized: true,
  },
  // Don't use basePath - let Ingress handle path rewriting
  // basePath causes issues with health probes
}

export default nextConfig
