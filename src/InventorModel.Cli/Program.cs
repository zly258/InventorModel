using System;
using System.IO;
using IOPath = System.IO.Path;
using Inventor;
using InventorModel.Inventor;

namespace InventorModel.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==0){Console.Error.WriteLine("InventorModel.Cli build <file.imodel> [output.ipt] | inspect | render [dir]");return 2;}
            var session=InventorSession.Connect();
            switch(args[0].ToLowerInvariant())
            {
                case "build":
                    if(args.Length<2)throw new ArgumentException("build requires a .imodel file.");
                    var doc=new ScriptExecutor(session.Application).Execute(File.ReadAllText(args[1]));
                    var output=args.Length>2?IOPath.GetFullPath(args[2]):IOPath.ChangeExtension(IOPath.GetFullPath(args[1]),".ipt");
                    doc.SaveAs(output,false);Console.WriteLine(output);Console.WriteLine(new ModelInspector().Inspect(doc));return 0;
                case "inspect":
                    if(!(session.Application.ActiveDocument is PartDocument part))throw new InvalidOperationException("Active document is not a Part.");
                    Console.WriteLine(new ModelInspector().Inspect(part));return 0;
                case "render":
                    if(!(session.Application.ActiveDocument is PartDocument renderPart))throw new InvalidOperationException("Active document is not a Part.");
                    var dir=args.Length>1?IOPath.GetFullPath(args[1]):IOPath.Combine(Environment.CurrentDirectory,"views");
                    foreach(var file in new ModelRenderer(session.Application).RenderFourViews(renderPart,dir))Console.WriteLine(file);return 0;
                default:throw new ArgumentException($"Unknown command '{args[0]}'.");
            }
        }
        catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
}
