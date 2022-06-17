FROM debian:stable-slim
WORKDIR /app

# Copy everything
COPY DOCKER/wrapper.sh ./
COPY DOCKER/testnet ./testnet
COPY DOCKER/bin ./testnet
COPY DOCKER/https ./https

RUN apt-get update; apt-get install -y libc6-dev \ 
    libsnappy-dev libicu-dev screen bash vim net-tools ca-certificates openssl openssl-devel


#ENV ASPNETCORE_URLS="https://+;http://+"
#ENV ASPNETCORE_HTTPS_PORT=8001
#ENV ASPNETCORE_Kestrel__Certificates__Default__Password="example"
#ENV ASPNETCORE_Kestrel__Certificates__Default__Path="./https/phantasma.pfx"

EXPOSE 5101 5102 5103 5104

ENTRYPOINT ["/app/wrapper.sh"]

