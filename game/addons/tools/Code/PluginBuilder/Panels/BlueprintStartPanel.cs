using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Landing screen when no blueprint is open.
/// Shows: New, Open, Recent, and Preset templates.
/// </summary>
public class BlueprintStartPanel : Widget
{
	private readonly PluginBuilderDock _dock;

	public BlueprintStartPanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;

		Layout = Layout.Column();
		Layout.Margin = 32;
		Layout.Spacing = 16;

		// Title
		var title = Layout.Add( new Label( "Plugin Builder", this ) );
		title.SetStyles( "font-size: 24px; font-weight: bold;" );

		var subtitle = Layout.Add( new Label( "Visual No-Code Component Designer", this ) );
		subtitle.SetStyles( "font-size: 14px; color: #888;" );

		Layout.AddSpacingCell( 24 );

		// Action buttons
		var newBtn = new Button.Primary( "New Blueprint", "add_circle", this );
		newBtn.Clicked = () => _dock.CreateNewBlueprint();
		Layout.Add( newBtn );

		var openBtn = new Button( "Open Blueprint", "folder_open", this );
		openBtn.Clicked = () => _dock.OpenBlueprint();
		Layout.Add( openBtn );

		Layout.AddSpacingCell( 16 );

		// Preset section header
		var presetsLabel = Layout.Add( new Label( "Quick Start Templates", this ) );
		presetsLabel.SetStyles( "font-size: 14px; font-weight: bold;" );

		// Preset buttons — a few built-in presets for quick start
		AddPresetButton( "Health System", "favorite", CreateHealthSystemPreset );
		AddPresetButton( "Character Settings", "person", CreateCharacterSettingsPreset );
		AddPresetButton( "Weapon Stats", "gps_fixed", CreateWeaponStatsPreset );

