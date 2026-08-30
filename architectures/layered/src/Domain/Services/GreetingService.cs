using LayeredArchitecture.Domain.Repositories;

namespace LayeredArchitecture.Domain.Services;

public sealed class GreetingService
{
    private readonly IGreetingRepository _greetingRepository;

    public GreetingService(IGreetingRepository greetingRepository)
    {
        _greetingRepository = greetingRepository;
    }

    public string GetMessage() => _greetingRepository.GetGreeting().Message;
}
