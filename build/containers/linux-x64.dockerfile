FROM ubuntu:24.04

# First, packages
RUN apt-get update \
 && apt-get upgrade -y \
 && apt-get install --no-install-recommends -y \
        apt-transport-https software-properties-common \
        git git-lfs curl wget bash \
        mono-runtime \
 && add-apt-repository ppa:dotnet/backports \
 && wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
 && dpkg -i packages-microsoft-prod.deb \
 && rm packages-microsoft-prod.deb \
 && curl -fsSL https://deb.nodesource.com/setup_23.x | bash \
 && apt-get update \
 && apt-get install --no-install-recommends -y \
        nodejs \
        dotnet-runtime-9.0 \
        powershell \
 && apt-get remove -y apt-transport-https software-properties-common \
 && apt-get autoremove -y \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

# Then, user
RUN useradd -rm -d /home/runner -s /bin/bash -g root -G sudo -u 1001 runner
USER runner
WORKDIR /home/runner
