using System;
using System.Collections.Generic;
using System.IO;
using Inventor;
using InventorModel.Inventor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventorModel.Mcp;

internal static class Program
{
    private static InventorSession _session;

    private static void Main()
    {
        Console.InputEncoding=System.Text.Encoding.UTF8;
        Console.OutputEncoding=new System.Text.UTF8Encoding(false);

        string line;
        while((line=Console.ReadLine())!=null)
        {
            if(string.IsNullOrWhiteSpace(line))continue;
            JObject request=null;
            try
            {
                request=JObject.Parse(line);
                JObject response=Dispatch(request);
                Console.WriteLine(response.ToString(Formatting.None));
            }
            catch(Exception ex)
            {
                Console.WriteLine(Error(request?["id"],-32603,ex.Message).ToString(Formatting.None));
            }
        }
    }

    private static JObject Dispatch(JObject r)
    {
        JToken id=r["id"];
        string method=r.Value<string>("method")??"";
        JObject p=r["params"] as JObject??new JObject();

        if(method=="initialize")
            return Result(id,new JObject{
                ["protocolVersion"]="2025-06-18",
                ["capabilities"]=new JObject{["tools"]=new JObject()},
                ["serverInfo"]=new JObject{["name"]="InventorModel",["version"]="0.1.0"}
            });

        if(method=="notifications/initialized")
            return nullResponse();

        if(method=="ping")
            return Result(id,new JObject());

        if(method=="tools/list")
            return Result(id,new JObject{["tools"]=Tools()});

        if(method=="tools/call")
        {
            string name=p.Value<string>("name")??"";
            JObject args=p["arguments"] as JObject??new JObject();
            return Result(id,new JObject{
                ["content"]=new JArray(new JObject{
                    ["type"]="text",
                    ["text"]=Call(name,args)
                }),
                ["isError"]=false
            });
        }

        return Error(id,-32601,"Method not found: "+method);
    }

    private static string Call(string name,JObject a)
    {
        EnsureSession();
        Application app=_session.Application;

        switch(name)
        {
            case "status":
                return Status(app);
            case "build":
            {
                string path=Need(a,"path");
                PartDocument doc=new ScriptExecutor(app).Execute(File.ReadAllText(Path.GetFullPath(path)));
                doc.Activate();
                return new ModelInspector().Inspect(doc);
            }
            case "modify":
            {
                PartDocument doc=ActivePart(app);
                string command=Need(a,"command");
                new ScriptExecutor(app).Execute(command,doc);
                return new ModelInspector().Inspect(doc);
            }
            case "inspect":
                return new ModelInspector().Inspect(ActivePart(app));
            case "render":
            {
                string dir=Path.GetFullPath(Need(a,"directory"));
                var files=new ModelRenderer(app).RenderFourViews(ActivePart(app),dir);
                return JsonConvert.SerializeObject(new{directory=dir,images=files});
            }
            case "save":
            {
                string path=Path.GetFullPath(Need(a,"path"));
                bool overwrite=a.Value<bool?>("overwrite")??false;
                if(File.Exists(path)&&!overwrite)throw new IOException("File already exists: "+path);
                ActivePart(app).SaveAs(path,false);
                return JsonConvert.SerializeObject(new{saved=true,path});
            }
            default:
                throw new InvalidOperationException("Unknown InventorModel tool: "+name);
        }
    }

    private static JArray Tools()
    {
        return new JArray(
            Tool("status","Report Inventor and active Part status",new JObject()),
            Tool("build","Build a native editable Part from an .imodel script",
                Props(("path","string","Path to .imodel script"))),
            Tool("modify","Apply a small conversational edit to the active Part. Examples: 'set width = 120', 'suppress fillet1', 'unsuppress fillet1', 'delete hole1'.",
                Props(("command","string","One InventorModel edit statement"))),
            Tool("inspect","Inspect active Part size, parameters and feature tree",new JObject()),
            Tool("render","Render front/top/right/isometric PNG views",
                Props(("directory","string","Output directory"))),
            Tool("save","Save active Part as native IPT",
                Props(("path","string","Output .ipt path"),("overwrite","boolean","Allow overwrite")))
        );
    }

    private static JObject Tool(string name,string description,JObject properties)
        => new JObject{
            ["name"]=name,
            ["description"]=description,
            ["inputSchema"]=new JObject{
                ["type"]="object",
                ["properties"]=properties,
                ["additionalProperties"]=false
            }
        };

    private static JObject Props(params (string Name,string Type,string Description)[] items)
    {
        var o=new JObject();
        foreach(var x in items)o[x.Name]=new JObject{{"type",x.Type},{"description",x.Description}};
        return o;
    }

    private static void EnsureSession()
    {
        if(_session==null)_session=InventorSession.Connect();
    }

    private static PartDocument ActivePart(Application app)
        => app.ActiveDocument as PartDocument??throw new InvalidOperationException("Active document is not an Inventor Part.");

    private static string Status(Application app)
    {
        var part=app.ActiveDocument as PartDocument;
        return JsonConvert.SerializeObject(new{
            connected=true,
            activeDocument=part?.DisplayName,
            documentType=part==null?"none":"part"
        });
    }

    private static string Need(JObject o,string key)
    {
        string s=o.Value<string>(key);
        if(string.IsNullOrWhiteSpace(s))throw new InvalidOperationException(key+" is required.");
        return s;
    }

    private static JObject Result(JToken id,JToken value)
        => new JObject{{"jsonrpc","2.0"},{"id",id},{"result",value}};

    private static JObject Error(JToken id,int code,string message)
        => new JObject{{"jsonrpc","2.0"},{"id",id},{"error",new JObject{{"code",code},{"message",message}}}};

    private static JObject nullResponse()=>new JObject();
}
