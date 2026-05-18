namespace picoblog.Models;

public class MapPostViewModel
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string LocationTitle { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public DateTime? Date { get; init; }
    public string? Description { get; init; }
    public string? CoverImageUrl { get; init; }
}

public class MapIndexViewModel
{
    public required string TileUrl { get; init; }
    public required int DefaultZoom { get; init; }
}

public class MapMarkerViewModel
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required IReadOnlyList<MapPostViewModel> Posts { get; init; }
}
