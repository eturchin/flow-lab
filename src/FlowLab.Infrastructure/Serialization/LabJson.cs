using System.Text.Json;
using System.Text.Json.Serialization;
using FlowLab.Application;
using FlowLab.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlowLab.Infrastructure;

public static class LabJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
