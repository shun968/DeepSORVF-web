#!/usr/bin/env bash
#
# Downloads the pretrained weights from this repository's GitHub Release (weights-v1) into
# the paths the code loads them from, and verifies their SHA-256. The weights are not in the
# repository itself: public forks cannot upload new Git LFS objects, and committing ~80MB of
# binaries directly would weigh down every clone.
#
# Uses `gh`, which runs outside Claude Code's sandbox, so an agent can run this too.

set -euo pipefail

REPO="shun968/DeepSORVF-web"
TAG="weights-v1"
ROOT="$(git rev-parse --show-toplevel)"

fetch() {
  local name="$1" dest_dir="$2" sha256="$3"
  local dest="$ROOT/$dest_dir/$name"

  if [[ -f "$dest" ]] && echo "$sha256  $dest" | sha256sum --check --status; then
    echo "up to date: $dest_dir/$name"
    return
  fi

  gh release download "$TAG" --repo "$REPO" --pattern "$name" --dir "$ROOT/$dest_dir" --clobber
  echo "$sha256  $dest" | sha256sum --check -
}

fetch ckpt.t7 deep_sort/deep_sort/deep/checkpoint \
  22628596f112dc7eb1fe7adfbfaf95bbc6ce8eb024205beafdc705232a646c29
fetch YOLOX-final.pth detection_yolox/model_data \
  848c659e876e570fa3427bebc84c5fdf7a86cc8e1a9d777cbbea603447860698
