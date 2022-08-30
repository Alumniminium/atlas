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
COPY gencert.sh /app/
COPY config.json /etc/atlas/
COPY capsules/ec.her.st /srv/gemini/ec.her.st

ENTRYPOINT [ "dotnet", "/app/atlas.dll" ]