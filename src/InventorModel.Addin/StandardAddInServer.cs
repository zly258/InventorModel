using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Inventor;
using InventorModel.Inventor;

namespace InventorModel.Addin;

[Guid("9D7D17FA-6A46-49A8-8E98-A7684F45B801")]
public sealed class StandardAddInServer : ApplicationAddInServer
{
    private Application _app; private ButtonDefinition _build; private ButtonDefinition _views;
    public void Activate(ApplicationAddInSite site,bool firstTime)
    {
        _app=site.Application;var defs=_app.CommandManager.ControlDefinitions;
        _build=defs.AddButtonDefinition("Build Script","InventorModel.Build",CommandTypesEnum.kNonShapeEditCmdType,
            "{9D7D17FA-6A46-49A8-8E98-A7684F45B801}","Build .imodel script","Build Script");
        _views=defs.AddButtonDefinition("Four Views","InventorModel.Views",CommandTypesEnum.kNonShapeEditCmdType,
            "{9D7D17FA-6A46-49A8-8E98-A7684F45B801}","Render model verification views","Four Views");
        _build.OnExecute+=Build;_views.OnExecute+=Views;
        var ribbon=_app.UserInterfaceManager.Ribbons["Part"];RibbonTab tab=null;
        foreach(RibbonTab t in ribbon.RibbonTabs)if(t.InternalName=="InventorModel.Tab")tab=t;
        if(tab==null)tab=ribbon.RibbonTabs.Add("InventorModel","InventorModel.Tab","{9D7D17FA-6A46-49A8-8E98-A7684F45B801}");
        RibbonPanel panel=null;foreach(RibbonPanel p in tab.RibbonPanels)if(p.InternalName=="InventorModel.Panel")panel=p;
        if(panel==null)panel=tab.RibbonPanels.Add("Model","InventorModel.Panel","{9D7D17FA-6A46-49A8-8E98-A7684F45B801}");
        panel.CommandControls.AddButton(_build,true);panel.CommandControls.AddButton(_views,true);
    }
    private void Build(NameValueMap context)
    {
        using(var dlg=new OpenFileDialog{Filter="InventorModel (*.imodel)|*.imodel"})
        {
            if(dlg.ShowDialog()!=DialogResult.OK)return;
            try{var doc=_app.ActiveDocument as PartDocument;new ScriptExecutor(_app).Execute(System.IO.File.ReadAllText(dlg.FileName),doc);MessageBox.Show("Build completed.","InventorModel");}
            catch(Exception ex){MessageBox.Show(ex.Message,"InventorModel",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
    }
    private void Views(NameValueMap context)
    {
        if(!(_app.ActiveDocument is PartDocument doc))return;
        using(var dlg=new FolderBrowserDialog()){if(dlg.ShowDialog()==DialogResult.OK)new ModelRenderer(_app).RenderFourViews(doc,dlg.SelectedPath);}
    }
    public void Deactivate(){if(_build!=null)_build.OnExecute-=Build;if(_views!=null)_views.OnExecute-=Views;_app=null;GC.Collect();GC.WaitForPendingFinalizers();}
    public void ExecuteCommand(int commandID){}
    public object Automation=>null;
}
