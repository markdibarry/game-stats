
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameCore.Stats;

public delegate void DeserializeDel(ref Utf8JsonReader reader, Condition condition);

public sealed class ConditionJsonConverter : JsonConverter<Condition>
{
    private static readonly Dictionary<string, DeserializeDel> s_deserialize = new()
    {
        {
            "Timed",
            (ref reader, cond) =>
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();

                TimedCondition.TimedState state = new();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    string stateProp = reader.GetString() ?? string.Empty;
                    reader.Read();

                    switch (stateProp)
                    {
                        case nameof(TimedCondition.TimedState.TimeLeft):
                            state.TimeLeft = reader.GetSingle();
                            break;
                        case nameof(TimedCondition.TimedState.Duration):
                            state.Duration = reader.GetSingle();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                ((TimedCondition)cond).State = state;
            }
        },
        {
            "Resource",
            (ref reader, cond) =>
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();

                ResourceCondition.ResourceState state = new();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    string stateProp = reader.GetString() ?? string.Empty;
                    reader.Read();

                    switch (stateProp)
                    {
                        case nameof(ResourceCondition.ResourceState.StatTypeId):
                            state.StatTypeId = reader.GetString() ?? string.Empty;
                            break;
                        case nameof(ResourceCondition.ResourceState.CompareOp):
                            state.CompareOp = Enum.Parse<CompareOp>(reader.GetString() ?? string.Empty);
                            break;
                        case nameof(ResourceCondition.ResourceState.TargetValue):
                            state.TargetValue = reader.GetInt32();
                            break;
                        case nameof(ResourceCondition.ResourceState.IsPercent):
                            state.IsPercent = reader.GetBoolean();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                ((ResourceCondition)cond).State = state;
            }
        }
    };
    private static readonly Dictionary<string, Action<Utf8JsonWriter, Condition>> s_serialize = new()
    {
        {
            "Timed",
            (writer, cond) =>
            {
                TimedCondition.TimedState state = ((TimedCondition)cond).State;
                writer.WriteNumber(nameof(TimedCondition.TimedState.TimeLeft), state.TimeLeft);
                writer.WriteNumber(nameof(TimedCondition.TimedState.Duration), state.Duration);
            }
        },
        {
            "Resource",
            (writer, cond) =>
            {
                ResourceCondition.ResourceState state = ((ResourceCondition)cond).State;
                writer.WriteString(nameof(ResourceCondition.ResourceState.StatTypeId), state.StatTypeId);
                writer.WriteString(nameof(ResourceCondition.ResourceState.CompareOp), state.CompareOp.ToString());
                writer.WriteNumber(nameof(ResourceCondition.ResourceState.TargetValue), state.TargetValue);
                writer.WriteBoolean(nameof(ResourceCondition.ResourceState.IsPercent), state.IsPercent);
            }
        },
    };

    public static void Register(string typeId, DeserializeDel deser, Action<Utf8JsonWriter, Condition> ser)
    {
        s_deserialize.Add(typeId, deser);
        s_serialize.Add(typeId, ser);
    }

    public static Condition DefaultRead(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        Utf8JsonReader scan = reader;
        string? typeId = null;

        while (scan.Read())
        {
            if (scan.TokenType == JsonTokenType.EndObject)
                break;
            else if (scan.TokenType != JsonTokenType.PropertyName)
                continue;

            string? propName = scan.GetString();
            scan.Read();

            if (propName == "ConditionType")
            {
                typeId = scan.GetString();
                break;
            }
            else
            {
                scan.Skip();
            }
        }

        if (typeId == null)
            throw new JsonException("Missing type discriminator");

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        Condition cond = ConditionDB.GetNew(typeId);
        bool hasState = cond is ICondition;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            string propName = reader.GetString() ?? string.Empty;
            reader.Read();

            if (hasState && propName == nameof(Condition<>.State))
            {
                s_deserialize[typeId](ref reader, cond);
                continue;
            }

            if (propName == "ConditionType")
                continue;
            else if (propName == nameof(Condition.AutoRefresh))
                cond.AutoRefresh = reader.GetBoolean();
            else if (propName == nameof(Condition.SourceIgnored))
                cond.SourceIgnored = reader.GetBoolean();
            else if (propName == nameof(Condition.And))
                cond.And = DefaultRead(ref reader);
            else if (propName == nameof(Condition.Or))
                cond.Or = DefaultRead(ref reader);
            else
                reader.Skip();
        }

        return cond;
    }

    public override Condition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DefaultRead(ref reader);
    }

    public static void WriteDefault(Utf8JsonWriter writer, Condition cond)
    {
        string typeId = cond.GetTypeId();
        writer.WriteStartObject();
        writer.WriteString("ConditionType", typeId);

        if (cond.AutoRefresh)
            writer.WriteBoolean(nameof(Condition.AutoRefresh), cond.AutoRefresh);

        if (cond.SourceIgnored)
            writer.WriteBoolean(nameof(Condition.SourceIgnored), cond.SourceIgnored);

        if (cond is ICondition)
        {
            writer.WritePropertyName(nameof(Condition<>.State));
            writer.WriteStartObject();
            s_serialize[typeId](writer, cond);
            writer.WriteEndObject();
        }

        if (cond.And != null)
        {
            writer.WritePropertyName(nameof(Condition.And));
            WriteDefault(writer, cond.And);
        }

        if (cond.Or != null)
        {
            writer.WritePropertyName(nameof(Condition.Or));
            WriteDefault(writer, cond.Or);
        }

        writer.WriteEndObject();
    }

    public override void Write(Utf8JsonWriter writer, Condition cond, JsonSerializerOptions options)
    {
        WriteDefault(writer, cond);
    }
}