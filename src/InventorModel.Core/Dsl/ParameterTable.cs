using System;
using System.Collections.Generic;
using System.Globalization;

namespace InventorModel.Core.Dsl;

public sealed class ParameterTable
{
    private readonly Dictionary<string,double> _values=new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string,double> Values=>_values;

    public double Add(string name,string expression)
    {
        var value=new ExpressionEvaluator(_values).Evaluate(expression);
        _values[name]=value; return value;
    }

    public void Import(string name,double millimeters)=>_values[name]=millimeters;

    public void Set(string name,string expression)
    {
        if(!_values.ContainsKey(name))throw new KeyNotFoundException($"Unknown parameter '{name}'.");
        _values[name]=new ExpressionEvaluator(_values).Evaluate(expression);
    }
    public double Mm(string expression)=>new ExpressionEvaluator(_values).Evaluate(expression);
    public double Cm(string expression)=>Mm(expression)/10.0;
    public double Degrees(string expression)=>new ExpressionEvaluator(_values).Evaluate(expression);
    public int Integer(string expression)=>(int)Math.Round(Mm(expression));

    public string Length(string expression)
    {
        var t=expression.Trim();
        return _values.ContainsKey(t)?t:$"{Mm(expression).ToString(CultureInfo.InvariantCulture)} mm";
    }

    public string Angle(string expression)
    {
        var t=expression.Trim();
        return _values.ContainsKey(t)?t:$"{Degrees(expression).ToString(CultureInfo.InvariantCulture)} deg";
    }
}
