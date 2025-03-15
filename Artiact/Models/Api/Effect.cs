﻿using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class Effect
{
    [JsonPropertyName( "code" )]
    public string Code { get; set; }

    [JsonPropertyName( "value" )]
    public int Value { get; set; }
}