namespace CleanArchitecture.Domain.Entities;

// The pixels of one video frame: three bytes per pixel in red, green, blue order, row by row
// from the top left. Plain bytes, so a detector can be handed a frame without the Domain
// knowing which library decoded it.
public sealed class FrameImage
{
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Rgb { get; }

    public FrameImage(int width, int height, ReadOnlyMemory<byte> rgb)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var expectedLength = (long)width * height * 3;
        if (rgb.Length != expectedLength)
        {
            throw new ArgumentException(
                $"A {width}x{height} RGB image needs {expectedLength} bytes, got {rgb.Length}.", nameof(rgb));
        }

        Width = width;
        Height = height;
        Rgb = rgb;
    }
}
