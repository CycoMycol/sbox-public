namespace Editor.PluginBuilder;

/// <summary>
/// Real-time validation as the user edits the blueprint.
/// Detects: attribute conflicts, circular references, duplicate names, reserved words, empty groups.
/// </summary>
public static class BlueprintValidator
{
	public static List<ValidationIssue> Validate( PluginBlueprint blueprint )
	{
		var issues = new List<ValidationIssue>();

		if ( blueprint == null ) return issues;

		ValidateElements( blueprint.Elements, issues, blueprint );
		ValidateCircularReferences( blueprint.Elements, issues );
		ValidateCustomEnums( blueprint.CustomEnums, issues );
		ValidateCustomStructs( blueprint.CustomStructs, issues );

		return issues;
	}

	private static void ValidateElements( List<BlueprintElement> elements, List<ValidationIssue> issues, PluginBlueprint blueprint, string scope = "" )
	{
		var names = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var el in elements )
		{
			var path = string.IsNullOrEmpty( scope ) ? el.Name : $"{scope}.{el.Name}";

			// Missing name
			if ( string.IsNullOrWhiteSpace( el.Name ) && el.ElementType != ElementType.Space )
			{
				issues.Add( new ValidationIssue( ValidationSeverity.Error, el, "Element has no name." ) );
			}

			// Duplicate names
			if ( !string.IsNullOrEmpty( el.Name ) && !names.Add( el.Name ) )
			{
				issues.Add( new ValidationIssue( ValidationSeverity.Error, el, $"Duplicate name \"{el.Name}\" in scope \"{scope}\"." ) );
			}

			// Reserved C# keywords
			if ( !string.IsNullOrEmpty( el.Name ) && IsCSharpReservedWord( el.Name ) )
			{
				issues.Add( new ValidationIssue( ValidationSeverity.Error, el, $"\"{el.Name}\" is a C# reserved word. Suggestion: \"{el.Name}Value\"" ) );
			}

			// Empty groups
			if ( (el.ElementType == ElementType.Group || el.ElementType == ElementType.ToggleGroup || el.ElementType == ElementType.Feature) && el.Children.Count == 0 )
			{
				issues.Add( new ValidationIssue( ValidationSeverity.Warning, el, $"Group \"{el.Name}\" has no children." ) );
			}

			// Attribute conflicts
			ValidateAttributeConflicts( el, issues, blueprint );

			// Recurse children
			if ( el.Children.Count > 0 )
				ValidateElements( el.Children, issues, blueprint, path );
		}
	}

	private static void ValidateAttributeConflicts( BlueprintElement el, List<ValidationIssue> issues, PluginBlueprint blueprint )
	{
		if ( el.Attributes.ContainsKey( "ReadOnly" ) && el.Attributes.ContainsKey( "Range" ) )
			issues.Add( new ValidationIssue( ValidationSeverity.Warning, el, "[ReadOnly] + [Range] — slider will be non-interactive." ) );

		if ( el.Attributes.ContainsKey( "TextArea" ) && el.PropertyType != PropertyType.String )
			issues.Add( new ValidationIssue( ValidationSeverity.Error, el, "[TextArea] only applies to string properties." ) );

		if ( el.Attributes.ContainsKey( "FontName" ) && el.PropertyType != PropertyType.String )
			issues.Add( new ValidationIssue( ValidationSeverity.Error, el, "[FontName] only applies to string properties." ) );

		if ( el.Attributes.TryGetValue( "Range", out var rangeParams ) )
		{
			var min = GetFloat( rangeParams, "min" );
			var max = GetFloat( rangeParams, "max" );
			if ( min.HasValue && max.HasValue && min.Value >= max.Value )
				issues.Add( new ValidationIssue( ValidationSeverity.Error, el, $"[Range] min ({min.Value}) >= max ({max.Value})." ) );
		}

		if ( el.Attributes.TryGetValue( "ShowIf", out var showIfParams ) )
		{
			if ( showIfParams.TryGetValue( "property", out var targetObj ) )
			{
				var target = targetObj?.ToString();
				if ( !string.IsNullOrEmpty( target ) )
				{
					var allElements = GetAllElements( blueprint.Elements );
					if ( !allElements.Any( e => e.Name == target ) )
						issues.Add( new ValidationIssue( ValidationSeverity.Error, el, $"[ShowIf] references \"{target}\" which does not exist." ) );
				}
			}
		}

		if ( el.Attributes.TryGetValue( "HideIf", out var hideIfParams ) )
		{
			if ( hideIfParams.TryGetValue( "property", out var targetObj ) )
			{
				var target = targetObj?.ToString();
				if ( !string.IsNullOrEmpty( target ) )
				{
					var allElements = GetAllElements( blueprint.Elements );
					if ( !allElements.Any( e => e.Name == target ) )
						issues.Add( new ValidationIssue( ValidationSeverity.Error, el, $"[HideIf] references \"{target}\" which does not exist." ) );
				}
			}
		}
	}

	/// <summary>
	/// DFS cycle detection for ShowIf/HideIf circular references.
	/// </summary>
	private static void ValidateCircularReferences( List<BlueprintElement> elements, List<ValidationIssue> issues )
	{
		var allElements = GetAllElements( elements );
		var graph = new Dictionary<string, List<string>>();

		foreach ( var el in allElements )
		{
			var targets = new List<string>();

			if ( el.Attributes.TryGetValue( "ShowIf", out var showIf ) &&
				showIf.TryGetValue( "property", out var showTarget ) )
				targets.Add( showTarget?.ToString() );

			if ( el.Attributes.TryGetValue( "HideIf", out var hideIf ) &&
				hideIf.TryGetValue( "property", out var hideTarget ) )
				targets.Add( hideTarget?.ToString() );

			if ( targets.Count > 0 )
				graph[el.Name] = targets.Where( t => !string.IsNullOrEmpty( t ) ).ToList();
		}

		// DFS for cycles
		var visited = new HashSet<string>();
		var inStack = new HashSet<string>();

		foreach ( var node in graph.Keys )
		{
			if ( !visited.Contains( node ) )
			{
				var cyclePath = new List<string>();
				if ( DfsCycleDetect( node, graph, visited, inStack, cyclePath ) )
				{
					cyclePath.Reverse();
					var cycleStr = string.Join( " → ", cyclePath );
					var element = allElements.FirstOrDefault( e => e.Name == node );
					issues.Add( new ValidationIssue( ValidationSeverity.Error, element, $"Circular reference: {cycleStr}" ) );
				}
			}
		}
	}

	private static bool DfsCycleDetect( string node, Dictionary<string, List<string>> graph,
		HashSet<string> visited, HashSet<string> inStack, List<string> cyclePath )
	{
		visited.Add( node );
		inStack.Add( node );

		if ( graph.TryGetValue( node, out var neighbors ) )
		{
			foreach ( var neighbor in neighbors )
			{
				if ( !visited.Contains( neighbor ) )
				{
					if ( DfsCycleDetect( neighbor, graph, visited, inStack, cyclePath ) )
					{
						cyclePath.Add( node );
						return true;
					}
				}
				else if ( inStack.Contains( neighbor ) )
				{
					cyclePath.Add( neighbor );
					cyclePath.Add( node );
					return true;
				}
			}
		}

		inStack.Remove( node );
		return false;
	}

	private static void ValidateCustomEnums( List<CustomEnumDefinition> enums, List<ValidationIssue> issues )
	{
		foreach ( var enumDef in enums )
		{
			if ( string.IsNullOrWhiteSpace( enumDef.Name ) )
				issues.Add( new ValidationIssue( ValidationSeverity.Error, null, "Custom enum has no name." ) );

			if ( IsCSharpReservedWord( enumDef.Name ) )
				issues.Add( new ValidationIssue( ValidationSeverity.Error, null, $"Enum name \"{enumDef.Name}\" is a C# reserved word." ) );

			var valueNames = new HashSet<string>();
			var valueInts = new HashSet<int>();

			foreach ( var val in enumDef.Values )
			{
				if ( !valueNames.Add( val.Name ) )
					issues.Add( new ValidationIssue( ValidationSeverity.Error, null, $"Duplicate enum value name \"{val.Name}\" in {enumDef.Name}." ) );

				if ( !valueInts.Add( val.IntValue ) )
					issues.Add( new ValidationIssue( ValidationSeverity.Warning, null, $"Duplicate int value {val.IntValue} in {enumDef.Name}." ) );

				if ( IsCSharpReservedWord( val.Name ) )
					issues.Add( new ValidationIssue( ValidationSeverity.Error, null, $"Enum value \"{val.Name}\" is a C# reserved word." ) );
			}
		}
	}

	private static void ValidateCustomStructs( List<CustomStructDefinition> structs, List<ValidationIssue> issues )
	{
		foreach ( var structDef in structs )
		{
			if ( string.IsNullOrWhiteSpace( structDef.Name ) )
				issues.Add( new ValidationIssue( ValidationSeverity.Error, null, "Custom struct has no name." ) );

			if ( IsCSharpReservedWord( structDef.Name ) )
				issues.Add( new ValidationIssue( ValidationSeverity.Error, null, $"Struct name \"{structDef.Name}\" is a C# reserved word." ) );
		}
	}

	private static List<BlueprintElement> GetAllElements( List<BlueprintElement> elements )
	{
		var all = new List<BlueprintElement>();
		foreach ( var el in elements )
		{
			all.Add( el );
			if ( el.Children.Count > 0 )
				all.AddRange( GetAllElements( el.Children ) );
		}
		return all;
	}

	private static readonly HashSet<string> CSharpKeywords = new( StringComparer.OrdinalIgnoreCase )
	{
		"abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
		"class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
		"enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
		"foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
		"long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
		"private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
		"sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
		"try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
		"void", "volatile", "while"
	};

	public static bool IsCSharpReservedWord( string name ) => CSharpKeywords.Contains( name );

	private static float? GetFloat( Dictionary<string, object> dict, string key )
	{
		if ( !dict.TryGetValue( key, out var val ) ) return null;
		if ( val is float f ) return f;
		if ( val is double d ) return (float)d;
		if ( val is int i ) return i;
		if ( val is string s && float.TryParse( s, out var p ) ) return p;
		if ( val is JsonElement je && je.ValueKind == JsonValueKind.Number ) return je.GetSingle();
		return null;
	}
}

public class ValidationIssue
{
	public ValidationSeverity Severity { get; }
	public BlueprintElement Element { get; }
	public string Message { get; }

	public ValidationIssue( ValidationSeverity severity, BlueprintElement element, string message )
	{
		Severity = severity;
		Element = element;
		Message = message;
	}
}

public enum ValidationSeverity
{
	Warning,
	Error
}
