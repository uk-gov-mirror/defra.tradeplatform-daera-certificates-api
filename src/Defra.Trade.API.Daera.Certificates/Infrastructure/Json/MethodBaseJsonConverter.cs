// Copyright DEFRA (c). All rights reserved.
// Licensed under the Open Government License v3.0.

#nullable enable

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Trade.API.Daera.Certificates.Infrastructure.Json;

/// <summary>
/// Prevents <see cref="System.Text.Json"/> from throwing <see cref="NotSupportedException"/>
/// when serializing <see cref="MethodBase"/> instances (e.g. <see cref="Exception.TargetSite"/>).
/// Writes <c>null</c> in place of any <see cref="MethodBase"/> value.
/// </summary>
internal sealed class MethodBaseJsonConverter : JsonConverter<MethodBase>
{
    // The default CanConvert only matches the exact type (typeof(MethodBase)).
    // At runtime, Exception.TargetSite is always a concrete subtype such as
    // RuntimeMethodInfo or RuntimeConstructorInfo, so we must match any derived type.
    public override bool CanConvert(Type typeToConvert)
        => typeof(MethodBase).IsAssignableFrom(typeToConvert);

    public override MethodBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => null;

    public override void Write(Utf8JsonWriter writer, MethodBase value, JsonSerializerOptions options)
        => writer.WriteNullValue();
}
