using System.Text.Json;
using System.Text.Json.Serialization;

public class GeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodingService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, GeocodingCacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool _cacheLoaded;
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public GeocodingService(HttpClient httpClient, ILogger<GeocodingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Config.MapUserAgent);
    }

    public async Task<LocationMetadata?> ResolveAsync(LocationMetadata? location)
    {
        if (location == null || location.HasCoordinates)
            return location;

        if (string.IsNullOrWhiteSpace(location.Lookup))
            return null;

        var query = location.Lookup.Trim();

        await _lock.WaitAsync();
        try
        {
            LoadCache();

            if (_cache.TryGetValue(query, out var cached))
            {
                if (cached.HasResultMetadata)
                {
                    location.Latitude = cached.Latitude;
                    location.Longitude = cached.Longitude;
                    location.Title ??= cached.DisplayName;
                    return location;
                }

                _cache.Remove(query);
            }

            var sinceLastRequest = DateTimeOffset.UtcNow - _lastRequest;
            if (sinceLastRequest < TimeSpan.FromSeconds(1))
                await Task.Delay(TimeSpan.FromSeconds(1) - sinceLastRequest);

            var results = Array.Empty<NominatimResult>() as IReadOnlyList<NominatimResult>;
            try
            {
                results = await SearchAsync(query);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Nominatim lookup failed for location {Location}", query);
            }
            _lastRequest = DateTimeOffset.UtcNow;

            var result = results.FirstOrDefault();
            if (result == null)
            {
                var kartverketResult = await SearchKartverketAsync(query);
                if (kartverketResult == null)
                {
                    location.Error = $"No geocoding result for: {query}";
                    return null;
                }

                location.Latitude = kartverketResult.Representasjonspunkt.Latitude;
                location.Longitude = kartverketResult.Representasjonspunkt.Longitude;
                location.Title ??= kartverketResult.DisplayName;

                _cache[query] = new GeocodingCacheEntry
                {
                    Latitude = location.Latitude.Value,
                    Longitude = location.Longitude.Value,
                    DisplayName = location.Title,
                    Class = "place",
                    Type = kartverketResult.PlaceType,
                };
                SaveCache();

                return location;
            }

            if (
                !double.TryParse(result.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                || !double.TryParse(result.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            )
            {
                location.Error = $"Invalid geocoding result for: {query}";
                return null;
            }

            location.Latitude = latitude;
            location.Longitude = longitude;
            location.Title ??= result.DisplayName;

            _cache[query] = new GeocodingCacheEntry
            {
                Latitude = latitude,
                Longitude = longitude,
                DisplayName = result.DisplayName,
                Class = result.Class,
                Type = result.Type,
            };
            SaveCache();

            return location;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve location {Location}", query);
            location.Error = ex.Message;
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<NominatimResult>> SearchAsync(string query)
    {
        var separator = Config.MapGeocodingUrl.Contains('?') ? '&' : '?';
        var url =
            $"{Config.MapGeocodingUrl}{separator}format=jsonv2&addressdetails=1&limit=10&q={Uri.EscapeDataString(query)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", Config.MapUserAgent);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        var results = await JsonSerializer.DeserializeAsync<List<NominatimResult>>(stream) ?? [];
        return results
            .OrderBy(result => result.ImportanceRank)
            .ThenByDescending(result => result.Importance)
            .ToList();
    }

    private async Task<KartverketPlace?> SearchKartverketAsync(string query)
    {
        var url =
            "https://api.kartverket.no/stedsnavn/v1/sted"
            + $"?sok={Uri.EscapeDataString(query)}&fuzzy=true&utkoordsys=4258&treffPerSide=10";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", Config.MapUserAgent);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        var results = await JsonSerializer.DeserializeAsync<KartverketSearchResult>(stream);
        return results
            ?.Places
            ?.Where(place => place.Representasjonspunkt.HasCoordinates)
            .OrderBy(place => place.MatchRank(query))
            .FirstOrDefault();
    }

    private void LoadCache()
    {
        if (_cacheLoaded)
            return;

        _cacheLoaded = true;
        var path = CachePath();
        if (!File.Exists(path))
            return;

        try
        {
            var content = File.ReadAllText(path);
            var cached = JsonSerializer.Deserialize<Dictionary<string, GeocodingCacheEntry>>(content);
            if (cached == null)
                return;

            foreach (var item in cached)
                _cache[item.Key] = item.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read geocoding cache {Path}", path);
        }
    }

    private void SaveCache()
    {
        var path = CachePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Config.ConfigDir);
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write geocoding cache {Path}", path);
        }
    }

    private static string CachePath() => Path.Combine(Config.ConfigDir, "geocoding-cache.json");

    private sealed class GeocodingCacheEntry
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string? DisplayName { get; init; }
        public string? Class { get; init; }
        public string? Type { get; init; }

        [JsonIgnore]
        public bool HasResultMetadata => !string.IsNullOrWhiteSpace(Class) || !string.IsNullOrWhiteSpace(Type);
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("lat")]
        public string Latitude { get; init; } = "";

        [JsonPropertyName("lon")]
        public string Longitude { get; init; } = "";

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("class")]
        public string? Class { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("importance")]
        public double Importance { get; init; }

        [JsonIgnore]
        public int ImportanceRank =>
            Type switch
            {
                "city" => 0,
                "town" => 1,
                "village" => 2,
                "municipality" => 3,
                "administrative" when Class == "boundary" => 8,
                "county" => 9,
                _ => 5,
            };
    }

    private sealed class KartverketSearchResult
    {
        [JsonPropertyName("navn")]
        public List<KartverketPlace> Places { get; init; } = [];
    }

    private sealed class KartverketPlace
    {
        [JsonPropertyName("representasjonspunkt")]
        public KartverketPoint Representasjonspunkt { get; init; } = new();

        [JsonPropertyName("stedsnavn")]
        public List<KartverketName> Names { get; init; } = [];

        [JsonPropertyName("kommuner")]
        public List<KartverketMunicipality> Municipalities { get; init; } = [];

        [JsonPropertyName("navneobjekttype")]
        public string? PlaceType { get; init; }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                var name = Names.FirstOrDefault()?.Name;
                var municipality = Municipalities.FirstOrDefault()?.Name;
                return string.Join(", ", new[] { name, municipality }.Where(part => !string.IsNullOrWhiteSpace(part)));
            }
        }

        public int MatchRank(string query)
        {
            var firstQueryPart = query.Split(',', 2)[0].Trim();
            var name = Names.FirstOrDefault()?.Name;
            var municipality = Municipalities.FirstOrDefault()?.Name;

            var rank = 0;
            if (!string.Equals(name, firstQueryPart, StringComparison.InvariantCultureIgnoreCase))
                rank += 10;
            if (!string.IsNullOrWhiteSpace(municipality) && query.Contains(municipality, StringComparison.InvariantCultureIgnoreCase))
                rank -= 1;

            return rank;
        }
    }

    private sealed class KartverketPoint
    {
        [JsonPropertyName("nord")]
        public double Latitude { get; init; }

        [JsonPropertyName("øst")]
        public double Longitude { get; init; }

        [JsonIgnore]
        public bool HasCoordinates => Latitude != 0 || Longitude != 0;
    }

    private sealed class KartverketName
    {
        [JsonPropertyName("skrivemåte")]
        public string? Name { get; init; }
    }

    private sealed class KartverketMunicipality
    {
        [JsonPropertyName("kommunenavn")]
        public string? Name { get; init; }
    }
}
