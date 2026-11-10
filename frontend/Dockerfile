FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine
# The official nginx image renders *.template files at start-up with the container's env vars.
ENV NGINX_ENVSUBST_TEMPLATE_DIR=/etc/nginx/templates \
    NGINX_ENVSUBST_TEMPLATE_SUFFIX=.template \
    NGINX_ENVSUBST_OUTPUT_DIR=/usr/share/nginx/html \
    API_BASE_URL=http://localhost:8080
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY config.js.template /etc/nginx/templates/config.js.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
