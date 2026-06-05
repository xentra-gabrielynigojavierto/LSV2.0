# ─────────────────────────────────────────────────────────────────────────────
# Control Center — multi-stage Dockerfile
#
# Stage 1  (deps)     Install production + dev dependencies with npm ci.
# Stage 2  (builder)  Run next build to produce the standalone output bundle.
# Stage 3  (runner)   Minimal runtime image — only the built artefact.
#
# Port:     5004
# Runtime:  Node.js 22 (LTS) on Alpine Linux
#
# Usage:
#   docker build -t control-center:latest .
#   docker run -p 5004:5004 \
#     -e CONTROL_CENTER_API_BASE=https://api.legalsynq.com \
#     -e NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=https://controlcenter.legalsynq.com \
#     control-center:latest
#
# Required env vars at runtime (see .env.example):
#   CONTROL_CENTER_API_BASE              — API gateway base URL
#   NEXT_PUBLIC_CONTROL_CENTER_ORIGIN    — public origin of this app
#
# The image is built with output: 'standalone' (see next.config.mjs TODO).
# TODO: add output: 'standalone' to next.config.mjs to enable standalone mode.
# TODO: add non-root user (addgroup / adduser) for CIS hardening
# TODO: add read-only filesystem mount for the runtime stage
# TODO: add HEALTHCHECK instruction pointing to /api/health
# TODO: sign image with cosign after push to registry
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Install dependencies ─────────────────────────────────────────────

FROM node:22-alpine AS deps

# Install OS-level build tools needed by native npm packages (e.g. node-gyp).
# libssl-dev, python3, and make are the most common requirements.
RUN apk add --no-cache libc6-compat openssl

WORKDIR /app

# Copy manifests first so Docker's layer cache is not invalidated on every
# source change — only when package.json or package-lock.json changes.
COPY package.json package-lock.json* ./

# Use npm ci for deterministic installs (exact lock file).
# --legacy-peer-deps matches the local dev setup (Tailwind v4 peer deps).
RUN npm ci --legacy-peer-deps

# ── Stage 2: Build the Next.js application ────────────────────────────────────

FROM node:22-alpine AS builder

WORKDIR /app

# Bring in installed node_modules from the deps stage.
COPY --from=deps /app/node_modules ./node_modules

# Copy the entire application source.
# .dockerignore excludes node_modules, .next/cache, .env*, and logs.
COPY . .

# Build-time env vars.
# NEXT_PUBLIC_* vars must be set AT BUILD TIME — they are inlined into
# the client-side JS bundle by Next.js. Pass them via --build-arg.
#
# Usage: docker build \
#   --build-arg NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=https://controlcenter.legalsynq.com \
#   -t control-center:latest .
ARG NEXT_PUBLIC_CONTROL_CENTER_ORIGIN
ARG NEXT_PUBLIC_BASE_PATH

ENV NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=${NEXT_PUBLIC_CONTROL_CENTER_ORIGIN}
ENV NEXT_PUBLIC_BASE_PATH=${NEXT_PUBLIC_BASE_PATH}

# Disable Next.js telemetry during build.
ENV NEXT_TELEMETRY_DISABLED=1

# Produce the production build.
# Output goes to .next/
RUN npm run build

# ── Stage 3: Minimal runtime image ────────────────────────────────────────────

FROM node:22-alpine AS runner

# Install only the minimal OS packages needed at runtime.
RUN apk add --no-cache openssl

WORKDIR /app

# Set production environment — Next.js and env.ts key off this.
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

# Create a non-root user so the process does not run as root inside the container.
# TODO: uncomment once confirmed compatible with the deployment platform.
# RUN addgroup --system --gid 1001 nodejs \
#  && adduser  --system --uid 1001 nextjs
# USER nextjs

# Copy only what is needed to run the application:
#   - The built .next/ output
#   - public/ static assets
#   - package.json (needed by next start)
#   - node_modules (needed by next start)
#
# NOTE: If output: 'standalone' is added to next.config.mjs the COPY list
# becomes much shorter (just .next/standalone/ and .next/static/). That is
# the recommended production pattern.
# TODO: switch to standalone output to reduce image size.
COPY --from=builder /app/public      ./public
COPY --from=builder /app/.next       ./.next
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/package.json ./package.json

# Runtime env vars — set at container start, not baked into the image.
# These are server-side only vars (no NEXT_PUBLIC_ prefix).
# Supply them via -e flags, an env file, or secrets manager injection.
ENV CONTROL_CENTER_API_BASE=""
ENV PORT=5004

EXPOSE 5004

# Start the production Next.js server on port 5004.
CMD ["npm", "run", "start"]
