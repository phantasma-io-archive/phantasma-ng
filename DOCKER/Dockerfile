FROM debian:stable-slim
WORKDIR /app

# Copy everything
COPY DOCKER/wrapper.sh ./
COPY DOCKER/testnet ./testnet
COPY DOCKER/bin ./testnet

RUN apt-get update; apt-get install -y libc6-dev \ 
    libsnappy-dev libicu-dev screen bash vim net-tools ca-certificates openssl libssl-dev

EXPOSE 5101 5102 5103 5104

ENTRYPOINT ["/app/wrapper.sh"]

