# herstfortress/atlas:latest

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine as build
WORKDIR /app
COPY . .
RUN dotnet tool install -g dotnet-script
RUN dotnet restore
RUN dotnet publish -o /app/published-app --configuration Release


FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine as runtime
WORKDIR /app
VOLUME [ "/srv", "/etc/atlas" ]

COPY --from=build /app/published-app /app
COPY mimetypes.tsv /app/
COPY gencert.sh /app/
COPY config.json /etc/atlas/
COPY --from=build /root/.dotnet/tools/ /usr/bin/
ENV PATH="/opt/bin:${PATH}"

ENTRYPOINT [ "dotnet", "/app/atlas.dll" ]