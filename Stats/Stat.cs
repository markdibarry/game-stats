using System.Text.Json.Serialization;

namespace GameCore.Statistics;

public struct Stat
{
    public Stat()
    {
    }

    public Stat(float baseValue)
    {
        BaseValue = baseValue;
    }

    [JsonConstructor]
    public Stat(float baseValue, float currentValue)
    {
        BaseValue = baseValue;
        CurrentValue = currentValue;
    }

    public float BaseValue { get; set; }
    public float CurrentValue { get; set; }
}
