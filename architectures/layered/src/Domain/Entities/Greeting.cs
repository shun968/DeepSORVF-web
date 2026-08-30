namespace LayeredArchitecture.Domain.Entities;

public sealed class Greeting
{
    public string Message { get; }

    public Greeting(string message)
    {
        if (message is null || message.Length == 0)
        {
            throw new ArgumentException("message must not be null or empty.", nameof(message));
        }

        Message = message;
    }
}
