using static Editor.BaseItemWidget;

namespace Editor.PluginBuilder;

/// <summary>
/// Tree view showing the blueprint element hierarchy.
/// Supports drag-to-reorder, right-click context menus, remove, duplicate.
/// Eyeball visibility toggle on each item. Attribute sorting and nesting.
/// </summary>
public class ElementTreePanel : Widget
{
	private readonly PluginBuilderDock _dock;
	private TreeView _tree;
	private LineEdit _searchBar;
	private BlueprintElement _selectedElement;
	private string _selectedAttrName;

	public ElementTreePanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;

		Layout = Layout.Column();
		Layout.Spacing = 4;

		// Header
		var header = Layout.Add( new Widget( this ) );
		header.Layout = Layout.Row();
		header.Layout.Spacing = 4;

		var title = new Label( "Elements", header );
		title.SetStyles( "font-weight: bold;" );
		header.Layout.Add( title );
		header.Layout.AddStretchCell( 1 );

		var upBtn = new Button( "", "arrow_upward", header );
		upBtn.Clicked = () => MoveSelected( -1 );
		upBtn.ToolTip = "Move selected item up";
		header.Layout.Add( upBtn );

		var downBtn = new Button( "", "arrow_downward", header );
		downBtn.Clicked = () => MoveSelected( 1 );
		downBtn.ToolTip = "Move selected item down";
		header.Layout.Add( downBtn );

		var addBtn = new Button( "", "add", header );
		addBtn.Clicked = () => ShowAddMenu();
		addBtn.ToolTip = "Add element";
		header.Layout.Add( addBtn );

		// Search bar
		_searchBar = Layout.Add( new LineEdit( this ) );
		_searchBar.PlaceholderText = "Search elements...";
		_searchBar.TextChanged += ( val ) => FilterTree( val );

		// Tree view (custom subclass handles eyeball clicks)
		_tree = new VisibilityTreeView( this, dock );
		_tree.ItemSelected = ( item ) => OnItemSelected( item );
		Layout.Add( _tree, 1 );

		// Wire up selection sync
		_dock.OnElementSelected += OnExternalSelection;
		_dock.OnBlueprintChanged += RebuildTree;

