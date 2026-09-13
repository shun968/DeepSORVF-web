using LayeredArchitecture.Domain.Entities;
using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Application.Services;

public sealed class AisService
{
    private readonly IAisRepository _aisRepository;

    public AisService(IAisRepository aisRepository)
    {
        _aisRepository = aisRepository;
    }

    public IReadOnlyList<AisRecord> GetValidRecordsAt(string aisDirectoryPath, DateTimeOffset timestampUtc) =>
        _aisRepository.GetRecordsAt(aisDirectoryPath, timestampUtc)
            .Where(record => record.IsValid)
            .ToList();
}
