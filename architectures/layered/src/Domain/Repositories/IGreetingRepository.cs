using LayeredArchitecture.Domain.Entities;

namespace LayeredArchitecture.Domain.Repositories;

public interface IGreetingRepository
{
    Greeting GetGreeting();
}
