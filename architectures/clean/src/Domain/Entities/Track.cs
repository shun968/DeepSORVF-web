namespace CleanArchitecture.Domain.Entities;

// A detection a tracker has given a lasting identity to.
public sealed class Track
{
    public int Id { get; }
    public Detection Detection { get; }

    public Track(int id, Detection detection)
    {
        Id = id;
        Detection = detection;
    }
}
