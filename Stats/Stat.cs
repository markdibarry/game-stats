using System.Text.Json.Serialization;

namespace GameCore.Statistics;

public struct Stat
{
    public Stat()
    {
        StatTypeId = string.Empty;
    }

    public Stat(string statTypeId, float baseValue)
    {
        StatTypeId = statTypeId;
        BaseValue = baseValue;
    }

    [JsonConstructor]
    public Stat(string statTypeId, float baseValue, float currentValue)
    {
        StatTypeId = statTypeId;
        BaseValue = baseValue;
        CurrentValue = currentValue;
    }

    public string StatTypeId { get; set; }
    public float BaseValue { get; set; }
    public float CurrentValue { get; set; }
}