		RebuildTree();
	}

	public override void OnDestroyed()
	{
		_dock.OnElementSelected -= OnExternalSelection;
		_dock.OnBlueprintChanged -= RebuildTree;
		base.OnDestroyed();
	}

	public void RebuildTree()
	{
		_tree.Clear();

		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return;

		foreach ( var element in blueprint.Elements )
		{
			AddTreeNode( null, element );
		}
	}

	private void AddTreeNode( TreeNode parent, BlueprintElement element )
	{
		var node = new BlueprintTreeNode( element, _dock );
		if ( parent == null )
			_tree.AddItem( node );
		else
			parent.AddItem( node );

		// Add attribute sub-nodes (respecting nesting via AttributeChildren)
		var topLevelAttrs = GetTopLevelAttributes( element );
		foreach ( var attrName in topLevelAttrs )
		{
			AddAttributeTreeNode( node, element, attrName );
		}

		foreach ( var child in element.Children )
		{
			AddTreeNode( node, child );
		}
	}

	private void AddAttributeTreeNode( TreeNode parent, BlueprintElement element, string attrName )
	{
		var attrNode = new AttributeTreeNode( element, attrName, _dock );
		parent.AddItem( attrNode );

		// Add nested child attributes
		if ( element.AttributeChildren.TryGetValue( attrName, out var children ) )
		{
			foreach ( var childAttr in children )
			{
				AddAttributeTreeNode( attrNode, element, childAttr );
			}
		}
	}

	/// <summary>
	/// Returns attribute names that are NOT children of any other attribute.
	/// </summary>
	private static List<string> GetTopLevelAttributes( BlueprintElement element )
	{
		var childSet = new HashSet<string>();
		foreach ( var kv in element.AttributeChildren )
		{
			foreach ( var child in kv.Value )
				childSet.Add( child );
		}

		return element.Attributes.Keys
			.Where( a => !childSet.Contains( a ) )
			.ToList();
	}

	private void OnItemSelected( object item )
	{
		if ( item is BlueprintElement element )
		{
			_selectedElement = element;
			_selectedAttrName = null;
			_dock.SelectElement( element );
		}
		else if ( item is string attrName )
		{
			// Attribute selected — keep tracking for sorting
			_selectedAttrName = attrName;
		}
	}

	private void OnExternalSelection( BlueprintElement element )
	{
		_selectedElement = element;
		_selectedAttrName = null;
		if ( element != null )
			_tree.SelectItem( element );
	}

	private void MoveSelected( int direction )
	{
		// Try moving an attribute first
		if ( _selectedAttrName != null && _selectedElement != null )
		{
			MoveAttribute( _selectedElement, _selectedAttrName, direction );
			return;
		}

		if ( _selectedElement == null ) return;

		// Find which list the element is in (could be top-level or a child list)
		var parentList = FindParentList( _selectedElement );
		if ( parentList == null ) return;

		var idx = parentList.IndexOf( _selectedElement );
		var newIdx = idx + direction;
		if ( newIdx < 0 || newIdx >= parentList.Count ) return;

		var el = _selectedElement;
		parentList.RemoveAt( idx );
		parentList.Insert( newIdx, el );

		_dock.Undo.Push( direction < 0 ? "Move element up" : "Move element down",
			undo: () => { parentList.RemoveAt( newIdx ); parentList.Insert( idx, el ); _dock.MarkDirty(); },
			redo: () => { parentList.RemoveAt( idx ); parentList.Insert( newIdx, el ); _dock.MarkDirty(); }
		);
		_dock.MarkDirty();
	}

	/// <summary>
	/// Reorders an attribute within its parent element's attribute dictionary.
	/// </summary>
	public static void MoveAttribute( BlueprintElement element, string attrName, int direction )
	{
		var keys = element.Attributes.Keys.ToList();
		var idx = keys.IndexOf( attrName );
		if ( idx < 0 ) return;

		var newIdx = idx + direction;
		if ( newIdx < 0 || newIdx >= keys.Count ) return;

		// Rebuild dictionary in new order
		keys.RemoveAt( idx );
		keys.Insert( newIdx, attrName );

		var newDict = new Dictionary<string, Dictionary<string, object>>();
		foreach ( var key in keys )
			newDict[key] = element.Attributes[key];

		element.Attributes = newDict;
	}

	private List<BlueprintElement> FindParentList( BlueprintElement element )
	{
		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return null;

		if ( blueprint.Elements.Contains( element ) )
			return blueprint.Elements;

		return FindInChildren( blueprint.Elements, element );
	}

	private List<BlueprintElement> FindInChildren( List<BlueprintElement> elements, BlueprintElement target )
	{
		foreach ( var el in elements )
		{
			if ( el.Children.Contains( target ) )
				return el.Children;

			var found = FindInChildren( el.Children, target );
			if ( found != null ) return found;
		}
		return null;
	}

	private void FilterTree( string search )
	{
		// TODO: Phase 3 enhancement
	}

	private void ShowAddMenu()
	{
		var menu = new Menu( this );

		menu.AddOption( "Property", "circle", () => AddElement( ElementType.Property ) );
		menu.AddOption( "Group", "folder", () => AddElement( ElementType.Group ) );
		menu.AddOption( "Toggle Group", "toggle_on", () => AddElement( ElementType.ToggleGroup ) );
		menu.AddOption( "Feature", "tab", () => AddElement( ElementType.Feature ) );

		menu.AddSeparator();

		menu.AddOption( "Header", "title", () => AddElement( ElementType.Header ) );
		menu.AddOption( "Space", "space_bar", () => AddElement( ElementType.Space ) );
		menu.AddOption( "Info Box", "info", () => AddElement( ElementType.InfoBox ) );

		menu.OpenAtCursor();
	}

	public void AddElement( ElementType type )
	{
		AddElementFromFactory( () =>
		{
			var el = new BlueprintElement
			{
				ElementType = type,
				Name = GetDefaultName( type ),
				Order = _dock.ActiveBlueprint?.Elements.Count ?? 0
			};

			if ( type == ElementType.Group || type == ElementType.Feature )
				el.PropertyType = PropertyType.String;

			return el;
		} );
	}

	/// <summary>
	/// Inserts an element (from a factory) below the currently selected item,
	/// or at the bottom if nothing is selected.
	/// </summary>
	public void AddElementFromFactory( Func<BlueprintElement> factory )
	{
		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return;

		var element = factory();
		if ( element == null ) return;

		// Determine insertion point — below the selected element
		List<BlueprintElement> targetList;
		int insertIdx;

		if ( _selectedElement != null )
		{
			targetList = FindParentList( _selectedElement );
			if ( targetList == null )
				targetList = blueprint.Elements;

			insertIdx = targetList.IndexOf( _selectedElement );
			if ( insertIdx >= 0 )
				insertIdx++; // insert after selected
			else
				insertIdx = targetList.Count;
		}
		else
		{
			targetList = blueprint.Elements;
			insertIdx = targetList.Count;
		}

		targetList.Insert( insertIdx, element );
		var capturedList = targetList;
		var capturedIdx = insertIdx;
		_dock.Undo.Push( $"Add {element.ElementType}",
			undo: () => { capturedList.Remove( element ); _dock.MarkDirty(); },
			redo: () =>
			{
				if ( capturedIdx <= capturedList.Count )
					capturedList.Insert( capturedIdx, element );
				else
					capturedList.Add( element );
				_dock.MarkDirty();
			}
		);
		_dock.SelectElement( element );
		_dock.MarkDirty();
	}

	private string GetDefaultName( ElementType type )
	{
		var blueprint = _dock.ActiveBlueprint;
		var baseName = type switch
		{
			ElementType.Property => "NewProperty",
			ElementType.Group => "NewGroup",
			ElementType.ToggleGroup => "NewToggleGroup",
			ElementType.Feature => "NewFeature",
			ElementType.Header => "Header",
			ElementType.Space => "Space",
			ElementType.InfoBox => "Info",
			_ => "New"
		};

		int suffix = 1;
		var name = baseName;
		while ( blueprint.Elements.Any( e => e.Name == name ) )
		{
			name = $"{baseName}{suffix}";
			suffix++;
		}

		return name;
	}
}

