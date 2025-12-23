// DeviceAutocomplete.cs - Context-aware autocomplete for BASIC IC10
// Supports two modes:
//   1. Property completion (on '.') - shows device LogicTypes
//   2. Identifier completion (while typing) - shows variables, aliases, keywords, functions

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
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

        // Name patterns to infer device type - loaded from JSON for localization support
        private static Dictionary<string, DeviceCategory> _namePatterns;
        private static Dictionary<string, DeviceCategory> NamePatterns
        {
            get
            {
                if (_namePatterns == null)
                {
                    LoadLanguageData();
                }
                return _namePatterns;
            }
        }
        
        private static readonly Dictionary<string, DeviceCategory> NamePatternsFallback = new Dictionary<string, DeviceCategory>(StringComparer.OrdinalIgnoreCase)
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

        // === ALL LogicTypes from game's Assembly-CSharp.dll ===
        // Extracted via ILSpy from Assets.Scripts.Objects.Motherboards.LogicType
        // Loaded from LanguageData.json for localization support
        private static string[] _allLogicTypes;
        private static string[] AllLogicTypes
        {
            get
            {
                if (_allLogicTypes == null)
                {
                    LoadLanguageData();
                }
                return _allLogicTypes;
            }
        }

        private static readonly string[] AllLogicTypesFallback = new[]
        {
            "None", "Power", "Open", "Mode", "Error", "Pressure", "Temperature",
            "PressureExternal", "PressureInternal", "Activate", "Lock", "Charge", "Setting",
            "Reagents", "RatioOxygen", "RatioCarbonDioxide", "RatioNitrogen", "RatioPollutant",
            "RatioVolatiles", "RatioWater", "Horizontal", "Vertical", "SolarAngle", "Maximum",
            "Ratio", "PowerPotential", "PowerActual", "Quantity", "On", "ImportQuantity",
            "ImportSlotOccupant", "ExportQuantity", "ExportSlotOccupant", "RequiredPower",
            "HorizontalRatio", "VerticalRatio", "PowerRequired", "Idle", "Color", "ElevatorSpeed",
            "ElevatorLevel", "RecipeHash", "ExportSlotHash", "ImportSlotHash",
            "PlantHealth1", "PlantHealth2", "PlantHealth3", "PlantHealth4",
            "PlantGrowth1", "PlantGrowth2", "PlantGrowth3", "PlantGrowth4",
            "PlantEfficiency1", "PlantEfficiency2", "PlantEfficiency3", "PlantEfficiency4",
            "PlantHash1", "PlantHash2", "PlantHash3", "PlantHash4",
            "RequestHash", "CompletionRatio", "ClearMemory", "ExportCount", "ImportCount",
            "PowerGeneration", "TotalMoles", "Volume", "Plant", "Harvest", "Output",
            "PressureSetting", "TemperatureSetting", "TemperatureExternal", "Filtration",
            "AirRelease", "PositionX", "PositionY", "PositionZ", "VelocityMagnitude",
            "VelocityRelativeX", "VelocityRelativeY", "VelocityRelativeZ", "RatioNitrousOxide",
            "PrefabHash", "ForceWrite", "SignalStrength", "SignalID", "TargetX", "TargetY", "TargetZ",
            "SettingInput", "SettingOutput", "CurrentResearchPodType", "ManualResearchRequiredPod",
            "MineablesInVicinity", "MineablesInQueue", "NextWeatherEventTime", "Combustion", "Fuel",
            "ReturnFuelCost", "CollectableGoods", "Time", "Bpm", "EnvironmentEfficiency",
            "WorkingGasEfficiency", "PressureInput", "TemperatureInput",
            "RatioOxygenInput", "RatioCarbonDioxideInput", "RatioNitrogenInput", "RatioPollutantInput",
            "RatioVolatilesInput", "RatioWaterInput", "RatioNitrousOxideInput", "TotalMolesInput",
            "PressureInput2", "TemperatureInput2", "RatioOxygenInput2", "RatioCarbonDioxideInput2",
            "RatioNitrogenInput2", "RatioPollutantInput2", "RatioVolatilesInput2", "RatioWaterInput2",
            "RatioNitrousOxideInput2", "TotalMolesInput2", "PressureOutput", "TemperatureOutput",
            "RatioOxygenOutput", "RatioCarbonDioxideOutput", "RatioNitrogenOutput", "RatioPollutantOutput",
            "RatioVolatilesOutput", "RatioWaterOutput", "RatioNitrousOxideOutput", "TotalMolesOutput",
            "PressureOutput2", "TemperatureOutput2", "RatioOxygenOutput2", "RatioCarbonDioxideOutput2",
            "RatioNitrogenOutput2", "RatioPollutantOutput2", "RatioVolatilesOutput2", "RatioWaterOutput2",
            "RatioNitrousOxideOutput2", "TotalMolesOutput2", "CombustionInput", "CombustionInput2",
            "CombustionOutput", "CombustionOutput2", "OperationalTemperatureEfficiency",
            "TemperatureDifferentialEfficiency", "PressureEfficiency", "CombustionLimiter", "Throttle",
            "Rpm", "Stress", "InterrogationProgress", "TargetPadIndex", "SizeX", "SizeY", "SizeZ",
            "MinimumWattsToContact", "WattsReachingContact", "Channel0", "Channel1", "Channel2",
            "Channel3", "Channel4", "Channel5", "Channel6", "Channel7", "LineNumber", "Flush",
            "SoundAlert", "SolarIrradiance", "RatioLiquidNitrogen", "RatioLiquidNitrogenInput",
            "RatioLiquidNitrogenInput2", "RatioLiquidNitrogenOutput", "RatioLiquidNitrogenOutput2",
            "VolumeOfLiquid", "RatioLiquidOxygen", "RatioLiquidOxygenInput", "RatioLiquidOxygenInput2",
            "RatioLiquidOxygenOutput", "RatioLiquidOxygenOutput2", "RatioLiquidVolatiles",
            "RatioLiquidVolatilesInput", "RatioLiquidVolatilesInput2", "RatioLiquidVolatilesOutput",
            "RatioLiquidVolatilesOutput2", "RatioSteam", "RatioSteamInput", "RatioSteamInput2",
            "RatioSteamOutput", "RatioSteamOutput2", "ContactTypeId", "RatioLiquidCarbonDioxide",
            "RatioLiquidCarbonDioxideInput", "RatioLiquidCarbonDioxideInput2",
            "RatioLiquidCarbonDioxideOutput", "RatioLiquidCarbonDioxideOutput2", "RatioLiquidPollutant",
            "RatioLiquidPollutantInput", "RatioLiquidPollutantInput2", "RatioLiquidPollutantOutput",
            "RatioLiquidPollutantOutput2", "RatioLiquidNitrousOxide", "RatioLiquidNitrousOxideInput",
            "RatioLiquidNitrousOxideInput2", "RatioLiquidNitrousOxideOutput",
            "RatioLiquidNitrousOxideOutput2", "Progress", "DestinationCode", "Acceleration", "ReferenceId",
            "AutoShutOff", "Mass", "DryMass", "Thrust", "Weight", "ThrustToWeight", "TimeToDestination",
            "BurnTimeRemaining", "AutoLand", "ForwardX", "ForwardY", "ForwardZ", "Orientation",
            "VelocityX", "VelocityY", "VelocityZ", "PassedMoles", "ExhaustVelocity", "FlightControlRule",
            "ReEntryAltitude", "Apex", "EntityState", "DrillCondition", "Index", "CelestialHash",
            "AlignmentError", "DistanceAu", "OrbitPeriod", "Inclination", "Eccentricity", "SemiMajorAxis",
            "DistanceKm", "CelestialParentHash", "TrueAnomaly", "RatioHydrogen", "RatioLiquidHydrogen",
            "RatioPollutedWater", "Discover", "Chart", "Survey", "NavPoints", "ChartedNavPoints", "Sites",
            "CurrentCode", "Density", "Richness", "Size", "TotalQuantity", "MinedQuantity",
            "BestContactFilter", "NameHash", "Altitude", "TargetSlotIndex", "TargetPrefabHash", "Extended",
            "NetworkFault", "ProportionalGain", "IntegralGain", "DerivativeGain", "Minimum", "Setpoint",
            "Reset", "StackSize", "NextWeatherHash"
        };

        // === ALL LogicSlotTypes from game's Assembly-CSharp.dll ===
        // Extracted via ILSpy from Assets.Scripts.Objects.Motherboards.LogicSlotType
        // Loaded from LanguageData.json for localization support
        private static string[] _allSlotLogicTypes;
        private static string[] AllSlotLogicTypes
        {
            get
            {
                if (_allSlotLogicTypes == null)
                {
                    LoadLanguageData();
                }
                return _allSlotLogicTypes;
            }
        }

        private static readonly string[] AllSlotLogicTypesFallback = new[]
        {
            "Occupied", "OccupantHash", "Quantity", "Damage", "Efficiency", "Health", "Growth",
            "Pressure", "Temperature", "Charge", "ChargeRatio", "Class", "PressureWaste", "PressureAir",
            "MaxQuantity", "Mature", "PrefabHash", "Seeding", "LineNumber", "Volume", "Open", "On",
            "Lock", "SortingClass", "FilterType", "ReferenceId", "HarvestedHash", "Mode", "MaturityRatio",
            "SeedingRatio", "FreeSlots", "TotalSlots"
        };

        // LogicTypes grouped by device category (loaded from JSON file)
        private static Dictionary<DeviceCategory, string[]> _categoryLogicTypes;
        private static Dictionary<DeviceCategory, string[]> CategoryLogicTypes
        {
            get
            {
                if (_categoryLogicTypes == null)
                {
                    LoadCategoryLogicTypesFromJson();
                }
                return _categoryLogicTypes;
            }
        }

        // Helper class for deserializing LanguageData.json
        private class LanguageDataJson
        {
            public string[] AllLogicTypes { get; set; }
            public string[] AllSlotLogicTypes { get; set; }
            public string[] Keywords { get; set; }
            public string[] Functions { get; set; }
            public Dictionary<string, string[]> NamePatterns { get; set; }
            public Dictionary<string, string[]> PrefabHashLookup { get; set; }
        }

        // Load language-specific data from JSON file
        private static void LoadLanguageData()
        {
            try
            {
                // Try multiple possible locations for the JSON file
                string[] possiblePaths = new[]
                {
                    // Production: Game's managed directory
                    Path.Combine(Application.dataPath, "Managed", "InGameTextEditor", "LanguageData.json"),
                    // Development: Next to the DLL in bin output
                    Path.Combine(Path.GetDirectoryName(typeof(DeviceAutocomplete).Assembly.Location), "LanguageData.json"),
                    // Development alternative: InGameTextEditor folder relative to DLL
                    Path.Combine(Path.GetDirectoryName(typeof(DeviceAutocomplete).Assembly.Location), "InGameTextEditor", "LanguageData.json")
                };
                
                string jsonPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        jsonPath = path;
                        break;
                    }
                }
                
                if (jsonPath == null)
                {
                    Debug.LogError($"LanguageData.json not found in any of the expected locations");
                    LoadFallbackLanguageData();
                    return;
                }
                
                string jsonContent = File.ReadAllText(jsonPath);
                var data = JsonConvert.DeserializeObject<LanguageDataJson>(jsonContent);
                
                _allLogicTypes = data.AllLogicTypes ?? AllLogicTypesFallback;
                _allSlotLogicTypes = data.AllSlotLogicTypes ?? AllSlotLogicTypesFallback;
                _keywords = data.Keywords ?? KeywordsFallback;
                _functions = data.Functions ?? FunctionsFallback;
                
                // Load NamePatterns from JSON (convert from string[] to flattened dictionary)
                if (data.NamePatterns != null)
                {
                    _namePatterns = new Dictionary<string, DeviceCategory>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in data.NamePatterns)
                    {
                        if (Enum.TryParse<DeviceCategory>(kvp.Key, out var category))
                        {
                            foreach (var pattern in kvp.Value)
                            {
                                _namePatterns[pattern] = category;
                            }
                        }
                    }
                }
                else
                {
                    _namePatterns = NamePatternsFallback;
                }
                
                // Load PrefabHashLookup from JSON (convert string keys to int)
                if (data.PrefabHashLookup != null)
                {
                    _prefabHashLookup = new Dictionary<int, (string Title, string PrefabName)>();
                    foreach (var kvp in data.PrefabHashLookup)
                    {
                        if (int.TryParse(kvp.Key, out int hash) && kvp.Value.Length >= 2)
                        {
                            _prefabHashLookup[hash] = (kvp.Value[0], kvp.Value[1]);
                        }
                    }
                }
                else
                {
                    _prefabHashLookup = PrefabHashLookupFallback;
                }
                
                Debug.Log($"Loaded language data: {_allLogicTypes.Length} LogicTypes, {_allSlotLogicTypes.Length} SlotTypes, {_keywords.Length} Keywords, {_functions.Length} Functions, {_namePatterns.Count} NamePatterns, {_prefabHashLookup.Count} PrefabHashes");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading LanguageData.json: {ex.Message}");
                LoadFallbackLanguageData();
            }
        }

        // Load fallback data if JSON fails
        private static void LoadFallbackLanguageData()
        {
            _allLogicTypes = AllLogicTypesFallback;
            _allSlotLogicTypes = AllSlotLogicTypesFallback;
            _keywords = KeywordsFallback;
            _functions = FunctionsFallback;
            _namePatterns = NamePatternsFallback;
            _prefabHashLookup = PrefabHashLookupFallback;
        }

        // Load CategoryLogicTypes from JSON file
        private static void LoadCategoryLogicTypesFromJson()
        {
            try
            {
                // Try multiple possible locations for the JSON file
                string[] possiblePaths = new[]
                {
                    // Production: Game's managed directory
                    Path.Combine(Application.dataPath, "Managed", "InGameTextEditor", "DeviceLogicTypes.json"),
                    // Development: Next to the DLL in bin output
                    Path.Combine(Path.GetDirectoryName(typeof(DeviceAutocomplete).Assembly.Location), "DeviceLogicTypes.json"),
                    // Development alternative: InGameTextEditor folder relative to DLL
                    Path.Combine(Path.GetDirectoryName(typeof(DeviceAutocomplete).Assembly.Location), "InGameTextEditor", "DeviceLogicTypes.json")
                };
                
                string jsonPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        jsonPath = path;
                        break;
                    }
                }
                
                if (jsonPath == null)
                {
                    Debug.LogError($"DeviceLogicTypes.json not found in any of the expected locations");
                    _categoryLogicTypes = GetFallbackCategoryLogicTypes();
                    return;
                }
                
                string jsonContent = File.ReadAllText(jsonPath);
                var jsonDict = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(jsonContent);
                
                _categoryLogicTypes = new Dictionary<DeviceCategory, string[]>();
                foreach (var kvp in jsonDict)
                {
                    if (Enum.TryParse<DeviceCategory>(kvp.Key, out var category))
                    {
                        _categoryLogicTypes[category] = kvp.Value;
                    }
                }
                
                Debug.Log($"Loaded {_categoryLogicTypes.Count} device categories from DeviceLogicTypes.json");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading DeviceLogicTypes.json: {ex.Message}");
                _categoryLogicTypes = GetFallbackCategoryLogicTypes();
            }
        }

        // Fallback data in case JSON fails to load
        private static Dictionary<DeviceCategory, string[]> GetFallbackCategoryLogicTypes()
        {
            return new Dictionary<DeviceCategory, string[]>
            {
                { DeviceCategory.GasSensor, new[] {
                // Common
                "Activate", "Error", "Lock", "Mode", "On", "Power", "PowerActual", "PowerRequired",
                "PrefabHash", "ReferenceId", "RequiredPower",
                // Gas sensing
                "Pressure", "Temperature", "TotalMoles", "Volume",
                "RatioCarbonDioxide", "RatioNitrogen", "RatioNitrousOxide", "RatioOxygen",
                "RatioPollutant", "RatioVolatiles", "RatioWater", "RatioHydrogen",
                // Liquid ratios
                "RatioLiquidCarbonDioxide", "RatioLiquidNitrogen", "RatioLiquidNitrousOxide",
                "RatioLiquidOxygen", "RatioLiquidPollutant", "RatioLiquidVolatiles", "RatioLiquidHydrogen",
                "RatioSteam", "RatioPollutedWater", "VolumeOfLiquid"
            }},
            
            { DeviceCategory.Display, new[] {
                "Activate", "Color", "Error", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "ReferenceId", "RequiredPower", "Setting"
            }},
            
            { DeviceCategory.Light, new[] {
                "Activate", "Color", "Error", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "ReferenceId", "RequiredPower", "Setting"
            }},
            
            { DeviceCategory.Vent, new[] {
                "Activate", "AirRelease", "Error", "Lock", "Mode", "On", "Open", "Power",
                "PowerActual", "PowerRequired", "PrefabHash", "Pressure", "PressureExternal",
                "PressureInternal", "PressureSetting", "ReferenceId", "RequiredPower", "Setting",
                "Temperature", "TemperatureExternal", "TemperatureSetting"
            }},
            
            { DeviceCategory.Pump, new[] {
                // Common
                "Activate", "Error", "Filtration", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "ReferenceId", "RequiredPower", "Setting",
                // Primary atmosphere
                "Pressure", "PressureSetting", "Temperature", "TemperatureSetting", "TotalMoles", "Volume",
                // Gas ratios
                "RatioCarbonDioxide", "RatioNitrogen", "RatioNitrousOxide", "RatioOxygen",
                "RatioPollutant", "RatioVolatiles", "RatioWater", "RatioHydrogen",
                // Input port
                "PressureInput", "TemperatureInput", "TotalMolesInput",
                "RatioCarbonDioxideInput", "RatioNitrogenInput", "RatioNitrousOxideInput",
                "RatioOxygenInput", "RatioPollutantInput", "RatioVolatilesInput", "RatioWaterInput",
                // Input port 2
                "PressureInput2", "TemperatureInput2", "TotalMolesInput2",
                "RatioCarbonDioxideInput2", "RatioNitrogenInput2", "RatioNitrousOxideInput2",
                "RatioOxygenInput2", "RatioPollutantInput2", "RatioVolatilesInput2", "RatioWaterInput2",
                // Output port
                "PressureOutput", "TemperatureOutput", "TotalMolesOutput",
                "RatioCarbonDioxideOutput", "RatioNitrogenOutput", "RatioNitrousOxideOutput",
                "RatioOxygenOutput", "RatioPollutantOutput", "RatioVolatilesOutput", "RatioWaterOutput",
                // Output port 2
                "PressureOutput2", "TemperatureOutput2", "TotalMolesOutput2",
                "RatioCarbonDioxideOutput2", "RatioNitrogenOutput2", "RatioNitrousOxideOutput2",
                "RatioOxygenOutput2", "RatioPollutantOutput2", "RatioVolatilesOutput2", "RatioWaterOutput2",
                // Liquid ratios
                "RatioLiquidCarbonDioxide", "RatioLiquidNitrogen", "RatioLiquidNitrousOxide",
                "RatioLiquidOxygen", "RatioLiquidPollutant", "RatioLiquidVolatiles",
                "RatioLiquidCarbonDioxideInput", "RatioLiquidNitrogenInput", "RatioLiquidNitrousOxideInput",
                "RatioLiquidOxygenInput", "RatioLiquidPollutantInput", "RatioLiquidVolatilesInput",
                "RatioLiquidCarbonDioxideOutput", "RatioLiquidNitrogenOutput", "RatioLiquidNitrousOxideOutput",
                "RatioLiquidOxygenOutput", "RatioLiquidPollutantOutput", "RatioLiquidVolatilesOutput",
                "RatioSteam", "RatioSteamInput", "RatioSteamOutput", "VolumeOfLiquid",
                // Efficiency
                "EnvironmentEfficiency", "WorkingGasEfficiency", "OperationalTemperatureEfficiency",
                "TemperatureDifferentialEfficiency", "PressureEfficiency"
            }},
            
            { DeviceCategory.Hydroponics, new[] {
                // Device LogicTypes
                "Activate", "Error", "Harvest", "Lock", "Mode", "On", "Plant", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "Pressure", "ReferenceId", "RequiredPower", "Temperature",
                // Per-slot plant data (1-4)
                "PlantEfficiency1", "PlantEfficiency2", "PlantEfficiency3", "PlantEfficiency4",
                "PlantGrowth1", "PlantGrowth2", "PlantGrowth3", "PlantGrowth4",
                "PlantHash1", "PlantHash2", "PlantHash3", "PlantHash4",
                "PlantHealth1", "PlantHealth2", "PlantHealth3", "PlantHealth4",
                // Environment
                "EnvironmentEfficiency",
                // Slot LogicTypes (unique ones not already listed above)
                "Charge", "ChargeRatio", "Class", "Damage", "Efficiency", "FilterType", "FreeSlots",
                "Growth", "HarvestedHash", "Health", "Mature", "MaturityRatio", "MaxQuantity",
                "OccupantHash", "Occupied", "Open", "Quantity", "Seeding", "SeedingRatio",
                "SortingClass", "TotalSlots", "Volume"
            }},
            
            { DeviceCategory.Logic, new[] {
                // Common
                "Activate", "Error", "Lock", "Mode", "On", "Power", "PowerActual", "PowerRequired",
                "PrefabHash", "ReferenceId", "RequiredPower", "Setting",
                // Channels
                "Channel0", "Channel1", "Channel2", "Channel3", "Channel4", "Channel5", "Channel6", "Channel7",
                // Memory/IC
                "ClearMemory", "Flush", "LineNumber", "StackSize",
                // Math unit
                "SettingInput", "SettingOutput",
                // Comparator/selector
                "Minimum", "Maximum",
                // PID controller
                "ProportionalGain", "IntegralGain", "DerivativeGain", "Setpoint", "Reset",
                // Networking
                "SignalID", "SignalStrength"
            }},
            
            { DeviceCategory.Power, new[] {
                // Common
                "Activate", "Charge", "Error", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerGeneration", "PowerPotential", "PowerRequired", "PrefabHash", "Ratio",
                "ReferenceId", "RequiredPower", "Setting",
                // Solar tracking
                "Horizontal", "HorizontalRatio", "SolarAngle", "SolarIrradiance", "Vertical", "VerticalRatio",
                // Battery
                "Maximum",
                // Generator
                "Combustion", "CombustionInput", "CombustionInput2", "CombustionOutput", "CombustionOutput2",
                "CombustionLimiter", "Fuel", "Rpm", "Throttle",
                // Efficiency
                "EnvironmentEfficiency", "OperationalTemperatureEfficiency", "TemperatureDifferentialEfficiency",
                // APC/Transformer
                "NetworkFault"
            }},
            
            { DeviceCategory.Fabricator, new[] {
                // Common
                "Activate", "Error", "Idle", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "Progress", "ReferenceId", "RequiredPower",
                // Recipe/production
                "CompletionRatio", "Quantity", "RecipeHash", "RequestHash",
                // Import/Export
                "ExportCount", "ExportQuantity", "ExportSlotHash", "ExportSlotOccupant",
                "ImportCount", "ImportQuantity", "ImportSlotHash", "ImportSlotOccupant",
                // Temperature
                "Temperature", "TemperatureSetting", "TemperatureInput", "TemperatureOutput",
                // Combustion (furnace/smelter)
                "Combustion", "CombustionInput", "CombustionInput2", "CombustionOutput", "CombustionOutput2",
                // Pressure (centrifuge/electrolyzer)
                "Pressure", "PressureInput", "PressureOutput", "PressureSetting",
                // Efficiency
                "EnvironmentEfficiency", "OperationalTemperatureEfficiency",
                // Slot types for items
                "Charge", "ChargeRatio", "Class", "Damage", "FreeSlots", "MaxQuantity",
                "OccupantHash", "Occupied", "SortingClass", "TotalSlots"
            }},
            
            { DeviceCategory.Door, new[] {
                "Activate", "Error", "Lock", "Mode", "On", "Open", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "ReferenceId", "RequiredPower", "Setting"
            }},
            
            { DeviceCategory.Console, new[] {
                // Common
                "Activate", "Color", "Error", "Lock", "Mode", "On", "Power", "PowerActual",
                "PowerRequired", "PrefabHash", "ReferenceId", "RequiredPower", "Setting",
                // Channels
                "Channel0", "Channel1", "Channel2", "Channel3", "Channel4", "Channel5", "Channel6", "Channel7",
                // Satellite dish / GPS
                "SignalID", "SignalStrength", "ContactTypeId", "BestContactFilter",
                "MinimumWattsToContact", "WattsReachingContact", "TargetSlotIndex", "TargetPrefabHash",
                // Research
                "CurrentResearchPodType", "ManualResearchRequiredPod",
                // Mining
                "CollectableGoods", "MineablesInQueue", "MineablesInVicinity",
                // Weather
                "NextWeatherEventTime", "NextWeatherHash",
                // Celestial tracking
                "AlignmentError", "CelestialHash", "CelestialParentHash", "Chart", "ChartedNavPoints",
                "CurrentCode", "Density", "DestinationCode", "Discover", "DistanceAu", "DistanceKm",
                "Eccentricity", "Inclination", "MinedQuantity", "NavPoints", "OrbitPeriod", "Richness",
                "SemiMajorAxis", "Sites", "Size", "Survey", "TotalQuantity", "TrueAnomaly"
            }},
            
            { DeviceCategory.Tank, new[] {
                // Common
                "Activate", "Error", "Lock", "On", "PrefabHash", "ReferenceId",
                // Pressure/Temperature
                "Pressure", "PressureInternal", "PressureExternal", "Temperature", "TotalMoles", "Volume",
                // Gas ratios
                "RatioCarbonDioxide", "RatioHydrogen", "RatioNitrogen", "RatioNitrousOxide",
                "RatioOxygen", "RatioPollutant", "RatioVolatiles", "RatioWater",
                // Liquid ratios
                "RatioLiquidCarbonDioxide", "RatioLiquidHydrogen", "RatioLiquidNitrogen",
                "RatioLiquidNitrousOxide", "RatioLiquidOxygen", "RatioLiquidPollutant",
                "RatioLiquidVolatiles", "RatioPollutedWater", "RatioSteam", "VolumeOfLiquid"
            }},
            
            { DeviceCategory.Rocket, new[] {
                // Common
                "Activate", "Error", "Lock", "Mode", "On", "Power", "PowerActual", "PowerRequired",
                "PrefabHash", "ReferenceId", "RequiredPower", "Setting",
                // Position/Velocity
                "Altitude", "ForwardX", "ForwardY", "ForwardZ", "Orientation",
                "PositionX", "PositionY", "PositionZ",
                "VelocityMagnitude", "VelocityRelativeX", "VelocityRelativeY", "VelocityRelativeZ",
                "VelocityX", "VelocityY", "VelocityZ",
                // Navigation
                "Acceleration", "AlignmentError", "Apex", "AutoLand", "BurnTimeRemaining",
                "DestinationCode", "FlightControlRule", "ReEntryAltitude", "TargetPadIndex",
                "TargetX", "TargetY", "TargetZ", "TimeToDestination",
                // Engine
                "DryMass", "ExhaustVelocity", "ForceWrite", "Fuel", "Horizontal", "HorizontalRatio",
                "Mass", "PassedMoles", "ReturnFuelCost", "Rpm", "Stress", "Throttle", "Thrust",
                "ThrustToWeight", "Vertical", "VerticalRatio", "Weight",
                // Status
                "AutoShutOff", "DrillCondition", "EntityState", "Extended", "Index",
                // Celestial
                "CelestialHash", "CelestialParentHash", "DistanceAu", "DistanceKm",
                "Eccentricity", "Inclination", "OrbitPeriod", "SemiMajorAxis", "TrueAnomaly"
            }},
            
            { DeviceCategory.Unknown, new[] {
                // Complete list of ALL LogicTypes for unknown devices
                "Acceleration", "Activate", "AirRelease", "AlignmentError", "Altitude", "Apex",
                "AutoLand", "AutoShutOff", "BestContactFilter", "Bpm", "BurnTimeRemaining",
                "CelestialHash", "CelestialParentHash", "Channel0", "Channel1", "Channel2", "Channel3",
                "Channel4", "Channel5", "Channel6", "Channel7", "Charge", "ChargeRatio", "Chart",
                "ChartedNavPoints", "Class", "ClearMemory", "CollectableGoods", "Color", "Combustion",
                "CombustionInput", "CombustionInput2", "CombustionLimiter", "CombustionOutput",
                "CombustionOutput2", "CompletionRatio", "ContactTypeId", "CurrentCode",
                "CurrentResearchPodType", "Damage", "Density", "DerivativeGain", "DestinationCode",
                "Discover", "DistanceAu", "DistanceKm", "DrillCondition", "DryMass", "Eccentricity",
                "Efficiency", "ElevatorLevel", "ElevatorSpeed", "EntityState", "EnvironmentEfficiency",
                "Error", "ExhaustVelocity", "ExportCount", "ExportQuantity", "ExportSlotHash",
                "ExportSlotOccupant", "Extended", "FilterType", "Filtration", "FlightControlRule",
                "Flush", "ForceWrite", "ForwardX", "ForwardY", "ForwardZ", "FreeSlots", "Fuel",
                "Growth", "HarvestedHash", "Health", "Horizontal", "HorizontalRatio", "Idle",
                "ImportCount", "ImportQuantity", "ImportSlotHash", "ImportSlotOccupant", "Inclination",
                "Index", "IntegralGain", "InterrogationProgress", "LineNumber", "Lock",
                "ManualResearchRequiredPod", "Mass", "Mature", "MaturityRatio", "Maximum", "MaxQuantity",
                "MinedQuantity", "MineablesInQueue", "MineablesInVicinity", "Minimum",
                "MinimumWattsToContact", "Mode", "NameHash", "NavPoints", "NetworkFault",
                "NextWeatherEventTime", "NextWeatherHash", "OccupantHash", "Occupied", "On", "Open",
                "OperationalTemperatureEfficiency", "OrbitPeriod", "Orientation", "Output",
                "PassedMoles", "Plant", "PlantEfficiency1", "PlantEfficiency2", "PlantEfficiency3",
                "PlantEfficiency4", "PlantGrowth1", "PlantGrowth2", "PlantGrowth3", "PlantGrowth4",
                "PlantHash1", "PlantHash2", "PlantHash3", "PlantHash4", "PlantHealth1", "PlantHealth2",
                "PlantHealth3", "PlantHealth4", "PositionX", "PositionY", "PositionZ", "Power",
                "PowerActual", "PowerGeneration", "PowerPotential", "PowerRequired", "PrefabHash",
                "Pressure", "PressureAir", "PressureEfficiency", "PressureExternal", "PressureInput",
                "PressureInput2", "PressureInternal", "PressureOutput", "PressureOutput2",
                "PressureSetting", "PressureWaste", "Progress", "ProportionalGain", "Quantity", "Ratio",
                "RatioCarbonDioxide", "RatioCarbonDioxideInput", "RatioCarbonDioxideInput2",
                "RatioCarbonDioxideOutput", "RatioCarbonDioxideOutput2", "RatioHydrogen",
                "RatioLiquidCarbonDioxide", "RatioLiquidCarbonDioxideInput",
                "RatioLiquidCarbonDioxideInput2", "RatioLiquidCarbonDioxideOutput",
                "RatioLiquidCarbonDioxideOutput2", "RatioLiquidHydrogen", "RatioLiquidNitrogen",
                "RatioLiquidNitrogenInput", "RatioLiquidNitrogenInput2", "RatioLiquidNitrogenOutput",
                "RatioLiquidNitrogenOutput2", "RatioLiquidNitrousOxide", "RatioLiquidNitrousOxideInput",
                "RatioLiquidNitrousOxideInput2", "RatioLiquidNitrousOxideOutput",
                "RatioLiquidNitrousOxideOutput2", "RatioLiquidOxygen", "RatioLiquidOxygenInput",
                "RatioLiquidOxygenInput2", "RatioLiquidOxygenOutput", "RatioLiquidOxygenOutput2",
                "RatioLiquidPollutant", "RatioLiquidPollutantInput", "RatioLiquidPollutantInput2",
                "RatioLiquidPollutantOutput", "RatioLiquidPollutantOutput2", "RatioLiquidVolatiles",
                "RatioLiquidVolatilesInput", "RatioLiquidVolatilesInput2", "RatioLiquidVolatilesOutput",
                "RatioLiquidVolatilesOutput2", "RatioNitrogen", "RatioNitrogenInput",
                "RatioNitrogenInput2", "RatioNitrogenOutput", "RatioNitrogenOutput2",
                "RatioNitrousOxide", "RatioNitrousOxideInput", "RatioNitrousOxideInput2",
                "RatioNitrousOxideOutput", "RatioNitrousOxideOutput2", "RatioOxygen", "RatioOxygenInput",
                "RatioOxygenInput2", "RatioOxygenOutput", "RatioOxygenOutput2", "RatioPollutant",
                "RatioPollutantInput", "RatioPollutantInput2", "RatioPollutantOutput",
                "RatioPollutantOutput2", "RatioPollutedWater", "RatioSteam", "RatioSteamInput",
                "RatioSteamInput2", "RatioSteamOutput", "RatioSteamOutput2", "RatioVolatiles",
                "RatioVolatilesInput", "RatioVolatilesInput2", "RatioVolatilesOutput",
                "RatioVolatilesOutput2", "RatioWater", "RatioWaterInput", "RatioWaterInput2",
                "RatioWaterOutput", "RatioWaterOutput2", "Reagents", "RecipeHash", "ReferenceId",
                "RequestHash", "RequiredPower", "Reset", "ReturnFuelCost", "Richness", "Rpm", "Seeding",
                "SeedingRatio", "SemiMajorAxis", "Setpoint", "Setting", "SettingInput", "SettingOutput",
                "SignalID", "SignalStrength", "Sites", "Size", "SizeX", "SizeY", "SizeZ", "SolarAngle",
                "SolarIrradiance", "SortingClass", "SoundAlert", "StackSize", "Stress", "Survey",
                "TargetPadIndex", "TargetPrefabHash", "TargetSlotIndex", "TargetX", "TargetY", "TargetZ",
                "Temperature", "TemperatureDifferentialEfficiency", "TemperatureExternal",
                "TemperatureInput", "TemperatureInput2", "TemperatureOutput", "TemperatureOutput2",
                "TemperatureSetting", "Throttle", "Thrust", "ThrustToWeight", "Time", "TimeToDestination",
                "TotalMoles", "TotalMolesInput", "TotalMolesInput2", "TotalMolesOutput",
                "TotalMolesOutput2", "TotalQuantity", "TotalSlots", "TrueAnomaly", "VelocityMagnitude",
                "VelocityRelativeX", "VelocityRelativeY", "VelocityRelativeZ", "VelocityX", "VelocityY",
                "VelocityZ", "Vertical", "VerticalRatio", "Volume", "VolumeOfLiquid", "WattsReachingContact",
                "Weight", "WorkingGasEfficiency", "Harvest"
            }}
            };
        }

        // BASIC IC10 Keywords
        // Loaded from LanguageData.json for localization support
        private static string[] _keywords;
        private static string[] Keywords
        {
            get
            {
                if (_keywords == null)
                {
                    LoadLanguageData();
                }
                return _keywords;
            }
        }

        private static readonly string[] KeywordsFallback = new[]
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
        // Loaded from LanguageData.json for localization support
        private static string[] _functions;
        private static string[] Functions
        {
            get
            {
                if (_functions == null)
                {
                    LoadLanguageData();
                }
                return _functions;
            }
        }

        private static readonly string[] FunctionsFallback = new[]
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
            Debug.Log($"[Autocomplete] '{tokenBeforeDot}' -> Category: {_currentCategory}");

            // Get LogicTypes for this category
            var logicTypes = CategoryLogicTypes.TryGetValue(_currentCategory, out var types) ? types : CategoryLogicTypes[DeviceCategory.Unknown];
            Debug.Log($"[Autocomplete] Showing {logicTypes.Length} properties for {_currentCategory}");

            _mode = AutocompleteMode.Property;
            _currentFilter = "";
            _filteredItems = logicTypes.Distinct().OrderBy(x => x).ToList();
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
                    .Distinct()
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
                    Debug.Log($"[Autocomplete] Alias '{aliasName}' matched pattern '{kvp.Key}' -> {kvp.Value}");
                    return kvp.Value;
                }
            }
            Debug.Log($"[Autocomplete] Alias '{aliasName}' did not match any pattern, checking target: '{target}'");

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

            Debug.Log($"[Autocomplete] Alias fell through to Unknown category");
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

        #region Prefab Hash Lookup

        // === Prefab Hash Dictionary ===
        // Maps PrefabHash (int) to (Title, PrefabName) for tooltip display
        // Data extracted from assets.ic10.dev/languages/EN/devices.json and items.json
        // Loaded from LanguageData.json for localization support
        private static Dictionary<int, (string Title, string PrefabName)> _prefabHashLookup;
        private static Dictionary<int, (string Title, string PrefabName)> PrefabHashLookup
        {
            get
            {
                if (_prefabHashLookup == null)
                {
                    LoadLanguageData();
                }
                return _prefabHashLookup;
            }
        }
        
        private static readonly Dictionary<int, (string Title, string PrefabName)> PrefabHashLookupFallback = new Dictionary<int, (string, string)>
        {
            // ===== STRUCTURES / DEVICES =====
            // IC Housing & Logic
            { -128473777, ("IC Housing", "StructureCircuitHousing") },
            { 1512322581, ("Kit (IC Housing)", "ItemKitLogicCircuit") },
            { -744098481, ("Integrated Circuit (IC10)", "ItemIntegratedCircuit10") },
            { -851746783, ("Logic Memory", "StructureLogicMemory") },
            { 1190215887, ("Logic I/O", "StructureLogicInputOutput") },
            { -412271893, ("Logic Switch", "StructureLogicSwitch") },
            { 1973198065, ("Logic Dial", "StructureLogicDial") },
            { 1220484876, ("Logic Reader", "StructureLogicReader") },
            { -755582377, ("Logic Writer", "StructureLogicWriter") },
            { -1613957550, ("Logic Math", "StructureLogicMath") },
            { -1535178339, ("Logic Comparator", "StructureLogicComparator") },
            { -1861565862, ("Logic Batch Reader", "StructureLogicBatchReader") },
            { -1809289672, ("Logic Batch Writer", "StructureLogicBatchWriter") },
            { -1243670210, ("Logic Transformer", "StructureLogicTransformer") },
            { -1194586830, ("Logic Select", "StructureLogicSelect") },
            { 1176003529, ("Logic Selector", "StructureLogicSelector") },
            { -1123085929, ("Logic Boolean", "StructureLogicBoolean") },
            { 1823753947, ("Batch Reader", "StructureLogicBatchReader") },

            // LED Displays
            { -815193061, ("LED Display (Small)", "StructureConsoleLED5") },
            { -1949054743, ("LED Display (Large)", "StructureConsoleLED1x3") },
            { 1001341323, ("Console LED", "StructureConsoleLED") },
            { 921115029, ("Console LED Tall", "StructureConsoleLEDTall") },

            // Consoles & Computers
            { -1778700623, ("Computer", "StructureComputer") },
            { 517275668, ("Console", "StructureConsole") },

            // Gas Sensors
            { 1717593480, ("Kit (Gas Sensor)", "ItemGasSensor") },
            { -1252983604, ("Gas Sensor", "StructureGasSensor") },

            // Vents & Air Control
            { -842048328, ("Kit (Active Vent)", "ItemActiveVent") },
            { 1793096320, ("Active Vent", "StructureActiveVent") },
            { 238631271, ("Kit (Passive Vent)", "ItemPassiveVent") },
            { -469742655, ("Passive Vent", "StructurePassiveVent") },
            { 2015439334, ("Kit (Powered Vent)", "ItemKitPoweredVent") },
            { 1369546270, ("Powered Vent", "StructurePoweredVent") },

            // Pumps
            { -1766301997, ("Kit (Volume Pump)", "ItemPipeVolumePump") },
            { 1199525748, ("Volume Pump", "StructureVolumePump") },
            { -1248429712, ("Kit (Turbo Volume Pump - Gas)", "ItemKitTurboVolumePump") },
            { -2106280569, ("Kit (Liquid Volume Pump)", "ItemLiquidPipeVolumePump") },

            // Tanks & Canisters
            { 42280099, ("Canister", "ItemGasCanisterEmpty") },
            { -668314371, ("Gas Canister (Smart)", "ItemGasCanisterSmart") },
            { -185207387, ("Liquid Canister", "ItemLiquidCanisterEmpty") },
            { 777684475, ("Liquid Canister (Smart)", "ItemLiquidCanisterSmart") },
            { 771439840, ("Kit (Tank)", "ItemKitTank") },
            { 1021053608, ("Kit (Insulated Tank)", "ItemKitTankInsulated") },

            // Hydroponics
            { 1441767298, ("Hydroponics Station", "StructureHydroponicsStation") },
            { 2057179799, ("Kit (Hydroponic Station)", "ItemKitHydroponicStation") },
            { -1193543727, ("Kit (Hydroponic Tray)", "ItemHydroponicTray") },
            { -927931558, ("Kit (Automated Hydroponics)", "ItemKitHydroponicAutomated") },
            { 341030083, ("Kit (Grow Light)", "ItemKitGrowLight") },

            // Power
            { -1924492105, ("Kit (Solar Panel)", "ItemKitSolarPanel") },
            { 1406656973, ("Kit (Battery)", "ItemKitBattery") },
            { -21225041, ("Kit (Battery Large)", "ItemKitBatteryLarge") },
            { 377745425, ("Kit (Gas Fuel Generator)", "ItemKitGasGenerator") },
            { 1293995736, ("Kit (Solid Generator)", "ItemKitSolidGenerator") },
            { -453039435, ("Kit (Transformer Large)", "ItemKitTransformer") },
            { 665194284, ("Kit (Transformer Small)", "ItemKitTransformerSmall") },
            { 1757673317, ("Kit (Area Power Controller)", "ItemAreaPowerControl") },

            // Batteries
            { 700133157, ("Battery Cell (Small)", "ItemBatteryCell") },
            { -459827268, ("Battery Cell (Large)", "ItemBatteryCellLarge") },
            { 544617306, ("Battery Cell (Nuclear)", "ItemBatteryCellNuclear") },

            // Fabricators
            { -806743925, ("Kit (Furnace)", "ItemKitFurnace") },
            { -616758353, ("Kit (Advanced Furnace)", "ItemKitAdvancedFurnace") },
            { -98995857, ("Kit (Arc Furnace)", "ItemKitArcFurnace") },
            { -1753893214, ("Kit (Autolathe)", "ItemKitAutolathe") },
            { 578182956, ("Kit (Centrifuge)", "ItemKitCentrifuge") },

            // Doors & Airlocks
            { 964043875, ("Kit (Airlock)", "ItemKitAirlock") },
            { 168615924, ("Kit (Door)", "ItemKitDoor") },
            { -1755116240, ("Kit (Blast Door)", "ItemKitBlastDoor") },

            // Walls & Structure
            { -1826855889, ("Kit (Wall)", "ItemKitWall") },
            { -524546923, ("Kit (Iron Wall)", "ItemKitWallIron") },

            // Lights
            { 1108423476, ("Kit (Lights)", "ItemWallLight") },

            // Sensors
            { -1776897113, ("Kit (Sensors)", "ItemKitSensor") },

            // Misc Devices
            { 890106742, ("Kit (Igniter)", "ItemIgniter") },
            { -2107840748, ("Kit (Flashing Light)", "ItemFlashingLight") },

            // Rocket Components
            { 1396305045, ("Kit (Avionics)", "ItemKitRocketAvionics") },
            { 721251202, ("Kit (Rocket Circuit Housing)", "ItemKitRocketCircuitHousing") },
            { -1256996603, ("Kit (Rocket Datalink)", "ItemKitRocketDatalink") },
            { -1629347579, ("Kit (Rocket Gas Fuel Tank)", "ItemKitRocketGasFuelTank") },
            { 2032027950, ("Kit (Rocket Liquid Fuel Tank)", "ItemKitRocketLiquidFuelTank") },

            // ===== ITEMS / INGOTS / ORES =====
            // Ingots
            { -404336834, ("Ingot (Copper)", "ItemCopperIngot") },
            { -1301215609, ("Ingot (Iron)", "ItemIronIngot") },
            { 226410516, ("Ingot (Gold)", "ItemGoldIngot") },
            { -929742000, ("Ingot (Silver)", "ItemSilverIngot") },
            { 2134647745, ("Ingot (Lead)", "ItemLeadIngot") },
            { -1406385572, ("Ingot (Nickel)", "ItemNickelIngot") },
            { -654790771, ("Ingot (Steel)", "ItemSteelIngot") },
            { -290196476, ("Ingot (Silicon)", "ItemSiliconIngot") },
            { 502280180, ("Ingot (Electrum)", "ItemElectrumIngot") },
            { 1058547521, ("Ingot (Constantan)", "ItemConstantanIngot") },
            { -82508479, ("Ingot (Solder)", "ItemSolderIngot") },
            { -297990285, ("Ingot (Invar)", "ItemInvarIngot") },
            { 412924554, ("Ingot (Astroloy)", "ItemAstroloyIngot") },
            { 1579842814, ("Ingot (Hastelloy)", "ItemHastelloyIngot") },
            { -787796599, ("Ingot (Inconel)", "ItemInconelIngot") },
            { -1897868623, ("Ingot (Stellite)", "ItemStelliteIngot") },
            { 156348098, ("Ingot (Waspaloy)", "ItemWaspaloyIngot") },

            // Ores
            { -707307845, ("Ore (Copper)", "ItemCopperOre") },
            { 1758427767, ("Ore (Iron)", "ItemIronOre") },
            { -1348105509, ("Ore (Gold)", "ItemGoldOre") },
            { -916518678, ("Ore (Silver)", "ItemSilverOre") },
            { -190236170, ("Ore (Lead)", "ItemLeadOre") },
            { 1830218956, ("Ore (Nickel)", "ItemNickelOre") },
            { 1103972403, ("Ore (Silicon)", "ItemSiliconOre") },
            { -983091249, ("Ore (Cobalt)", "ItemCobaltOre") },
            { 1724793494, ("Ore (Coal)", "ItemCoalOre") },
            { -1516581844, ("Ore (Uranium)", "ItemUraniumOre") },

            // Ice
            { 1217489948, ("Ice (Water)", "ItemIce") },
            { -1805394113, ("Ice (Oxite)", "ItemOxite") },
            { -1499471529, ("Ice (Nitrice)", "ItemNitrice") },
            { 1253102035, ("Ice (Volatiles)", "ItemVolatiles") },

            // Gases (for filter type hashes)
            { 632853248, ("Filter (Nitrogen)", "ItemGasFilterNitrogen") },
            { -721824748, ("Filter (Oxygen)", "ItemGasFilterOxygen") },
            { 1635000764, ("Filter (Carbon Dioxide)", "ItemGasFilterCarbonDioxide") },
            { 15011598, ("Filter (Volatiles)", "ItemGasFilterVolatiles") },
            { -1247674305, ("Filter (Nitrous Oxide)", "ItemGasFilterNitrousOxide") },
            { 1915566057, ("Filter (Pollutant)", "ItemGasFilterPollutants") },
            { -1993197973, ("Filter (Water)", "ItemGasFilterWater") },

            // Food / Plants
            { 1929046963, ("Potato", "ItemPotato") },
            { -998592080, ("Tomato", "ItemTomato") },
            { 258339687, ("Corn", "ItemCorn") },
            { 658916791, ("Rice", "ItemRice") },
            { 1924673028, ("Soybean", "ItemSoybean") },
            { 1277828144, ("Pumpkin", "ItemPumpkin") },
            { -1057658015, ("Wheat", "ItemWheat") },
            { 2044798572, ("Mushroom", "ItemMushroom") },

            // Tools
            { 687940869, ("Screwdriver", "ItemScrewdriver") },
            { -1886261558, ("Wrench", "ItemWrench") },
            { 1535854074, ("Wire Cutters", "ItemWireCutters") },
            { 856108234, ("Crowbar", "ItemCrowbar") },
            { 2009673399, ("Hand Drill", "ItemDrill") },
            { 201215010, ("Angle Grinder", "ItemAngleGrinder") },
            { 1385062886, ("Arc Welder", "ItemArcWelder") },
            { -838472102, ("Flashlight", "ItemFlashlight") },

            // Mining
            { -913649823, ("Pickaxe", "ItemPickaxe") },
            { 1055173191, ("Mining Drill", "ItemMiningDrill") },
            { -1663349918, ("Mining Drill (Heavy)", "ItemMiningDrillHeavy") },

            // Suits & Helmets
            { 714830451, ("Space Helmet", "ItemSpaceHelmet") },
            { 1677018918, ("Eva Suit", "ItemEvaSuit") },
            { -1758310454, ("Hardsuit", "ItemHardSuit") },
            { -84573099, ("Hardsuit Helmet", "ItemHardsuitHelmet") },

            // Misc
            { -466050668, ("Cable Coil", "ItemCableCoil") },
            { 731250882, ("Electronic Parts", "ItemElectronicParts") },
            { 1588896491, ("Glass Sheets", "ItemGlassSheets") },
            { -487378546, ("Iron Sheets", "ItemIronSheets") },
            { 38555961, ("Steel Sheets", "ItemSteelSheets") },
            { 1225836666, ("Iron Frames", "ItemIronFrames") },
            { -1448105779, ("Steel Frames", "ItemSteelFrames") },
        };

        /// <summary>
        /// Looks up a prefab hash and returns the display string "Title [PrefabName]"
        /// Returns null if the hash is not found
        /// </summary>
        public static string LookupPrefabHash(int hash)
        {
            if (PrefabHashLookup.TryGetValue(hash, out var info))
            {
                return $"{info.Title} [{info.PrefabName}]";
            }
            return null;
        }

        /// <summary>
        /// Looks up a prefab hash and returns just the Title
        /// Returns null if the hash is not found
        /// </summary>
        public static string LookupPrefabHashTitle(int hash)
        {
            if (PrefabHashLookup.TryGetValue(hash, out var info))
            {
                return info.Title;
            }
            return null;
        }

        /// <summary>
        /// Check if a string looks like a prefab hash (negative or positive integer)
        /// </summary>
        public static bool TryParseHashFromWord(string word, out int hash)
        {
            hash = 0;
            if (string.IsNullOrEmpty(word)) return false;
            
            // Check if it looks like a hash (negative number with many digits, or positive number)
            if (word.StartsWith("-") && word.Length > 4)
            {
                return int.TryParse(word, out hash);
            }
            else if (char.IsDigit(word[0]) && word.Length > 4)
            {
                return int.TryParse(word, out hash);
            }
            return false;
        }

        #endregion
    }
}


