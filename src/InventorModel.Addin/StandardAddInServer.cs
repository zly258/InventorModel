using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using Inventor;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

[Guid("9D7D17FA-6A46-49A8-8E98-A7684F45B801")]
public sealed class StandardAddInServer : ApplicationAddInServer
{
    private const string ClientId =
        "{9D7D17FA-6A46-49A8-8E98-A7684F45B801}";
    private const string TabInternalName =
        "InventorModel.Tab";
    private const string PanelInternalName =
        "InventorModel.Main.Panel";
    private const string LegacyModelPanelInternalName =
        "InventorModel.Model.Panel";
    private const string LegacyAiPanelInternalName =
        "InventorModel.AI.Panel";

    private global::Inventor.Application? _application;
    private ButtonDefinition? _ai;
    private ButtonDefinition? _settings;
    private UserInterfaceEvents? _uiEvents;
    private AiChatWindow? _chatWindow;
    private string _uiLanguage = AiSettings.LanguageChinese;

    private global::Inventor.Application Application =>
        _application ??
        throw new InvalidOperationException(
            "InventorModel Addin is not active.");

    public void Activate(
        ApplicationAddInSite site,
        bool firstTime)
    {
        _application = site.Application;
        _uiLanguage = AiSettings.Load().UiLanguage;

        CreateButtonDefinitions();
        BuildRibbon(forceRecreate: true);

        try
        {
            _uiEvents =
                Application.UserInterfaceManager
                    .UserInterfaceEvents;
            _uiEvents.OnResetRibbonInterface +=
                UiEvents_OnResetRibbonInterface;
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Addin.Ribbon",
                "Ribbon reset events could not be subscribed.",
                ex);
        }

