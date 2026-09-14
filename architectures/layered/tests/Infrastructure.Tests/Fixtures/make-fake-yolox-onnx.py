"""Writes fake-yolox.onnx: a stand-in for the exported YOLOX model that ignores the picture
and always predicts the same rows, so the detector's ONNX Runtime path can be tested without
the real 35MB model. Same interface as scripts/export-yolox-onnx.py's output: input "images"
[1, 3, 640, 640], output "predictions" [1, N, 6] = centre x, centre y, width, height (640x640
input pixels), objectness, vessel score.

Regenerate with: python3 make-fake-yolox-onnx.py
"""
import torch

ROWS = [
    [320.0, 320.0, 100.0, 50.0, 0.9, 0.9],   # kept: score 0.81
    [322.0, 321.0, 100.0, 50.0, 0.8, 0.8],   # overlaps the first with a lower score: suppressed
    [100.0, 200.0, 40.0, 20.0, 0.7, 0.9],    # kept: score 0.63
    [500.0, 400.0, 60.0, 30.0, 0.5, 0.5],    # below the 0.5 threshold: dropped (score 0.25)
    [630.0, 150.0, 40.0, 20.0, 0.9, 0.6],    # kept (score 0.54), runs past the right edge
]


class FakeYolox(torch.nn.Module):
    def __init__(self):
        super().__init__()
        self.register_buffer("rows", torch.tensor([ROWS]))

    def forward(self, images):
        # Touch the input so it stays a graph input, without changing the output.
        return self.rows + images[:, :1, :1, :1].reshape(1, 1, 1) * 0.0


torch.onnx.export(
    FakeYolox().eval(), torch.zeros(1, 3, 640, 640), "fake-yolox.onnx",
    input_names=["images"], output_names=["predictions"], opset_version=12)
print("wrote fake-yolox.onnx")
