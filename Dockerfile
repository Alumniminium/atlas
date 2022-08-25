# herstfortress/atlas:latest

FROM mcr.microsoft.com/dotnet/nightly/sdk:6.0-alpine as build
VOLUME [ "/srv", "/etc/atlas" ]
WORKDIR /app
COPY . .
RUN dotnet tool install -g dotnet-script --version 1.3.1
COPY mimetypes.tsv /app/
RUN dotnet restore
COPY gencert.sh /app/
RUN dotnet publish -o /app --configuration Release
RUN mkdir -p /etc/atlas/
COPY config.json /etc/atlas
ENV PATH="$PATH:/root/.dotnet/tools/"

ENTRYPOINT [ "dotnet", "/app/atlas.dll" ]