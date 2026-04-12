namespace Sandbox.Internal;

/// <summary>
/// Interface for a control sheet that manages the display of serialized properties in a structured way.
/// </summary>
[Expose]
public interface IControlSheet
{
	/// <summary>
	/// Adds properties to the control sheet, filtering them based on the provided filter function.
	/// </summary>
	public static void FilterSortAndAdd( IControlSheet sheet, List<SerializedProperty> q, bool allowFeatures = true )
	{
		var props = q.Where( sheet.TestFilter )
						.OrderBy( x => x.Order )
						.ThenBy( x => x.SourceFile )
						.ThenBy( x => x.SourceLine )
						.ToList();

		AddProperties( sheet, props, allowFeatures );
	}

	/// <summary>
	/// Add properties to a controlsheet, with a minimal of filtering and no sorting.
	/// </summary>
	public static void AddProperties( IControlSheet sheet, List<SerializedProperty> properties, bool allowFeatures = true )
	{
		sheet.RemoveUnusedMethods( properties );

		if ( allowFeatures )
		{
			string defaultFeature = "";

			//
			// If we have features then we group up into feature tabs
			//
			var features = properties.GroupBy( x => x.GetAttributes<FeatureAttribute>().FirstOrDefault()?.Identifier ?? defaultFeature ).ToDictionary( x => x.Key, x => x.ToList() );
			if ( features.Count > 1 || (features.FirstOrDefault().Key ?? defaultFeature) != defaultFeature )
			{
				// Split DecoratorOnly carrier properties (e.g. [Header]) into two buckets:
				//   preFeat  — srcLine before the first Feature-tagged property  → render above tab bar
				//   postFeat — srcLine at/after the first Feature-tagged property → render below tab bar
				// This preserves the declaration order: Header1, Feature, Header2 renders as
				// Header1 / [tab bar] / Header2 in the inspector.
				int minFeatureLine = features
					.Where( f => f.Key != defaultFeature )
					.SelectMany( f => f.Value )
					.Select( p => p.SourceLine )
					.DefaultIfEmpty( int.MaxValue )
					.Min();

				var allDecorators = properties
					.Where( p => p.TryGetAttribute<EditorAttribute>( out var ea ) && ea.Value == "DecoratorOnly" )
					.ToList();

				var preFeat  = allDecorators.Where( p => p.SourceLine <  minFeatureLine ).ToList();
				var postFeat = allDecorators.Where( p => p.SourceLine >= minFeatureLine ).ToList();

				// Remove all decorators from their feature groups before adding tabs
				foreach ( var featureGroup in features.Values )
					featureGroup.RemoveAll( p => p.TryGetAttribute<EditorAttribute>( out var ea ) && ea.Value == "DecoratorOnly" );

				if ( preFeat.Count > 0 )
					sheet.AddPropertiesWithGrouping( preFeat );

				foreach ( var feature in features.Where( f => f.Value.Count > 0 ) )
				{
					var csf = new Feature( feature.Value );
					sheet.AddFeature( csf );
				}

				if ( postFeat.Count > 0 )
					sheet.AddPropertiesWithGrouping( postFeat );

				return;
			}
		}

		//
		// No features - just flat, normal groups
		//
		sheet.AddPropertiesWithGrouping( properties );
	}

	/// <summary>
	/// Remove methods that we have no hope of displaying
	/// </summary>
	void RemoveUnusedMethods( List<SerializedProperty> properties )
	{
		properties.RemoveAll( x => x.IsMethod && !x.HasAttribute<ButtonAttribute>() );
	}

	void AddPropertiesWithGrouping( List<SerializedProperty> properties )
	{
		RemoveUnusedMethods( properties );

		var grouped = properties.GroupBy( x => x.GroupName ?? null ).ToList();

		foreach ( var group in grouped.OrderBy( x => x.Key != null ).ThenBy( x => x.Max( y => y.Order ) ).ThenBy( x => x.Key ) )
		{
			var csg = new Group( group.ToList() );
			AddGroup( csg );
		}
	}

	/// <summary>
	/// We're adding a feature. Normally would store these in a tab control
	/// </summary>
	void AddFeature( Feature feature );

	/// <summary>
	/// We're adding a group. Normally would have a Group Panel with the properties as children
	/// </summary>
	void AddGroup( Group group );

	/// <summary>
	/// Implement to filter properties that should be displayed in the control sheet.
	/// </summary>
	bool TestFilter( SerializedProperty prop );

	/// <summary>
	/// A feature is usually displayed as a tab, to break things up in the inspector. They can sometimes be turned on and off.
	/// </summary>
	public sealed class Feature
	{
		/// <summary>
		/// The name of the feature, usually displayed as a tab title in the inspector.
		/// </summary>
		public string Name { get; init; }

		/// <summary>
		/// The description of the feature
		/// </summary>
		public string Description { get; init; }

		/// <summary>
		/// The icon of the feature
		/// </summary>
		public string Icon { get; init; }

		/// <summary>
		/// Allows tinting this feature, for some reason
		/// </summary>
		public EditorTint Tint { get; init; }

		/// <summary>
		/// The properties that are part of this feature, usually displayed together in the inspector.
		/// </summary>
		public List<SerializedProperty> Properties { get; init; }

		/// <summary>
		/// If we have a FeatureEnabled property, this will be it. If not then we assume it should always be enabled.
		/// </summary>
		public SerializedProperty EnabledProperty { get; init; }

		public Feature( List<SerializedProperty> properties )
		{
			Properties = properties;

			Name = properties.Select( x => x.GetAttributes<FeatureAttribute>().FirstOrDefault() )
						.Where( x => x is not null )
						.Select( x => x.Title )
						.Where( x => !string.IsNullOrWhiteSpace( x ) )
						.FirstOrDefault() ?? "";

			Description = properties.Select( x => x.GetAttributes<FeatureAttribute>().FirstOrDefault() )
						.Where( x => x is not null )
						.Select( x => x.Description )
						.Where( x => !string.IsNullOrWhiteSpace( x ) )
						.FirstOrDefault() ?? "";

			Icon = properties.Select( x => x.GetAttributes<FeatureAttribute>().FirstOrDefault() )
						.Where( x => x is not null )
						.Select( x => x.Icon )
						.Where( x => !string.IsNullOrWhiteSpace( x ) )
						.FirstOrDefault() ?? "";

			Tint = properties.Select( x => x.GetAttributes<FeatureAttribute>().FirstOrDefault() )
						.Where( x => x is not null )
						.Select( x => x.Tint )
						.Where( x => x != EditorTint.White )
						.FirstOrDefault( EditorTint.White );

			EnabledProperty = Properties.Where( x => x.GetAttributes<FeatureEnabledAttribute>().Any() ).FirstOrDefault();
		}
	}

	/// <summary>
	/// A group is a collection of properties that are related to each other, and can be displayed together in the inspector, usually with a title.
	/// </summary>
	public sealed class Group
	{
		/// <summary>
		/// The name of the group, usually displayed as a title in the inspector.
		/// </summary>
		public string Name { get; init; }

		/// <summary>
		/// The properties that are part of this group, usually displayed together in the inspector.
		/// </summary>
		public List<SerializedProperty> Properties { get; init; }

		public Group( List<SerializedProperty> properties )
		{
			Properties = properties;
			Name = properties.FirstOrDefault()?.GroupName ?? "";
		}
	}
}
