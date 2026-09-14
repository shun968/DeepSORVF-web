#!/usr/bin/env python3
"""Exports detection_yolox's YOLOX-final.pth to ONNX, for the C# ports under architectures/.

The output decoding (decode_outputs in detection_yolox/utils/utils_bbox.py) is part of the
graph, so the model takes "images" [1, 3, 640, 640] (letterboxed RGB, normalised with the
ImageNet mean and deviation) and returns "predictions" [1, 8400, 5 + classes]: centre x,
centre y, width and height in input pixels, objectness, then a score per class. Filtering by
score and non-maximum suppression are left to the caller, as in detection_yolox/yolo.py.

Writes detection_yolox/model_data/YOLOX-final.onnx.

Usage: python3 scripts/export-yolox-onnx.py [--if-missing]
"""
import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WEIGHTS = ROOT / "detection_yolox" / "model_data" / "YOLOX-final.pth"
CLASSES = ROOT / "detection_yolox" / "model_data" / "ship_classes.txt"
OUTPUT = ROOT / "detection_yolox" / "model_data" / "YOLOX-final.onnx"
INPUT_SIZE = 640


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--if-missing",
        action="store_true",
        help="do nothing if the output exists, and only warn if the weights have not been fetched")
    args = parser.parse_args()

    if args.if_missing and OUTPUT.exists():
        return
    if not WEIGHTS.exists():
        message = f"{WEIGHTS} not found: fetch it first with scripts/fetch-model-weights.sh"
        if args.if_missing:
            print(f"warning: {message}. Runs with a video cannot detect vessels until then.", file=sys.stderr)
            return
        sys.exit(message)

    # Imported only once there is something to export: torch takes a few seconds to load.
    import torch

    sys.path.insert(0, str(ROOT))
    from detection_yolox.nets.yolo import YoloBody
    from detection_yolox.utils.utils import get_classes

    _, num_classes = get_classes(str(CLASSES))
    body = YoloBody(num_classes, "s")
    body.load_state_dict(torch.load(str(WEIGHTS), map_location="cpu", weights_only=True))

    model = DecodedYolox(body).eval()
    torch.onnx.export(
        model,
        torch.zeros(1, 3, INPUT_SIZE, INPUT_SIZE),
        str(OUTPUT),
        input_names=["images"],
        output_names=["predictions"],
        opset_version=12)
    print(f"exported {OUTPUT}")


def decoded(outputs):
    """decode_outputs from detection_yolox/utils/utils_bbox.py, without the normalisation."""
    import torch

    sizes = [output.shape[-2:] for output in outputs]
    outputs = torch.cat([output.flatten(start_dim=2) for output in outputs], dim=2).permute(0, 2, 1)
    grids, strides = [], []
    for height, width in sizes:
        grid_y, grid_x = torch.meshgrid([torch.arange(height), torch.arange(width)], indexing="ij")
        grid = torch.stack((grid_x, grid_y), 2).view(1, -1, 2)
        grids.append(grid)
        strides.append(torch.full((1, grid.shape[1], 1), INPUT_SIZE / height))
    grids = torch.cat(grids, dim=1).type(outputs.type())
    strides = torch.cat(strides, dim=1).type(outputs.type())
    centres = (outputs[..., :2] + grids) * strides
    sizes = torch.exp(outputs[..., 2:4]) * strides
    return torch.cat([centres, sizes, torch.sigmoid(outputs[..., 4:])], dim=2)


def DecodedYolox(body):
    import torch

    class Module(torch.nn.Module):
        def __init__(self):
            super().__init__()
            self.body = body

        def forward(self, images):
            return decoded(self.body(images))

    return Module()


if __name__ == "__main__":
    main()
