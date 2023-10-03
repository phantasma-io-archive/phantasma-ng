#--platform=linux/x86_64
#FROM debian:stable-slim
FROM  mcr.microsoft.com/dotnet/aspnet:6.0 as runtime
WORKDIR /app

# Copy everything
COPY DOCKER/wrapper.sh /app/wrapper.sh
COPY DOCKER/testnet /app/testnet
COPY DOCKER/bin /app/bin

RUN apt-get update && apt-get install -y curl inotify-tools libgflags-dev libsnappy-dev zlib1g-dev libbz2-dev libzstd-dev libc6-dev libicu-dev libgflags-dev libsnappy-dev zlib1g-dev libbz2-dev libzstd-dev librocksdb-dev libc6-dev libsnappy-dev libicu-dev screen bash vim net-tools ca-certificates openssl libssl-dev nano

#RUN curl -sSL https://dot.net/v1/dotnet-install.sh | /bin/bash /dev/stdin -Channel 6.0 -Runtime dotnet -InstallDir /usr/share/dotnet \
#    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

EXPOSE 7077 7078 7079 7080 26056 26156 26256 26356 26057 26157 26257 26357

RUN chmod +x /app/wrapper.sh
ENTRYPOINT ["/bin/bash", "-c", "/app/wrapper.sh"]
