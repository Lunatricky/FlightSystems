using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region TODO
        // Original functionality has been fully achieved.
        // Add more feautres
        #endregion TODO

        #region Fields
        //=================================================
        // Fields
        //=================================================

        OperationalDamageDisplay _myOperationalDamageDisplay;
        StringBuilder _echoBuilder = new StringBuilder();
        const bool _useExtraDebug = false;
        #endregion Fields

        //=================================================
        // Touching below this point may break things
        //=================================================

        public Program()
        {
            _myOperationalDamageDisplay = new OperationalDamageDisplay(Me, GridTerminalSystem);
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        public void Main(string arg, UpdateType updateSource)
        {
            if (Runtime.UpdateFrequency == UpdateFrequency.None)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            _myOperationalDamageDisplay.Run();
            //_myOperationalDamageDisplay.UpdatePanelSettings();

            #region Output          
            _echoBuilder.Append(OperationalDamageDisplay.NAME + "\n");
            _echoBuilder.Append("- - - - - - - - - -\n");
            _echoBuilder.Append($"Registered Groups: {OperationalDamageDisplay.NumberOfGroups}\n");
            _echoBuilder.Append($"Registered Panels: {OperationalDamageDisplay.NumberOfPanels}\n");
            _echoBuilder.Append($"Registered Surfaces: {OperationalDamageDisplay.NumberOfSurfaces}\n");
            _echoBuilder.Append($"Registered Components: {OperationalDamageDisplay.NumberOfComponents}\n");

            #region Runtime Info Output
            _echoBuilder.Append("- - - - - - - - - -\n" +
                 $"Runtime: {Runtime.LastRunTimeMs} Ms\n" +
                 $"Instruction Count: {Runtime.CurrentInstructionCount}\n" +
                 $"Ms/Instruction: {Math.Round(Runtime.LastRunTimeMs / Runtime.CurrentInstructionCount, 5)}\n" +
                 $"Complexity: {Math.Round((double)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount, 5)}%");
            #endregion Runtime Info Output

            Echo(_echoBuilder.ToString());
            _echoBuilder.Clear();
            #endregion Output
        }

        public void Save()
        {

        }

        //=================================================
        // Touching below this point might really break things
        //=================================================

        public class OperationalDamageDisplay
        {
            #region Properties
            private static Dictionary<string, List<IMyTerminalBlock>> BlockListDictionary
            {
                get
                {
                    return _blockListDictionary;
                }
            }
            private static Dictionary<string, int> BlockCountDictionary
            {
                get
                {
                    return _blockCountDictionary;
                }
            }
            public static int NumberOfGroups { get { return BlockListDictionary.Count(); } }
            public static int NumberOfPanels { get; private set; } = 0;
            public static int NumberOfSurfaces { get; protected set; } = 0;
            public static int NumberOfComponents { get; private set; } = 0;
            public static bool UsePanelUpdateQueue
            {
                get
                {
                    return _usePanelUpdateQueue;
                }
                set
                {
                    _usePanelUpdateQueue = value;
                }
            }
            public static bool UseComponentUpdateQueue
            {
                get
                {
                    return _useComponentUpdateQueue;
                }
                set
                {
                    _useComponentUpdateQueue = value;
                }
            }
            #endregion Properties

            #region Fields
            private MyIni _ini = new MyIni();

            private IMyTerminalBlock _customDataProvider;
            private List<ODDPanel> _ODDPanels = new List<ODDPanel>();
            private IMyGridTerminalSystem _gridTerminalSystem;
            private static Dictionary<string, List<IMyTerminalBlock>> _blockListDictionary = new Dictionary<string, List<IMyTerminalBlock>>();
            private static Dictionary<string, int> _blockCountDictionary = new Dictionary<string, int>();

            private static string _panelGroupName = "ODD Panels";
            private static float _damageStart = 0.95f;
            private static float _damageEnd = 0.25f;

            private Color _colorDefault = new Color(255, 255, 255);
            private Color _colorDamageStart = new Color(255, 255, 0);
            private Color _colorDamageEnd = new Color(255, 0, 0);

            private static bool _usePanelUpdateQueue = true;
            private static int _panelUpdateQueueBudget = 1;
            private static bool _useComponentUpdateQueue = false;
            private static int _componentUpdateQueueBudget = 5;
            private static bool _useGroupHealthUpdateQueue = true;
            private static int _groupHealthUpdateQueueBudget = 10;


            private Queue<ODDPanel> _ODDPanelQueue = new Queue<ODDPanel>();
            #endregion Fields

            #region Constants
            private const string INI_KEY_GROUP_NAME = "Group Name";
            private const string INI_KEY_PANEL_GROUP_NAME = "Panel Group Name";
            private const string INI_KEY_DAMAGE_START = "Damage Start";
            private const string INI_KEY_DAMAGE_END = "Damage End";
            private const string INI_KEY_COLOR_DEFAULT = "Default Color";
            private const string INI_KEY_COLOR_DAMAGE_START = "Damage Start Color";
            private const string INI_KEY_COLOR_DAMAGE_END = "Damage End Color";
            private const string INI_KEY_PERFORMANCE_PANEL_QUEUE = "Use Panel Update Queue";
            private const string INI_KEY_PERFORMANCE_PANEL_QUEUE_COUNT = "Panel Queue Budget";
            private const string INI_KEY_PERFORMANCE_COMPONENT_QUEUE = "Use Component Update Queue";
            private const string INI_KEY_PERFORMANCE_COMPONENT_QUEUE_COUNT = "Component Queue Budget";
            private const string INI_KEY_PERFORMANCE_GROUP_HEALTH_QUEUE = "Use Group Health Update Queue";
            private const string INI_KEY_PERFORMANCE_GROUP_HEALTH_QUEUE_COUNT = "Group Health Queue Budget";
            private const string INI_KEY_EXPECTED_BLOCK_COUNT = "Expected Block Count";

            private const string INI_SECTION_GENERAL = "ODD - General";
            private const string INI_SECTION_GROUP = "ODD - Group";
            private const string INI_SECTION_PERFORMANCE = "ODD - Performance";


            public const string VERSION = "1.1";
            public const string NAME = "Operation Damage Display | V" + VERSION;
            #endregion Constants

            public OperationalDamageDisplay(IMyTerminalBlock customDataprovider, IMyGridTerminalSystem myGridTerminalSystem)
            {
                _customDataProvider = customDataprovider;
                _gridTerminalSystem = myGridTerminalSystem;

                ParseIni();

                List<IMyTerminalBlock> surfaceProviders = new List<IMyTerminalBlock>();
                IMyBlockGroup blockGroup = _gridTerminalSystem.GetBlockGroupWithName(_panelGroupName);
                if (blockGroup != null)
                {
                    blockGroup.GetBlocksOfType<IMyTerminalBlock>(surfaceProviders, block => block as IMyTextSurfaceProvider != null);
                }

                foreach (IMyTerminalBlock block in surfaceProviders)
                {
                    ODDPanel panel = new ODDPanel(block, _colorDefault, _colorDamageStart, _colorDamageEnd);
                    _ODDPanels.Add(panel);
                    NumberOfPanels++;
                }

                _ODDPanels.ForEach(panel => _ODDPanelQueue.Enqueue(panel));
            }


            public void Run()
            {
                if (_usePanelUpdateQueue)
                {
                    int count = 0;
                    while (count < _panelUpdateQueueBudget && _ODDPanelQueue.Count != 0)
                    {
                        _ODDPanelQueue.Dequeue().Update();
                        count++;
                    }

                    if (_ODDPanelQueue.Count == 0)
                    {
                        _ODDPanels.ForEach(panel => _ODDPanelQueue.Enqueue(panel));
                    }
                }
                else
                {
                    foreach (ODDPanel panel in _ODDPanels)
                    {
                        panel.Update();
                    }
                }

            }

            private void ParseIni()
            {
                _ini.Clear();
                string customData = _customDataProvider.CustomData;
                bool parsed = _ini.TryParse(customData);

                if (!parsed && !string.IsNullOrWhiteSpace(_customDataProvider.CustomData.Trim()))
                {
                    _ini.EndContent = _customDataProvider.CustomData;
                }



                List<string> sections = new List<string>();
                _ini.GetSections(sections);

                foreach (string sectionName in sections)
                {
                    if (sectionName.Contains(INI_SECTION_GROUP))
                    {
                        string groupName = _ini.Get(sectionName, INI_KEY_GROUP_NAME).ToString(null);
                        if (groupName == null) continue;
                        List<IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
                        IMyBlockGroup blockGroup = _gridTerminalSystem.GetBlockGroupWithName(groupName);
                        if (blockGroup != null)
                        {
                            blockGroup.GetBlocksOfType<IMyTerminalBlock>(terminalBlocks);
                        }
                        _blockListDictionary.Add(groupName, terminalBlocks);
                        int expectedBlockCount = _ini.Get(sectionName, INI_KEY_EXPECTED_BLOCK_COUNT).ToInt32(BlockListDictionary.ContainsKey(groupName) ? BlockListDictionary[groupName].Count : 0);
                        _blockCountDictionary.Add(groupName, expectedBlockCount);
                        continue;
                    }
                    if (sectionName.Contains(INI_SECTION_GENERAL))
                    {
                        _panelGroupName = _ini.Get(sectionName, INI_KEY_PANEL_GROUP_NAME).ToString(_panelGroupName);
                        _damageStart = (float)_ini.Get(sectionName, INI_KEY_DAMAGE_START).ToDouble(_damageStart);
                        _damageEnd = (float)_ini.Get(sectionName, INI_KEY_DAMAGE_END).ToDouble(_damageEnd);
                        _colorDefault = MyIniHelper.GetColor(sectionName, INI_KEY_COLOR_DEFAULT, _ini);
                        _colorDamageStart = MyIniHelper.GetColor(sectionName, INI_KEY_COLOR_DAMAGE_START, _ini);
                        _colorDamageEnd = MyIniHelper.GetColor(sectionName, INI_KEY_COLOR_DAMAGE_END, _ini);
                        continue;
                    }
                    if (sectionName.Contains(INI_SECTION_PERFORMANCE))
                    {
                        _usePanelUpdateQueue = _ini.Get(sectionName, INI_KEY_PERFORMANCE_PANEL_QUEUE).ToBoolean(_usePanelUpdateQueue);
                        _panelUpdateQueueBudget = _ini.Get(sectionName, INI_KEY_PERFORMANCE_PANEL_QUEUE_COUNT).ToInt32(_panelUpdateQueueBudget);
                        _useComponentUpdateQueue = _ini.Get(sectionName, INI_KEY_PERFORMANCE_COMPONENT_QUEUE).ToBoolean(_useComponentUpdateQueue);
                        _componentUpdateQueueBudget = _ini.Get(sectionName, INI_KEY_PERFORMANCE_COMPONENT_QUEUE_COUNT).ToInt32(_componentUpdateQueueBudget);
                        _useGroupHealthUpdateQueue = _ini.Get(sectionName, INI_KEY_PERFORMANCE_GROUP_HEALTH_QUEUE).ToBoolean(_useGroupHealthUpdateQueue);
                        _groupHealthUpdateQueueBudget = _ini.Get(sectionName, INI_KEY_PERFORMANCE_GROUP_HEALTH_QUEUE_COUNT).ToInt32(_groupHealthUpdateQueueBudget);
                    }
                }

                _ini.Set(INI_SECTION_GENERAL, INI_KEY_PANEL_GROUP_NAME, _panelGroupName);
                _ini.Set(INI_SECTION_GENERAL, INI_KEY_DAMAGE_START, _damageStart);
                _ini.Set(INI_SECTION_GENERAL, INI_KEY_DAMAGE_END, _damageEnd);
                MyIniHelper.SetColor(INI_SECTION_GENERAL, INI_KEY_COLOR_DEFAULT, _colorDefault, _ini);
                MyIniHelper.SetColor(INI_SECTION_GENERAL, INI_KEY_COLOR_DAMAGE_START, _colorDamageStart, _ini);
                MyIniHelper.SetColor(INI_SECTION_GENERAL, INI_KEY_COLOR_DAMAGE_END, _colorDamageEnd, _ini);
                _ini.Set(INI_SECTION_PERFORMANCE, INI_KEY_PERFORMANCE_PANEL_QUEUE, _usePanelUpdateQueue);
                _ini.Set(INI_SECTION_PERFORMANCE, INI_KEY_PERFORMANCE_PANEL_QUEUE_COUNT, _panelUpdateQueueBudget);
                _ini.Set(INI_SECTION_PERFORMANCE, INI_KEY_PERFORMANCE_COMPONENT_QUEUE, _useComponentUpdateQueue);
                _ini.Set(INI_SECTION_PERFORMANCE, INI_KEY_PERFORMANCE_COMPONENT_QUEUE_COUNT, _componentUpdateQueueBudget);
                //_ini.Set(INI_SECTION_PERFORMANCE, INI_KEY_PERFORMANCE_GROUP_HEALTH_QUEUE, _useGroupHealthUpdateQueue);
                //_ini.Set(INI_SECTION_PERFORMANCE, INI_KEY_PERFORMANCE_GROUP_HEALTH_QUEUE_COUNT, _groupHealthUpdateQueueBudget);

                foreach (string sectionName in sections)
                {
                    if (sectionName.Contains(INI_SECTION_GROUP))
                    {
                        string groupName = _ini.Get(sectionName, INI_KEY_GROUP_NAME).ToString(null);
                        if (groupName == null) continue;

                        _ini.Set(sectionName, INI_KEY_GROUP_NAME, groupName);
                    }
                }

                string output = _ini.ToString();
                if (!string.Equals(output, _customDataProvider.CustomData))
                {
                    _customDataProvider.CustomData = output;
                }
            }

            public void UpdatePanelSettings()
            {
                _ODDPanels.ForEach(panel => panel.ParseIniSectionGeneral());
            }

            public class ODDPanel
            {
                #region Properties
                public IMyTerminalBlock Panel
                {
                    get
                    {
                        return _panel;
                    }
                }
                public List<IMyTextSurface> Surfaces
                {
                    get
                    {
                        return _surfaces;
                    }
                }
                public float Zoom
                {
                    get
                    {
                        return _zoom;
                    }
                }
                public float Rotation
                {
                    get
                    {
                        return _rotation;
                    }
                }
                public Matrix RotationMatrix
                {
                    get
                    {
                        return _rotationMatrix;
                    }
                }
                public Color BackgroundColor
                {
                    get
                    {
                        return _backgroundColor;
                    }
                }
                public List<ODDLayoutComponent> Components
                {
                    get
                    {
                        return _components;
                    }
                }
                #endregion Properties

                #region Fields
                private MyIni _ini = new MyIni();
                private MyIni _textSurfaceProviderIni = new MyIni();

                private List<ODDLayoutComponent> _components = new List<ODDLayoutComponent>();
                private Queue<ODDLayoutComponent> _componentQueue = new Queue<ODDLayoutComponent>();

                private readonly IMyTerminalBlock _panel;
                private readonly List<IMyTextSurface> _surfaces;

                public MySpriteDrawFrame? frame = null;

                private Color _colorDefault;
                private Color _colorDamageStart;
                private Color _colorDamageEnd;

                private Matrix _rotationMatrix;

                private float _zoom = 1f;
                private float _rotation = 0f;
                private Color _backgroundColor = new Color(0, 0, 0);
                #endregion Fields

                #region Constants
                private const string INI_KEY_PANEL_ZOOM = "Zoom";
                private const string INI_KEY_PANEL_ROTATION = "Rotation (deg)";
                private const string INI_KEY_BACKGROUND_COLOR = "Background Color";
                private const string INI_KEY_COMPONENT_COLOR_DEFAULT = "Default Color";
                private const string INI_KEY_COMPONENT_COLOR_DAMAGE_START = "Damage Start Color";
                private const string INI_KEY_COMPONENT_COLOR_DAMAGE_END = "Damage End Color";
                private const string INI_KEY_GROUP_NAME = "Group Name";
                private const string INI_KEY_EXPECTED_BLOCK_COUNT = "Expected Block Count";
                private const string INI_KEY_SPRITE_TYPE = "Sprite Type";
                private const string INI_KEY_SPRITE_DATA = "Sprite Data";
                private const string INI_KEY_POSITION = "Position";
                private const string INI_KEY_ROTATION = "Rotation";
                private const string INI_KEY_SIZE = "Size";
                private const string INI_KEY_TEXT_ALIGNMENT = "Alignment";
                private const string INI_KEY_FLASH = "Flash";
                private const string INI_KEY_ANCHOR = "Anchored";

                private const string INI_SECTION_GENERAL = "ODD Panel - General";
                private const string INI_SECTION_SURFACE = "ODD - Surfaces";
                private const string INI_SECTION_STANDARD_COMPONENT = "Standard Component";
                private const string INI_SECTION_DAMAGE_COMPONENT = "Damage Component";
                #endregion Constants

                public ODDPanel(IMyTerminalBlock block, Color colorDefault, Color colorDamageStart, Color colorDamageEnd)
                {
                    _panel = block;

                    _surfaces = new List<IMyTextSurface>();
                    AddTextSurfaces(block, _surfaces);

                    _colorDefault = colorDefault;
                    _colorDamageStart = colorDamageStart;
                    _colorDamageEnd = colorDamageEnd;

                    ParseFullIni();

                    _rotationMatrix = CreateRotMatrix(_rotation);
                }

                private void ParseFullIni()
                {

                    _ini.Clear();
                    string customData = _panel.CustomData;
                    bool parsed = _ini.TryParse(customData);

                    if (!parsed && !string.IsNullOrWhiteSpace(_panel.CustomData.Trim()))
                    {
                        _ini.EndContent = _panel.CustomData;
                    }

                    List<string> sections = new List<string>();
                    _ini.GetSections(sections);

                    foreach (string sectionName in sections)
                    {
                        if (sectionName.Contains(INI_SECTION_STANDARD_COMPONENT))
                        {
                            SpriteType spriteType = (SpriteType)Enum.Parse(typeof(SpriteType), _ini.Get(sectionName, INI_KEY_SPRITE_TYPE).ToString("TEXTURE")); // optional
                            string data = _ini.Get(sectionName, INI_KEY_SPRITE_DATA).ToString("");
                            Vector2 position = MyIniHelper.GetVector2(sectionName, INI_KEY_POSITION, _ini);
                            float rotation = (float)_ini.Get(sectionName, INI_KEY_ROTATION).ToDouble(0); // optional
                            Vector2 size = MyIniHelper.GetVector2(sectionName, INI_KEY_SIZE, _ini);
                            Color colorDefault = MyIniHelper.GetColor(sectionName, INI_KEY_COMPONENT_COLOR_DEFAULT, _ini, _colorDefault); // optional
                            TextAlignment textAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), _ini.Get(sectionName, INI_KEY_TEXT_ALIGNMENT).ToString("CENTER")); // optional
                            bool anchor = _ini.Get(sectionName, INI_KEY_ANCHOR).ToBoolean(false); // optional
                            ODDLayoutComponent component = new ODDLayoutComponent(spriteType, data, position, rotation, size, textAlignment, anchor, colorDefault);
                            _components.Add(component);
                            NumberOfComponents++;
                            continue;
                        }
                        if (sectionName.Contains(INI_SECTION_DAMAGE_COMPONENT))
                        {
                            string groupName = _ini.Get(sectionName, INI_KEY_GROUP_NAME).ToString("NotProvided");
                            int expectedBlockCount = _ini.Get(sectionName, INI_KEY_EXPECTED_BLOCK_COUNT).ToInt32(BlockCountDictionary.ContainsKey(groupName) ? BlockCountDictionary[groupName] : BlockListDictionary.ContainsKey(groupName) ? BlockListDictionary[groupName].Count : 0); // optional
                            SpriteType spriteType = (SpriteType)Enum.Parse(typeof(SpriteType), _ini.Get(sectionName, INI_KEY_SPRITE_TYPE).ToString("TEXTURE")); // optional
                            string data = _ini.Get(sectionName, INI_KEY_SPRITE_DATA).ToString("");
                            Vector2 position = MyIniHelper.GetVector2(sectionName, INI_KEY_POSITION, _ini);
                            float rotation = (float)_ini.Get(sectionName, INI_KEY_ROTATION).ToDouble(0); // optional
                            Vector2 size = MyIniHelper.GetVector2(sectionName, INI_KEY_SIZE, _ini);
                            Color colorDefault = MyIniHelper.GetColor(sectionName, INI_KEY_COMPONENT_COLOR_DEFAULT, _ini, _colorDefault); // optional
                            Color colorDamageStart = MyIniHelper.GetColor(sectionName, INI_KEY_COMPONENT_COLOR_DAMAGE_START, _ini, _colorDamageStart); // optional
                            Color colorDamageEnd = MyIniHelper.GetColor(sectionName, INI_KEY_COMPONENT_COLOR_DAMAGE_END, _ini, _colorDamageEnd); // optional
                            TextAlignment textAlignment = (TextAlignment)Enum.Parse(typeof(TextAlignment), _ini.Get(sectionName, INI_KEY_TEXT_ALIGNMENT).ToString("CENTER")); // optional
                            bool shouldFlash = _ini.Get(sectionName, INI_KEY_FLASH).ToBoolean(true); // optional
                            bool anchor = _ini.Get(sectionName, INI_KEY_ANCHOR).ToBoolean(false); // optional
                            ODDLayoutComponent component = new ODDLayoutDamageComponent(spriteType, data, position, rotation, size, textAlignment, anchor, colorDefault, colorDamageStart, colorDamageEnd, groupName, expectedBlockCount, shouldFlash);
                            _components.Add(component);
                            NumberOfComponents++;
                            continue;
                        }
                        if (sectionName.Contains(INI_SECTION_GENERAL))
                        {
                            _zoom = (float)_ini.Get(sectionName, INI_KEY_PANEL_ZOOM).ToDouble(_zoom); // optional
                            _backgroundColor = MyIniHelper.GetColor(sectionName, INI_KEY_BACKGROUND_COLOR, _ini, _backgroundColor); // optional
                            _rotation = MathHelper.ToRadians((float)_ini.Get(sectionName, INI_KEY_PANEL_ROTATION).ToDouble(_rotation)); // optional
                            continue;
                        }
                    }
                }

                public void ParseIniSectionGeneral()
                {
                    _ini.Clear();
                    string customData = _panel.CustomData;
                    bool parsed = _ini.TryParse(customData);

                    if (!parsed && !string.IsNullOrWhiteSpace(_panel.CustomData.Trim()))
                    {
                        _ini.EndContent = _panel.CustomData;
                    }

                    List<string> sections = new List<string>();
                    _ini.GetSections(sections);

                    foreach (string sectionName in sections)
                    {

                        if (sectionName.Contains(INI_SECTION_GENERAL))
                        {
                            _zoom = (float)_ini.Get(sectionName, INI_KEY_PANEL_ZOOM).ToDouble(_zoom); // optional
                            _backgroundColor = MyIniHelper.GetColor(sectionName, INI_KEY_BACKGROUND_COLOR, _ini, _backgroundColor); // optional
                            _rotation = MathHelper.ToRadians((float)_ini.Get(sectionName, INI_KEY_PANEL_ROTATION).ToDouble(_rotation)); // optional
                            continue;
                        }
                    }
                }

                private void AddTextSurfaces(IMyTerminalBlock block, List<IMyTextSurface> textSurfaces)
                {
                    IMyTextSurfaceProvider textSurfaceProvider = block as IMyTextSurfaceProvider;
                    if (textSurfaceProvider != null)
                    {
                        _textSurfaceProviderIni.Clear();
                        string customData = block.CustomData;
                        bool parsed = _textSurfaceProviderIni.TryParse(customData);

                        if (!parsed && !string.IsNullOrWhiteSpace(block.CustomData.Trim()))
                        {
                            _textSurfaceProviderIni.EndContent = block.CustomData;
                        }

                        for (int i = 0; i < textSurfaceProvider.SurfaceCount; i++)
                        {
                            string iniKey = "Screen" + i;
                            bool enabled = _textSurfaceProviderIni.Get(INI_SECTION_SURFACE, iniKey).ToBoolean(false); //i == 0 for default screen
                            if (enabled)
                            {
                                textSurfaces.Add(textSurfaceProvider.GetSurface(i));
                                NumberOfSurfaces++;
                            }

                            _textSurfaceProviderIni.Set(INI_SECTION_SURFACE, iniKey, enabled);
                        }

                        string output = _textSurfaceProviderIni.ToString();
                        if (!string.Equals(output, block.CustomData))
                        {
                            block.CustomData = output;
                        }
                    }
                }

                public void Update()
                {
                    // Add more checks for performance at some point
                    if (_panel.Closed || !_panel.IsWorking) return;



                    if (_useComponentUpdateQueue)
                    {
                        if (_componentQueue.Count == 0)
                        {
                            _components.ForEach(component => _componentQueue.Enqueue(component));
                        }

                        int count = 0;
                        while (count < _componentUpdateQueueBudget && _componentQueue.Count != 0)
                        {
                            _componentQueue.Dequeue().Update();
                            count++;
                        }
                    }
                    else
                    {
                        foreach (ODDLayoutComponent component in _components)
                        {
                            component.Update();
                        }
                    }
                    ODDLayoutDamageComponent.Flash = !ODDLayoutDamageComponent.Flash;
                    DrawScreen();
                }

                private void DrawScreen()
                {
                    foreach (IMyTextSurface surface in _surfaces)
                    {
                        if (surface != null)
                        {
                            MySpriteDrawFrame frame = surface.DrawFrame();
                            DrawSprites(ref frame, this, surface, _components);
                            frame.Dispose();
                        }
                    }
                }

                private static void DrawSprites(ref MySpriteDrawFrame frame, ODDPanel screen, IMyTextSurface surface, List<ODDLayoutComponent> componenets)
                {
                    float zoom = screen.Zoom;
                    surface.ScriptBackgroundColor = screen.BackgroundColor;
                    Vector2 screenCenter = surface.TextureSize * 0.5f;
                    Vector2 rotationOffset = new Vector2(0, 0);
                    Vector2 position;
                    float rotationOrScale;
                    Vector2 size;

                    MySprite sprite;
                    foreach (ODDLayoutComponent component in componenets)
                    {
                        if (!component.IsAnchored)
                        {
                            if (screen.Rotation != 0)
                            {
                                Matrix rotationMatrix = screen.RotationMatrix;
                                Vector2 screenCenterOffset = component.Position;
                                Vector2.TransformNormal(ref screenCenterOffset, ref rotationMatrix, out rotationOffset);
                            }
                            position = screen.Rotation == 0 ? (component.Position * zoom + screenCenter) : (rotationOffset * zoom + screenCenter);
                            rotationOrScale = component.SpriteType == SpriteType.TEXT ? (component.Rotation + screen.Rotation) * zoom : component.Rotation + screen.Rotation;
                            size = component.Size * zoom;
                        }
                        else
                        {
                            position = component.Position + screenCenter;
                            rotationOrScale = component.Rotation;
                            size = component.Size;
                        }

                        sprite = new MySprite()
                        {
                            Type = component.SpriteType,
                            Data = component.SpriteData,
                            Position = position,
                            RotationOrScale = rotationOrScale,
                            Size = size,
                            Color = component.Color,
                            Alignment = component.TextAlignment,
                            FontId = "Debug"
                        };
                        frame.Add(sprite);
                    }
                }
                public static Matrix CreateRotMatrix(float rotation)
                {
                    float sin = MyMath.FastSin(rotation);
                    float cos = MyMath.FastCos(rotation);
                    return new Matrix
                    {
                        M11 = cos,
                        M12 = sin,
                        M21 = -sin,
                        M22 = cos,
                    };
                }

                public class ODDLayoutComponent
                {
                    #region Properties
                    public SpriteType SpriteType { get; private set; }
                    public string SpriteData { get; private set; }
                    public Vector2 Position { get; private set; }
                    public float Rotation { get; private set; }
                    public Vector2 Size { get; private set; }
                    public virtual Color Color { get; private set; }
                    public TextAlignment TextAlignment { get; private set; }
                    public bool IsAnchored { get; private set; }
                    #endregion Properties

                    #region Fields

                    #endregion Fields

                    public ODDLayoutComponent(SpriteType spriteType, string spriteData, Vector2 position, float rotation, Vector2 size, TextAlignment textAlignment, bool anchor, Color colorDefault)
                    {
                        SpriteType = spriteType;
                        SpriteData = spriteData;
                        Position = position;
                        Rotation = rotation;
                        Size = size;
                        Color = colorDefault;
                        TextAlignment = textAlignment;
                        IsAnchored = anchor;
                    }

                    public virtual void Update()
                    {
                        return;
                    }
                }

                public class ODDLayoutDamageComponent : ODDLayoutComponent
                {
                    #region Properties
                    public override Color Color
                    {
                        get
                        {
                            Color color = new Color(0, 0, 0, 255);
                            if (Health > _damageStart)
                            {
                                color = _colorDefault;
                            }
                            else if (Health < _damageEnd)
                            {
                                //_flash = !_flash || !_shouldFlash;
                                color = _flash ? Health == 0 ? new Color(10, 0, 0) : _colorDamageEnd * 0.25f : _colorDamageEnd;
                            }
                            else
                            {
                                // Could cache the calculation to make this faster because instructions are wasted recalculating values
                                color.R = (byte)(int)BoundedLinearInterpolation(_damageStart, _damageEnd, _colorDamageStart.R, _colorDamageEnd.R, Health);
                                color.G = (byte)(int)BoundedLinearInterpolation(_damageStart, _damageEnd, _colorDamageStart.G, _colorDamageEnd.G, Health);
                                color.B = (byte)(int)BoundedLinearInterpolation(_damageStart, _damageEnd, _colorDamageStart.B, _colorDamageEnd.B, Health);
                                color.A = (byte)(int)BoundedLinearInterpolation(_damageStart, _damageEnd, _colorDamageStart.A, _colorDamageEnd.A, Health);
                            }
                            return color;
                        }
                    }
                    public float Health
                    {
                        get
                        {
                            if (_expectedBlockCount == 0) return _health = 1;

                            return _health;
                        }
                    }
                    public string GroupName { get; private set; }

                    public static bool Flash
                    {
                        get
                        {
                            return _flash;
                        }
                        set
                        {
                            _flash = value;
                        }
                    }
                    #endregion Properties

                    #region Field
                    private static bool _flash = false;
                    private bool _shouldFlash = false;
                    private Color _colorDefault;
                    private Color _colorDamageStart;
                    private Color _colorDamageEnd;

                    private float _health = 1;
                    private float _expectedBlockCount;
                    private float _currentFunctionalBlockCount;

                    private static Queue<string> _blockGroupHealtQueue = new Queue<string>();
                    private static Dictionary<string, float> _blockGroupHealthDictionary = new Dictionary<string, float>();
                    private static int _numComponentsUpdated = 0;
                    #endregion Fields

                    public ODDLayoutDamageComponent(SpriteType spriteType, string spriteData, Vector2 position, float rotation, Vector2 size, TextAlignment textAlignment, bool anchor, Color colorDefault, Color colorDamageStart, Color colorDamageEnd, string groupName, int expectedBlockCount, bool shouldFlash)
                        : base(spriteType, spriteData, position, rotation, size, textAlignment, anchor, colorDefault)
                    {
                        _colorDefault = colorDefault;
                        _colorDamageStart = colorDamageStart;
                        _colorDamageEnd = colorDamageEnd;
                        GroupName = groupName;
                        _expectedBlockCount = expectedBlockCount;
                        _shouldFlash = shouldFlash;

                        if (!_blockGroupHealthDictionary.ContainsKey(GroupName) && _blockListDictionary.ContainsKey(GroupName))
                        {
                            _currentFunctionalBlockCount = GetCurrentBlockCount(_blockListDictionary[GroupName]);
                            _blockGroupHealthDictionary.Add(GroupName, _health);
                            /*if (_useGroupHealthUpdateQueue)
                            {
                                _blockGroupHealtQueue.Enqueue(GroupName);
                            }*/
                        }
                    }

                    /*public static void UpdateBlockGroupHealth(int numToUpdate)
                    {
                        if (_blockGroupHealtQueue.Count == 0)
                        {
                            foreach (string key in _blockGroupHealthDictionary.Keys)
                            {
                                _blockGroupHealtQueue.Enqueue(key);
                            }                         
                        }

                        string groupName = _blockGroupHealtQueue.Dequeue();
                        _blockGroupHealthDictionary[groupName] = GetCurrentBlockCount(_blockListDictionary[groupName]);

                    }*/

                    public override void Update()
                    {
                        /*if (_blockGroupHealthDictionary.ContainsKey(GroupName))
                        {                         
                            _health = _blockGroupHealthDictionary[GroupName];
                        }
                        else if (_blockListDictionary.ContainsKey(GroupName))
                        {
                            _currentFunctionalBlockCount = GetCurrentBlockCount(_blockListDictionary[GroupName]);
                            _health = MathHelper.Clamp(_currentFunctionalBlockCount / _expectedBlockCount, 0, 1);
                            _blockGroupHealthDictionary.Add(GroupName, _health);
                        }

                        _numComponentsUpdated++;
                        if (_numComponentsUpdated >= NumberOfComponents)
                        {
                            _blockGroupHealthDictionary.Clear();
                            _numComponentsUpdated = 0;
                        }*/

                        /*if (_useGroupHealthUpdateQueue)
                        {
                            if (_groupHealthUpdateQueueBudget <= 0) return;

                            if (_blockGroupHealtQueue.Count > 0)
                            {
                                string groupName = _blockGroupHealtQueue.Dequeue();
                                _groupHealthUpdateQueueBudget--;
                                float health;
                                if (!_blockGroupHealthDictionary.TryGetValue(groupName, out health))
                                {
                                    List<IMyTerminalBlock> blockList;
                                    if (_blockListDictionary.TryGetValue(groupName, out blockList))
                                    {
                                        health = MathHelper.Clamp(GetCurrentBlockCount(blockList) / BlockCountDictionary[groupName], 0, 1);
                                        _blockGroupHealthDictionary[groupName] = health;
                                    }
                                }
                            }
                            else
                            {
                                foreach(string groupName in _blockListDictionary.Keys)
                                {
                                    _blockGroupHealtQueue.Enqueue(groupName);
                                }
                            }

                            if (_blockGroupHealthDictionary.TryGetValue(GroupName, out _health))
                            {
                                if (++_numComponentsUpdated >= NumberOfComponents)
                                {
                                    _blockGroupHealthDictionary.Clear();
                                    _numComponentsUpdated = 0;
                                }
                            }
                            return;
                        }*/


                        if (!_blockGroupHealthDictionary.TryGetValue(GroupName, out _health))
                        {
                            List<IMyTerminalBlock> blockList;
                            if (_blockListDictionary.TryGetValue(GroupName, out blockList))
                            {
                                _currentFunctionalBlockCount = GetCurrentBlockCount(blockList);
                                _health = MathHelper.Clamp(_currentFunctionalBlockCount / _expectedBlockCount, 0, 1);
                                _blockGroupHealthDictionary[GroupName] = _health;
                            }
                        }


                        if (++_numComponentsUpdated >= NumberOfComponents)
                        {
                            _blockGroupHealthDictionary.Clear();
                            _numComponentsUpdated = 0;
                        }
                    }



                    private void UpdateHealth(List<IMyTerminalBlock> blocks)
                    {
                        int count = 0;
                        foreach (IMyTerminalBlock block in blocks)
                        {
                            if (!block.Closed && block.IsFunctional) count++;
                        }
                        _currentFunctionalBlockCount = count;
                        _health = MathHelper.Clamp(_currentFunctionalBlockCount / _expectedBlockCount, 0, 1);
                    }

                    private float GetCurrentBlockCount(List<IMyTerminalBlock> blocks)
                    {
                        int count = 0;
                        foreach (IMyTerminalBlock block in blocks)
                        {
                            if (!block.Closed && block.IsFunctional) count++;
                        }
                        _currentFunctionalBlockCount = count;
                        return _currentFunctionalBlockCount;
                    }

                    private float BoundedLinearInterpolation(float lowerBoundInput, float upperBoundInput, float lowerBoundOutput, float upperBoundOutput, float value)
                    {
                        float slope = (upperBoundOutput - lowerBoundOutput) / (upperBoundInput - lowerBoundInput);
                        float intercept = lowerBoundOutput - (slope * lowerBoundInput);
                        return slope * value + intercept;
                    }
                }
            }

            #region Tools

            #endregion Tools
        }

        #region INCLUDES
        public static class MyIniHelper
        {
            #region List<string>
            /// <summary>
            /// Deserializes a List<string> from MyIni
            /// </summary>
            public static void GetStringList(string section, string name, MyIni ini, List<string> list)
            {
                string raw = ini.Get(section, name).ToString(null);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    // Preserve contents
                    return;
                }

                list.Clear();
                string[] split = raw.Split(',');
                foreach (var s in split)
                {
                    list.Add(s);
                }
            }

            /// <summary>
            /// Serializes a List<string> to MyIni
            /// </summary>
            public static void SetStringList(string section, string name, MyIni ini, List<string> list)
            {
                string output = string.Join($"\n", list);
                ini.Set(section, name, output);
            }
            #endregion

            #region List<int>
            const char LIST_DELIMITER = ',';

            /// <summary>
            /// Deserializes a List<int> from MyIni
            /// </summary>
            public static void GetListInt(string section, string name, MyIni ini, List<int> list)
            {
                list.Clear();
                string raw = ini.Get(section, name).ToString();
                string[] split = raw.Split(LIST_DELIMITER);
                foreach (var s in split)
                {
                    int i;
                    if (int.TryParse(s, out i))
                    {
                        list.Add(i);
                    }
                }
            }

            /// <summary>
            /// Serializes a List<int> to MyIni
            /// </summary>
            public static void SetListInt(string section, string name, MyIni ini, List<int> list)
            {
                string output = string.Join($"{LIST_DELIMITER}", list);
                ini.Set(section, name, output);
            }
            #endregion

            #region Vector2
            /// <summary>
            /// Adds a Vector3D to a MyIni object
            /// </summary>
            public static void SetVector2(string sectionName, string vectorName, ref Vector2 vector, MyIni ini)
            {
                string vectorString = string.Format("{0}, {1}", vector.X, vector.Y);
                ini.Set(sectionName, vectorName, vectorString);
            }

            /// <summary>
            /// Parses a MyIni object for a Vector3D
            /// </summary>
            public static Vector2 GetVector2(string sectionName, string vectorName, MyIni ini, Vector2? defaultVector = null)
            {
                string vectorString = ini.Get(sectionName, vectorName).ToString("null");
                string[] stringSplit = vectorString.Split(',');

                float x, y;
                if (stringSplit.Length != 2)
                {
                    if (defaultVector.HasValue)
                        return defaultVector.Value;
                    else
                        return default(Vector2);
                }

                float.TryParse(stringSplit[0].Trim(), out x);
                float.TryParse(stringSplit[1].Trim(), out y);

                return new Vector2(x, y);
            }
            #endregion

            #region Vector3D
            /// <summary>
            /// Adds a Vector3D to a MyIni object
            /// </summary>
            public static void SetVector3D(string sectionName, string vectorName, ref Vector3D vector, MyIni ini)
            {
                ini.Set(sectionName, vectorName, vector.ToString());
            }

            /// <summary>
            /// Parses a MyIni object for a Vector3D
            /// </summary>
            public static Vector3D GetVector3D(string sectionName, string vectorName, MyIni ini, Vector3D? defaultVector = null)
            {
                var vector = Vector3D.Zero;
                if (Vector3D.TryParse(ini.Get(sectionName, vectorName).ToString(), out vector))
                    return vector;
                else if (defaultVector.HasValue)
                    return defaultVector.Value;
                return default(Vector3D);
            }
            #endregion

            #region ColorChar
            /// <summary>
            /// Adds a color character to a MyIni object
            /// </summary>
            public static void SetColorChar(string sectionName, string charName, char colorChar, MyIni ini)
            {
                int rgb = (int)colorChar - 0xe100;
                int b = rgb & 7;
                int g = rgb >> 3 & 7;
                int r = rgb >> 6 & 7;
                string colorString = $"{r}, {g}, {b}";

                ini.Set(sectionName, charName, colorString);
            }

            /// <summary>
            /// Parses a MyIni for a color character 
            /// </summary>
            public static char GetColorChar(string sectionName, string charName, MyIni ini, char defaultChar = (char)(0xe100))
            {
                string rgbString = ini.Get(sectionName, charName).ToString("null");
                string[] rgbSplit = rgbString.Split(',');

                int r = 0, g = 0, b = 0;
                if (rgbSplit.Length != 3)
                    return defaultChar;

                int.TryParse(rgbSplit[0].Trim(), out r);
                int.TryParse(rgbSplit[1].Trim(), out g);
                int.TryParse(rgbSplit[2].Trim(), out b);

                r = MathHelper.Clamp(r, 0, 7);
                g = MathHelper.Clamp(g, 0, 7);
                b = MathHelper.Clamp(b, 0, 7);

                return (char)(0xe100 + (r << 6) + (g << 3) + b);
            }
            #endregion

            #region Color
            /// <summary>
            /// Adds a Color to a MyIni object
            /// </summary>
            public static void SetColor(string sectionName, string itemName, Color color, MyIni ini, bool writeAlpha = true)
            {
                if (writeAlpha)
                {
                    ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}, {3}", color.R, color.G, color.B, color.A));
                }
                else
                {
                    ini.Set(sectionName, itemName, string.Format("{0}, {1}, {2}", color.R, color.G, color.B));
                }
            }

            /// <summary>
            /// Parses a MyIni for a Color
            /// </summary>
            public static Color GetColor(string sectionName, string itemName, MyIni ini, Color? defaultChar = null)
            {
                string rgbString = ini.Get(sectionName, itemName).ToString("null");
                string[] rgbSplit = rgbString.Split(',');

                int r = 0, g = 0, b = 0, a = 0;
                if (rgbSplit.Length < 3)
                {
                    if (defaultChar.HasValue)
                        return defaultChar.Value;
                    else
                        return Color.Transparent;
                }

                int.TryParse(rgbSplit[0].Trim(), out r);
                int.TryParse(rgbSplit[1].Trim(), out g);
                int.TryParse(rgbSplit[2].Trim(), out b);
                bool hasAlpha = rgbSplit.Length >= 4 && int.TryParse(rgbSplit[3].Trim(), out a);
                if (!hasAlpha)
                    a = 255;

                r = MathHelper.Clamp(r, 0, 255);
                g = MathHelper.Clamp(g, 0, 255);
                b = MathHelper.Clamp(b, 0, 255);
                a = MathHelper.Clamp(a, 0, 255);

                return new Color(r, g, b, a);
            }
            #endregion
        }
        #endregion INCLUDES
    }
}