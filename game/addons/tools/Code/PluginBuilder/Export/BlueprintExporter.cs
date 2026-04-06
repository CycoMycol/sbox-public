using System.Text;

using System.Text.Json.Serialization;

namespace Editor.PluginBuilder;

/// <summary>
/// Serializes a blueprint to export files: JSON, Markdown, and C# stub.
/// </summary>
public static class BlueprintExporter
{
	/// <summary>
	/// Export the blueprint as a structured JSON spec.
	/// </summary>
	public static string ExportJson( PluginBlueprint blueprint )
	{
		return JsonSerializer.Serialize( blueprint, new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		} );
	}

	/// <summary>
	/// Export the blueprint as a human-readable Markdown document.
	/// </summary>
	public static string ExportMarkdown( PluginBlueprint blueprint )
	{
		var sb = new StringBuilder();

		sb.AppendLine( $"# {blueprint.Name} {blueprint.Type}" );
		sb.AppendLine( $"**Type:** {blueprint.Type}  " );
		sb.AppendLine( $"**Base Class:** {blueprint.BaseClass}  " );

		if ( !string.IsNullOrEmpty( blueprint.Description ) )
		{
			sb.AppendLine();
			sb.AppendLine( "## Description" );
			sb.AppendLine( blueprint.Description );
		}

		sb.AppendLine();
		sb.AppendLine( "## Properties" );
		WriteElementsMarkdown( sb, blueprint.Elements, 0 );

		if ( blueprint.CustomEnums.Count > 0 )
		{
			sb.AppendLine();
			sb.AppendLine( "## Custom Enums" );
			foreach ( var e in blueprint.CustomEnums )
			{
				sb.AppendLine( $"### {e.Name}{(e.IsFlags ? " [Flags]" : "")}" );
				if ( !string.IsNullOrEmpty( e.Description ) )
					sb.AppendLine( e.Description );
				foreach ( var v in e.Values )
					sb.AppendLine( $"- **{v.Name}** = {v.IntValue}{(!string.IsNullOrEmpty( v.Description ) ? $" — {v.Description}" : "")}" );
			}
		}

		if ( blueprint.CustomStructs.Count > 0 )
		{
			sb.AppendLine();
			sb.AppendLine( "## Custom Structs" );
			foreach ( var s in blueprint.CustomStructs )
			{
				sb.AppendLine( $"### {s.Name}" );
				if ( !string.IsNullOrEmpty( s.Description ) )
					sb.AppendLine( s.Description );
				foreach ( var f in s.Fields )
					sb.AppendLine( $"- **{f.Name}** ({f.PropertyType})" );
			}
		}

		if ( !string.IsNullOrEmpty( blueprint.AiInstructions ) )
		{
			sb.AppendLine();
			sb.AppendLine( "## AI Instructions" );
			sb.AppendLine( blueprint.AiInstructions );
		}

		return sb.ToString();
	}

	private static void WriteElementsMarkdown( StringBuilder sb, List<BlueprintElement> elements, int depth )
	{
		var indent = new string( ' ', depth * 2 );

		foreach ( var el in elements )
		{
			switch ( el.ElementType )
			{
				case ElementType.Group:
					sb.AppendLine( $"{indent}### Group: {el.Name}" );
					WriteElementsMarkdown( sb, el.Children, depth + 1 );
					break;

				case ElementType.ToggleGroup:
					sb.AppendLine( $"{indent}### Toggle Group: {el.Name}" );
					WriteElementsMarkdown( sb, el.Children, depth + 1 );
					break;

				case ElementType.Feature:
					sb.AppendLine( $"{indent}### Feature: {el.Name}" );
					WriteElementsMarkdown( sb, el.Children, depth + 1 );
					break;

				case ElementType.Header:
					sb.AppendLine( $"{indent}#### {el.Name}" );
					break;

				case ElementType.Property:
					var attrList = el.Attributes.Count > 0
						? string.Join( ", ", el.Attributes.Select( a => FormatAttrMarkdown( a.Key, a.Value ) ) )
						: "";
					var defaultStr = el.DefaultValue != null && el.DefaultValue.ValueType != DefaultValueType.Null
						? $", Default: {el.DefaultValue.ToCSharpLiteral()}"
						: "";
					var aiHint = !string.IsNullOrEmpty( el.AiHint ) ? $" — *AI: {el.AiHint}*" : "";
					sb.AppendLine( $"{indent}- **{el.Name}** ({el.PropertyType}){(attrList != "" ? $" — {attrList}" : "")}{defaultStr}{aiHint}" );
					break;
			}
		}
	}

	private static string FormatAttrMarkdown( string name, Dictionary<string, object> parameters )
	{
		if ( parameters.Count == 0 ) return name;

		var paramStr = string.Join( ", ", parameters.Select( p => $"{p.Key}: {p.Value}" ) );
		return $"{name}({paramStr})";
	}
}
