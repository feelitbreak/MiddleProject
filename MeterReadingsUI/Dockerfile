FROM node:20-alpine AS build
WORKDIR /app

# Dependencies first, so a source-only change does not reinstall them.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
COPY nginx.default.conf.template /etc/nginx/templates/default.conf.template

# The entrypoint renders templates here rather than into /etc/nginx/conf.d, which the nginx user
# cannot write; /tmp is where this image already keeps everything else it writes.
ENV NGINX_ENVSUBST_OUTPUT_DIR=/tmp/nginx-conf

# Port 8080 matches the other services, and being above 1024 it binds without root.
RUN mkdir -p /tmp/nginx-conf \
    && touch /tmp/nginx.pid \
    && chown -R nginx:nginx /tmp/nginx-conf /tmp/nginx.pid /usr/share/nginx/html /var/cache/nginx
USER nginx
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --spider -q http://127.0.0.1:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
