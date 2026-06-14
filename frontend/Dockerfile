# ── Build stage ──
FROM node:24-alpine AS build
WORKDIR /app

# VITE_* vars are baked at build time; pass via build args from docker compose.
ARG VITE_API_URL=http://localhost:5000
ENV VITE_API_URL=${VITE_API_URL}
ARG VITE_SUPABASE_URL=http://localhost:9999
ENV VITE_SUPABASE_URL=${VITE_SUPABASE_URL}
ARG VITE_SUPABASE_ANON_KEY=local-anon-key
ENV VITE_SUPABASE_ANON_KEY=${VITE_SUPABASE_ANON_KEY}

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

# ── Runtime stage (static files served by nginx) ──
FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
