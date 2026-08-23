FROM python:3.14-slim

RUN apt-get update && apt-get install -y --no-install-recommends \
        git \
        curl \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --no-cache-dir lefthook

RUN useradd --create-home --shell /bin/bash vscode \
    && mkdir -p /home/vscode/.claude \
    && chown vscode:vscode /home/vscode/.claude
USER vscode
WORKDIR /workspace
