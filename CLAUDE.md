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

* Python 3.7, PyTorch 1.13.1 (or 1.9.1 per README badge), CUDA 11.7 per the README's documented
  environment — the devcontainer (see below) installs a CPU-only PyTorch 1.13.1 build instead of CUDA
  11.7, since this repo is used for inference, not training GPU-bound models.
* Top-level `requirements.txt` (installed by the devcontainer's `postCreateCommand`) covers every
  third-party import in the repo, pinned to the latest release still shipping a Python 3.7 wheel.
  `detection_yolox/requirements.txt` also exists but targets an older, narrower torch/opencv pin —
  prefer the top-level file.
* Before running, fetch the two checkpoints with `bash scripts/fetch-model-weights.sh` — they are
  attached to this repo's `weights-v1` GitHub Release rather than committed (public forks can't
  upload new Git LFS objects), and the script verifies their SHA-256. Both paths are gitignored:
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
* `.devcontainer/Dockerfile` (`python:3.7-slim` base) plus the top-level `requirements.txt`
  (`postCreateCommand` runs `pip install --user -r requirements.txt`) reproduce the README's
  documented runtime, CPU-only. `libgl1`/`libglib2.0-0` are installed for `opencv-python`, which needs
  them even headless, and `libgtk-3-0` for the OpenCvSharp runtime the C# ports under
  `architectures/` decode video with (its native library links against GTK). The two model checkpoints aren't fetched by `postCreateCommand`, since `gh`
  may not be logged in yet at that point — run `scripts/fetch-model-weights.sh` once, which pulls
  them from the `weights-v1` GitHub Release. Trade-off worth knowing: Python 3.7 reached end-of-life in 2023, so
  `python:3.7-slim` gets no further upstream security patches — pinned only because the README (and
  PyTorch 1.13.1) require it.
* `.claude/settings.json` (project-shared, committed) turns on Claude Code's built-in Bash sandbox
  (`sandbox.enabled`), restricts sandboxed commands to an explicit host allowlist
  (`sandbox.network.allowedDomains` — GitHub, npm, PyPI, NuGet, Notion), and blocks `~/.ssh`, `~/.aws`, and any
  `.env*` file anywhere in the repo — `**/.env*` already covers a bare `.env` at the repo root, since
  `**` matches zero or more directories — from sandboxed commands (`sandbox.credentials`). `gh` (used
  by this file's Git conventions below, and installed in `.devcontainer/Dockerfile` from GitHub's apt
  repository) is listed in `sandbox.excludedCommands` and runs unsandboxed, so that a `deny` on its
  credential file (`~/.config/gh/hosts.yml`) in any settings scope can't break it — everything else
  runs inside the sandbox. `permissions.deny` adds a second layer: it denies
  `Bash(curl:*)`/`Bash(wget:*)` outright, and `Read`/`Edit` on `.credentials*`/`secrets/**` alongside
  the existing `.env*`/`~/.ssh`/`~/.aws` entries (pattern from
  [shun968/marketing-data-pipeline](https://github.com/shun968/marketing-data-pipeline/blob/main/.claude/settings.json)).
* `sandbox.network.strictAllowlist` (the flag that turns `allowedDomains` into a deterministic deny
  instead of a prompt) is only honored from user, managed/policy, or CLI (`--settings`) settings — a
  project-committed `.claude/settings.json` can't set it. `.devcontainer/wrap-claude-cli.sh` (run once
  from `postCreateCommand`) works around this: it shadows the `claude` binary on `PATH` with a wrapper
  that always adds `--settings /workspace/.devcontainer/claude-strict-network-settings.json`, which
  sets `strictAllowlist: true` at CLI scope and merges with `allowedDomains` from `.claude/settings.json`.
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
* `.devcontainer/devcontainer.json`'s `runArgs` disable Docker's default AppArmor and seccomp
  confinement for the whole devcontainer (`--security-opt apparmor=unconfined` and
  `--security-opt seccomp=unconfined`) — bubblewrap needs both to create the nested user/mount
  namespaces it relies on. No host-level AppArmor setup is needed: Ubuntu's bundled
  `/etc/apparmor.d/bwrap-userns-restrict` already grants bubblewrap the exception it needs, once
  Docker's own container-level confinement is out of the way. Trade-off worth knowing: this removes
  Docker's own sandboxing of the *entire* devcontainer, not just the bwrap-based command sandbox —
  Claude Code's `sandbox.*` config above remains the actual access-control boundary for agent-run
  commands.
* When work needs a host that isn't in `sandbox.network.allowedDomains` (e.g. Google Drive, where the
  `clip-01` test data is hosted), either fetch it manually outside the sandbox or add the host to the
  allowlist in `.claude/settings.json`.
