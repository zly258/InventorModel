using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using Inventor;

namespace InventorModel.Addin;

[Guid("9D7D17FA-6A46-49A8-8E98-A7684F45B801")]
public sealed class StandardAddInServer : ApplicationAddInServer
{
    private const string ClientId = "{9D7D17FA-6A46-49A8-8E98-A7684F45B801}";
    private const string TabInternalName = "InventorModel.Tab";
    private const string PanelInternalName = "InventorModel.Main.Panel";
    private const string LegacyModelPanelInternalName = "InventorModel.Model.Panel";
    private const string LegacyAiPanelInternalName = "InventorModel.AI.Panel";

    private global::Inventor.Application? _application;
    private ButtonDefinition? _ai;
    private ButtonDefinition? _settings;
    private UserInterfaceEvents? _uiEvents;
    private AiChatWindow? _chatWindow;

    private global::Inventor.Application Application =>
        _application ?? throw new InvalidOperationException("InventorModel Addin is not active.");

    public void Activate(ApplicationAddInSite site, bool firstTime)
    {
        _application = site.Application;
        CreateButtonDefinitions();
        BuildRibbon();

        try
        {
            _uiEvents = Application.UserInterfaceManager.UserInterfaceEvents;
            _uiEvents.OnResetRibbonInterface += UiEvents_OnResetRibbonInterface;
        }
        catch { }
    }

    private void CreateButtonDefinitions()
    {
        ControlDefinitions definitions = Application.CommandManager.ControlDefinitions;

        _ai = CreateButton(
            definitions,
            "AI 对话",
            "InventorModel.AI",
            "打开 InventorModel AI 建模助手",
            "通过文本或工程图创建、检查和修改 Inventor 零件",
            "InventorModel.Addin.Resources.AiModel16.png",
            "InventorModel.Addin.Resources.AiModel32.png");

        _settings = CreateButton(
            definitions,
            "AI 配置",
            "InventorModel.Settings",
            "配置 InventorModel AI 服务",
            "配置接口地址、模型和生成参数",
            "InventorModel.Addin.Resources.AiSettings16.png",
            "InventorModel.Addin.Resources.AiSettings32.png");

        _ai.OnExecute += OpenAi;
        _settings.OnExecute += OpenSettings;
    }

    private static ButtonDefinition CreateButton(
        ControlDefinitions definitions,
        string displayName,
        string internalName,
        string description,
        string tooltip,
        string smallResource,
        string largeResource)
    {
        object smallIcon = LoadButtonIcon(smallResource) ?? Type.Missing;
        object largeIcon = LoadButtonIcon(largeResource) ?? Type.Missing;

        return definitions.AddButtonDefinition(
            displayName,
            internalName,
            CommandTypesEnum.kNonShapeEditCmdType,
            ClientId,
            description,
            tooltip,
            smallIcon,
            largeIcon);
    }

    private void BuildRibbon()
    {
        if (_application == null || _ai == null || _settings == null)
            return;

        foreach (string ribbonName in new[] { "Part", "ZeroDoc" })
        {
            try
            {
                Ribbon? ribbon = null;
                try { ribbon = _application.UserInterfaceManager.Ribbons[ribbonName]; }
                catch { }
                if (ribbon == null)
                    continue;

                RibbonTab tab = FindTab(ribbon, TabInternalName) ??
                                ribbon.RibbonTabs.Add("AI建模", TabInternalName, ClientId);

                RemoveLegacyPanels(tab);

                RibbonPanel panel = FindPanel(tab, PanelInternalName) ??
                                    tab.RibbonPanels.Add("AI建模", PanelInternalName, ClientId);

                AddButtonIfMissing(panel, _ai, "InventorModel.AI");
                AddButtonIfMissing(panel, _settings, "InventorModel.Settings");
            }
            catch { }
        }
    }

    private static RibbonTab? FindTab(Ribbon ribbon, string internalName)
    {
        foreach (RibbonTab item in ribbon.RibbonTabs)
            if (string.Equals(item.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
                return item;
        return null;
    }

    private static void RemoveLegacyPanels(RibbonTab tab)
    {
        var legacy = new System.Collections.Generic.List<RibbonPanel>();
        foreach (RibbonPanel item in tab.RibbonPanels)
        {
            if (string.Equals(item.InternalName, LegacyModelPanelInternalName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.InternalName, LegacyAiPanelInternalName, StringComparison.OrdinalIgnoreCase))
                legacy.Add(item);
        }

        foreach (RibbonPanel panel in legacy)
        {
            try { ((dynamic)panel).Delete(); }
            catch { }
        }
    }

    private static RibbonPanel? FindPanel(RibbonTab tab, string internalName)
    {
        foreach (RibbonPanel item in tab.RibbonPanels)
            if (string.Equals(item.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
                return item;
        return null;
    }

    private static void AddButtonIfMissing(
        RibbonPanel panel,
        ButtonDefinition definition,
        string internalName)
    {
        foreach (CommandControl control in panel.CommandControls)
        {
            try
            {
                if (string.Equals(control.InternalName, internalName, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch { }
        }

        panel.CommandControls.AddButton(definition, true);
    }

    private static object? LoadButtonIcon(string resourceName)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var image = Image.FromStream(stream);
            using var bitmap = new Bitmap(image);
            return PictureDispConverter.ToPictureDisp(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private void UiEvents_OnResetRibbonInterface(NameValueMap context) => BuildRibbon();

    private void OpenAi(NameValueMap context)
    {
        try
        {
            global::Inventor.Application application = Application;

            if (_chatWindow == null)
            {
                _chatWindow = new AiChatWindow(application);
                _chatWindow.AttachOwner(new IntPtr(application.MainFrameHWND));
                _chatWindow.Closed += (_, __) => _chatWindow = null;
            }

            if (!_chatWindow.IsVisible)
                _chatWindow.Show();

            if (_chatWindow.WindowState == System.Windows.WindowState.Minimized)
                _chatWindow.WindowState = System.Windows.WindowState.Normal;

            _chatWindow.Activate();
            _chatWindow.FocusInput();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "InventorModel AI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenSettings(NameValueMap context)
    {
        try
        {
            var window = new AiSettingsWindow(AiSettings.Load());
            try
            {
                new WindowInteropHelper(window).Owner =
                    new IntPtr(Application.MainFrameHWND);
            }
            catch { }

            if (window.ShowDialog() == true)
                _chatWindow?.ReloadSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "InventorModel AI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    public void Deactivate()
    {
        try { if (_ai != null) _ai.OnExecute -= OpenAi; } catch { }
        try { if (_settings != null) _settings.OnExecute -= OpenSettings; } catch { }
        try { if (_uiEvents != null) _uiEvents.OnResetRibbonInterface -= UiEvents_OnResetRibbonInterface; } catch { }
        try { _chatWindow?.Close(); } catch { }

        ReleaseComObject(_ai);
        ReleaseComObject(_settings);
        ReleaseComObject(_uiEvents);

        _chatWindow = null;
        _application = null;
        _ai = null;
        _settings = null;
        _uiEvents = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static void ReleaseComObject(object? value)
    {
        if (value == null)
            return;

        try
        {
            if (Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }
        catch { }
    }

    public void ExecuteCommand(int commandID) { }

    public object Automation => null!;

    private sealed class PictureDispConverter : AxHost
    {
        private PictureDispConverter() : base(string.Empty) { }

        public static object ToPictureDisp(Image image) =>
            GetIPictureDispFromPicture(image);
    }
}
