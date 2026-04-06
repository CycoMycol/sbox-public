using System.Text;

namespace Editor.PluginBuilder;

/// <summary>
/// Generates complete C# source files from a blueprint.
/// Emits valid Component/Tool/Addon classes with correct attributes.
/// </summary>
public static class CodeGenerator
{
	public static string Generate( PluginBlueprint blueprint )
	{
		var sb = new StringBuilder();

		// Usings
		sb.AppendLine( "using Sandbox;" );
		sb.AppendLine();

		// Custom enums
		foreach ( var enumDef in blueprint.CustomEnums )
		{
			GenerateEnum( sb, enumDef );
			sb.AppendLine();
		}

		// Custom structs
		foreach ( var structDef in blueprint.CustomStructs )
		{
			GenerateStruct( sb, structDef );
			sb.AppendLine();
		}

		// Class
		if ( !string.IsNullOrEmpty( blueprint.Description ) )
		{
			sb.AppendLine( "/// <summary>" );
			foreach ( var line in blueprint.Description.Split( '\n' ) )
				sb.AppendLine( $"/// {line.Trim()}" );
			sb.AppendLine( "/// </summary>" );
		}

		if ( !string.IsNullOrEmpty( blueprint.Icon ) )
			sb.AppendLine( $"[Icon( \"{blueprint.Icon}\" )]" );

		var baseClass = string.IsNullOrEmpty( blueprint.BaseClass ) ? "Component" : blueprint.BaseClass;
		sb.AppendLine( $"public sealed class {SanitizeName( blueprint.Name )} : {baseClass}" );
		sb.AppendLine( "{" );

		// Properties
		GenerateProperties( sb, blueprint.Elements, "\t" );

		// Lifecycle stubs
		sb.AppendLine();
		sb.AppendLine( "\tprotected override void OnStart()" );
		sb.AppendLine( "\t{" );
		if ( !string.IsNullOrEmpty( blueprint.AiInstructions ) )
		{
			sb.AppendLine( "\t\t// AI Instructions:" );
			foreach ( var line in blueprint.AiInstructions.Split( '\n' ) )
				sb.AppendLine( $"\t\t// {line.Trim()}" );
		}
		sb.AppendLine( "\t}" );

		sb.AppendLine();
		sb.AppendLine( "\tprotected override void OnUpdate()" );
		sb.AppendLine( "\t{" );
		sb.AppendLine( "\t}" );

		sb.AppendLine( "}" );

		return sb.ToString();
	}

	private static void GenerateProperties( StringBuilder sb, List<BlueprintElement> elements, string indent )
	{
		foreach ( var el in elements )
		{
			switch ( el.ElementType )
			{
				case ElementType.Header:
					sb.AppendLine( $"{indent}[Header( \"{Escape( el.Name )}\" )]" );
					break;

				case ElementType.Space:
					var height = "8";
					if ( el.Attributes.TryGetValue( "Space", out var spaceParams ) &&
						spaceParams.TryGetValue( "height", out var h ) )
						height = h?.ToString() ?? "8";
					sb.AppendLine( $"{indent}[Space( {height} )]" );
					break;

				case ElementType.Group:
					sb.AppendLine();
					sb.AppendLine( $"{indent}// Group: {el.Name}" );
					foreach ( var child in el.Children )
					{
						child.Attributes["Group"] = new Dictionary<string, object> { ["name"] = el.Name };
						GeneratePropertyDeclaration( sb, child, indent );
					}
					break;

				case ElementType.ToggleGroup:
					sb.AppendLine();
					sb.AppendLine( $"{indent}[Property, ToggleGroup( \"{Escape( el.Name )}\" )]" );
					sb.AppendLine( $"{indent}public bool {SanitizeName( el.Name )} {{ get; set; }}" );
					foreach ( var child in el.Children )
					{
						child.Attributes["Group"] = new Dictionary<string, object> { ["name"] = el.Name };
						child.Attributes["ShowIf"] = new Dictionary<string, object> { ["property"] = el.Name, ["value"] = true };
						GeneratePropertyDeclaration( sb, child, indent );
					}
					break;

				case ElementType.Feature:
					sb.AppendLine();
					sb.AppendLine( $"{indent}// Feature: {el.Name}" );
					sb.AppendLine( $"{indent}[Property, Feature( \"{Escape( el.Name )}\" )]" );
					sb.AppendLine( $"{indent}public bool {SanitizeName( el.Name )}Enabled {{ get; set; }}" );
					foreach ( var child in el.Children )
					{
						child.Attributes["Feature"] = new Dictionary<string, object> { ["name"] = el.Name };
						GeneratePropertyDeclaration( sb, child, indent );
					}
					break;

				case ElementType.Property:
					GeneratePropertyDeclaration( sb, el, indent );
					break;
			}
		}
	}

