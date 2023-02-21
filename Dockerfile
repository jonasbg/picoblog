FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS backend

ARG TARGETPLATFORM
ARG BUILDPLATFORM

WORKDIR /build
COPY . .

RUN echo "Build:" $BUILDPLATFORM
RUN echo "Target:" $TARGETPLATFORM

RUN if [ "$TARGETPLATFORM" = "linux/arm64" ] ; then DOTNET_TARGET=linux-musl-arm64 ; else DOTNET_TARGET=linux-musl-x64 ; fi \
    && echo $DOTNET_TARGET > /tmp/rid

RUN cat /tmp/rid
RUN dotnet restore "picoblog.csproj" -r $(cat /tmp/rid) /p:PublishReadyToRun=true
RUN dotnet publish "picoblog.csproj"  -c Release -o /publish --runtime $(cat /tmp/rid) --self-contained true --no-restore /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishSingleFile=true

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
EXPOSE 8080

RUN apk add --no-cache icu-libs icu-data-full tzdata
RUN adduser \
  --disabled-password \
  --home /app \
  --gecos '' app
USER app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV TZ=Europe/Oslo
ENV DOTNET_EnableDiagnostics=0
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENV DATA_DIR=/data
ENV CONFIG_DIR=/config
ENV DOMAIN=localhost
ENV SYNOLOGY_SIZE=XL
ENV PASSWORD=""
ENV SYNOLOGY_SUPPORT=false

WORKDIR /app

COPY --from=backend --chown=app /publish .

ENTRYPOINT ["/app/picoblog"]
