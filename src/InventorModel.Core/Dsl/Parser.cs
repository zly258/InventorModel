using System;
using System.Collections.Generic;
using System.Linq;

namespace InventorModel.Core.Dsl;

public sealed class DslParser
{
    private static readonly HashSet<string> SketchKinds=new(StringComparer.OrdinalIgnoreCase)
    {"point","line","circle","arc","ellipse","rect","centerrect","slot","polygon","spline","constraint","dim","project","offset"};

    public ModelScript Parse(string source)
    {
        var model=new ModelScript(); SketchStatement? sketch=null;
        var lines=source.Replace("\r\n","\n").Split('\n');
        for(int i=0;i<lines.Length;i++)
        {
            var raw=StripComment(lines[i]).Trim();
            if(raw.Length==0)continue;
            var t=Tokens(raw); if(t.Count==0)continue;

            if(sketch!=null)
            {
                if(t[0].Equals("end",StringComparison.OrdinalIgnoreCase)){model.Statements.Add(sketch);sketch=null;continue;}
                if(!SketchKinds.Contains(t[0]))throw new DslException(i+1,$"Unknown sketch command '{t[0]}'.");
                var s=new SketchLineStatement{Kind=t[0].ToLowerInvariant()};
                var argStart=1;
                if(t.Count>1&&NeedsName(s.Kind)){s.Name=t[1];argStart=2;}
                for(int a=argStart;a<t.Count;a++)s.Args.Add(t[a]);
                sketch.Lines.Add(s); continue;
            }

            switch(t[0].ToLowerInvariant())
            {
                case "part":
                    Need(t.Count>=2,i,"part <name>"); model.PartName=t[1]; break;
                case "param":
                    Need(t.Count>=4&&t[2]=="=",i,"param <name> = <expression>");
                    model.Statements.Add(new ParameterStatement{Name=t[1],Expression=string.Join(" ",t.Skip(3))}); break;
                case "sketch":
                    Need(t.Count>=4&&t[2].Equals("on",StringComparison.OrdinalIgnoreCase),i,"sketch <name> on <plane>");
                    sketch=new SketchStatement{Name=t[1],Plane=t[3]}; break;
                case "set":
                    Need(t.Count>=4&&t[2]=="=",i,"set <parameter> = <expression>");
                    model.Statements.Add(new EditStatement{Kind="set",Target=t[1],Value=string.Join(" ",t.Skip(3))}); break;
                case "suppress":
                case "unsuppress":
                case "delete":
                    Need(t.Count>=2,i,$"{t[0]} <feature>");
                    model.Statements.Add(new EditStatement{Kind=t[0].ToLowerInvariant(),Target=t[1]}); break;
                default:
                    model.Statements.Add(ParseFeature(t,i)); break;
            }
        }
        if(sketch!=null)throw new DslException(lines.Length,"Unclosed sketch block.");
        ValidateNames(model); return model;
    }

    private static FeatureStatement ParseFeature(List<string> t,int line)
    {
        Need(t.Count>=2,line,"feature requires a name");
        var f=new FeatureStatement{Kind=t[0].ToLowerInvariant(),Name=t[1]};
        int p=2;
        while(p<t.Count)
        {
            var k=t[p].ToLowerInvariant();
            if(k=="through"){f.Args["extent"]="through";p++;continue;}
            if(k=="join"||k=="cut"||k=="new"){f.Args["operation"]=k;p++;continue;}
            if(k=="from"||k=="on"||k=="profile"||k=="path"||k=="depth"||k=="distance"||k=="angle"||
               k=="axis"||k=="operation"||k=="diameter"||k=="radius"||k=="thickness"||k=="faces"||
               k=="edges"||k=="source"||k=="plane"||k=="count"||k=="spacing"||k=="at")
            {
                Need(p+1<t.Count,line,$"{k} requires a value");
                if((k=="at"||k=="spacing"||k=="count")&&p+2<t.Count&&!IsKeyword(t[p+2]))
                {f.Args[k]=t[p+1]+","+t[p+2];p+=3;}
                else {f.Args[k]=t[p+1];p+=2;}
                continue;
            }
            f.Items.Add(t[p]);p++;
        }
        return f;
    }

    private static bool IsKeyword(string s)=>new[]{"from","on","profile","path","depth","distance","angle","axis","operation","diameter","radius","thickness","faces","edges","source","plane","count","spacing","at","through","join","cut","new"}.Contains(s,StringComparer.OrdinalIgnoreCase);
    private static bool NeedsName(string kind)=>kind=="point"||kind=="line"||kind=="circle"||kind=="arc"||kind=="ellipse"||kind=="slot"||kind=="spline";
    private static void ValidateNames(ModelScript m)
    {
        var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var s in m.Statements)
        {
            string? n=s is ParameterStatement p?p.Name:s is SketchStatement sk?sk.Name:s is FeatureStatement f?f.Name:null;
            if(n!=null&&!set.Add(n))throw new InvalidOperationException($"Duplicate name '{n}'.");
        }
    }
    private static List<string> Tokens(string s)
    {
        var r=new List<string>();var cur="";bool q=false;
        foreach(var c in s){if(c=='"'){q=!q;continue;}if(char.IsWhiteSpace(c)&&!q){if(cur.Length>0){r.Add(cur);cur="";}}else cur+=c;}
        if(cur.Length>0)r.Add(cur);return r;
    }
    private static string StripComment(string s){var i=s.IndexOf('#');return i<0?s:s.Substring(0,i);}
    private static void Need(bool ok,int zeroLine,string message){if(!ok)throw new DslException(zeroLine+1,message);}
}
