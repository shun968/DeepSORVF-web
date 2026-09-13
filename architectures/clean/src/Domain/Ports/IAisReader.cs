using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Domain.Ports;

public interface IAisReader
{
    IReadOnlyList<AisRecord> ReadAt(string aisDirectoryPath, DateTimeOffset timestampUtc);
}
