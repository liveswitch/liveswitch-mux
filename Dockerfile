FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build
WORKDIR /app

COPY FM.LiveSwitch.Mux.sln FM.LiveSwitch.Mux.sln
COPY src src
COPY .git .git
RUN dotnet restore
RUN dotnet publish src/FM.LiveSwitch.Mux/FM.LiveSwitch.Mux.csproj -c Release -o lib
RUN rm -rf src
RUN rm -rf .git
RUN rm FM.LiveSwitch.Mux.sln

FROM mcr.microsoft.com/dotnet/runtime:3.1
WORKDIR /app
COPY --from=build /app/lib .

RUN apt-get -y update && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*
RUN apt-get install -y --no-install-recommends ffmpeg=7:4.*

ENTRYPOINT ["dotnet", "lsmux.dll"]