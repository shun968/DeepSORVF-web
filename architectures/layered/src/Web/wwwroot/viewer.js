"use strict";

// ---- The parts that depend on this port's API contract (src/Web/Contracts) ----

// Reduces one frame of the run response to what the viewer draws.
function toFrameView(frame) {
  return {
    index: frame.frameIndex,
    timestamp: frame.timestamp,
    vessels: frame.aisRecords,
    fusions: frame.fusedTracks,
  };
}

// Builds the POST /api/vessel-tracking/runs body from the form.
function readRunRequest(fieldValue) {
  const request = {
    aisDataDirectory: fieldValue("aisDataDirectory"),
    cameraParametersPath: fieldValue("cameraParametersPath"),
    startTime: fieldValue("startTime"),
    frameCount: Number(fieldValue("frameCount")),
    frameIntervalSeconds: Number(fieldValue("frameIntervalSeconds")),
  };
  const resultDirectory = fieldValue("resultDirectory");
  if (resultDirectory) {
    request.resultDirectory = resultDirectory;
  }
  return request;
}

// ---- Viewer ----

// How long each frame stays on screen during playback when there is no video to follow.
const STILL_FRAME_MILLISECONDS = 1000;
const UNMATCHED_COLOUR = "#9aa4ae";

const form = document.getElementById("run-form");
const submitButton = form.querySelector("button[type=submit]");
const statusLine = document.getElementById("status");
const viewer = document.getElementById("viewer");
const stage = document.getElementById("stage");
const video = document.getElementById("video");
const canvas = document.getElementById("overlay");
const noVideoNote = document.getElementById("no-video");
const context = canvas.getContext("2d");
const playButton = document.getElementById("play");
const seek = document.getElementById("seek");
const frameLabel = document.getElementById("frame-label");
const fusionRows = document.getElementById("fusion-rows");
const vesselRows = document.getElementById("vessel-rows");

const state = {
  frames: [],
  intervalSeconds: 1,
  // Where frame 0 falls on the video's own timeline, in seconds.
  videoOffsetSeconds: 0,
  hasVideo: false,
  current: -1,
  timer: null,
};

const fieldValue = (name) => form.elements.namedItem(name).value.trim();
const isMatched = (fusion) => fusion.mmsi !== null && fusion.mmsi !== undefined;
const colourFor = (mmsi) => `hsl(${(mmsi * 47) % 360} 85% 55%)`;

function showStatus(message, isError = false) {
  statusLine.textContent = message;
  statusLine.classList.toggle("error", isError);
}

async function loadDefaults() {
  const response = await fetch("api/vessel-tracking/run-defaults");
  if (!response.ok) {
    return;
  }

  const defaults = await response.json();
  for (const [name, value] of Object.entries(defaults)) {
    const input = form.elements.namedItem(name);
    if (input && value !== null && value !== undefined) {
      input.value = value;
    }
  }

  if (fieldValue("aisDataDirectory") && fieldValue("cameraParametersPath")) {
    form.requestSubmit();
  }
}

