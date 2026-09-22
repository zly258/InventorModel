using System;
using System.Collections.Generic;
using System.Globalization;

namespace InventorModel.Core.Dsl;

public sealed class ExpressionEvaluator
{
    private readonly IReadOnlyDictionary<string,double> _vars;
    private string _text="";
    private int _pos;
    public ExpressionEvaluator(IReadOnlyDictionary<string,double> vars)=>_vars=vars;

    public double Evaluate(string expression)
    {
        _text=Normalize(expression); _pos=0;
        var value=ParseExpression(); Skip();
        if(_pos!=_text.Length) throw new FormatException($"Unexpected expression tail '{_text.Substring(_pos)}'.");
        return value;
    }

    private static string Normalize(string s)=>s.Replace("mm","").Replace("MM","").Replace("deg","").Replace("DEG","").Trim();
    private double ParseExpression(){var v=ParseTerm();while(true){Skip();if(Match('+'))v+=ParseTerm();else if(Match('-'))v-=ParseTerm();else return v;}}
    private double ParseTerm(){var v=ParseFactor();while(true){Skip();if(Match('*'))v*=ParseFactor();else if(Match('/'))v/=ParseFactor();else return v;}}
    private double ParseFactor()
    {
        Skip();
        if(Match('+'))return ParseFactor();
        if(Match('-'))return -ParseFactor();
        if(Match('(')){var v=ParseExpression();if(!Match(')'))throw new FormatException("Missing ')'.");return v;}
        if(_pos<_text.Length&&(char.IsLetter(_text[_pos])||_text[_pos]=='_'))
        {
            var s=_pos++;while(_pos<_text.Length&&(char.IsLetterOrDigit(_text[_pos])||_text[_pos]=='_'))_pos++;
            var name=_text.Substring(s,_pos-s);
            if(!_vars.TryGetValue(name,out var value))throw new KeyNotFoundException($"Unknown parameter '{name}'.");
            return value;
        }
        var n=_pos;while(_pos<_text.Length&&(char.IsDigit(_text[_pos])||_text[_pos]=='.'))_pos++;
        if(n==_pos)throw new FormatException($"Expected number at {_pos}.");
        return double.Parse(_text.Substring(n,_pos-n),CultureInfo.InvariantCulture);
    }
    private void Skip(){while(_pos<_text.Length&&char.IsWhiteSpace(_text[_pos]))_pos++;}
    private bool Match(char c){Skip();if(_pos<_text.Length&&_text[_pos]==c){_pos++;return true;}return false;}
}
