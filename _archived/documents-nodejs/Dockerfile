###############################################################################
# Stage 1 — builder
###############################################################################
FROM node:20-alpine AS builder

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts

COPY tsconfig.json ./
COPY src ./src

RUN npm run build

###############################################################################
# Stage 2 — production runtime
###############################################################################
FROM node:20-alpine AS runtime

RUN addgroup -S docsvc && adduser -S docsvc -G docsvc

WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --omit=dev --ignore-scripts

COPY --from=builder /app/dist ./dist

# Drop root privileges
USER docsvc

EXPOSE 5005

ENV NODE_ENV=production

CMD ["node", "dist/server.js"]
