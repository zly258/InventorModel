using System;
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
    private const string ModelPanelInternalName = "InventorModel.Model.Panel";
    private const string AiPanelInternalName = "InventorModel.AI.Panel";

    private global::Inventor.Application? _application;
    private ButtonDefinition? _build;
    private ButtonDefinition? _views;
    private ButtonDefinition? _ai;
    private ButtonDefinition? _settings;
    private UserInterfaceEvents? _uiEvents;
    private AiChatWindow? _chatWindow;

    private global::Inventor.Application Application =>
        _application ?? throw new InvalidOperationException("InventorModel Addin is not active.");

    public void Activate(ApplicationAddInSite site, bool firstTime)
    {
        global::Inventor.Application application = site.Application;
        _application = application;
        ControlDefinitions definitions = application.CommandManager.ControlDefinitions;

        _build = CreateButton(
            definitions,
            "生成模型",
            "InventorModel.Build",
            "从 .imodel 脚本生成原生可编辑 Inventor 零件",
            "选择并生成 .imodel 模型",
            RibbonIconKind.Model);

        _views = CreateButton(
            definitions,
            "四视图",
            "InventorModel.Views",
            "渲染当前零件的前、上、右和轴测验证视图",
            "生成四视图",
            RibbonIconKind.Views);

        _ai = CreateButton(
            definitions,
            "AI 对话",
            "InventorModel.AI",
            "打开 InventorModel AI 建模助手",
            "使用 AI 创建、检查和修改 Inventor 零件",
            RibbonIconKind.Chat);

        _settings = CreateButton(
            definitions,
            "AI 配置",
            "InventorModel.Settings",
            "配置 OpenAI Compatible 接口、模型和生成参数",
            "配置 AI 服务",
            RibbonIconKind.Settings);

        _build.OnExecute += Build;
        _views.OnExecute += Views;
        _ai.OnExecute += OpenAi;
        _settings.OnExecute += OpenSettings;

        BuildRibbon();

        try
        {
            _uiEvents = application.UserInterfaceManager.UserInterfaceEvents;
            _uiEvents.OnResetRibbonInterface += UiEvents_OnResetRibbonInterface;
        }
        catch { }
    }

    private ButtonDefinition CreateButton(
        ControlDefinitions definitions,
        string displayName,
        string internalName,
        string description,
        string tooltip,
        RibbonIconKind icon)
    {
        try
        {
            object smallIcon = RibbonIconFactory.Create(icon, 16);
            object largeIcon = RibbonIconFactory.Create(icon, 32);
            dynamic dynamicDefinitions = definitions;
            return (ButtonDefinition)dynamicDefinitions.AddButtonDefinition(
                displayName,
                internalName,
                CommandTypesEnum.kNonShapeEditCmdType,
                ClientId,
                description,
                tooltip,
                smallIcon,
                largeIcon);
        }
        catch
        {
            return definitions.AddButtonDefinition(
                displayName,
                internalName,
                CommandTypesEnum.kNonShapeEditCmdType,
                ClientId,
                description,
                tooltip);
        }
    }

    private void BuildRibbon()
    {
        if (_application == null || _build == null || _views == null || _ai == null || _settings == null)
            return;

        foreach (string ribbonName in new[] { "Part", "ZeroDoc" })
        {
            try
            {
                Ribbon? ribbon = null;
                try { ribbon = _application.UserInterfaceManager.Ribbons[ribbonName]; }
                catch { }
                if (ribbon == null) continue;

                RibbonTab? tab = FindTab(ribbon, TabInternalName);
                if (tab == null)
                    tab = ribbon.RibbonTabs.Add("AI建模", TabInternalName, ClientId);

                RibbonPanel? modelPanel = FindPanel(tab, ModelPanelInternalName);
                if (modelPanel == null)
                    modelPanel = tab.RibbonPanels.Add("模型", ModelPanelInternalName, ClientId);

                RibbonPanel? aiPanel = FindPanel(tab, AiPanelInternalName);
                if (aiPanel == null)
                    aiPanel = tab.RibbonPanels.Add("AI助手", AiPanelInternalName, ClientId);

                AddButtonIfMissing(modelPanel, _build, "InventorModel.Build", true);
                AddButtonIfMissing(modelPanel, _views, "InventorModel.Views", false);
                AddButtonIfMissing(aiPanel, _ai, "InventorModel.AI", true);
                AddButtonIfMissing(aiPanel, _settings, "InventorModel.Settings", false);
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
        string internalName,
        bool large)
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

        panel.CommandControls.AddButton(definition, large);
    }

    private void UiEvents_OnResetRibbonInterface(NameValueMap context) => BuildRibbon();

    private void Build(NameValueMap context)
    {
        using (var dialog = new OpenFileDialog { Filter = "InventorModel (*.imodel)|*.imodel" })
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                global::Inventor.Application application = Application;
                var document = application.ActiveDocument as PartDocument;
                new InventorModel.Inventor.ScriptExecutor(application).Execute(
                    System.IO.File.ReadAllText(dialog.FileName),
                    document);
                MessageBox.Show("模型生成完成。", "InventorModel");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "InventorModel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    private void Views(NameValueMap context)
    {
        global::Inventor.Application application = Application;
        if (!(application.ActiveDocument is PartDocument document)) return;

        using (var dialog = new FolderBrowserDialog())
        {
            if (dialog.ShowDialog() == DialogResult.OK)
                new InventorModel.Inventor.ModelRenderer(application).RenderFourViews(document, dialog.SelectedPath);
        }
    }

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

            if (!_chatWindow.IsVisible) _chatWindow.Show();
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
                new WindowInteropHelper(window).Owner = new IntPtr(Application.MainFrameHWND);
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
        try { if (_build != null) _build.OnExecute -= Build; } catch { }
        try { if (_views != null) _views.OnExecute -= Views; } catch { }
        try { if (_ai != null) _ai.OnExecute -= OpenAi; } catch { }
        try { if (_settings != null) _settings.OnExecute -= OpenSettings; } catch { }
        try { if (_uiEvents != null) _uiEvents.OnResetRibbonInterface -= UiEvents_OnResetRibbonInterface; } catch { }
        try { _chatWindow?.Close(); } catch { }

        _chatWindow = null;
        _application = null;
        _build = null;
        _views = null;
        _ai = null;
        _settings = null;
        _uiEvents = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void ExecuteCommand(int commandID) { }

    public object Automation => null!;
}