        AiSettings.Changed += AiSettings_Changed;
    }

    private void CreateButtonDefinitions()
    {
        ButtonDefinition? previousAi = _ai;
        ButtonDefinition? previousSettings = _settings;

        ControlDefinitions definitions =
            Application.CommandManager.ControlDefinitions;

        string suffix =
            UiText.IsEnglish(_uiLanguage)
                ? ".en"
                : ".zh";

        ButtonDefinition newAi = GetOrCreateButton(
            definitions,
            UiText.Get(_uiLanguage, "Ribbon.Ai"),
            "InventorModel.AI" + suffix,
            UiText.Get(
                _uiLanguage,
                "Ribbon.AiDescription"),
            UiText.Get(
                _uiLanguage,
                "Ribbon.AiTooltip"),
            "InventorModel.Addin.Resources.AiModel16.png",
            "InventorModel.Addin.Resources.AiModel32.png");

        ButtonDefinition newSettings = GetOrCreateButton(
            definitions,
            UiText.Get(_uiLanguage, "Ribbon.Settings"),
            "InventorModel.Settings" + suffix,
            UiText.Get(
                _uiLanguage,
                "Ribbon.SettingsDescription"),
            UiText.Get(
                _uiLanguage,
                "Ribbon.SettingsTooltip"),
            "InventorModel.Addin.Resources.AiSettings16.png",
            "InventorModel.Addin.Resources.AiSettings32.png");

        if (previousAi != null)
        {
            TryCleanup(
                "Detach previous AI command handler",
                () => previousAi.OnExecute -= OpenAi);
        }

        if (previousSettings != null)
        {
            TryCleanup(
                "Detach previous settings command handler",
                () => previousSettings.OnExecute -= OpenSettings);
        }

        TryCleanup(
            "Detach duplicate localized AI command handler",
            () => newAi.OnExecute -= OpenAi);
        TryCleanup(
            "Detach duplicate localized settings command handler",
            () => newSettings.OnExecute -= OpenSettings);

        _ai = newAi;
        _settings = newSettings;

        _ai.OnExecute += OpenAi;
        _settings.OnExecute += OpenSettings;

        if (previousAi != null &&
            !ReferenceEquals(previousAi, _ai))
        {
            ReleaseComObject(previousAi);
        }

        if (previousSettings != null &&
            !ReferenceEquals(previousSettings, _settings))
        {
            ReleaseComObject(previousSettings);
        }
    }

    private static ButtonDefinition GetOrCreateButton(
        ControlDefinitions definitions,
        string displayName,
        string internalName,
        string description,
        string tooltip,
        string smallResource,
        string largeResource)
    {
        try
        {
            ButtonDefinition? existing =
                definitions[internalName] as ButtonDefinition;
            if (existing != null)
                return existing;
        }
        catch (Exception ex)
        {
            RuntimeLog.Info(
                "Addin.Ribbon",
                "Localized control definition does not exist yet: " +
                internalName +
                " (" +
                ex.GetType().Name +
                ")");
        }

        object smallIcon =
            LoadButtonIcon(smallResource) ??
            Type.Missing;
        object largeIcon =
            LoadButtonIcon(largeResource) ??
            Type.Missing;

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

    private void BuildRibbon(bool forceRecreate)
    {
        if (_application == null ||
            _ai == null ||
            _settings == null)
            return;

        foreach (string ribbonName in
                 new[] { "Part", "ZeroDoc" })
        {
            try
            {
                Ribbon? ribbon = TryGetRibbon(ribbonName);
                if (ribbon == null)
                    continue;

                RibbonTab? existing =
                    FindTab(ribbon, TabInternalName);

                if (forceRecreate && existing != null)
                {
                    try
                    {
                        existing.Delete();
                        existing = null;
                    }
                    catch (Exception ex)
                    {
                        RuntimeLog.Warning(
                            "Addin.Ribbon",
                            "Existing localized ribbon tab could not be recreated.",
                            ex);
                    }
                }

                RibbonTab tab =
                    existing ??
                    ribbon.RibbonTabs.Add(
                        UiText.Get(
                            _uiLanguage,
                            "Ribbon.Tab"),
                        TabInternalName,
                        ClientId);

                RemoveLegacyPanels(tab);

                RibbonPanel panel =
                    FindPanel(
                        tab,
                        PanelInternalName) ??
                    tab.RibbonPanels.Add(
                        UiText.Get(
                            _uiLanguage,
                            "Ribbon.Panel"),
                        PanelInternalName,
                        ClientId);

                AddButtonIfMissing(
                    panel,
                    _ai,
                    _ai.InternalName);
                AddButtonIfMissing(
                    panel,
                    _settings,
                    _settings.InternalName);
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Addin.Ribbon",
                    "InventorModel ribbon could not be built for " +
                    ribbonName +
                    ".",
                    ex);
            }
        }
    }

    private Ribbon? TryGetRibbon(string ribbonName)
    {
        try
        {
            return Application
                .UserInterfaceManager
                .Ribbons[ribbonName];
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Addin.Ribbon",
                "Inventor ribbon could not be accessed: " +
                ribbonName,
                ex);
            return null;
        }
    }

    private static RibbonTab? FindTab(
        Ribbon ribbon,
        string internalName)
    {
        foreach (RibbonTab item in ribbon.RibbonTabs)
        {
            if (string.Equals(
                    item.InternalName,
                    internalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static void RemoveLegacyPanels(
        RibbonTab tab)
    {
        var legacy =
            new System.Collections.Generic.List<RibbonPanel>();

        foreach (RibbonPanel item in tab.RibbonPanels)
        {
            if (string.Equals(
                    item.InternalName,
                    LegacyModelPanelInternalName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    item.InternalName,
                    LegacyAiPanelInternalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                legacy.Add(item);
            }
        }

        foreach (RibbonPanel panel in legacy)
        {
            try
            {
                panel.Delete();
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Addin.Ribbon",
                    "A legacy InventorModel ribbon panel could not be removed.",
                    ex);
            }
        }
    }

    private static RibbonPanel? FindPanel(
        RibbonTab tab,
        string internalName)
    {
        foreach (RibbonPanel item in tab.RibbonPanels)
        {
            if (string.Equals(
                    item.InternalName,
                    internalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private static void AddButtonIfMissing(
        RibbonPanel panel,
        ButtonDefinition definition,
        string internalName)
    {
        foreach (CommandControl control in
                 panel.CommandControls)
        {
            try
            {
                if (string.Equals(
                        control.InternalName,
                        internalName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Addin.Ribbon",
                    "A ribbon command control could not be inspected.",
                    ex);
            }
        }

        panel.CommandControls.AddButton(
            definition,
            true);
    }

    private static object? LoadButtonIcon(
        string resourceName)
    {
        try
        {
            Assembly assembly =
                Assembly.GetExecutingAssembly();

            using Stream? stream =
                assembly.GetManifestResourceStream(
                    resourceName);

            if (stream == null)
                return null;

            using var image =
                Image.FromStream(stream);
            using var bitmap =
                new Bitmap(image);

            return PictureDispConverter
                .ToPictureDisp(bitmap);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Addin.Ribbon",
                "Ribbon icon could not be loaded: " +
                resourceName,
                ex);
            return null;
        }
    }

    private void UiEvents_OnResetRibbonInterface(
        NameValueMap context) =>
        BuildRibbon(forceRecreate: false);

    private void AiSettings_Changed(
        object? sender,
        EventArgs e)
    {
        string language =
            AiSettings.Load().UiLanguage;

        if (string.Equals(
                language,
                _uiLanguage,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _uiLanguage = language;
        CreateButtonDefinitions();
        BuildRibbon(forceRecreate: true);
    }

    private void OpenAi(NameValueMap context)
    {
        try
        {
            global::Inventor.Application application =
                Application;

            if (_chatWindow == null)
            {
                _chatWindow =
                    new AiChatWindow(application);
                _chatWindow.AttachOwner(
                    new IntPtr(
                        application.MainFrameHWND));
                _chatWindow.Closed +=
                    (_, __) => _chatWindow = null;
            }

            if (!_chatWindow.IsVisible)
                _chatWindow.Show();

            if (_chatWindow.WindowState ==
                System.Windows.WindowState.Minimized)
            {
                _chatWindow.WindowState =
                    System.Windows.WindowState.Normal;
            }

            _chatWindow.Activate();
            _chatWindow.FocusInput();
        }
        catch (Exception ex)
        {
            RuntimeLog.Error(
                "Addin.UI",
                "AI Chat could not be opened.",
                ex);

            MessageBox.Show(
                ex.Message,
                "InventorModel AI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenSettings(
        NameValueMap context)
    {
        try
        {
            var window =
                new AiSettingsWindow(
                    AiSettings.Load());

            try
            {
                new WindowInteropHelper(window).Owner =
                    new IntPtr(
                        Application.MainFrameHWND);
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Addin.UI",
                    "Inventor main window could not be assigned as settings owner.",
                    ex);
            }

            if (window.ShowDialog() == true)
                _chatWindow?.ReloadSettings();
        }
        catch (Exception ex)
        {
            RuntimeLog.Error(
                "Addin.UI",
                "AI settings could not be opened.",
                ex);

            MessageBox.Show(
                ex.Message,
                "InventorModel AI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    public void Deactivate()
    {
        AiSettings.Changed -= AiSettings_Changed;

        TryCleanup(
            "Detach AI command handler",
            () =>
            {
                if (_ai != null)
                    _ai.OnExecute -= OpenAi;
            });
        TryCleanup(
            "Detach settings command handler",
            () =>
            {
                if (_settings != null)
                    _settings.OnExecute -=
                        OpenSettings;
            });
        TryCleanup(
            "Detach ribbon reset handler",
            () =>
            {
                if (_uiEvents != null)
                {
                    _uiEvents.OnResetRibbonInterface -=
                        UiEvents_OnResetRibbonInterface;
                }
            });
        TryCleanup(
            "Close AI Chat window",
            () => _chatWindow?.Close());

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

    private static void ReleaseComObject(
        object? value)
    {
        if (value == null)
            return;

        try
        {
            if (Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Addin.COM",
                "COM object could not be released.",
                ex);
        }
    }

    private static void TryCleanup(
        string operation,
        Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Addin.Cleanup",
                operation + " failed.",
                ex);
        }
    }

    public void ExecuteCommand(int commandID) { }

    public object Automation => null!;

    private sealed class PictureDispConverter :
        AxHost
    {
        private PictureDispConverter() :
            base(string.Empty) { }

        public static object ToPictureDisp(
            Image image) =>
            GetIPictureDispFromPicture(image);
    }
}