/// <summary>
/// Custom TreeNode that shows element name, type icon, eyeball visibility toggle, and context menu.
/// </summary>
public class BlueprintTreeNode : TreeNode<BlueprintElement>
{
	private readonly PluginBuilderDock _dock;

	public BlueprintTreeNode( BlueprintElement element, PluginBuilderDock dock ) : base( element )
	{
		_dock = dock;
	}

	public override void OnPaint( VirtualWidget item )
	{
		PaintSelection( item );

		var el = Value;
		if ( el == null ) return;

		var icon = GetElementIcon( el );
		var label = string.IsNullOrEmpty( el.Name ) ? $"({el.ElementType})" : el.Name;
		var typeTag = el.ElementType == ElementType.Property ? $"[{el.PropertyType}]" : $"[{el.ElementType}]";

		// Dim hidden elements
		var alpha = el.Hidden ? 0.35f : 1.0f;

		// Icon
		var iconRect = item.Rect;
		iconRect.Width = 20;
		iconRect.Left += 4;
		Paint.SetPen( Theme.Blue.WithAlpha( alpha ) );
		Paint.DrawIcon( iconRect, icon, 14, TextFlag.LeftCenter );

		// Label
		var textRect = item.Rect;
		textRect.Left += 28;
		textRect.Right -= 100;
		Paint.SetPen( Theme.Text.WithAlpha( alpha ) );
		Paint.DrawText( textRect, label, TextFlag.LeftCenter );

		// Type tag (dimmed, right-aligned before the eyeball)
		var tagRect = item.Rect;
		tagRect.Left = tagRect.Right - 96;
		tagRect.Right -= 24;
		Paint.SetPen( Theme.Text.WithAlpha( 0.4f * alpha ) );
		Paint.DrawText( tagRect, typeTag, TextFlag.RightCenter );

		// Eyeball visibility icon (right edge)
		var eyeRect = item.Rect;
		eyeRect.Left = eyeRect.Right - 22;
		eyeRect.Width = 20;
		var eyeIcon = el.Hidden ? "visibility_off" : "visibility";
		Paint.SetPen( el.Hidden ? Theme.Text.WithAlpha( 0.3f ) : Theme.Text.WithAlpha( 0.6f ) );
		Paint.DrawIcon( eyeRect, eyeIcon, 14, TextFlag.Center );
	}

