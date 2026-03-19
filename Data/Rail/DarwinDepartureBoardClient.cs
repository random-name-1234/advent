using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace advent.Data.Rail;

internal static class DarwinDepartureBoardClient
{
    private const int StationFetchRows = 10;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TimeSpan PreparationTimeout => HttpClient.Timeout + TimeSpan.FromSeconds(2);

    public static async Task<StationBoardDto> FetchDirectionalDepartureBoardAsync(
        RailBoardOptions railOptions,
        string stationCrs,
        string stationLabel,
        string counterpartCrs,
        string boardTime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(railOptions);

        var url =
            $"{railOptions.BaseUrl}/api/20220120/GetDepBoardWithDetails/{stationCrs.ToUpperInvariant()}/{boardTime}?numRows={StationFetchRows}&timeWindow=120&filterCRS={counterpartCrs.ToUpperInvariant()}&filterType=to&services=P";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ApplyAuthentication(request, railOptions);

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Rail API returned {(int)response.StatusCode} ({response.ReasonPhrase}) for '{url}'. Body: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var board = await JsonSerializer.DeserializeAsync<StationBoardDto>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return board ?? new StationBoardDto(
            stationLabel,
            stationCrs,
            DateTimeOffset.MinValue,
            true,
            [],
            []);
    }

    private static void ApplyAuthentication(HttpRequestMessage request, RailBoardOptions railOptions)
    {
        if (!string.IsNullOrWhiteSpace(railOptions.AuthHeaderName) &&
            !string.IsNullOrWhiteSpace(railOptions.AuthHeaderValue))
        {
            request.Headers.TryAddWithoutValidation(railOptions.AuthHeaderName, railOptions.AuthHeaderValue);
            return;
        }

        if (!string.IsNullOrWhiteSpace(railOptions.Username) &&
            !string.IsNullOrWhiteSpace(railOptions.Password))
        {
            var bytes = Encoding.UTF8.GetBytes($"{railOptions.Username}:{railOptions.Password}");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(bytes));
            return;
        }

        throw new InvalidOperationException("Rail board credentials are not configured.");
    }
}
