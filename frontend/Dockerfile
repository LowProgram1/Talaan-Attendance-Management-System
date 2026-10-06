FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . ./
ARG NEXT_PUBLIC_API_URL=/api
ARG API_PROXY_TARGET=http://backend:8080
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV API_PROXY_TARGET=$API_PROXY_TARGET
RUN npm run build
FROM node:24-alpine
WORKDIR /app
ENV NODE_ENV=production
ENV API_PROXY_TARGET=http://backend:8080
COPY --from=build /app ./
EXPOSE 3000
CMD ["npm", "run", "start"]
