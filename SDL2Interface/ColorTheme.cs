using RegexTokenizer;
using SDL_Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL2Interface
{
    public class ColorTheme
    {
        public static Color GetColor(TokenType type)
        {
            return type switch
            {
                TokenType.Comment => new Color(128, 128, 128, 255),
                TokenType.MultilineComment => new Color(180, 128, 180, 255),
                TokenType.Char => new Color(255, 128, 0, 255),
                TokenType.String => new Color(255, 255, 0, 255),
                TokenType.RawString => new Color(255, 255, 0, 255),
                TokenType.FormatString => new Color(255, 255, 0, 255),
                TokenType.IntegerLiteral => new Color(100, 200, 0, 255),
                TokenType.FloatLiteral => new Color(0, 255, 255, 255),
                TokenType.Variable => new Color(0, 128, 255, 255),
                TokenType.Costant => new Color(0, 128, 255, 255),
                TokenType.Macro => new Color(255, 128, 128, 255),
                TokenType.Function => new Color(0, 128, 128, 255),
                TokenType.Class => new Color(0, 255, 128, 255),
                TokenType.Keyword => new Color(255, 0, 0, 255),
                TokenType.Operator => new Color(255, 0, 255, 255),
                TokenType.NameHint => new Color(100, 100, 100, 255),
                TokenType.OpenBraceRound => new Color(255, 0, 255, 255),
                TokenType.CloseBraceRound => new Color(255, 0, 255, 255),
                TokenType.OpenBraceSquare => new Color(255, 0, 255, 255),
                TokenType.CloseBraceSquare => new Color(255, 0, 255, 255),
                TokenType.OpenBraceCurl => new Color(255, 0, 255, 255),
                TokenType.CloseBraceCurl => new Color(255, 0, 255, 255),
                _ => new Color(255, 255, 255, 255),
            };
        }
    }
}
