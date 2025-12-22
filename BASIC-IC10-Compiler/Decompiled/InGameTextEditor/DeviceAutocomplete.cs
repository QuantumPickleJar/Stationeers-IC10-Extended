// DeviceAutocomplete.cs - Context-aware autocomplete for BASIC IC10
// Supports two modes:
//   1. Property completion (on '.') - shows device LogicTypes
//   2. Identifier completion (while typing) - shows variables, aliases, keywords, functions

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace InGameTextEditor
{
    public enum DeviceCategory
    {
        Unknown,
        GasSensor,
        Display,
        Light,
        Vent,
        Pump,
        Hydroponics,
        Logic,      // Memory, Diode, etc.
        Power,      // Solar, Battery, Generator
        Fabricator, // Furnace, Autolathe, etc.
        Door,
        Console,
        Tank,
        Rocket
    }

    public enum AutocompleteMode
    {
        None,
        Property,    // After '.' - device properties
        Identifier   // While typing - variables, keywords, etc.
    }

    public enum IdentifierKind
    {
        Variable,
        Alias,
        Constant,
        Label,
        Keyword,
        Function
    }

    public class AutocompleteItem
    {
        public string Text { get; set; }
        public IdentifierKind Kind { get; set; }
        public string Description { get; set; }

        public AutocompleteItem(string text, IdentifierKind kind, string description = null)
        {
            Text = text;
            Kind = kind;
            Description = description;
        }
    }

    public class DeviceAutocomplete
    {
        #region Singleton
        private static DeviceAutocomplete _instance;
        public static DeviceAutocomplete Instance => _instance ?? (_instance = new DeviceAutocomplete());
        #endregion

        #region Device Type Data

        // Raw device references that should NOT trigger autocomplete
        private static readonly HashSet<string> RawDeviceRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "d0", "d1", "d2", "d3", "d4", "d5", "db",
            "dr0", "dr1", "dr2", "dr3", "dr4", "dr5", "dr6", "dr7",
            "dr8", "dr9", "dr10", "dr11", "dr12", "dr13", "dr14", "dr15"
        };

        // Name patterns to infer device type
        private static readonly Dictionary<string, DeviceCategory> NamePatterns = new Dictionary<string, DeviceCategory>(StringComparer.OrdinalIgnoreCase)
        {
            // Gas Sensors
            { "sensor", DeviceCategory.GasSensor },
            { "gassensor", DeviceCategory.GasSensor },
            { "gas", DeviceCategory.GasSensor },
            { "atmo", DeviceCategory.GasSensor },
            
            // Displays
            { "display", DeviceCategory.Display },
            { "disp", DeviceCategory.Display },
            { "readout", DeviceCategory.Display },
            { "screen", DeviceCategory.Display },
            { "led", DeviceCategory.Display },
            
            // Lights
            { "light", DeviceCategory.Light },
            { "lamp", DeviceCategory.Light },
            { "grow", DeviceCategory.Light },
            { "growlight", DeviceCategory.Light },
            
            // Vents
            { "vent", DeviceCategory.Vent },
            { "purge", DeviceCategory.Vent },
            { "passive", DeviceCategory.Vent },
            { "activevent", DeviceCategory.Vent },
            
            // Pumps
            { "pump", DeviceCategory.Pump },
            { "turbo", DeviceCategory.Pump },
            { "filtration", DeviceCategory.Pump },
            { "filter", DeviceCategory.Pump },
            
            // Hydroponics
            { "tray", DeviceCategory.Hydroponics },
            { "hydro", DeviceCategory.Hydroponics },
            { "plant", DeviceCategory.Hydroponics },
            { "planter", DeviceCategory.Hydroponics },
            { "harvest", DeviceCategory.Hydroponics },
            
            // Logic devices
            { "diode", DeviceCategory.Logic },
            { "memory", DeviceCategory.Logic },
            { "logic", DeviceCategory.Logic },
            { "math", DeviceCategory.Logic },
            { "dial", DeviceCategory.Logic },
            { "switch", DeviceCategory.Logic },
            { "lever", DeviceCategory.Logic },
            { "button", DeviceCategory.Logic },
            
            // Power
            { "solar", DeviceCategory.Power },
            { "battery", DeviceCategory.Power },
            { "apc", DeviceCategory.Power },
            { "generator", DeviceCategory.Power },
            { "power", DeviceCategory.Power },
            { "stirling", DeviceCategory.Power },
            
            // Fabricators
            { "furnace", DeviceCategory.Fabricator },
            { "smelter", DeviceCategory.Fabricator },
            { "arc", DeviceCategory.Fabricator },
            { "autolathe", DeviceCategory.Fabricator },
            { "printer", DeviceCategory.Fabricator },
            { "centrifuge", DeviceCategory.Fabricator },
            { "electrolyzer", DeviceCategory.Fabricator },
            
            // Doors
            { "door", DeviceCategory.Door },
            { "airlock", DeviceCategory.Door },
            { "gate", DeviceCategory.Door },
            
            // Consoles
            { "console", DeviceCategory.Console },
            { "computer", DeviceCategory.Console },
            
            // Tanks
            { "tank", DeviceCategory.Tank },
            { "canister", DeviceCategory.Tank },
            { "cylinder", DeviceCategory.Tank },
            
            // Rocket
            { "rocket", DeviceCategory.Rocket },
            { "thruster", DeviceCategory.Rocket },
            { "nav", DeviceCategory.Rocket }
        };

        // LogicTypes grouped by device category
        private static readonly Dictionary<DeviceCategory, string[]> CategoryLogicTypes = new Dictionary<DeviceCategory, string[]>
        {
            { DeviceCategory.GasSensor, new[] {
                "Activate", "Error", "Lock", "Mode", "On", "Power", "PowerActual", "PowerRequired",
                "Pressure", "RatioCarbonDioxide", "RatioNitrogen", "RatioNitrousOxide",
                "RatioOxygen", "RatioPollutant", "RatioVolatiles", "RatioWater",
                "Temperature", "TotalMoles", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Display, new[] {
                "Activate", "Color", "Error", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "Setting", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Light, new[] {
                "Activate", "Color", "Error", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "Setting", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Vent, new[] {
                "Activate", "Error", "Lock", "Mode", "On", "Open", "Power", "PowerActual",
                "PowerRequired", "Pressure", "PressureExternal", "PressureInternal",
                "PressureSetting", "Temperature", "TemperatureExternal", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Pump, new[] {
                "Activate", "Error", "Filtration", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "Pressure", "PressureInput", "PressureOutput", "PressureSetting",
                "RatioCarbonDioxide", "RatioNitrogen", "RatioNitrousOxide", "RatioOxygen",
                "RatioPollutant", "RatioVolatiles", "RatioWater", "Setting", "Temperature",
                "TemperatureInput", "TemperatureOutput", "TotalMoles", "TotalMolesInput",
                "TotalMolesOutput", "Volume", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Hydroponics, new[] {
                "Activate", "Efficiency", "Error", "Harvest", "Lock", "On", "Plant", "Power",
                "PowerActual", "PowerRequired", "Pressure", "Progress", "Seeding", "Temperature",
                "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Logic, new[] {
                "Activate", "Channel", "Channel0", "Channel1", "Channel2", "Channel3",
                "Error", "LineNumber", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "Setting", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Power, new[] {
                "Activate", "Charge", "Error", "Horizontal", "Lock", "Maximum", "Mode", "On",
                "Power", "PowerActual", "PowerGenerated", "PowerPotential", "PowerRequired",
                "Ratio", "Setting", "SolarAngle", "SolarIrradiance", "Vertical", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Fabricator, new[] {
                "Activate", "Combustion", "CompletionRatio", "Error", "ExportCount", "Idle",
                "ImportCount", "Lock", "Mode", "On", "Power", "PowerActual", "PowerRequired",
                "Progress", "Quantity", "RecipeHash", "RequestHash", "RequiredPower",
                "Temperature", "TemperatureSetting", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Door, new[] {
                "Activate", "Error", "Lock", "Mode", "On", "Open", "Power", "PowerActual",
                "PowerRequired", "Setting", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Console, new[] {
                "Activate", "Channel", "Channel0", "Channel1", "Channel2", "Channel3",
                "Color", "Error", "Lock", "Mode", "On", "Power", "PowerActual", "PowerRequired",
                "Setting", "SignalID", "SignalStrength", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Tank, new[] {
                "Activate", "Error", "Lock", "On", "Pressure", "PressureInternal",
                "RatioCarbonDioxide", "RatioNitrogen", "RatioNitrousOxide", "RatioOxygen",
                "RatioPollutant", "RatioVolatiles", "RatioWater", "Temperature",
                "TotalMoles", "Volume", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Rocket, new[] {
                "Activate", "Error", "ForceWrite", "Fuel", "Horizontal", "Lock", "Mode", "On",
                "Power", "PowerActual", "PowerRequired", "PositionX", "PositionY", "PositionZ",
                "ReturnFuelCost", "Setting", "TargetPadIndex", "TargetX", "TargetY", "TargetZ",
                "Throttle", "VelocityMagnitude", "VelocityX", "VelocityY", "VelocityZ",
                "Vertical", "PrefabHash", "ReferenceId"
            }},
            
            { DeviceCategory.Unknown, new[] {
                // Common properties for unknown devices
                "Activate", "Charge", "Class", "Color", "Damage", "Error", "Lock", "Mode",
                "On", "Open", "Power", "PowerActual", "PowerRequired", "Pressure", "Setting",
                "Temperature", "PrefabHash", "ReferenceId",
                // Include ratio types as they're common
                "RatioCarbonDioxide", "RatioNitrogen", "RatioOxygen", "RatioVolatiles", "RatioWater"
            }}
        };

        // BASIC IC10 Keywords
        private static readonly string[] Keywords = new[]
        {
            "If", "Then", "Else", "ElseIf", "EndIf",
            "While", "EndWhile",
            "For", "To", "Step", "Next",
            "ForEach", "In",
            "Function", "EndFunction",
            "Continue", "Break",
            "Goto", "GoSub", "Return",
            "Var", "Const", "Alias", "Array",
            "IC", "Pin", "Device", "Slot", "Reagent",
            "Name", "Id", "Port", "Channel",
            "True", "False"
        };

        // BASIC IC10 Built-in Functions
        private static readonly string[] Functions = new[]
        {
            // Math
            "Abs", "Ceil", "Floor", "Round", "Trunc",
            "Max", "Min", "Clamp",
            "Log", "Exp", "Sqrt", "Pow",
            "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2",
            "Rand",
            // Logic
            "And", "Or", "Xor", "Not",
            // String/Display
            "Hash"
        };

        #endregion

        #region State

        private AutocompleteMode _mode = AutocompleteMode.None;

        // Maps alias name -> inferred device category
        private Dictionary<string, DeviceCategory> _aliasCategories = new Dictionary<string, DeviceCategory>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _userAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Document-extracted identifiers
        private HashSet<string> _userVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _userConstants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _userLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Property mode (original) - just strings
        private List<string> _filteredItems = new List<string>();

        // Identifier mode - rich items with kinds
        private List<AutocompleteItem> _identifierItems = new List<AutocompleteItem>();

        private int _selectedIndex;
        private bool _isVisible;
        private Vector2 _popupPosition;
        private Vector2 _scrollPosition;
        private string _currentFilter = "";
        private DeviceCategory _currentCategory = DeviceCategory.Unknown;
        private string _typingPrefix = "";  // What user has typed so far (for identifier mode)

        // GUI styles
        private GUIStyle _boxStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _selectedStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _kindStyle;
        private bool _stylesInit;

        private const float PopupWidth = 320f;
        private const float ItemHeight = 20f;
        private const int MaxVisible = 10;
        private const int MinPrefixLength = 1;  // Minimum chars before showing suggestions

        #endregion

        #region Public API

        public bool IsVisible => _isVisible;

        public bool TryTrigger(TextEditor editor, string tokenBeforeDot)
        {
            if (string.IsNullOrEmpty(tokenBeforeDot))
                return false;

            if (RawDeviceRefs.Contains(tokenBeforeDot))
                return false;

            // Don't trigger autocomplete inside comments
            if (IsInComment(editor))
                return false;

            // Parse document to find aliases and their types
            ParseDocument(editor.Text);

            if (!_userAliases.Contains(tokenBeforeDot))
                return false;

            // Get the device category for this alias
            _currentCategory = _aliasCategories.TryGetValue(tokenBeforeDot, out var cat) ? cat : DeviceCategory.Unknown;

            // Get LogicTypes for this category
            var logicTypes = CategoryLogicTypes.TryGetValue(_currentCategory, out var types) ? types : CategoryLogicTypes[DeviceCategory.Unknown];

            _mode = AutocompleteMode.Property;
            _currentFilter = "";
            _filteredItems = logicTypes.OrderBy(x => x).ToList();
            _selectedIndex = 0;
            _scrollPosition = Vector2.zero;
            _popupPosition = CalculatePosition(editor);
            _isVisible = true;

            return true;
        }

        /// <summary>
        /// Try to trigger identifier autocomplete (while typing).
        /// Returns true if autocomplete was activated.
        /// </summary>
        public bool TryTriggerIdentifier(TextEditor editor, string currentWord)
        {
            if (string.IsNullOrEmpty(currentWord) || currentWord.Length < MinPrefixLength)
                return false;

            // Don't trigger autocomplete inside comments
            if (IsInComment(editor))
                return false;

            // Don't trigger if it looks like a raw device ref being typed
            if (currentWord.Length <= 3 && currentWord.StartsWith("d", StringComparison.OrdinalIgnoreCase))
            {
                // Allow d, dr but check if it's becoming d0-d5, dr0-dr15
                if (Regex.IsMatch(currentWord, @"^d[rb]?\d*$", RegexOptions.IgnoreCase))
                    return false;
            }

            // Parse document for identifiers
            ParseDocument(editor.Text);

            // Build candidate list
            _identifierItems.Clear();
            _typingPrefix = currentWord;

            // Add matching user variables
            foreach (var v in _userVariables)
            {
                if (v.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && !v.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
                    _identifierItems.Add(new AutocompleteItem(v, IdentifierKind.Variable));
            }

            // Add matching user aliases
            foreach (var a in _userAliases)
            {
                if (a.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && !a.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
                    _identifierItems.Add(new AutocompleteItem(a, IdentifierKind.Alias));
            }

            // Add matching constants
            foreach (var c in _userConstants)
            {
                if (c.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && !c.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
                    _identifierItems.Add(new AutocompleteItem(c, IdentifierKind.Constant));
            }

            // Add matching labels (for Goto)
            foreach (var l in _userLabels)
            {
                if (l.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && !l.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
                    _identifierItems.Add(new AutocompleteItem(l, IdentifierKind.Label));
            }

            // Add matching keywords
            foreach (var kw in Keywords)
            {
                if (kw.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && !kw.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
                    _identifierItems.Add(new AutocompleteItem(kw, IdentifierKind.Keyword));
            }

            // Add matching functions
            foreach (var fn in Functions)
            {
                if (fn.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && !fn.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
                    _identifierItems.Add(new AutocompleteItem(fn, IdentifierKind.Function));
            }

            if (_identifierItems.Count == 0)
                return false;

            // Sort: user-defined first (variables, aliases, constants, labels), then keywords, then functions
            // Within each group, alphabetical
            _identifierItems = _identifierItems
                .OrderBy(x => GetKindPriority(x.Kind))
                .ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _mode = AutocompleteMode.Identifier;
            _selectedIndex = 0;
            _scrollPosition = Vector2.zero;
            _popupPosition = CalculatePosition(editor);
            _isVisible = true;

            return true;
        }

        private int GetKindPriority(IdentifierKind kind)
        {
            switch (kind)
            {
                case IdentifierKind.Variable: return 0;
                case IdentifierKind.Alias: return 1;
                case IdentifierKind.Constant: return 2;
                case IdentifierKind.Label: return 3;
                case IdentifierKind.Keyword: return 4;
                case IdentifierKind.Function: return 5;
                default: return 99;
            }
        }

        public void UpdateFilter(string partial)
        {
            if (!_isVisible) return;

            if (_mode == AutocompleteMode.Property)
            {
                _currentFilter = partial ?? "";
                var logicTypes = CategoryLogicTypes.TryGetValue(_currentCategory, out var types) ? types : CategoryLogicTypes[DeviceCategory.Unknown];
                
                _filteredItems = logicTypes
                    .Where(x => x.StartsWith(_currentFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x)
                    .ToList();
                _selectedIndex = 0;

                if (_filteredItems.Count == 0)
                    Hide();
            }
            else if (_mode == AutocompleteMode.Identifier)
            {
                UpdateIdentifierFilter(partial);
            }
        }

        public void UpdateIdentifierFilter(string newPrefix)
        {
            if (!_isVisible || _mode != AutocompleteMode.Identifier) return;

            _typingPrefix = newPrefix ?? "";

            if (_typingPrefix.Length < MinPrefixLength)
            {
                Hide();
                return;
            }

            // Rebuild the filtered list with new prefix
            var newItems = new List<AutocompleteItem>();

            foreach (var v in _userVariables)
                if (v.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase) && !v.Equals(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    newItems.Add(new AutocompleteItem(v, IdentifierKind.Variable));

            foreach (var a in _userAliases)
                if (a.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase) && !a.Equals(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    newItems.Add(new AutocompleteItem(a, IdentifierKind.Alias));

            foreach (var c in _userConstants)
                if (c.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase) && !c.Equals(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    newItems.Add(new AutocompleteItem(c, IdentifierKind.Constant));

            foreach (var l in _userLabels)
                if (l.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase) && !l.Equals(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    newItems.Add(new AutocompleteItem(l, IdentifierKind.Label));

            foreach (var kw in Keywords)
                if (kw.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase) && !kw.Equals(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    newItems.Add(new AutocompleteItem(kw, IdentifierKind.Keyword));

            foreach (var fn in Functions)
                if (fn.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase) && !fn.Equals(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    newItems.Add(new AutocompleteItem(fn, IdentifierKind.Function));

            _identifierItems = newItems
                .OrderBy(x => GetKindPriority(x.Kind))
                .ThenBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _selectedIndex = 0;

            if (_identifierItems.Count == 0)
                Hide();
        }

        public void SelectNext()
        {
            int count = (_mode == AutocompleteMode.Identifier) ? _identifierItems.Count : _filteredItems.Count;
            if (count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % count;
            EnsureVisible();
        }

        public void SelectPrevious()
        {
            int count = (_mode == AutocompleteMode.Identifier) ? _identifierItems.Count : _filteredItems.Count;
            if (count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + count) % count;
            EnsureVisible();
        }

        /// <summary>
        /// Gets the text to insert to complete the current selection.
        /// Returns only the remaining portion (what hasn't been typed yet).
        /// </summary>
        public string GetSelectedCompletion()
        {
            if (_mode == AutocompleteMode.Identifier)
            {
                if (_selectedIndex < 0 || _selectedIndex >= _identifierItems.Count)
                    return null;

                var item = _identifierItems[_selectedIndex];
                // Return only the suffix that hasn't been typed
                if (_typingPrefix.Length > 0 && item.Text.StartsWith(_typingPrefix, StringComparison.OrdinalIgnoreCase))
                    return item.Text.Substring(_typingPrefix.Length);
                return item.Text;
            }
            else
            {
                if (_selectedIndex < 0 || _selectedIndex >= _filteredItems.Count)
                    return null;

                var full = _filteredItems[_selectedIndex];
                if (_currentFilter.Length > 0 && full.StartsWith(_currentFilter, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(_currentFilter.Length);
                return full;
            }
        }

        public AutocompleteMode CurrentMode => _mode;

        public void Hide()
        {
            _isVisible = false;
            _mode = AutocompleteMode.None;
            _filteredItems.Clear();
            _identifierItems.Clear();
        }

        public void DrawPopup()
        {
            if (!_isVisible)
                return;

            if (_mode == AutocompleteMode.Property)
                DrawPropertyPopup();
            else if (_mode == AutocompleteMode.Identifier)
                DrawIdentifierPopup();
        }

        private void DrawPropertyPopup()
        {
            if (_filteredItems.Count == 0)
                return;

            InitStyles();

            int visibleCount = Mathf.Min(_filteredItems.Count, MaxVisible);
            float headerHeight = 22f;
            float height = visibleCount * ItemHeight + headerHeight + 8;
            Rect popupRect = new Rect(_popupPosition.x, _popupPosition.y, PopupWidth, height);

            // Background
            GUI.Box(popupRect, "", _boxStyle);

            // Header showing device category
            Rect headerRect = new Rect(popupRect.x + 5, popupRect.y + 3, PopupWidth - 10, headerHeight - 2);
            string categoryName = _currentCategory == DeviceCategory.Unknown ? "Device" : _currentCategory.ToString();
            GUI.Label(headerRect, $"[{categoryName}]", _categoryStyle);

            // Scroll view for items
            Rect viewRect = new Rect(0, 0, PopupWidth - 20, _filteredItems.Count * ItemHeight);
            Rect scrollRect = new Rect(popupRect.x + 3, popupRect.y + headerHeight + 3, PopupWidth - 6, height - headerHeight - 6);

            _scrollPosition = GUI.BeginScrollView(scrollRect, _scrollPosition, viewRect, false, _filteredItems.Count > MaxVisible);

            for (int i = 0; i < _filteredItems.Count; i++)
            {
                Rect itemRect = new Rect(0, i * ItemHeight, viewRect.width, ItemHeight);
                bool selected = (i == _selectedIndex);

                GUI.Box(itemRect, "", selected ? _selectedStyle : _itemStyle);
                GUI.Label(itemRect, "  " + _filteredItems[i], selected ? _selectedStyle : _itemStyle);

                if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawIdentifierPopup()
        {
            if (_identifierItems.Count == 0)
                return;

            InitStyles();

            int visibleCount = Mathf.Min(_identifierItems.Count, MaxVisible);
            float height = visibleCount * ItemHeight + 8;
            Rect popupRect = new Rect(_popupPosition.x, _popupPosition.y, PopupWidth, height);

            // Background
            GUI.Box(popupRect, "", _boxStyle);

            // Scroll view for items
            Rect viewRect = new Rect(0, 0, PopupWidth - 20, _identifierItems.Count * ItemHeight);
            Rect scrollRect = new Rect(popupRect.x + 3, popupRect.y + 4, PopupWidth - 6, height - 8);

            _scrollPosition = GUI.BeginScrollView(scrollRect, _scrollPosition, viewRect, false, _identifierItems.Count > MaxVisible);

            for (int i = 0; i < _identifierItems.Count; i++)
            {
                Rect itemRect = new Rect(0, i * ItemHeight, viewRect.width, ItemHeight);
                bool selected = (i == _selectedIndex);

                GUI.Box(itemRect, "", selected ? _selectedStyle : _itemStyle);

                // Draw kind indicator
                var item = _identifierItems[i];
                string kindChar = GetKindChar(item.Kind);
                Color kindColor = GetKindColor(item.Kind);

                // Kind icon
                Rect kindRect = new Rect(itemRect.x + 4, itemRect.y, 20, ItemHeight);
                var oldColor = GUI.color;
                GUI.color = kindColor;
                GUI.Label(kindRect, kindChar, _kindStyle ?? _itemStyle);
                GUI.color = oldColor;

                // Item text (highlight matching prefix)
                Rect textRect = new Rect(itemRect.x + 24, itemRect.y, itemRect.width - 28, ItemHeight);
                string displayText = item.Text;
                GUI.Label(textRect, displayText, selected ? _selectedStyle : _itemStyle);

                if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
                {
                    _selectedIndex = i;
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();
        }

        private string GetKindChar(IdentifierKind kind)
        {
            switch (kind)
            {
                case IdentifierKind.Variable: return "v";
                case IdentifierKind.Alias: return "a";
                case IdentifierKind.Constant: return "c";
                case IdentifierKind.Label: return "L";
                case IdentifierKind.Keyword: return "K";
                case IdentifierKind.Function: return "f";
                default: return "?";
            }
        }

        private Color GetKindColor(IdentifierKind kind)
        {
            switch (kind)
            {
                case IdentifierKind.Variable: return new Color(0.6f, 0.8f, 1f);    // Light blue
                case IdentifierKind.Alias: return new Color(1f, 0.8f, 0.5f);       // Orange
                case IdentifierKind.Constant: return new Color(0.8f, 0.6f, 1f);    // Purple
                case IdentifierKind.Label: return new Color(1f, 1f, 0.6f);         // Yellow
                case IdentifierKind.Keyword: return new Color(0.9f, 0.5f, 0.3f);   // Red-orange
                case IdentifierKind.Function: return new Color(0.6f, 1f, 0.6f);    // Green
                default: return Color.gray;
            }
        }

        #endregion

        #region Private Methods

        private void ParseDocument(string code)
        {
            _userAliases.Clear();
            _aliasCategories.Clear();
            _userVariables.Clear();
            _userConstants.Clear();
            _userLabels.Clear();

            if (string.IsNullOrEmpty(code)) return;

            // Match: ALIAS <name> = <target> or ALIAS <name> <target>
            var aliasRegex = new Regex(@"^\s*ALIAS\s+(\w+)\s*[=]?\s*(.+?)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match m in aliasRegex.Matches(code))
            {
                string aliasName = m.Groups[1].Value;
                string target = m.Groups[2].Value.Trim();
                
                _userAliases.Add(aliasName);
                
                // Infer device category
                var category = InferCategory(aliasName, target);
                _aliasCategories[aliasName] = category;
            }

            // Match: VAR <name> = <value> or VAR <name>
            var varRegex = new Regex(@"^\s*VAR\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match m in varRegex.Matches(code))
            {
                _userVariables.Add(m.Groups[1].Value);
            }

            // Match: CONST <name> = <value>
            var constRegex = new Regex(@"^\s*CONST\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match m in constRegex.Matches(code))
            {
                _userConstants.Add(m.Groups[1].Value);
            }

            // Match: <name>: (labels, must be at start of line, identifier followed by colon)
            var labelRegex = new Regex(@"^\s*([a-zA-Z_]\w*)\s*:", RegexOptions.Multiline);
            foreach (Match m in labelRegex.Matches(code))
            {
                string label = m.Groups[1].Value;
                // Exclude keywords that might look like labels
                if (!IsKeyword(label))
                    _userLabels.Add(label);
            }

            // Match: DEFINE <name> <value>
            var defineRegex = new Regex(@"^\s*DEFINE\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match m in defineRegex.Matches(code))
            {
                _userConstants.Add(m.Groups[1].Value);
            }
        }

        private bool IsKeyword(string word)
        {
            foreach (var kw in Keywords)
                if (kw.Equals(word, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>
        /// Check if the caret is currently inside a comment (after # on the current line).
        /// Also handles the case where # is inside a string literal.
        /// </summary>
        private bool IsInComment(TextEditor editor)
        {
            var caret = editor.CaretPosition;
            if (caret == null) return false;

            // Get the current line text
            string fullText = editor.Text;
            if (string.IsNullOrEmpty(fullText)) return false;

            string[] lines = fullText.Split('\n');
            if (caret.lineIndex >= lines.Length) return false;

            string line = lines[caret.lineIndex];
            int caretCol = Mathf.Min(caret.colIndex, line.Length);

            // Scan the line up to the caret position to find if we're after a #
            // We need to track if we're inside a string to ignore # inside strings
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < caretCol; i++)
            {
                char c = line[i];

                if (inString)
                {
                    // Check for escape sequences
                    if (c == '\\' && i + 1 < line.Length)
                    {
                        i++; // Skip next character
                        continue;
                    }
                    // Check for end of string
                    if (c == stringChar)
                    {
                        inString = false;
                    }
                }
                else
                {
                    // Check for start of string (standard quotes only for simplicity)
                    if (c == '"' || c == '\'')
                    {
                        inString = true;
                        stringChar = c;
                    }
                    // Check for comment start (not inside string)
                    else if (c == '#')
                    {
                        return true; // We're in a comment!
                    }
                }
            }

            return false;
        }

        private DeviceCategory InferCategory(string aliasName, string target)
        {
            // First, try to match alias name against known patterns
            foreach (var kvp in NamePatterns)
            {
                if (aliasName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return kvp.Value;
                }
            }

            // Check target for IC.Device with known hash
            // Example: IC.Device[-815193061] - this is a display device hash
            var hashMatch = Regex.Match(target, @"IC\.Device\[(-?\d+)\]", RegexOptions.IgnoreCase);
            if (hashMatch.Success)
            {
                long hash = long.Parse(hashMatch.Groups[1].Value);
                return InferFromHash(hash);
            }

            // Check target for Name hints
            // Example: .Name["o2Readout"] - "readout" suggests display
            var nameMatch = Regex.Match(target, @"\.Name\[""([^""]+)""\]", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                string deviceName = nameMatch.Groups[1].Value;
                foreach (var kvp in NamePatterns)
                {
                    if (deviceName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return kvp.Value;
                    }
                }
            }

            return DeviceCategory.Unknown;
        }

        private DeviceCategory InferFromHash(long hash)
        {
            // Known device hashes (partial list - can be expanded)
            // These are PrefabHash values from Stationeers
            switch (hash)
            {
                // Displays
                case -815193061: // Small LED Display
                case -815193062:
                case 1947941498: // Console LED Display
                    return DeviceCategory.Display;
                
                // Gas Sensors
                case -1252983604: // Gas Sensor
                case 1155865682:  // Wall Gas Sensor
                    return DeviceCategory.GasSensor;
                
                // Vents
                case -1716238413: // Active Vent
                case 1840110321:  // Passive Vent
                    return DeviceCategory.Vent;
                
                // Lights
                case -1894548089: // Wall Light
                case 1537050543:  // Grow Light
                    return DeviceCategory.Light;
                
                // Add more as needed...
                default:
                    return DeviceCategory.Unknown;
            }
        }

        private Vector2 CalculatePosition(TextEditor editor)
        {
            var caret = editor.CaretPosition;
            float charW = editor.CharacterWidth;
            float charH = editor.CharacterHeight;

            float x = 80 + (caret.colIndex * charW);
            float y = 60 + ((caret.lineIndex + 1) * charH);

            float h = Mathf.Min(_filteredItems.Count, MaxVisible) * ItemHeight + 30;
            x = Mathf.Clamp(x, 10, Screen.width - PopupWidth - 10);
            y = Mathf.Clamp(y, 10, Screen.height - h - 10);

            return new Vector2(x, y);
        }

        private void EnsureVisible()
        {
            float selectedY = _selectedIndex * ItemHeight;
            float viewH = MaxVisible * ItemHeight;

            if (selectedY < _scrollPosition.y)
                _scrollPosition.y = selectedY;
            else if (selectedY + ItemHeight > _scrollPosition.y + viewH)
                _scrollPosition.y = selectedY + ItemHeight - viewH;
        }

        private void InitStyles()
        {
            if (_stylesInit) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(new Color(0.1f, 0.1f, 0.12f, 0.97f));

            _itemStyle = new GUIStyle(GUI.skin.label);
            _itemStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            _itemStyle.fontSize = 12;
            _itemStyle.padding = new RectOffset(4, 4, 2, 2);

            _selectedStyle = new GUIStyle(_itemStyle);
            _selectedStyle.normal.background = MakeTex(new Color(0.25f, 0.45f, 0.75f, 1f));
            _selectedStyle.normal.textColor = Color.white;

            _categoryStyle = new GUIStyle(GUI.skin.label);
            _categoryStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
            _categoryStyle.fontSize = 11;
            _categoryStyle.fontStyle = FontStyle.Italic;

            _kindStyle = new GUIStyle(GUI.skin.label);
            _kindStyle.fontSize = 11;
            _kindStyle.fontStyle = FontStyle.Bold;
            _kindStyle.alignment = TextAnchor.MiddleCenter;
            _kindStyle.padding = new RectOffset(0, 0, 2, 2);

            _stylesInit = true;
        }

        private Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        #endregion
    }
}


