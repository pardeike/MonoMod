using System.Text.Json.Serialization;

namespace GenTestMatrix.Models;

internal abstract record Enableable
{
    [JsonIgnore]
    public bool Enabled { get; init; } = true;
}
