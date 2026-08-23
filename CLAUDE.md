# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DeepSORVF ("Deep Learning-based Simple Online and Real-time Vessel data Fusion") is the reference
implementation for the TITS 2023 paper *Asynchronous Trajectory Matching-Based Multimodal Maritime
Data Fusion for Vessel Traffic Surveillance in Inland Waterways*. It fuses AIS (ship transponder)
trajectories with video-based vessel detection/tracking to produce combined vessel tracks with an
anti-occlusion tracking method. This is a research codebase (no test suite, no linter, no CI) — most
comments and identifiers in `utils/` and `AIS_utils.py`/`FUS_utils.py` are in Chinese.

## Environment / Running

* Python 3.7, PyTorch 1.13.1 (or 1.9.1 per README badge), CUDA 11.7
* Key deps: `easydict`, `geopy`, `pyproj`, `fastdtw`, `pandas`, `numpy` — there is no consolidated
  top-level `requirements.txt`; only `detection_yolox/requirements.txt` exists (and targets an older
  torch/opencv pin than what the top-level README documents — prefer the README's versions).
* Before running, download and place two checkpoints (not included in the repo):
  * `ckpt.t7` → `deep_sort/deep_sort/deep/checkpoint/`
  * `YOLOX-final.pth` → `detection_yolox/model_data/`
* Run with: `python main.py --data_path ./clip-01/ --result_path ./result/`
* `--data_path` must point to a directory containing exactly one `*.mp4`/`*.avi` video, an `ais/`
  subfolder of AIS text files, and one `*.txt` camera-parameter file (see `utils/file_read.py:read_all`
  for the exact parsing/naming assumptions — e.g. the video filename must encode the start timestamp
  as `..._YYYY_MM_DD_HH_MM_SS...`).
* Outputs land under `result_path/video/<clip>.<ext>` (annotated video) and
  `result_path/metric/<clip>_{detection,tracking,fusion}.txt` (per-frame metrics, MOT-style rows).
* To visualize AIS trajectory / detection box / fusion result overlays simultaneously, swap the
  `import draw` line in `main.py` for `import draw_org` (uses `utils/draw_org.py` instead of
  `utils/draw.py`).
* There are no automated tests, lint, or build steps in this repo.

## Architecture

`main.py` runs a per-frame pipeline over a video, orchestrating four stateful processor objects that
are each instantiated once and then called every frame:

1. **`AISPRO`** (`utils/AIS_utils.py`) — reads/interpolates AIS records for the current timestamp,
   and projects lon/lat AIS positions into image pixel coordinates via `visual_transform` (needs the
   per-clip camera parameters loaded by `file_read.read_all`: camera lon/lat, heading, tilt, height,
   FOV, etc.).
2. **`VISPRO`** (`utils/VIS_utils.py`) — the video-based detection + tracking half. It owns a module-
   level `YOLO()` detector (`detection_yolox/yolo.py`, YOLOX architecture in `detection_yolox/nets/`)
   and a `DeepSort` tracker (`deep_sort/deep_sort/deep_sort.py`, configured from
   `deep_sort/configs/deep_sort.yaml`). `feedCap()` detects vessels, updates tracks, and implements
   the paper's anti-occlusion logic (`arg.anti`/`arg.anti_rate`) using AIS-informed motion priors to
   keep IDs stable through occlusion — this is the core novel contribution over vanilla DeepSORT.
3. **`FUSPRO`** (`utils/FUS_utils.py`) — the asynchronous trajectory matching step: aligns AIS tracks
   (`AIS_vis`/`AIS_cur`) with visual tracks (`Vis_tra`/`Vis_cur`) using DTW-based trajectory similarity
   (`fastdtw`/`DTW_fast`, trajectory angle/speed features) plus a max-distance gate derived from image
   size, to bind each visual track ID to an AIS MMSI (`bin_inf`).
4. **`DRAW`** (`utils/draw.py`, or `utils/draw_org.py`) — renders AIS trajectory, detection boxes, and
   fused vessel labels onto each output frame.

Per frame, data flows: `AIS.process()` → `VIS.feedCap()` (consuming AIS output as a prior) →
`FUS.fusion()` (binding vision output to AIS) → `gen_result()` (`utils/gen_result.py`, appends CSV-style
metric rows) → `DRA.draw_traj()` → written to `videoWriter`/shown via `cv2.imshow`.

`detection_yolox/` and `deep_sort/` are vendored, largely self-contained sub-libraries (each has its
own README/LICENSE) adapted from the external YOLOX and DeepSORT reference implementations noted in
the root README's "Reference" section; treat them as third-party code and prefer minimal, targeted
changes there over refactors.

## Dev container / sandboxed development (issue #7)

* `.devcontainer/` builds a container with Claude Code itself installed inside it, via the official
  [`ghcr.io/anthropics/devcontainer-features/claude-code`](https://github.com/anthropics/devcontainer-features/tree/main/src/claude-code)
  feature (plus `ghcr.io/devcontainers/features/node:1`, since the CLI needs Node.js and the base image
  is `python:3.14-slim`, which has none).
* **Design constraint, verified 2026-08-23**: for Claude Code's access-control guarantees (filesystem,
  network, tool restrictions) to hold, the `claude` **process itself** must run inside the container's
  namespaces — not just individual Bash commands forwarded there. Launching `claude` from the
  container's own terminal (VS Code's integrated terminal when using "Reopen in Container", or
  `docker exec -it <container> claude`) satisfies this, because the process tree lives inside the
  container regardless of where the keystrokes originate. Running `claude` on the host and only
  routing selected Bash calls into the container does **not** satisfy it: Read/Edit/Write, MCP
  servers, and hooks would still execute unrestricted on the host. See
  [Sandbox environments](https://code.claude.com/docs/en/sandbox-environments) and
  [Dev container](https://code.claude.com/docs/en/devcontainer) in the official docs.
* `~/.claude` (auth/config) is mounted as a named volume (`claude-code-config-${devcontainerId}`) so
  login persists across container rebuilds. This directory must be owned by the `vscode` user for
  `claude` to write to it; the Dockerfile pre-creates `/home/vscode/.claude` with that ownership
  *before* the volume is declared, since Docker only copies a mount target's existing
  ownership/permissions into a brand-new named volume the first time it's mounted — a plain
  `useradd` without this step leaves the volume root-owned and unwritable by `vscode` (verified by
  testing both ways).
* Still open (issue #7 scope not yet implemented here): a network egress allowlist (the Claude Code
  feature installs an optional `init-firewall.sh`, disabled by default — enabling it needs
  `runArgs: ["--cap-add=NET_ADMIN", "--cap-add=NET_RAW"]` plus running it from `postCreateCommand`),
  and layering Claude Code's own built-in bubblewrap-based Bash sandbox (`/sandbox`,
  [docs](https://code.claude.com/docs/en/sandboxing)) inside the container for defense-in-depth
  command-level restriction (this is the same bubblewrap the issue originally called for — Claude
  Code ships it natively, so it doesn't need to be built from scratch). Secrets/credential handling
  (`sandbox.credentials`) is likewise unconfigured so far.

## Git conventions

* Branch names must follow `<type>/<issue-number>-<slug>`, e.g. `feature/7-sandbox-dev-environment`,
  `fix/12-network-allowlist-bug`, `docs/15-sandbox-design-doc`. `<type>` is one of `feature`, `fix`,
  `docs`, `chore`, `refactor`, `test`. `<slug>` is a short kebab-case description.
* This is enforced locally via [lefthook](https://github.com/evilmartians/lefthook)'s `pre-commit`
  hook (`lefthook.yml` → `scripts/check-branch-name.sh`), which rejects commits on a branch whose name
  doesn't match `^(feature|fix|docs|chore|refactor|test)/[0-9]+-[a-z0-9-]+$` (default branches `main`/
  `master` are exempt).
* **`git clone` does NOT register the hook** — git never executes repo-tracked code automatically on
  clone/checkout, and `.git/hooks/` itself is not version-controlled, only `lefthook.yml` is. A
  one-time `lefthook install` is required per checkout to activate it, and this repo provides two
  ways to do that:
  * **Dev container (recommended)** — open the repo in the `.devcontainer/` container (VS Code
    "Reopen in Container", or `devcontainer up`). The image (`.devcontainer/Dockerfile`) installs
    `lefthook` inside the container only — nothing is installed into the host's Python/pip — and
    `postCreateCommand` (`.devcontainer/devcontainer.json`) runs `lefthook install` automatically the
    first time the container is created, so the hook is active with no manual step, as long as you
    run `git commit` from inside the container (its terminal has `lefthook` on `PATH`).
  * **Bare host checkout (fallback)** — if not using the dev container, install and register it
    manually; note this does install `lefthook` into your host Python environment:
    ```
    pip install lefthook   # or: uv tool install lefthook / uvx lefthook install
    lefthook install
    ```
  **Caveat (verified 2026-08-23):** the `.git/hooks/pre-commit` shim `lefthook install` writes is
  inside the bind-mounted workspace, so it is physically present on the host filesystem either way —
  but the shim only *runs the check* if it can find a `lefthook` binary on `PATH` (or a few other
  fallback locations such as `node_modules`). If you commit from a bare host shell that never had
  `lefthook` installed on it — e.g. the hook was only ever registered from inside the dev
  container — the shim prints `Can't find lefthook in PATH` and **exits 0, silently letting the
  commit through unchecked**; it does not fail closed. So enforcement is only real when `git commit`
  runs somewhere `lefthook` is actually installed (inside the dev container, or on the host after the
  fallback path above) — don't rely on it if you commit from an environment that doesn't have either.
  GitHub's own `branch_name_pattern` repository ruleset was tried first but is **not available on
  GitHub Free** for personal accounts (confirmed by API testing on 2026-08-23: the rule is rejected
  with an empty validation error, and `enforcement: evaluate` is explicitly Enterprise-only) — hence
  the lefthook-based fallback. There is no server-side enforcement script in this repo since it
  couldn't be made to work on the current plan; revisit this if the repository ever moves to GitHub
  Team/Enterprise.
* Prefer `gh issue develop <number> --name <type>/<number>-<slug> --checkout` to create branches, since
  it also links the branch to the issue in GitHub's UI.
* **Commit messages use [Conventional Commits](https://www.conventionalcommits.org/), title only** —
  e.g. `feat(devcontainer): Claude Code CLIを追加する (#7)`, `fix(sandbox): ...`. No body, no footer:
  `commitlint.config.js` (`body-empty`/`footer-empty: always`) rejects both, and `references-empty:
  never` requires an issue reference (the `(#N)` suffix) in the title itself. This means commits in
  this repo do **not** get a trailing `Co-Authored-By`/`Claude-Session` footer — that convention is
  overridden here. Enforced via lefthook's `commit-msg` hook (`npx commitlint --edit`), which needs
  `npm install` run once (installs `@commitlint/cli` and `lefthook` from `package.json`; the dev
  container's Node.js feature makes `npx` available for this). Convention and enforcement setup
  ported from [shun968/marketing-data-pipeline](https://github.com/shun968/marketing-data-pipeline).
* **This repo follows [GitHub Flow](https://docs.github.com/en/get-started/using-github/github-flow)**:
  `main` is always deployable, all changes land via a PR from a feature branch, and the feature branch
  is deleted after merge. `scripts/setup-github-flow-branch-protection.sh` provisions this on the
  GitHub side (idempotent — re-run after changing it): sets `delete_branch_on_merge: true`, and creates
  a `github-flow-main-protection` ruleset on `main` with `pull_request` (PR required, 0 mandatory
  approvals — solo-dev friendly, just forces the PR path), `non_fast_forward` (no force-push),
  `deletion` (can't delete `main`), and `required_linear_history`. **Direct `git push` to `main`,
  including by the repo owner, is rejected after this runs** — verified 2026-08-23 via
  `gh api repos/<repo>/rules/branches/main`, which is the reliable check: `git push --dry-run` does
  **not** trigger GitHub's server-side ruleset evaluation, so a passing dry-run proves nothing here.
* **Squash-merging a PR needs an explicit `--subject`/`--body ""`.** GitHub's merge/squash operation
  runs server-side and never invokes lefthook/commitlint, so a plain `gh pr merge --squash` on a
  multi-commit PR defaults to `<PR title> (#<PR number>)` as the subject and a bullet list of the
  squashed commits as the body — breaking both the scoped-type format and the title-only rule. This
  actually happened once (PR #8 → `a8686dd` on `main` has a doubled `(#7) (#8)` reference and a
  non-empty body; left as-is rather than rewriting protected `main` history for a cosmetic fix).
  Always merge with:
  ```
  gh pr merge <number> --squash --delete-branch \
    --subject "type(scope): 説明 (#issue番号)" \
    --body ""
  ```

## Architecture decision records

Technical decisions (e.g. why the dev container is built the way it is, why GitHub's branch-naming
ruleset couldn't be used) belong in `docs/adr/` as Nygard-format ADRs — see the `adr` skill
(`.claude/skills/adr/SKILL.md`, also ported from
[shun968/marketing-data-pipeline](https://github.com/shun968/marketing-data-pipeline)) for the
template, status vocabulary, and update rules. `scripts/check-adr-format.sh` enforces the format via
lefthook's `pre-commit` hook whenever a file under `docs/adr/` is staged.

## Localization

`README.md` is written in Japanese and is the sole, canonical README for this repository (it was
promoted from a former `README_ja.md`). There is no English or Chinese README anymore — the former
`README_zh-CN.md` and the English-language variant have both been removed, and their language
badges/links have been dropped from `README.md` accordingly. Don't reintroduce `README_en.md` /
`README_zh-CN.md` or their badges without the user asking for it.
