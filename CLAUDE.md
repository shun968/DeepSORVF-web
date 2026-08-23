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

## Dev container

* Launch `claude` from inside `.devcontainer/` (VS Code "Reopen in Container", or
  `docker exec -it <container> claude`), not on the host — Claude Code's access-control guarantees
  only hold when its process runs inside the container's namespaces.
* `.claude/settings.json` (project-shared, committed) turns on Claude Code's built-in Bash sandbox
  (`sandbox.enabled`), restricts sandboxed commands to an explicit host allowlist
  (`sandbox.network.allowedDomains` — GitHub, npm, PyPI, NuGet; `strictAllowlist: true` denies anything
  else outright rather than prompting), and blocks `~/.ssh`, `~/.aws`, and any `.env*` file anywhere
  in the repo — `**/.env*` already covers a bare `.env` at the repo root, since `**` matches zero or
  more directories — from sandboxed commands (`sandbox.credentials`). `gh` is listed in
  `sandbox.excludedCommands` and runs unsandboxed, since sandboxing its credential file
  (`~/.config/gh/hosts.yml`) would break the `gh` CLI usage required by this file's Git conventions
  below — everything else runs inside the sandbox.
* `sandbox.credentials` deny rules can't be selectively re-opened (a `deny` only ever narrows access
  in every settings scope, with no counterpart `allow`), so blocking every `.env*` file blocks
  non-production ones too (`.env.test`, `.env.development`, …), not just `.env.production`. This repo
  has no `.env` files or test suite yet, so the broad block is deliberate; if a future test suite
  needs to read a non-secret `.env.*` config file inside the sandbox, narrow the
  `sandbox.credentials.files` glob in `.claude/settings.json` at that point (e.g. list `.env`,
  `.env.local`, `.env*.production*` explicitly) rather than leaving it broad.
* Run `/sandbox` inside a session to check whether `bubblewrap`/`socat` (the sandbox's Linux
  dependencies, installed in `.devcontainer/Dockerfile`) are present and to inspect the effective
  allowlist/credentials config.
