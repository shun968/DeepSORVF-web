namespace LayeredArchitecture.Domain.Entities;

public sealed class Greeting
{
    public string Message { get; }

    public Greeting(string message)
    {
        Message = message;
    }
}