	private static void GeneratePropertyDeclaration( StringBuilder sb, BlueprintElement el, string indent )
	{
		// AI Hint as comment
		if ( !string.IsNullOrEmpty( el.AiHint ) )
		{
			sb.AppendLine( $"{indent}// AI Hint: {el.AiHint}" );
		}

		// Help text as Description
		if ( !string.IsNullOrEmpty( el.HelpText ) && !el.Attributes.ContainsKey( "Description" ) )
		{
			el.Attributes["Description"] = new Dictionary<string, object> { ["text"] = el.HelpText };
		}

		// Build attribute list
		var attrs = new List<string> { "Property" };

		foreach ( var (attrName, parameters) in el.Attributes )
		{
			attrs.Add( FormatAttribute( attrName, parameters ) );
		}

		sb.AppendLine( $"{indent}[{string.Join( ", ", attrs )}]" );

		// Property declaration
		var typeName = GetCSharpTypeName( el );
		var propName = SanitizeName( el.Name );
		var defaultStr = el.DefaultValue != null ? $" = {el.DefaultValue.ToCSharpLiteral()}" : "";
		var setter = el.Attributes.ContainsKey( "ReadOnly" ) ? "get;" : "get; set;";

		sb.AppendLine( $"{indent}public {typeName} {propName} {{ {setter} }}{(defaultStr != "" ? $" {defaultStr};" : "")}" );
		sb.AppendLine();
	}

	private static string FormatAttribute( string name, Dictionary<string, object> parameters )
	{
		if ( parameters.Count == 0 )
			return name;

		var def = AttributeCatalog.Get( name );
		if ( def == null )
			return name;

		var paramStrings = new List<string>();
		foreach ( var param in def.Parameters )
		{
			if ( parameters.TryGetValue( param.Name, out var val ) && val != null )
			{
				var valStr = param.ParamType switch
				{
					"string" => $"\"{Escape( val.ToString() )}\"",
					"bool" => val.ToString().ToLower(),
					_ => val.ToString()
				};
				paramStrings.Add( valStr );
			}
			else if ( param.Required )
			{
				paramStrings.Add( param.DefaultValue ?? "default" );
			}
		}

		return paramStrings.Count > 0 ? $"{name}( {string.Join( ", ", paramStrings )} )" : name;
	}

	private static string GetCSharpTypeName( BlueprintElement el )
	{
		return el.PropertyType switch
		{
			PropertyType.String => "string",
			PropertyType.Int => "int",
			PropertyType.Float => "float",
			PropertyType.Bool => "bool",
			PropertyType.Color => "Color",
			PropertyType.Vector3 => "Vector3",
			PropertyType.Vector2 => "Vector2",
			PropertyType.Angles => "Angles",
			PropertyType.Enum => "int", // generic enum fallback
			PropertyType.Model => "Model",
			PropertyType.Material => "Material",
			PropertyType.GameObject => "GameObject",
			PropertyType.CustomEnum => string.IsNullOrEmpty( el.CustomTypeName ) ? "int" : el.CustomTypeName,
			PropertyType.CustomStruct => string.IsNullOrEmpty( el.CustomTypeName ) ? "object" : el.CustomTypeName,
			_ => "object"
		};
	}

	private static void GenerateEnum( StringBuilder sb, CustomEnumDefinition enumDef )
	{
		if ( enumDef.IsFlags )
			sb.AppendLine( "[Flags]" );

		sb.AppendLine( $"public enum {SanitizeName( enumDef.Name )}" );
		sb.AppendLine( "{" );

		for ( int i = 0; i < enumDef.Values.Count; i++ )
		{
			var val = enumDef.Values[i];
			var comma = i < enumDef.Values.Count - 1 ? "," : "";

			if ( !string.IsNullOrEmpty( val.Description ) )
				sb.AppendLine( $"\t[Description( \"{Escape( val.Description )}\" )]" );
			if ( !string.IsNullOrEmpty( val.Icon ) )
				sb.AppendLine( $"\t[Icon( \"{Escape( val.Icon )}\" )]" );

			sb.AppendLine( $"\t{SanitizeName( val.Name )} = {val.IntValue}{comma}" );
		}

		sb.AppendLine( "}" );
	}

	private static void GenerateStruct( StringBuilder sb, CustomStructDefinition structDef )
	{
		if ( !string.IsNullOrEmpty( structDef.Description ) )
		{
			sb.AppendLine( "/// <summary>" );
			sb.AppendLine( $"/// {structDef.Description}" );
			sb.AppendLine( "/// </summary>" );
		}

		sb.AppendLine( $"public struct {SanitizeName( structDef.Name )}" );
		sb.AppendLine( "{" );

		foreach ( var field in structDef.Fields )
		{
			GeneratePropertyDeclaration( sb, field, "\t" );
		}

		sb.AppendLine( "}" );
	}

	public static string SanitizeName( string name )
	{
		if ( string.IsNullOrEmpty( name ) ) return "Unnamed";

		// Remove invalid chars, ensure starts with letter/underscore
		var sb = new StringBuilder();
		foreach ( var c in name )
		{
			if ( char.IsLetterOrDigit( c ) || c == '_' )
				sb.Append( c );
		}

		var result = sb.ToString();
		if ( result.Length == 0 ) return "Unnamed";
		if ( char.IsDigit( result[0] ) ) result = "_" + result;

		// PascalCase
		if ( char.IsLower( result[0] ) )
			result = char.ToUpper( result[0] ) + result.Substring( 1 );

		return result;
	}

	private static string Escape( string s ) => s?.Replace( "\\", "\\\\" ).Replace( "\"", "\\\"" ) ?? "";
}
