namespace picoblog.Models;

public static class LocationParser
{
    public static LocationMetadata? ParseLocation(string frontmatter)
    {
        var lines = Regex.Split(frontmatter, "\r?\n");

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
                continue;

            var parts = line.Split(':', 2);
            if (parts.Length < 2 || !parts[0].Trim().Equals(MetadataHeader.Location, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = parts[1].Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return FromScalar(value);

            return FromObject(lines, index + 1);
        }

        return null;
    }

    public static bool TryParseCoordinates(string? value, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
        )
        {
            return false;
        }

        return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    }

    private static LocationMetadata FromScalar(string value)
    {
        var location = new LocationMetadata { Raw = value };

        if (TryParseCoordinates(value, out var latitude, out var longitude))
        {
            location.Gps = value;
            location.Latitude = latitude;
            location.Longitude = longitude;
        }
        else
        {
            location.Lookup = value;
        }

        return location;
    }

    private static LocationMetadata? FromObject(string[] lines, int startIndex)
    {
        var location = new LocationMetadata();

        for (var index = startIndex; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!char.IsWhiteSpace(line[0]))
                break;

            var parts = line.Trim().Split(':', 2);
            if (parts.Length < 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (key.Equals("title", StringComparison.OrdinalIgnoreCase))
                location.Title = value;
            else if (key.Equals("gps", StringComparison.OrdinalIgnoreCase))
                location.Gps = value;
            else if (key.Equals("lookup", StringComparison.OrdinalIgnoreCase))
                location.Lookup = value;
        }

        if (TryParseCoordinates(location.Gps, out var latitude, out var longitude))
        {
            location.Latitude = latitude;
            location.Longitude = longitude;
        }
        else if (!string.IsNullOrWhiteSpace(location.Gps))
        {
            location.Error = $"Invalid gps value: {location.Gps}";
        }

        if (
            string.IsNullOrWhiteSpace(location.Title)
            && string.IsNullOrWhiteSpace(location.Gps)
            && string.IsNullOrWhiteSpace(location.Lookup)
        )
        {
            return null;
        }

        return location;
    }
}
