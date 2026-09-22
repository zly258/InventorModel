using System;
using System.IO;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using Inventor;
using InventorModel.Core.Dsl;
using InventorModel.Inventor;

namespace InventorModel.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine(
                    "InventorModel.Cli validate <file.imodel> | build <file.imodel> [output.ipt] | inspect | render [dir]");
                return 2;
            }

            if (args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2) throw new ArgumentException("validate requires a .imodel file.");
                ValidationResult validation = new ModelValidator().Validate(IOFile.ReadAllText(args[1]));
                if (validation.IsValid) { Console.WriteLine("valid"); return 0; }
                foreach (string error in validation.Errors) Console.Error.WriteLine(error);
                return 1;
            }

            InventorSession session = InventorSession.Connect();

            switch (args[0].ToLowerInvariant())
            {
                case "build":
                {
                    if (args.Length < 2)
                        throw new ArgumentException(
                            "build requires a .imodel file.");

                    PartDocument document =
                        new ScriptExecutor(session.Application).Execute(
                            IOFile.ReadAllText(args[1]));

                    string output = args.Length > 2
                        ? IOPath.GetFullPath(args[2])
                        : IOPath.ChangeExtension(
                            IOPath.GetFullPath(args[1]),
                            ".ipt");

                    document.SaveAs(output, false);
                    Console.WriteLine(output);
                    Console.WriteLine(
                        new ModelInspector().Inspect(document));
                    return 0;
                }

                case "inspect":
                {
                    if (!(session.Application.ActiveDocument is PartDocument part))
                        throw new InvalidOperationException(
                            "Active document is not a Part.");

                    Console.WriteLine(
                        new ModelInspector().Inspect(part));
                    return 0;
                }

                case "render":
                {
                    if (!(session.Application.ActiveDocument is PartDocument part))
                        throw new InvalidOperationException(
                            "Active document is not a Part.");

                    string directory = args.Length > 1
                        ? IOPath.GetFullPath(args[1])
                        : IOPath.Combine(
                            System.Environment.CurrentDirectory,
                            "views");

                    foreach (string file in
                        new ModelRenderer(session.Application)
                            .RenderFourViews(part, directory))
                    {
                        Console.WriteLine(file);
                    }

                    return 0;
                }

                default:
                    throw new ArgumentException(
                        $"Unknown command '{args[0]}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
