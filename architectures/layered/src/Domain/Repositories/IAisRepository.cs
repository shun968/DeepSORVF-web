using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Domain.Repositories;

public interface IAisRepository
{
    IReadOnlyList<AisRecord> GetRecordsAt(string aisDirectoryPath, DateTimeOffset timestampUtc);
}