		Layout.AddStretchCell( 1 );
	}

	private void AddPresetButton( string name, string icon, Func<PluginBlueprint> factory )
	{
		var btn = new Button( name, icon, this );
		btn.Clicked = () =>
		{
			var blueprint = factory();
			_dock.LoadBlueprint( blueprint );
		};
		Layout.Add( btn );
	}

	private PluginBlueprint CreateHealthSystemPreset()
	{
		var bp = new PluginBlueprint
		{
			Name = "HealthSystem",
			Type = BlueprintType.Component,
			Description = "A health and shield system for game entities.",
			Icon = "favorite"
		};

		var vitalsGroup = new BlueprintElement
		{
			ElementType = ElementType.Group,
			Name = "Vitals",
			Children = new()
			{
				new BlueprintElement
				{
					Name = "Health", PropertyType = PropertyType.Float, Order = 0,
					DefaultValue = DefaultValueContainer.FromFloat( 100f ),
					Attributes = new()
					{
						["Range"] = new() { ["min"] = (object)0f, ["max"] = (object)200f, ["step"] = (object)1f },
						["Title"] = new() { ["text"] = (object)"HP" }
					}
				},
				new BlueprintElement
				{
					Name = "RegenRate", PropertyType = PropertyType.Float, Order = 1,
					DefaultValue = DefaultValueContainer.FromFloat( 5f ),
					Attributes = new()
					{
						["Range"] = new() { ["min"] = (object)0f, ["max"] = (object)50f }
					}
				}
			}
		};

		var shieldsGroup = new BlueprintElement
		{
			ElementType = ElementType.ToggleGroup,
			Name = "HasShields",
			Children = new()
			{
				new BlueprintElement
				{
					Name = "ShieldAmount", PropertyType = PropertyType.Float, Order = 0,
					DefaultValue = DefaultValueContainer.FromFloat( 50f ),
					Attributes = new()
					{
						["Range"] = new() { ["min"] = (object)0f, ["max"] = (object)100f }
					}
				},
				new BlueprintElement
				{
					Name = "Armor", PropertyType = PropertyType.Float, Order = 1,
					DefaultValue = DefaultValueContainer.FromFloat( 10f )
				}
			}
		};

		var invincible = new BlueprintElement
		{
			Name = "IsInvincible", PropertyType = PropertyType.Bool, Order = 2,
			DefaultValue = DefaultValueContainer.FromBool( false )
		};

		bp.Elements.Add( vitalsGroup );
		bp.Elements.Add( shieldsGroup );
		bp.Elements.Add( invincible );

		return bp;
	}

	private PluginBlueprint CreateCharacterSettingsPreset()
	{
		var bp = new PluginBlueprint
		{
			Name = "CharacterSettings",
			Type = BlueprintType.Component,
			Description = "Character identity, appearance, and nametag settings.",
			Icon = "person"
		};

		var identity = new BlueprintElement
		{
			ElementType = ElementType.Group, Name = "Identity",
			Children = new()
			{
				new BlueprintElement { Name = "Name", PropertyType = PropertyType.String, Order = 0 },
				new BlueprintElement
				{
					Name = "Description", PropertyType = PropertyType.String, Order = 1,
					Attributes = new() { ["TextArea"] = new() }
				},
				new BlueprintElement
				{
					Name = "Icon", PropertyType = PropertyType.String, Order = 2,
					Attributes = new() { ["ImageAssetPath"] = new() }
				}
			}
		};

		var appearance = new BlueprintElement
		{
			ElementType = ElementType.Group, Name = "Appearance",
			Children = new()
			{
				new BlueprintElement { Name = "SkinColor", PropertyType = PropertyType.Color, Order = 0,
					Attributes = new() { ["ColorUsage"] = new() { ["showAlpha"] = (object)true } }
				},
				new BlueprintElement { Name = "Scale", PropertyType = PropertyType.Float, Order = 1,
					DefaultValue = DefaultValueContainer.FromFloat( 1f ),
					Attributes = new() { ["Range"] = new() { ["min"] = (object)0.5f, ["max"] = (object)3f } }
				}
			}
		};

		bp.Elements.Add( identity );
		bp.Elements.Add( appearance );

		return bp;
	}

	private PluginBlueprint CreateWeaponStatsPreset()
	{
		var bp = new PluginBlueprint
		{
			Name = "WeaponStats",
			Type = BlueprintType.Component,
			Description = "Weapon damage, firing, and spread configuration.",
			Icon = "gps_fixed"
		};

		var damage = new BlueprintElement
		{
			ElementType = ElementType.Group, Name = "Damage",
			Children = new()
			{
				new BlueprintElement { Name = "BaseDamage", PropertyType = PropertyType.Float, Order = 0,
					DefaultValue = DefaultValueContainer.FromFloat( 25f ),
					Attributes = new() { ["Range"] = new() { ["min"] = (object)0f, ["max"] = (object)200f } }
				},
				new BlueprintElement { Name = "CritMultiplier", PropertyType = PropertyType.Float, Order = 1,
					DefaultValue = DefaultValueContainer.FromFloat( 2f ),
					Attributes = new() { ["Range"] = new() { ["min"] = (object)1f, ["max"] = (object)5f } }
				}
			}
		};

		var firing = new BlueprintElement
		{
			ElementType = ElementType.Group, Name = "Firing",
			Children = new()
			{
				new BlueprintElement { Name = "FireRate", PropertyType = PropertyType.Float, Order = 0,
					DefaultValue = DefaultValueContainer.FromFloat( 10f ),
					Attributes = new() { ["Range"] = new() { ["min"] = (object)0.1f, ["max"] = (object)20f, ["step"] = (object)0.1f } }
				},
				new BlueprintElement { Name = "AmmoPerClip", PropertyType = PropertyType.Int, Order = 1,
					DefaultValue = DefaultValueContainer.FromInt( 30 ),
					Attributes = new() { ["Range"] = new() { ["min"] = (object)1f, ["max"] = (object)200f } }
				},
				new BlueprintElement { Name = "ReloadTime", PropertyType = PropertyType.Float, Order = 2,
					DefaultValue = DefaultValueContainer.FromFloat( 2f )
				}
			}
		};

		bp.Elements.Add( damage );
		bp.Elements.Add( firing );

		return bp;
	}
}