async function runPipeline(event) {
  event.preventDefault();
  stopPlayback();
  const request = readRunRequest(fieldValue);
  submitButton.disabled = true;
  showStatus("実行中…");

  try {
    const response = await fetch("api/vessel-tracking/runs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    });
    const body = await response.text();
    if (!response.ok) {
      showStatus(`実行に失敗しました（HTTP ${response.status}）\n${body}`, true);
      return;
    }

    await openRun(JSON.parse(body), request);
  } catch (error) {
    showStatus(`実行に失敗しました: ${error.message}`, true);
  } finally {
    submitButton.disabled = false;
  }
}

async function openRun(run, request) {
  state.frames = run.frames.map(toFrameView);
  state.intervalSeconds = request.frameIntervalSeconds;
  const videoStart = Date.parse(fieldValue("videoStartTime") || request.startTime);
  const offsetSeconds = (Date.parse(request.startTime) - videoStart) / 1000;
  state.videoOffsetSeconds = Number.isFinite(offsetSeconds) ? offsetSeconds : 0;
  const videoRequested = fieldValue("videoPath") !== "";
  state.hasVideo = videoRequested && (await loadVideo());

  canvas.width = state.hasVideo ? video.videoWidth : run.imageWidth;
  canvas.height = state.hasVideo ? video.videoHeight : run.imageHeight;
  stage.classList.toggle("has-video", state.hasVideo);
  video.hidden = !state.hasVideo;
  noVideoNote.hidden = state.hasVideo;
  noVideoNote.textContent = videoRequested
    ? "動画を読み込めませんでした。ブラウザが再生できる形式（H.264のmp4など）か確認してください。"
    : "動画が設定されていないため、映像なしで描画しています。映像に重ねるには、リポジトリ直下に clip-01/ を置くか、VIDEO_PATH を指定して task run を起動してください。";
  seek.max = String(Math.max(state.frames.length - 1, 0));
  viewer.hidden = false;

  state.current = -1;
  showFrame(0, true);

  const background = state.hasVideo ? "動画に重ねて表示しています" : "動画なしで表示しています";
  showStatus(`${state.frames.length} フレームを処理しました（${background}）。`);
}

function loadVideo() {
  return new Promise((resolve) => {
    const finish = (loaded) => {
      video.onloadedmetadata = null;
      video.onerror = null;
      resolve(loaded);
    };
    video.onloadedmetadata = () => finish(true);
    video.onerror = () => finish(false);
    video.src = `api/vessel-tracking/video?v=${Date.now()}`;
  });
}

function frameAtVideoTime(seconds) {
  const index = Math.floor((seconds - state.videoOffsetSeconds) / state.intervalSeconds + 1e-6);
  return Math.min(Math.max(index, 0), state.frames.length - 1);
}

function showFrame(index, seekVideo = false) {
  if (state.frames.length === 0) {
    return;
  }

  if (seekVideo && state.hasVideo) {
    video.currentTime = Math.max(state.videoOffsetSeconds + index * state.intervalSeconds, 0);
  }

  if (index === state.current) {
    return;
  }

  state.current = index;
  seek.value = String(index);
  draw(state.frames[index], index);
  describe(state.frames[index]);
}

// ---- Drawing ----

function draw(frame, index) {
  const scale = Math.max(canvas.width / 960, 1);
  context.clearRect(0, 0, canvas.width, canvas.height);
  context.lineWidth = 2 * scale;
  context.font = `${Math.round(14 * scale)}px system-ui, sans-serif`;
  context.textBaseline = "bottom";

  for (const vessel of frame.vessels) {
    drawVessel(vessel, index, scale);
  }

  for (const fusion of frame.fusions) {
    drawFusion(fusion, scale);
  }
}

function trailOf(mmsi, lastIndex) {
  return state.frames
    .slice(0, lastIndex + 1)
    .flatMap((frame) => frame.vessels.filter((vessel) => vessel.mmsi === mmsi));
}

function drawVessel(vessel, index, scale) {
  const colour = colourFor(vessel.mmsi);
  context.strokeStyle = colour;
  context.fillStyle = colour;
  context.setLineDash([]);

  context.beginPath();
  trailOf(vessel.mmsi, index).forEach((point, i) => {
    if (i === 0) {
      context.moveTo(point.x, point.y);
    } else {
      context.lineTo(point.x, point.y);
    }
  });
  context.stroke();

  context.beginPath();
  context.arc(vessel.x, vessel.y, 5 * scale, 0, Math.PI * 2);
  context.fill();

  drawLabel(`AIS ${vessel.mmsi}`, vessel.x + 8 * scale, vessel.y - 6 * scale, colour);
}

function drawFusion(fusion, scale) {
  const matched = isMatched(fusion);
  const colour = matched ? colourFor(fusion.mmsi) : UNMATCHED_COLOUR;
  context.strokeStyle = colour;
  context.setLineDash(matched ? [] : [6 * scale, 4 * scale]);
  context.strokeRect(fusion.x1, fusion.y1, fusion.x2 - fusion.x1, fusion.y2 - fusion.y1);
  context.setLineDash([]);

  const text = matched ? `#${fusion.trackId} → ${fusion.mmsi}` : `#${fusion.trackId} AISなし`;
  drawLabel(text, fusion.x1, fusion.y1 - 4 * scale, colour);
}

function drawLabel(text, x, y, colour) {
  const padding = 3;
  const width = context.measureText(text).width;
  const height = Number.parseFloat(context.font);
  const left = Math.min(Math.max(x, padding), canvas.width - width - padding);
  const bottom = Math.max(y, height + padding);

  context.fillStyle = "rgba(0, 0, 0, 0.65)";
  context.fillRect(left - padding, bottom - height - padding, width + padding * 2, height + padding * 2);
  context.fillStyle = colour;
  context.fillText(text, left, bottom);
}

// ---- Frame details ----

function cell(text, colour) {
  const td = document.createElement("td");
  if (colour) {
    const swatch = document.createElement("span");
    swatch.className = "swatch";
    swatch.style.color = colour;
    td.append(swatch);
  }
  td.append(text);
  return td;
}

function row(...cells) {
  const tr = document.createElement("tr");
  tr.append(...cells);
  return tr;
}

const formatBox = (fusion) => [fusion.x1, fusion.y1, fusion.x2, fusion.y2].map((v) => Math.round(v)).join(", ");

function describe(frame) {
  frameLabel.textContent = `フレーム ${frame.index + 1} / ${state.frames.length}　${frame.timestamp}`;

  fusionRows.replaceChildren(
    ...frame.fusions.map((fusion) => {
      const matched = isMatched(fusion);
      return row(
        cell(`#${fusion.trackId}`),
        matched ? cell(String(fusion.mmsi), colourFor(fusion.mmsi)) : cell("AISなし", UNMATCHED_COLOUR),
        cell(formatBox(fusion)),
      );
    }),
  );

  vesselRows.replaceChildren(
    ...frame.vessels.map((vessel) =>
      row(
        cell(String(vessel.mmsi), colourFor(vessel.mmsi)),
        cell(`${vessel.x}, ${vessel.y}`),
        cell(vessel.speedKnots.toFixed(1)),
        cell(vessel.courseDegrees.toFixed(1)),
      ),
    ),
  );
}

// ---- Playback ----

const isPlaying = () => state.timer !== null || (state.hasVideo && !video.paused);
const lastFrameIndex = () => state.frames.length - 1;

function startPlayback() {
  if (state.current >= lastFrameIndex()) {
    showFrame(0, true);
  }

  if (state.hasVideo) {
    video.play().then(followVideo, (error) => showStatus(`動画を再生できませんでした: ${error.message}`, true));
  } else {
    state.timer = setInterval(() => {
      if (state.current >= lastFrameIndex()) {
        stopPlayback();
      } else {
        showFrame(state.current + 1);
      }
    }, STILL_FRAME_MILLISECONDS);
  }

  playButton.textContent = "一時停止";
}

function stopPlayback() {
  clearInterval(state.timer);
  state.timer = null;
  if (!video.paused) {
    video.pause();
  }
  playButton.textContent = "再生";
}

// Keeps the overlay on the frame the video is showing, and stops once the video runs past
// the last processed frame.
function followVideo() {
  if (video.paused || video.ended) {
    return;
  }

  const runEndSeconds = state.videoOffsetSeconds + state.frames.length * state.intervalSeconds;
  if (video.currentTime >= runEndSeconds) {
    stopPlayback();
    return;
  }

  showFrame(frameAtVideoTime(video.currentTime));
  requestAnimationFrame(followVideo);
}

form.addEventListener("submit", runPipeline);
playButton.addEventListener("click", () => (isPlaying() ? stopPlayback() : startPlayback()));
seek.addEventListener("input", () => showFrame(Number(seek.value), true));
video.addEventListener("pause", () => {
  playButton.textContent = "再生";
});
video.addEventListener("seeked", () => showFrame(frameAtVideoTime(video.currentTime)));

loadDefaults().catch((error) => showStatus(`既定値を読み込めませんでした: ${error.message}`, true));
