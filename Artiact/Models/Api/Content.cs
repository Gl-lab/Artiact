﻿using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class Content
{
    [JsonPropertyName( "type" )]
    public string Type { get; set; }

    [JsonPropertyName( "code" )]
    public string Code { get; set; }
}