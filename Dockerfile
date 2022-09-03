# herstfortress/atlas:latest

FROM mcr.microsoft.com/dotnet/nightly/sdk:7.0-alpine as build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -o /app/published-app --configuration Release


FROM mcr.microsoft.com/dotnet/nightly/runtime:7.0-alpine as runtime
WORKDIR /app
VOLUME ["/etc/atlas"]

COPY --from=build /app/published-app /app
COPY mimetypes.tsv /app/

# pick your poison
#COPY config.json /app/
COPY config.json /etc/atlas/

# only required if you use netcore cgi apps
RUN apk add icu-libs 

ENTRYPOINT [ "dotnet", "/app/atlas.dll" ]