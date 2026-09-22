using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Inventor;
using InventorModel.Inventor;

namespace InventorModel.Addin;

[Guid("9D7D17FA-6A46-49A8-8E98-A7684F45B801")]
public sealed class StandardAddInServer : ApplicationAddInServer
{
    private const string ClientId = "{9D7D17FA-6A46-49A8-8E98-A7684F45B801}";

    private global::Inventor.Application? _application;
    private ButtonDefinition? _build;
    private ButtonDefinition? _views;
    private ButtonDefinition? _ai;
    private AiChatWindow? _chatWindow;

    private global::Inventor.Application Application =>
        _application ?? throw new InvalidOperationException("InventorModel Addin is not active.");

    public void Activate(ApplicationAddInSite site, bool firstTime)
    {
        global::Inventor.Application application = site.Application;
        _application = application;
        var definitions = application.CommandManager.ControlDefinitions;

        _build = definitions.AddButtonDefinition(
            "Build Script",
            "InventorModel.Build",
            CommandTypesEnum.kNonShapeEditCmdType,
            ClientId,
            "Build .imodel script",
            "Build Script");

        _views = definitions.AddButtonDefinition(
            "Four Views",
            "InventorModel.Views",
            CommandTypesEnum.kNonShapeEditCmdType,
            ClientId,
            "Render model verification views",
            "Four Views");

        _ai = definitions.AddButtonDefinition(
            "AI Chat",
            "InventorModel.AI",
            CommandTypesEnum.kNonShapeEditCmdType,
            ClientId,
            "Open InventorModel AI modeling assistant",
            "AI Chat");

        _build.OnExecute += Build;
        _views.OnExecute += Views;
        _ai.OnExecute += OpenAi;

        Ribbon ribbon = application.UserInterfaceManager.Ribbons["Part"];
        RibbonTab? tab = null;
        foreach (RibbonTab item in ribbon.RibbonTabs)
            if (item.InternalName == "InventorModel.Tab") tab = item;

        if (tab == null)
            tab = ribbon.RibbonTabs.Add("InventorModel", "InventorModel.Tab", ClientId);

        RibbonPanel? panel = null;
        foreach (RibbonPanel item in tab.RibbonPanels)
            if (item.InternalName == "InventorModel.Panel") panel = item;

        if (panel == null)
            panel = tab.RibbonPanels.Add("Model", "InventorModel.Panel", ClientId);

        AddButtonIfMissing(panel, _build!, "InventorModel.Build");
        AddButtonIfMissing(panel, _views!, "InventorModel.Views");
        AddButtonIfMissing(panel, _ai!, "InventorModel.AI");
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

    private void Build(NameValueMap context)
    {
        using (var dialog = new OpenFileDialog { Filter = "InventorModel (*.imodel)|*.imodel" })
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                global::Inventor.Application application = Application;
                var document = application.ActiveDocument as PartDocument;
                new ScriptExecutor(application).Execute(
                    System.IO.File.ReadAllText(dialog.FileName),
                    document);
                MessageBox.Show("Build completed.", "InventorModel");
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
                new ModelRenderer(application).RenderFourViews(document, dialog.SelectedPath);
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

    public void Deactivate()
    {
        try { if (_build != null) _build.OnExecute -= Build; } catch { }
        try { if (_views != null) _views.OnExecute -= Views; } catch { }
        try { if (_ai != null) _ai.OnExecute -= OpenAi; } catch { }
        try { _chatWindow?.Close(); } catch { }

        _chatWindow = null;
        _application = null;
        _build = null;
        _views = null;
        _ai = null;

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void ExecuteCommand(int commandID) { }

    public object Automation => null!;
}
