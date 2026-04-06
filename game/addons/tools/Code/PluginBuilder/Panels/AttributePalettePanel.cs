using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Categorized grid/list of all available S&Box attributes.
/// Drag from palette → drop onto Preview or Element Tree.
/// </summary>
public class AttributePalettePanel : Widget
{
	private readonly PluginBuilderDock _dock;
	private LineEdit _searchBar;
	private Widget _cardContainer;
	private AttributeCategory _selectedCategory = AttributeCategory.Layout;

	public AttributePalettePanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		var header = new Label( "Attribute Palette", this );
		header.SetStyles( "font-weight: bold; font-size: 14px;" );
		Layout.Add( header );

		// Search
		_searchBar = new LineEdit( this );
		_searchBar.PlaceholderText = "Search attributes...";
		_searchBar.TextChanged += ( _ ) => RebuildCards();
		Layout.Add( _searchBar );

		// Category tabs
		var tabs = new Widget( this );
		tabs.Layout = Layout.Row();
		tabs.Layout.Spacing = 2;

		foreach ( var category in Enum.GetValues<AttributeCategory>() )
		{
			var btn = new Button( category.ToString(), parent: tabs );
			btn.Clicked = () =>
			{
				_selectedCategory = category;
				RebuildCards();
			};
			tabs.Layout.Add( btn );
		}

		Layout.Add( tabs );

		// Cards scroll area
		var scroll = new ScrollArea( this );
		scroll.Canvas = new Widget( scroll );
		scroll.Canvas.Layout = Layout.Column();
		scroll.Canvas.Layout.Spacing = 4;
		_cardContainer = scroll.Canvas;
		Layout.Add( scroll, 1 );

		RebuildCards();
	}

	private void RebuildCards()
	{
		_cardContainer.Layout.Clear( true );

		var search = _searchBar.Text?.Trim() ?? "";
		var attrs = AttributeCatalog.GetByCategory( _selectedCategory );

		if ( !string.IsNullOrEmpty( search ) )
		{
			attrs = AttributeCatalog.All.Where( a =>
				a.Name.Contains( search, StringComparison.OrdinalIgnoreCase ) ||
				a.Description.Contains( search, StringComparison.OrdinalIgnoreCase )
			);
		}

		foreach ( var attr in attrs )
		{
			var card = CreateCard( attr );
			_cardContainer.Layout.Add( card );
		}

		_cardContainer.Layout.AddStretchCell( 1 );
	}

	private Widget CreateCard( AttributeDefinition attr )
	{
		var card = new Widget( _cardContainer );
		card.Layout = Layout.Row();
		card.Layout.Spacing = 8;
		card.Layout.Margin = new Margin( 8, 4, 8, 4 );
		card.SetStyles( "background-color: rgba(255,255,255,0.05); border-radius: 4px; padding: 6px;" );
		card.Cursor = CursorShape.Finger;
		card.ToolTip = $"[{attr.Name}] — {attr.Description}\n\n{attr.CodeExample}";

		card.MouseClick = () => ShowPreview( attr );

		var nameLabel = new Label( $"[{attr.Name}]", card );
		nameLabel.SetStyles( "font-weight: bold;" );
		card.Layout.Add( nameLabel );

		card.Layout.AddStretchCell( 1 );

		var desc = new Label( attr.Description, card );
		desc.SetStyles( "color: #aaa; font-size: 11px;" );
		card.Layout.Add( desc );

		return card;
	}

	private void ShowPreview( AttributeDefinition attr )
	{
		// TODO: Phase 4 — show AttributePreviewPanel with live widget demo
		Log.Info( $"Plugin Builder: Preview for [{attr.Name}]" );
	}
}