	public override bool OnContextMenu()
	{
		var el = Value;
		if ( el == null ) return false;

		var menu = new Menu();

		// Visibility toggle
		var hideLabel = el.Hidden ? "Show" : "Hide";
		var hideIcon = el.Hidden ? "visibility" : "visibility_off";
		menu.AddOption( hideLabel, hideIcon, () =>
		{
			el.Hidden = !el.Hidden;
			_dock.MarkDirty();
		} );

		menu.AddSeparator();

		menu.AddOption( "Duplicate", "content_copy", () =>
		{
			var blueprint = _dock.ActiveBlueprint;
			if ( blueprint == null ) return;

			var list = FindParentList( blueprint, el );
			if ( list == null ) return;

			var clone = el.Clone();
			clone.Name = el.Name + "_copy";
			var idx = list.IndexOf( el );
			if ( idx >= 0 )
				list.Insert( idx + 1, clone );
			else
				list.Add( clone );

			_dock.Undo.Push( "Duplicate element",
				undo: () => { list.Remove( clone ); _dock.MarkDirty(); },
				redo: () =>
				{
					var i = list.IndexOf( el );
					if ( i >= 0 ) list.Insert( i + 1, clone );
					else list.Add( clone );
					_dock.MarkDirty();
				}
			);
			_dock.MarkDirty();
		} );

		menu.AddSeparator();

		menu.AddOption( "Move Up", "arrow_upward", () =>
		{
			var blueprint = _dock.ActiveBlueprint;
			if ( blueprint == null ) return;
			var list = FindParentList( blueprint, el );
			if ( list == null ) return;
			var idx = list.IndexOf( el );
			if ( idx > 0 )
			{
				list.RemoveAt( idx );
				list.Insert( idx - 1, el );
				_dock.Undo.Push( "Move element up",
					undo: () => { list.RemoveAt( idx - 1 ); list.Insert( idx, el ); _dock.MarkDirty(); },
					redo: () => { list.RemoveAt( idx ); list.Insert( idx - 1, el ); _dock.MarkDirty(); }
				);
				_dock.MarkDirty();
			}
		} );

		menu.AddOption( "Move Down", "arrow_downward", () =>
		{
			var blueprint = _dock.ActiveBlueprint;
			if ( blueprint == null ) return;
			var list = FindParentList( blueprint, el );
			if ( list == null ) return;
			var idx = list.IndexOf( el );
			if ( idx >= 0 && idx < list.Count - 1 )
			{
				list.RemoveAt( idx );
				list.Insert( idx + 1, el );
				_dock.Undo.Push( "Move element down",
					undo: () => { list.RemoveAt( idx + 1 ); list.Insert( idx, el ); _dock.MarkDirty(); },
					redo: () => { list.RemoveAt( idx ); list.Insert( idx + 1, el ); _dock.MarkDirty(); }
				);
				_dock.MarkDirty();
			}
		} );

		menu.AddSeparator();

		menu.AddOption( "Remove", "delete", () =>
		{
			var blueprint = _dock.ActiveBlueprint;
			if ( blueprint == null ) return;
			var list = FindParentList( blueprint, el );
			if ( list == null ) return;
			var idx = list.IndexOf( el );
			list.Remove( el );
			_dock.Undo.Push( "Remove element",
				undo: () =>
				{
					if ( idx >= 0 && idx <= list.Count )
						list.Insert( idx, el );
					else
						list.Add( el );
					_dock.MarkDirty();
				},
				redo: () => { list.Remove( el ); _dock.MarkDirty(); }
			);
			_dock.MarkDirty();
		} );

		menu.OpenAtCursor();
		return true;
	}

