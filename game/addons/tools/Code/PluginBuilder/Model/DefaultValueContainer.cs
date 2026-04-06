using System.Text.Json.Serialization;

namespace Editor.PluginBuilder;

/// <summary>
/// Discriminated wrapper for property default values.
/// Ensures System.Text.Json roundtrips correctly — raw object would deserialize as JsonElement.
/// </summary>
[JsonConverter( typeof( DefaultValueContainerConverter ) )]
public class DefaultValueContainer
{
	public DefaultValueType ValueType { get; set; } = DefaultValueType.Null;
	public string StringValue { get; set; }
	public int IntValue { get; set; }
	public float FloatValue { get; set; }
	public bool BoolValue { get; set; }
	public float[] ColorValue { get; set; } // r, g, b, a
	public float[] Vector3Value { get; set; } // x, y, z
	public string EnumValue { get; set; }

	public static DefaultValueContainer FromNull() => new() { ValueType = DefaultValueType.Null };
	public static DefaultValueContainer FromString( string v ) => new() { ValueType = DefaultValueType.String, StringValue = v };
	public static DefaultValueContainer FromInt( int v ) => new() { ValueType = DefaultValueType.Int, IntValue = v };
	public static DefaultValueContainer FromFloat( float v ) => new() { ValueType = DefaultValueType.Float, FloatValue = v };
	public static DefaultValueContainer FromBool( bool v ) => new() { ValueType = DefaultValueType.Bool, BoolValue = v };
	public static DefaultValueContainer FromColor( float r, float g, float b, float a ) => new() { ValueType = DefaultValueType.Color, ColorValue = new[] { r, g, b, a } };
	public static DefaultValueContainer FromVector3( float x, float y, float z ) => new() { ValueType = DefaultValueType.Vector3, Vector3Value = new[] { x, y, z } };
	public static DefaultValueContainer FromEnum( string v ) => new() { ValueType = DefaultValueType.Enum, EnumValue = v };

	public string ToCSharpLiteral()
	{
		return ValueType switch
		{
			DefaultValueType.Null => "default",
			DefaultValueType.String => $"\"{StringValue}\"",
			DefaultValueType.Int => IntValue.ToString(),
			DefaultValueType.Float => $"{FloatValue}f",
			DefaultValueType.Bool => BoolValue ? "true" : "false",
			DefaultValueType.Color => $"new Color( {ColorValue[0]}f, {ColorValue[1]}f, {ColorValue[2]}f, {ColorValue[3]}f )",
			DefaultValueType.Vector3 => $"new Vector3( {Vector3Value[0]}f, {Vector3Value[1]}f, {Vector3Value[2]}f )",
			DefaultValueType.Enum => EnumValue ?? "default",
			_ => "default"
		};
	}
}

public enum DefaultValueType
{
	Null,
	String,
	Int,
	Float,
	Bool,
	Color,
	Vector3,
	Enum
}

/// <summary>
/// Custom JSON converter for DefaultValueContainer — handles the discriminated union correctly.
/// </summary>
public class DefaultValueContainerConverter : JsonConverter<DefaultValueContainer>
{
	public override DefaultValueContainer Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		if ( reader.TokenType == JsonTokenType.Null )
			return null;

		using var doc = JsonDocument.ParseValue( ref reader );
		var root = doc.RootElement;

		if ( !root.TryGetProperty( "valueType", out var typeEl ) && !root.TryGetProperty( "ValueType", out typeEl ) )
			return null;

		var valueType = (DefaultValueType)typeEl.GetInt32();
		var container = new DefaultValueContainer { ValueType = valueType };

		switch ( valueType )
		{
			case DefaultValueType.String:
				container.StringValue = GetString( root, "stringValue", "StringValue" );
				break;
			case DefaultValueType.Int:
				container.IntValue = GetInt( root, "intValue", "IntValue" );
				break;
			case DefaultValueType.Float:
				container.FloatValue = GetFloat( root, "floatValue", "FloatValue" );
				break;
			case DefaultValueType.Bool:
				container.BoolValue = GetBool( root, "boolValue", "BoolValue" );
				break;
			case DefaultValueType.Color:
				container.ColorValue = GetFloatArray( root, "colorValue", "ColorValue" );
				break;
			case DefaultValueType.Vector3:
				container.Vector3Value = GetFloatArray( root, "vector3Value", "Vector3Value" );
				break;
			case DefaultValueType.Enum:
				container.EnumValue = GetString( root, "enumValue", "EnumValue" );
				break;
		}

		return container;
	}

	public override void Write( Utf8JsonWriter writer, DefaultValueContainer value, JsonSerializerOptions options )
	{
		if ( value == null )
		{
			writer.WriteNullValue();
			return;
		}

		writer.WriteStartObject();
		writer.WriteNumber( "valueType", (int)value.ValueType );

		switch ( value.ValueType )
		{
			case DefaultValueType.String:
				writer.WriteString( "stringValue", value.StringValue );
				break;
			case DefaultValueType.Int:
				writer.WriteNumber( "intValue", value.IntValue );
				break;
			case DefaultValueType.Float:
				writer.WriteNumber( "floatValue", value.FloatValue );
				break;
			case DefaultValueType.Bool:
				writer.WriteBoolean( "boolValue", value.BoolValue );
				break;
			case DefaultValueType.Color:
				WriteFloatArray( writer, "colorValue", value.ColorValue );
				break;
			case DefaultValueType.Vector3:
				WriteFloatArray( writer, "vector3Value", value.Vector3Value );
				break;
			case DefaultValueType.Enum:
				writer.WriteString( "enumValue", value.EnumValue );
				break;
		}

		writer.WriteEndObject();
	}

	private static string GetString( JsonElement el, string camel, string pascal )
	{
		if ( el.TryGetProperty( camel, out var v ) || el.TryGetProperty( pascal, out v ) )
			return v.GetString();
		return null;
	}

	private static int GetInt( JsonElement el, string camel, string pascal )
	{
		if ( el.TryGetProperty( camel, out var v ) || el.TryGetProperty( pascal, out v ) )
			return v.GetInt32();
		return 0;
	}

	private static float GetFloat( JsonElement el, string camel, string pascal )
	{
		if ( el.TryGetProperty( camel, out var v ) || el.TryGetProperty( pascal, out v ) )
			return v.GetSingle();
		return 0f;
	}

	private static bool GetBool( JsonElement el, string camel, string pascal )
	{
		if ( el.TryGetProperty( camel, out var v ) || el.TryGetProperty( pascal, out v ) )
			return v.GetBoolean();
		return false;
	}

	private static float[] GetFloatArray( JsonElement el, string camel, string pascal )
	{
		if ( el.TryGetProperty( camel, out var v ) || el.TryGetProperty( pascal, out v ) )
		{
			var arr = new float[v.GetArrayLength()];
			for ( int i = 0; i < arr.Length; i++ )
				arr[i] = v[i].GetSingle();
			return arr;
		}
		return null;
	}

	private static void WriteFloatArray( Utf8JsonWriter writer, string name, float[] arr )
	{
		if ( arr == null )
		{
			writer.WriteNull( name );
			return;
		}

		writer.WriteStartArray( name );
		foreach ( var f in arr )
			writer.WriteNumberValue( f );
		writer.WriteEndArray();
	}
}
