using Common;
using RegexTokenizer.Languages;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace RegexTokenizer;


[AttributeUsage(AttributeTargets.Class)]
public class LanguageAttribute : Attribute
{
    public string[] Identifiers { get; }

    public LanguageAttribute(string[] identifiers)
    {
        Identifiers = identifiers;
    }
}


public abstract partial class BaseTokenizer
{
    // FrozenDictionary оптимизирован для чтения и работает на уровне switch
    private static readonly FrozenDictionary<string, Func<BaseTokenizer>> TokenizerRegistry;

    static BaseTokenizer()
    {
        var registry = new Dictionary<string, Func<BaseTokenizer>>(StringComparer.Ordinal);

        var tokenizerTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BaseTokenizer)) && !t.IsAbstract);

        foreach (var type in tokenizerTypes)
        {
            var attribute = type.GetCustomAttribute<LanguageAttribute>();
            if (attribute != null)
            {
                BaseTokenizer factory() => (BaseTokenizer)Activator.CreateInstance(type)!;

                foreach (var id in attribute.Identifiers)
                {
                    if (!registry.TryAdd(id, factory))
                    {
                        Logger.Log($"Warning: Duplicate language ID '{id}' ignored for {type.Name}");
                    }
                }
            }
        }

        TokenizerRegistry = registry.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static BaseTokenizer CreateTokenizer(string? languageId)
    {
        Logger.Log($"Creating ... {languageId} tokenizer");

        if (languageId is not null && TokenizerRegistry.TryGetValue(languageId, out var factory))
        {
            return factory();
        }
        return new SimpleTokenizer();
    }

    public abstract List<Token> ParseContent(string content);
    public virtual long MaxContentSize => 256 * 1024;


    public static BaseTokenizer CreateBaseTokenizer()
    {
        return new SimpleTokenizer();
    }

    static public List<Token> UpdateTokensAsUTF8(string input, List<Token> tokens)
    {
        int currentUtf16Pos = 0;
        long currentUtf8BytePos = 0;
        var encoding = System.Text.Encoding.UTF8;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            int diffBegin = (int)token.begin - currentUtf16Pos;
            if (diffBegin > 0)
            {
                currentUtf8BytePos += encoding.GetByteCount(input, currentUtf16Pos, diffBegin);
                currentUtf16Pos = (int)token.begin;
            }
            long utf8Begin = currentUtf8BytePos;
            int diffEnd = (int)token.end - currentUtf16Pos;
            if (diffEnd > 0)
            {
                currentUtf8BytePos += encoding.GetByteCount(input, currentUtf16Pos, diffEnd);
                currentUtf16Pos = (int)token.end;
            }
            long utf8End = currentUtf8BytePos;
            tokens[i] = new Token(token.type, utf8Begin, utf8End);
        }
        return tokens;
    }
}