* `.devcontainer/Dockerfile` installs the Notion CLI (`ntn`, `curl -fsSL https://ntn.dev | bash`,
  which redirects to `developers.notion.com/cli`) as the `vscode` user, and symlinks it into
  `/home/vscode/.local/bin` if the installer places it elsewhere. `ntn login` is deliberately not run
  from the Dockerfile or any automated script — it opens a browser to authorize the Notion workspace
  and stores credentials in the OS keychain, so it must be run interactively by whoever uses the
  container. `api.notion.com`/`notion.so` are in `sandbox.network.allowedDomains` so `ntn` can reach
  Notion from inside the sandbox once logged in. See the `managing-notion-docs` skill
  (`.claude/skills/managing-notion-docs/SKILL.md`) for how requirements/design docs are managed
  through it.
* `scripts/check-no-secrets.sh` (lefthook `pre-commit` job `no-secrets`) greps the *added* lines of
  each staged file's diff for common secret shapes (AWS/Google/GitHub/Slack/OpenAI-style keys, PEM
  private-key headers, generic `*_key`/`*_token` assignments) and blocks the commit on a match. This
  is deliberately simple pattern-matching, not a real secrets scanner (e.g. gitleaks/detect-secrets)
  — it won't catch secrets in shapes the patterns don't cover.
* Policy: everything an agent runs in this repo — including any future test suite — is expected to
  execute inside the access-controlled sandbox above by default. The only exceptions are operations
  that are destructive (force-push, history rewrite, deleting branches/files outside what was asked)
  or that publish something externally (pushing, opening/merging PRs, posting comments) — those
  already require explicit human confirmation per Claude Code's own permission system, independent of
  `sandbox.*`, and that stays true regardless of what `sandbox.*` allows.
* Not yet addressed from issue #7: per-Skill command profiles (Claude Code has no such mechanism);
  a policy for sandboxed test execution, and a limited-injection mechanism for secret-requiring tests
  (both deferred together — this repo has no test suite yet, and none of its current code makes
  outbound calls that would need a secret, so there is nothing concrete to design either policy
  against yet; revisit both when a test suite is added).

## Git conventions

Branch naming, commit message format, and the PR/merge process are project rules, not Claude-Code
guidance — see [`CONTRIBUTING.md`](./CONTRIBUTING.md) for the authoritative rules. In short: branch
names follow `<type>/<issue-number>-<slug>`, commits follow Conventional Commits (title only, must
reference an issue), and PRs are squash-merged per GitHub Flow. Both are enforced mechanically by
lefthook (`lefthook install` once per checkout, automatic inside the dev container) and commitlint
(`npm install` once per checkout).

## Architecture decision records

Decisions about DeepSORVF's own architecture (the fusion pipeline, tracking algorithm, etc. — not
dev-environment or repo-management tooling) live in `docs/adr/` (Nygard format) — see the
`managing-adrs` skill (`.claude/skills/managing-adrs/SKILL.md`) for the template and update rules.

## Requirements / basic design (Notion)

Requirements definitions (要件定義) and basic design docs (基本設計) are managed in Notion, not as
repo Markdown — see the `managing-notion-docs` skill (`.claude/skills/managing-notion-docs/SKILL.md`)
for the index-page-rooted tree templates and the `ntn` CLI commands used to create/update them.
`ntn login` is a manual, interactive prerequisite (see the Dev container section above); Claude does
not attempt it. No page-ID index is kept in the repo — each use of the skill asks which Notion
teamspace (and its index page, which the skill uses as the sole entry point instead of searching) is
the target, and walks that page's tree live via `ntn` rather than trusting a possibly-stale local
record.

## Localization

`README.md` is written in Japanese and is the sole, canonical README for this repository (it was
promoted from a former `README_ja.md`). There is no English or Chinese README anymore — the former
`README_zh-CN.md` and the English-language variant have both been removed, and their language
badges/links have been dropped from `README.md` accordingly. Don't reintroduce `README_en.md` /
`README_zh-CN.md` or their badges without the user asking for it.