	public override bool OnDragStart()
	{
		return true;
	}

	public override DropAction OnDragDrop( ItemDragEvent e )
	{
		var targetEl = Value;
		if ( targetEl == null ) return DropAction.Ignore;

		// Get the dragged element from tree selection
		var sourceEl = TreeView?.Selection
			?.OfType<BlueprintElement>()
			.FirstOrDefault();

		// Also try resolving from TreeNode wrappers
		if ( sourceEl == null )
		{
			sourceEl = TreeView?.Selection
				?.OfType<TreeNode>()
				.Select( n => n.Value )
				.OfType<BlueprintElement>()
				.FirstOrDefault();
		}

		if ( sourceEl == null || sourceEl == targetEl ) return DropAction.Ignore;

		// Don't allow dropping a parent into its own child
		if ( IsDescendantOf( targetEl, sourceEl ) ) return DropAction.Ignore;

		if ( !e.IsDrop ) return DropAction.Move;

		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return DropAction.Ignore;

		// Find source's current parent list and index
		var sourceList = FindParentList( blueprint, sourceEl );
		if ( sourceList == null ) return DropAction.Ignore;
		var oldIdx = sourceList.IndexOf( sourceEl );

		// Remove from current location
		sourceList.Remove( sourceEl );

		if ( e.DropEdge.HasFlag( ItemEdge.Top ) )
		{
			// Insert before target (sibling above)
			var targetList = FindParentList( blueprint, targetEl );
			if ( targetList == null ) { sourceList.Insert( oldIdx, sourceEl ); return DropAction.Ignore; }
			var targetIdx = targetList.IndexOf( targetEl );
			targetList.Insert( targetIdx, sourceEl );
		}
		else if ( e.DropEdge.HasFlag( ItemEdge.Bottom ) )
		{
			// Insert after target (sibling below)
			var targetList = FindParentList( blueprint, targetEl );
			if ( targetList == null ) { sourceList.Insert( oldIdx, sourceEl ); return DropAction.Ignore; }
			var targetIdx = targetList.IndexOf( targetEl );
			targetList.Insert( targetIdx + 1, sourceEl );
		}
		else
		{
			// Center drop — nest as child of target
			targetEl.Children.Add( sourceEl );
		}

		var capturedSourceList = sourceList;
		var capturedOldIdx = oldIdx;
		_dock.Undo.Push( "Rearrange element",
			undo: () =>
			{
				// Remove from wherever it ended up
				var currentList = FindParentList( blueprint, sourceEl );
				currentList?.Remove( sourceEl );
				targetEl.Children.Remove( sourceEl );
				// Restore to original position
				if ( capturedOldIdx <= capturedSourceList.Count )
					capturedSourceList.Insert( capturedOldIdx, sourceEl );
				else
					capturedSourceList.Add( sourceEl );
				_dock.MarkDirty();
			},
			redo: () => { _dock.MarkDirty(); }
		);

		_dock.MarkDirty();
		return DropAction.Move;
	}

	private static List<BlueprintElement> FindParentList( PluginBlueprint blueprint, BlueprintElement element )
	{
		if ( blueprint.Elements.Contains( element ) )
			return blueprint.Elements;

		return FindInChildLists( blueprint.Elements, element );
	}

	private static List<BlueprintElement> FindInChildLists( List<BlueprintElement> elements, BlueprintElement target )
	{
		foreach ( var el in elements )
		{
			if ( el.Children.Contains( target ) )
				return el.Children;

			var found = FindInChildLists( el.Children, target );
			if ( found != null ) return found;
		}
		return null;
	}

