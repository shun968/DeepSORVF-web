using System.Globalization;
using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Infrastructure.Repositories;

// Writes the three result files utils/gen_result.py produces, one per kind. The original
// names them after the clip's video file; there is no video here, so the kind alone names
// the file.
public sealed class MotResultFileWriter : IMotResultWriter
{
    public void Write(string resultDirectoryPath, MotResultKind kind, IReadOnlyList<MotResultRow> rows)
    {
        Directory.CreateDirectory(resultDirectoryPath);

        var path = Path.Combine(resultDirectoryPath, $"{kind.ToString().ToLowerInvariant()}.txt");
        File.WriteAllLines(path, rows.Select(Format));
    }

    private static string Format(MotResultRow row) => string.Join(
        ',',
        row.Frame.ToString(CultureInfo.InvariantCulture),
        row.Id.ToString(CultureInfo.InvariantCulture),
        row.X.ToString(CultureInfo.InvariantCulture),
        row.Y.ToString(CultureInfo.InvariantCulture),
        row.Width.ToString(CultureInfo.InvariantCulture),
        row.Height.ToString(CultureInfo.InvariantCulture),
        "1",
        "1",
        "1",
        "1");
}