* **Known bug, unresolved: `sandbox.network.allowedDomains` is not actually enforced.** Confirmed by
  direct reproduction on Claude Code 2.1.241, both on the bare host and inside the devcontainer (not
  container-specific): a sandboxed Bash `curl` reaches domains outside the allowlist (`example.com`,
  `www.google.com`) exactly like `github.com` — `curl -v` shows every `CONNECT` through the sandbox's
  local proxy (`https_proxy=...@localhost:3128`) getting an unconditional `200 Connection Established`
  with no per-domain filtering. Bubblewrap's own filesystem/mount-namespace isolation is confirmed
  working (`SANDBOX_RUNTIME=1`, a distinct mount namespace) — only this network filter on top of it is
  broken. **Mitigation in place:** `permissions.deny` hard-denies `Bash(curl:*)` and `Bash(wget:*)`
  outright (pattern borrowed from
  [shun968/marketing-data-pipeline](https://github.com/shun968/marketing-data-pipeline/blob/main/.claude/settings.json),
  which skips domain-allowlist sandboxing entirely and denies the raw HTTP client tools instead) —
  confirmed working (both commands are now denied before they run, regardless of destination host).
  This is a *narrower* boundary than a real domain sandbox, not a fix for it: it only stops `curl`/
  `wget` invocations specifically — it does not stop egress via other tools (`git clone` to an
  arbitrary host, Python's `requests`/`urllib`, `nc`, `scp`, `rsync`, …). Treat "no domain-level
  network sandbox" as the honest baseline and re-test `sandbox.network.allowedDomains` after
  upgrading Claude Code before trusting it as a real boundary again; report via `/help`'s feedback
  link.
* **`sandbox.credentials` / matching `permissions.deny` `Read`/`Edit` globs ARE enforced, but only
  for paths inside the project directory.** Verified directly: a file created (via sandboxed Bash)
  under `secrets/` or named `.credentials*` at the repo root became unreadable by both the `Read` tool
  (`"File is in a directory that is denied by your permission settings"`) and by a sandboxed `cat`
  (OS-level `Permission denied` — an actual bind-mount block, not just a tool-level refusal); removing
  it even failed with `Permission denied`/`Device or resource busy` while the mount was active,
  including with the sandbox explicitly disabled. Creating a `.env*` file at the repo root was refused
  by every tool tried (`Write`, Bash redirection, even sandbox-disabled Bash) before it could even be
  written. The one earlier test that looked like a `sandbox.credentials` failure — a dummy `.env` file
  placed *outside* the repo, in an OS scratch/temp directory, which a sandboxed `cat` read back fine —
  was a flawed test, not evidence of a broken mechanism: these path-glob rules are scoped to the
  project directory, so anything written outside it (e.g. this session's own scratchpad temp dir under
  `/tmp`) is simply out of scope for them, not a bypass of them. Do rely on these protections for
  paths under the project root; do not assume they cover paths elsewhere on disk.
* `permissions.deny` also blocks `/**/.credentials*` and `/**/secrets/**` (Read+Edit), matching the
  same reference repo above, as defense-in-depth even though this repo has neither today — same
  rationale as the pre-existing broad `.env*` block below.
* `.devcontainer/devcontainer.json`'s `runArgs` disable Docker's default AppArmor and seccomp
  confinement for the whole devcontainer (`--security-opt apparmor=unconfined` and
  `--security-opt seccomp=unconfined`) — bubblewrap needs both to create the nested user/mount
  namespaces it relies on, even on a host with no kernel-level namespace restrictions. Confirmed by
  direct reproduction (isolated `docker run` tests against a bare `python:3.14-slim` image, outside
  this repo): Docker's default seccomp profile alone blocks the user-namespace creation bwrap needs
  (`bwrap: No permissions to create new namespace`), and once that's lifted, Docker's default AppArmor
  profile (`docker-default`) separately blocks the mount operations bwrap performs inside the new
  namespace (`bwrap: Failed to make / slave: Permission denied`) — both had to be disabled together;
  neither alone was sufficient. No host-level AppArmor setup is needed: on a host where
  `kernel.apparmor_restrict_unprivileged_userns` is enforced (e.g. Ubuntu 24.04+), the distro's own
  bundled `/etc/apparmor.d/bwrap-userns-restrict` already grants bubblewrap the exception it needs,
  once Docker's own container-level confinement is out of the way — no custom host AppArmor profile
  needs to be installed. This is a real trade-off worth knowing: it removes Docker's own sandboxing of
  the *entire* devcontainer, not just the bwrap-based command sandbox — Claude Code's `sandbox.*`
  config above remains the actual access-control boundary for agent-run commands.
* When work needs a host that isn't in `sandbox.network.allowedDomains` (e.g. wherever `ckpt.t7` /
  `YOLOX-final.pth` are hosted), either fetch it manually outside the sandbox or add the host to the
  allowlist in `.claude/settings.json`.
* Not yet addressed from issue #7: per-Skill command profiles (Claude Code has no such mechanism)
  and a policy for sandboxed test execution (this repo has no test suite yet).

## Git conventions

* Branch names: `<type>/<issue-number>-<slug>` (`feature`, `fix`, `docs`, `chore`, `refactor`, `test`),
  e.g. `feature/7-sandbox-dev-environment`. Enforced by lefthook's `pre-commit` hook — run
  `lefthook install` once per checkout (automatic inside the dev container).
* Prefer `gh issue develop <number> --name <type>/<number>-<slug> --checkout` to create branches.
* Commit messages: [Conventional Commits](https://www.conventionalcommits.org/), **title only** (no
  body/footer — this repo does not use a trailing `Co-Authored-By`/`Claude-Session` footer), must
  reference an issue, e.g. `feat(devcontainer): 説明 (#7)`. Enforced by commitlint via lefthook's
  `commit-msg` hook — run `npm install` once per checkout.
* This repo follows [GitHub Flow](https://docs.github.com/en/get-started/using-github/github-flow):
  `main` is protected (PR required, no force-push/deletion, linear history only) via
  `scripts/setup-github-flow-branch-protection.sh`. Squash-merge PRs with explicit `--subject`/
  `--body ""`:
  ```
  gh pr merge <number> --squash --delete-branch --subject "type(scope): 説明 (#issue番号)" --body ""
  ```

## Architecture decision records

Decisions about DeepSORVF's own architecture (the fusion pipeline, tracking algorithm, etc. — not
dev-environment or repo-management tooling) live in `docs/adr/` (Nygard format) — see the `adr` skill
(`.claude/skills/adr/SKILL.md`) for the template and update rules.

## Localization

`README.md` is written in Japanese and is the sole, canonical README for this repository (it was
promoted from a former `README_ja.md`). There is no English or Chinese README anymore — the former
`README_zh-CN.md` and the English-language variant have both been removed, and their language
badges/links have been dropped from `README.md` accordingly. Don't reintroduce `README_en.md` /
`README_zh-CN.md` or their badges without the user asking for it.