	private static bool IsDescendantOf( BlueprintElement potentialChild, BlueprintElement potentialParent )
	{
		foreach ( var child in potentialParent.Children )
		{
			if ( child == potentialChild ) return true;
			if ( IsDescendantOf( potentialChild, child ) ) return true;
		}
		return false;
	}

	internal static string GetElementIcon( BlueprintElement element )
	{
		return element.ElementType switch
		{
			ElementType.Property => GetPropertyIcon( element.PropertyType ),
			ElementType.Group => "folder",
			ElementType.ToggleGroup => "toggle_on",
			ElementType.Feature => "tab",
			ElementType.Header => "title",
			ElementType.Space => "space_bar",
			ElementType.Button => "smart_button",
			ElementType.Separator => "horizontal_rule",
			ElementType.InfoBox => "info",
			ElementType.Struct => "data_object",
			_ => "circle"
		};
	}

	private static string GetPropertyIcon( PropertyType type )
	{
		return type switch
		{
			PropertyType.String => "abc",
			PropertyType.Int => "123",
			PropertyType.Float => "decimal_increase",
			PropertyType.Bool => "check_box",
			PropertyType.Color => "palette",
			PropertyType.Vector3 => "open_with",
			PropertyType.Vector2 => "swap_horiz",
			PropertyType.Enum => "list",
			PropertyType.Model => "view_in_ar",
			PropertyType.Material => "texture",
			PropertyType.GameObject => "videogame_asset",
			PropertyType.CustomEnum => "list",
			PropertyType.CustomStruct => "data_object",
			_ => "circle"
		};
	}
}

/// <summary>
/// Tree node representing an attribute on an element.
/// Supports eyeball visibility toggle, sorting, nesting, and context menu.
/// </summary>
public class AttributeTreeNode : TreeNode<string>
{
	private readonly BlueprintElement _element;
	private readonly string _attrName;
	private readonly PluginBuilderDock _dock;

	public BlueprintElement Element => _element;
	public string AttrName => _attrName;

	public AttributeTreeNode( BlueprintElement element, string attrName, PluginBuilderDock dock ) : base( attrName )
	{
		_element = element;
		_attrName = attrName;
		_dock = dock;
	}

	public override void OnPaint( VirtualWidget item )
	{
		PaintSelection( item );

		var def = AttributeCatalog.Get( _attrName );
		var icon = def?.Icon ?? "label";

		// Check if this attribute is hidden via position
		var isHidden = _element.AttributePositions.TryGetValue( _attrName, out var pos )
			&& pos == AttributePosition.Hidden;
		var alpha = isHidden ? 0.35f : 1.0f;

		// Icon
		var iconRect = item.Rect;
		iconRect.Width = 20;
		iconRect.Left += 4;
		var attrColor = Color.Parse( "#6ab4ff" ) ?? Theme.Blue;
		Paint.SetPen( attrColor.WithAlpha( alpha ) );
		Paint.DrawIcon( iconRect, icon, 12, TextFlag.LeftCenter );

		// Label
		var textRect = item.Rect;
		textRect.Left += 28;
		textRect.Right -= 24;
		Paint.SetPen( attrColor.WithAlpha( alpha ) );
		Paint.DrawText( textRect, $"[{_attrName}]", TextFlag.LeftCenter );

		// Eyeball visibility icon (right edge)
		var eyeRect = item.Rect;
		eyeRect.Left = eyeRect.Right - 22;
		eyeRect.Width = 20;
		var eyeIcon = isHidden ? "visibility_off" : "visibility";
		Paint.SetPen( isHidden ? Theme.Text.WithAlpha( 0.3f ) : Theme.Text.WithAlpha( 0.6f ) );
		Paint.DrawIcon( eyeRect, eyeIcon, 12, TextFlag.Center );
	}

