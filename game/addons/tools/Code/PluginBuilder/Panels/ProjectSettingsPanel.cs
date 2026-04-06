using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Settings panel for creating/editing a blueprint's project-level properties.
/// Name, type, description, base class, icon.
/// </summary>
public class ProjectSettingsPanel : Widget
{
	private readonly PluginBlueprint _blueprint;

	public Action OnCreateClicked { get; set; }

	public ProjectSettingsPanel( Widget parent, PluginBlueprint blueprint ) : base( parent )
	{
		_blueprint = blueprint;

		Layout = Layout.Column();
		Layout.Margin = 16;
		Layout.Spacing = 8;

		var heading = Layout.Add( new Label( "New Blueprint", this ) );
		heading.SetStyles( "font-size: 18px; font-weight: bold;" );

		// Name
		Layout.Add( new Label( "Name", this ) );
		var nameEntry = Layout.Add( new LineEdit( this ) );
		nameEntry.Text = _blueprint.Name;
		nameEntry.TextChanged += ( val ) => _blueprint.Name = val;

		// Type
		Layout.Add( new Label( "Type", this ) );
		var typeDropdown = new ComboBox( this );
		foreach ( var t in Enum.GetValues<BlueprintType>() )
			typeDropdown.AddItem( t.ToString(), null, () => _blueprint.Type = t );
		typeDropdown.CurrentIndex = (int)_blueprint.Type;
		Layout.Add( typeDropdown );

		// Base Class
		Layout.Add( new Label( "Base Class", this ) );
		var baseClassEntry = Layout.Add( new LineEdit( this ) );
		baseClassEntry.Text = _blueprint.BaseClass;
		baseClassEntry.TextChanged += ( val ) => _blueprint.BaseClass = val;

		// Description
		Layout.Add( new Label( "Description", this ) );
		var descEntry = Layout.Add( new TextEdit( this ) );
		descEntry.MinimumHeight = 120;
		descEntry.PlainText = _blueprint.Description;
		descEntry.TextChanged += (_) => _blueprint.Description = descEntry.PlainText;

		// Icon (simplified for now — Phase 21 adds the full picker)
		Layout.Add( new Label( "Icon (material icon name)", this ) );
		var iconEntry = Layout.Add( new LineEdit( this ) );
		iconEntry.Text = _blueprint.Icon;
		iconEntry.TextChanged += ( val ) => _blueprint.Icon = val;

		Layout.AddStretchCell( 1 );

		// Create button
		var createBtn = new Button( "Create Blueprint", "add_circle", this );
		createBtn.Clicked = () => OnCreateClicked?.Invoke();
		Layout.Add( createBtn );
	}
}
