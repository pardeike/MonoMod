FROM ubuntu:24.04

# First, packages
RUN apt-get update && apt-get upgrade -y \
    # for add-apt-repository
    && apt-get install --no-install-recommends -y software-properties-common \
    && add-apt-repository ppa:dotnet/backports \
    && apt-get install --no-install-recommends -y \
            dotnet-runtime-9.0 mono-runtime git curl wget bash \
    && curl -fsSL https://deb.nodesource.com/setup_23.x | bash \
    # pretty sure we need nodejs for stock github actions
    && apt-get install --no-install-recommends -y \
            nodejs \
    && apt-get remove -y software-properties-common \
    && apt-get autoremove -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Then, user
RUN useradd -rm -d /home/runner -s /bin/bash -g root -G sudo -u 1001 runner
USER runner
WORKDIR /home/runner
