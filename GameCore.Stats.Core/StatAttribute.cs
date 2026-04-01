using System.Text.Json.Serialization;

namespace GameCore.Stats;

public struct StatAttribute
{
    public StatAttribute()
    {
    }

    public StatAttribute(float baseValue)
    {
        BaseValue = baseValue;
    }

    [JsonConstructor]
    public StatAttribute(float baseValue, float currentValue)
    {
        BaseValue = baseValue;
        CurrentValue = currentValue;
    }

    public float BaseValue { get; set; }
    public float CurrentValue { get; set; }
}
