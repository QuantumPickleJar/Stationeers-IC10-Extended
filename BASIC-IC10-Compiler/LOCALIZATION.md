# Localization Support

The BASIC-IC10 Compiler autocomplete system now supports localization through external JSON files. This makes it easy to translate the mod for different languages without recompiling the code.

## JSON Data Files

All localizable strings are stored in the `InGameTextEditor` folder within the game's `Managed` directory:

### 1. **LanguageData.json**
Contains language-specific strings for the compiler and autocomplete:

- **AllLogicTypes** (281 items) - All LogicType property names from the game
- **AllSlotLogicTypes** (32 items) - All LogicSlotType property names 
- **Keywords** (35 items) - BASIC IC10 language keywords (If, While, For, etc.)
- **Functions** (25 items) - Built-in functions (Abs, Sin, Hash, etc.)

**Example structure:**
```json
{
  "AllLogicTypes": ["None", "Power", "Open", "Mode", ...],
  "AllSlotLogicTypes": ["Occupied", "OccupantHash", "Quantity", ...],
  "Keywords": ["If", "Then", "Else", "ElseIf", ...],
  "Functions": ["Abs", "Ceil", "Floor", "Round", ...]
}
```

### 2. **DeviceLogicTypes.json**
Contains device-specific LogicType lists organized by category:

- **GasSensor** - Gas sensor properties (Pressure, Temperature, RatioOxygen, etc.)
- **Display** - Display/LED properties (Color, Mode, Setting, etc.)
- **Light** - Light/lamp properties
- **Vent** - Vent properties (AirRelease, PressureSetting, etc.)
- **Pump** - Pump/filtration properties with input/output variants
- **Hydroponics** - Hydroponics tray properties (Plant, Harvest, PlantHealth, etc.)
- **Logic** - Logic device properties (Channels, Memory, PID controller, etc.)
- **Power** - Power device properties (Solar, Battery, Generator, etc.)
- **Fabricator** - Fabricator properties (Recipe, Progress, Import/Export, etc.)
- **Door** - Door/airlock properties
- **Console** - Console properties (Satellite, Research, Mining, Weather, etc.)
- **Tank** - Tank/canister properties
- **Rocket** - Rocket/thruster properties (Position, Velocity, Navigation, etc.)
- **Unknown** - Fallback list with all LogicTypes

**Example structure:**
```json
{
  "GasSensor": ["Activate", "Error", "Lock", ...],
  "Display": ["Activate", "Color", "Error", ...],
  ...
}
```

## How It Works

1. **Lazy Loading**: Data is loaded from JSON files only when first accessed
2. **Fallback System**: If JSON files are missing or corrupt, the mod uses built-in fallback data
3. **Debug Logging**: Loading status is logged to Unity's debug console for troubleshooting

## Creating a Translation

To translate the mod to another language:

1. **Copy the JSON files** from the installation directory:
   - `DeviceLogicTypes.json`
   - `LanguageData.json`

2. **Translate the strings** in both files to your target language
   - Keep property names that are programmatic (like "Open", "Lock") if they match the game's internal names
   - Translate keywords and function names as appropriate for your language
   - Note: Some properties like "PrefabHash" are technical and may not need translation

3. **Test thoroughly** with the compiler to ensure autocomplete works correctly

4. **Share your translation** with the community!

## Installation Path

After running `install_autocomplete.ps1`, the JSON files are located at:
```
<Compiler Directory>\BASIC IC10 Compiler for Stationeers_Data\Managed\InGameTextEditor\
├── DeviceLogicTypes.json
└── LanguageData.json
```

## Developer Notes

- The code uses `Newtonsoft.Json` for deserialization
- Files are loaded from `Application.dataPath/Managed/InGameTextEditor/`
- If loading fails, fallback data embedded in the DLL is used
- The system supports hot-swapping JSON files (restart the compiler to reload)

## Benefits

✅ **Easy Updates** - No recompilation needed to update strings  
✅ **Community Translations** - Anyone can create translations  
✅ **Maintainable** - Separate data from code  
✅ **Future-Proof** - New game properties can be added via JSON  
✅ **Fallback Safety** - Always works even if JSON is missing
