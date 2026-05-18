namespace picoblog.Controllers;

public class MapController : Controller
{
    private readonly GeocodingService _geocodingService;

    public MapController(GeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    [HttpGet]
    [Route("map")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Map = "class = active";

        return View(
            new MapIndexViewModel
            {
                TileUrl = Config.MapTileUrl,
                DefaultZoom = Config.MapDefaultZoom,
            }
        );
    }

    [HttpGet]
    [Route("map/posts")]
    public async Task<IActionResult> Posts([FromQuery] double? north, [FromQuery] double? south, [FromQuery] double? east, [FromQuery] double? west)
    {
        var posts = new List<MapPostViewModel>();
        foreach (var post in Cache.Models.Where(post => post.Location != null))
        {
            var location = await _geocodingService.ResolveAsync(post.Location);
            if (location?.HasCoordinates != true)
                continue;

            if (!InsideBounds(location, north, south, east, west))
                continue;

            posts.Add(ToMapPost(post, location));
        }

        var markers = posts
            .GroupBy(post => new
            {
                Latitude = Math.Round(post.Latitude, 6),
                Longitude = Math.Round(post.Longitude, 6),
            })
            .Select(group => new MapMarkerViewModel
            {
                Latitude = group.Key.Latitude,
                Longitude = group.Key.Longitude,
                Posts = group.OrderByDescending(post => post.Date).ToList(),
            })
            .ToList();

        return Json(markers);
    }

    private static bool InsideBounds(
        LocationMetadata location,
        double? north,
        double? south,
        double? east,
        double? west
    )
    {
        if (!north.HasValue || !south.HasValue || !east.HasValue || !west.HasValue)
            return true;

        var latitude = location.Latitude!.Value;
        var longitude = location.Longitude!.Value;
        if (latitude > north.Value || latitude < south.Value)
            return false;

        if (west.Value <= east.Value)
            return longitude >= west.Value && longitude <= east.Value;

        return longitude >= west.Value || longitude <= east.Value;
    }

    private static MapPostViewModel ToMapPost(MarkdownModel post, LocationMetadata location)
    {
        var postTitle = ContentSecurity.UrlEncodePathSegment(post.Title);
        var coverImageUrl = string.IsNullOrWhiteSpace(post.CoverImage)
            ? null
            : $"/post/{post.Date?.Year}/{postTitle}/{ContentSecurity.UrlEncodePathSegment(post.CoverImage)}";

        return new MapPostViewModel
        {
            Title = post.Title,
            LocationTitle = location.DisplayTitle,
            Latitude = location.Latitude!.Value,
            Longitude = location.Longitude!.Value,
            Date = post.Date,
            Description = post.Description,
            CoverImageUrl = coverImageUrl,
            Url = $"/post/{post.Date?.Year}/{postTitle}",
        };
    }
}
