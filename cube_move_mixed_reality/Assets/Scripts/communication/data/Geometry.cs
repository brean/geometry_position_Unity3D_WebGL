// generated using https://quicktype.io
// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//
//    var data = Geometry.FromJson(jsonString);
//

using System;
using System.Net;
using System.Collections.Generic;

using Newtonsoft.Json;

public partial class Geometry
{
    public Geometry(string Type, float[] Position, float[] Rotation)
    {
        this.Type = Type;
        this.Position = Position;
        this.Rotation = Rotation;
    }

    [JsonProperty("position")]
    public float[] Position { get; set; }

    [JsonProperty("rotation")]
    public float[] Rotation { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}

public partial class Geometry
{
    public static Geometry FromObject(dynamic data) {
        return new Geometry(data.type, data.position, data.rotation);
    }
}

public partial class Geometry
{
    public static Geometry FromJson(string json) => JsonConvert.DeserializeObject<Geometry>(json, Converter.Settings);
}

public static class Serialize
{
    public static string ToJson(this Geometry self) => JsonConvert.SerializeObject(self, Converter.Settings);
}

public class Converter
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling = DateParseHandling.None,
    };
}