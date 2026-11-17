FROM node:24.7.0-alpine AS frontend-build
WORKDIR /src
COPY frontend/sso-session-ui/package*.json ./
RUN npm ci
COPY frontend/sso-session-ui/ ./
RUN npm run build

FROM nginx:alpine
COPY nginx/conf/nginx.docker.conf /etc/nginx/conf.d/default.conf
COPY --from=frontend-build /src/dist/sso-session-ui/browser /usr/share/nginx/html
EXPOSE 80