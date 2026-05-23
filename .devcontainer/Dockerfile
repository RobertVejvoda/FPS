FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        netcat-openbsd \
        procps \
        unzip

RUN curl -fsSL https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -o /tmp/install-dapr.sh \
    && /bin/bash /tmp/install-dapr.sh \
    && rm -f /tmp/install-dapr.sh

USER vscode
