namespace picoblog.Models;

public class LocationMetadata
{
    public string? Raw { get; internal set; }
    public string? Title { get; internal set; }
    public string? Gps { get; internal set; }
    public string? Lookup { get; internal set; }
    public double? Latitude { get; internal set; }
    public double? Longitude { get; internal set; }
    public string? Error { get; internal set; }

    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title)
            ? Lookup ?? Raw ?? $"{Latitude:0.#####}, {Longitude:0.#####}"
            : Title;
}