	public override bool OnContextMenu()
	{
		var menu = new Menu();

		// Visibility toggle
		var isHidden = _element.AttributePositions.TryGetValue( _attrName, out var pos )
			&& pos == AttributePosition.Hidden;
		var hideLabel = isHidden ? "Show" : "Hide";
		var hideIcon = isHidden ? "visibility" : "visibility_off";
		menu.AddOption( hideLabel, hideIcon, () =>
		{
			if ( isHidden )
				_element.AttributePositions[_attrName] = AttributePosition.Below;
			else
				_element.AttributePositions[_attrName] = AttributePosition.Hidden;
			_dock.MarkDirty();
		} );

		menu.AddSeparator();

		// Sort up/down
		menu.AddOption( "Move Up", "arrow_upward", () =>
		{
			ElementTreePanel.MoveAttribute( _element, _attrName, -1 );
			_dock.MarkDirty();
		} );

		menu.AddOption( "Move Down", "arrow_downward", () =>
		{
			ElementTreePanel.MoveAttribute( _element, _attrName, 1 );
			_dock.MarkDirty();
		} );

		menu.AddSeparator();

		// Nest / Unnest
		var keys = _element.Attributes.Keys.ToList();
		var idx = keys.IndexOf( _attrName );

		// Check if this is a child of another attribute
		string currentParent = null;
		foreach ( var kv in _element.AttributeChildren )
		{
			if ( kv.Value.Contains( _attrName ) )
			{
				currentParent = kv.Key;
				break;
			}
		}

		if ( currentParent != null )
		{
			menu.AddOption( "Unnest (make top-level)", "subdirectory_arrow_left", () =>
			{
				_element.AttributeChildren[currentParent].Remove( _attrName );
				if ( _element.AttributeChildren[currentParent].Count == 0 )
					_element.AttributeChildren.Remove( currentParent );
				_dock.MarkDirty();
			} );
		}
		else if ( idx > 0 )
		{
			// Can nest under the attribute above it
			var parentCandidate = keys[idx - 1];
			menu.AddOption( $"Nest inside [{parentCandidate}]", "subdirectory_arrow_right", () =>
			{
				if ( !_element.AttributeChildren.ContainsKey( parentCandidate ) )
					_element.AttributeChildren[parentCandidate] = new List<string>();
				_element.AttributeChildren[parentCandidate].Add( _attrName );
				_dock.MarkDirty();
			} );
		}

		menu.AddSeparator();

		menu.AddOption( "Remove", "delete", () =>
		{
			_element.Attributes.Remove( _attrName );
			_element.AttributePositions.Remove( _attrName );
			_element.AttributeChildren.Remove( _attrName );
			// Also remove from any parent's child list
			foreach ( var kv in _element.AttributeChildren.ToList() )
			{
				kv.Value.Remove( _attrName );
				if ( kv.Value.Count == 0 )
					_element.AttributeChildren.Remove( kv.Key );
			}
			_dock.MarkDirty();
		} );

		menu.OpenAtCursor();
		return true;
	}
}

/// <summary>
/// Custom TreeView that intercepts mouse clicks in the eyeball icon area (last 24px)
/// to toggle element/attribute visibility, since TreeNode doesn't expose OnClick.
/// </summary>
internal class VisibilityTreeView : TreeView
{
	private readonly PluginBuilderDock _dock;

	public VisibilityTreeView( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		var item = GetItemAt( e.LocalPosition );
		if ( item != null && e.LocalPosition.x >= item.Rect.Right - 24 )
		{
			if ( item.Object is BlueprintTreeNode btn )
			{
				var el = btn.Value;
				if ( el != null )
				{
					el.Hidden = !el.Hidden;
					_dock.MarkDirty();
					return;
				}
			}
			else if ( item.Object is AttributeTreeNode atn )
			{
				var isHidden = atn.Element.AttributePositions.TryGetValue( atn.AttrName, out var p )
					&& p == AttributePosition.Hidden;
				atn.Element.AttributePositions[atn.AttrName] = isHidden ? AttributePosition.Below : AttributePosition.Hidden;
				_dock.MarkDirty();
				return;
			}
		}

		base.OnMousePress( e );
	}
}
