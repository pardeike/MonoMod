using System.Text.Json.Serialization;

namespace GenTestMatrix.Models;

internal sealed record MatrixResult
{
    [JsonPropertyName("include")]
    public required IEnumerable<Job> Jobs { get; init; }
}
