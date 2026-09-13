using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Domain.Repositories;

public enum MotResultKind
{
    Detection,
    Tracking,
    Fusion,
}

public interface IMotResultWriter
{
    void Write(string resultDirectoryPath, MotResultKind kind, IReadOnlyList<MotResultRow> rows);
}
