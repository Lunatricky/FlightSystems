using System.Collections.Generic;
using VRageMath;

namespace IngameScript
{
    class ColorMap
    {
        static Dictionary<Color, string> ByColor;

        public static Color SelectedColor(string colStr, float factor)
        {
            return SelectedColor(GetColorFromString(colStr), factor);
        }

        public static Color SelectedColor(Color col, float factor)
        {
            float R = col.R + factor > 255 ? col.R - factor : col.R + factor;
            float G = col.G + factor > 255 ? col.G - factor : col.G + factor;
            float B = col.B + factor > 255 ? col.B - factor : col.B + factor;
            return new Color(R, G, B + factor);
        }

        public static Color GetColorFromString(string nameString)
        {
            Color c;
            if (ColorMap.ByName.TryGetValue(nameString, out c)) { /* use c */ }
            return c;
        }

        public static string GetStringFromColor(Color colorValue)
        {
            string name;
            if (ColorMap.ByColor.TryGetValue(colorValue, out name)) { /* use name */ }
            return name;
        }

        static ColorMap()
        {
            ByColor = new Dictionary<Color, string>(ByName.Count);
            foreach (var kv in ByName) ByColor[kv.Value] = kv.Key;
        }

        static readonly Dictionary<string, Color> ByName = new Dictionary<string, Color>
        {
            {"Transparent", Color.Transparent},
            {"AliceBlue", Color.AliceBlue},
            {"AntiqueWhite", Color.AntiqueWhite},
            {"Aqua", Color.Aqua},
            {"Aquamarine", Color.Aquamarine},
            {"Azure", Color.Azure},
            {"Beige", Color.Beige},
            {"Bisque", Color.Bisque},
            {"Black", Color.Black},
            {"BlanchedAlmond", Color.BlanchedAlmond},
            {"Blue", Color.Blue},
            {"BlueViolet", Color.BlueViolet},
            {"Brown", Color.Brown},
            {"BurlyWood", Color.BurlyWood},
            {"CadetBlue", Color.CadetBlue},
            {"Chartreuse", Color.Chartreuse},
            {"Chocolate", Color.Chocolate},
            {"Coral", Color.Coral},
            {"CornflowerBlue", Color.CornflowerBlue},
            {"Cornsilk", Color.Cornsilk},
            {"Crimson", Color.Crimson},
            {"Cyan", Color.Cyan},
            {"DarkBlue", Color.DarkBlue},
            {"DarkCyan", Color.DarkCyan},
            {"DarkGoldenrod", Color.DarkGoldenrod},
            {"DarkGray", Color.DarkGray},
            {"DarkGreen", Color.DarkGreen},
            {"DarkKhaki", Color.DarkKhaki},
            {"DarkMagenta", Color.DarkMagenta},
            {"DarkOliveGreen", Color.DarkOliveGreen},
            {"DarkOrange", Color.DarkOrange},
            {"DarkOrchid", Color.DarkOrchid},
            {"DarkRed", Color.DarkRed},
            {"DarkSalmon", Color.DarkSalmon},
            {"DarkSeaGreen", Color.DarkSeaGreen},
            {"DarkSlateBlue", Color.DarkSlateBlue},
            {"DarkSlateGray", Color.DarkSlateGray},
            {"DarkTurquoise", Color.DarkTurquoise},
            {"DarkViolet", Color.DarkViolet},
            {"DeepPink", Color.DeepPink},
            {"DeepSkyBlue", Color.DeepSkyBlue},
            {"DimGray", Color.DimGray},
            {"DodgerBlue", Color.DodgerBlue},
            {"Firebrick", Color.Firebrick},
            {"FloralWhite", Color.FloralWhite},
            {"ForestGreen", Color.ForestGreen},
            {"Fuchsia", Color.Fuchsia},
            {"Gainsboro", Color.Gainsboro},
            {"GhostWhite", Color.GhostWhite},
            {"Gold", Color.Gold},
            {"Goldenrod", Color.Goldenrod},
            {"Gray", Color.Gray},
            {"Green", Color.Green},
            {"GreenYellow", Color.GreenYellow},
            {"Honeydew", Color.Honeydew},
            {"HotPink", Color.HotPink},
            {"IndianRed", Color.IndianRed},
            {"Indigo", Color.Indigo},
            {"Ivory", Color.Ivory},
            {"Khaki", Color.Khaki},
            {"Lavender", Color.Lavender},
            {"LavenderBlush", Color.LavenderBlush},
            {"LawnGreen", Color.LawnGreen},
            {"LemonChiffon", Color.LemonChiffon},
            {"LightBlue", Color.LightBlue},
            {"LightCoral", Color.LightCoral},
            {"LightCyan", Color.LightCyan},
            {"LightGoldenrodYellow", Color.LightGoldenrodYellow},
            {"LightGray", Color.LightGray},
            {"LightGreen", Color.LightGreen},
            {"LightPink", Color.LightPink},
            {"LightSalmon", Color.LightSalmon},
            {"LightSeaGreen", Color.LightSeaGreen},
            {"LightSkyBlue", Color.LightSkyBlue},
            {"LightSlateGray", Color.LightSlateGray},
            {"LightSteelBlue", Color.LightSteelBlue},
            {"LightYellow", Color.LightYellow},
            {"Lime", Color.Lime},
            {"LimeGreen", Color.LimeGreen},
            {"Linen", Color.Linen},
            {"Magenta", Color.Magenta},
            {"Maroon", Color.Maroon},
            {"MediumAquamarine", Color.MediumAquamarine},
            {"MediumBlue", Color.MediumBlue},
            {"MediumOrchid", Color.MediumOrchid},
            {"MediumPurple", Color.MediumPurple},
            {"MediumSeaGreen", Color.MediumSeaGreen},
            {"MediumSlateBlue", Color.MediumSlateBlue},
            {"MediumSpringGreen", Color.MediumSpringGreen},
            {"MediumTurquoise", Color.MediumTurquoise},
            {"MediumVioletRed", Color.MediumVioletRed},
            {"MidnightBlue", Color.MidnightBlue},
            {"MintCream", Color.MintCream},
            {"MistyRose", Color.MistyRose},
            {"Moccasin", Color.Moccasin},
            {"NavajoWhite", Color.NavajoWhite},
            {"Navy", Color.Navy},
            {"OldLace", Color.OldLace},
            {"Olive", Color.Olive},
            {"OliveDrab", Color.OliveDrab},
            {"Orange", Color.Orange},
            {"OrangeRed", Color.OrangeRed},
            {"Orchid", Color.Orchid},
            {"PaleGoldenrod", Color.PaleGoldenrod},
            {"PaleGreen", Color.PaleGreen},
            {"PaleTurquoise", Color.PaleTurquoise},
            {"PaleVioletRed", Color.PaleVioletRed},
            {"PapayaWhip", Color.PapayaWhip},
            {"PeachPuff", Color.PeachPuff},
            {"Peru", Color.Peru},
            {"Pink", Color.Pink},
            {"Plum", Color.Plum},
            {"PowderBlue", Color.PowderBlue},
            {"Purple", Color.Purple},
            {"Red", Color.Red},
            {"RosyBrown", Color.RosyBrown},
            {"RoyalBlue", Color.RoyalBlue},
            {"SaddleBrown", Color.SaddleBrown},
            {"Salmon", Color.Salmon},
            {"SandyBrown", Color.SandyBrown},
            {"SeaGreen", Color.SeaGreen},
            {"SeaShell", Color.SeaShell},
            {"Sienna", Color.Sienna},
            {"Silver", Color.Silver},
            {"SkyBlue", Color.SkyBlue},
            {"SlateBlue", Color.SlateBlue},
            {"SlateGray", Color.SlateGray},
            {"Snow", Color.Snow},
            {"SpringGreen", Color.SpringGreen},
            {"SteelBlue", Color.SteelBlue},
            {"Tan", Color.Tan},
            {"Teal", Color.Teal},
            {"Thistle", Color.Thistle},
            {"Tomato", Color.Tomato},
            {"Turquoise", Color.Turquoise},
            {"Violet", Color.Violet},
            {"Wheat", Color.Wheat},
            {"White", Color.White},
            {"WhiteSmoke", Color.WhiteSmoke},
            {"Yellow", Color.Yellow},
            {"YellowGreen", Color.YellowGreen}
        };

        public static readonly string All = "Transparent, AliceBlue, AntiqueWhite, Aqua, Aquamarine, Azure, Beige, Bisque, Black, " +
            "BlanchedAlmond, Blue, BlueViolet, Brown, BurlyWood, CadetBlue, Chartreuse, Chocolate, Coral, CornflowerBlue, Cornsilk, " +
            "Crimson, Cyan, DarkBlue, DarkCyan, DarkGoldenrod, DarkGray, DarkGreen, DarkKhaki, DarkMagenta, DarkOliveGreen, DarkOrange, " +
            "DarkOrchid, DarkRed, DarkSalmon, DarkSeaGreen, DarkSlateBlue, DarkSlateGray, DarkTurquoise, DarkViolet, DeepPink, DeepSkyBlue, " +
            "DimGray, DodgerBlue, Firebrick, FloralWhite, ForestGreen, Fuchsia, Gainsboro, GhostWhite, Gold, Goldenrod, Gray, Green, GreenYellow, " +
            "Honeydew, HotPink, IndianRed, Indigo, Ivory, Khaki, Lavender, LavenderBlush, LawnGreen, LemonChiffon, LightBlue, LightCoral, LightCyan, " +
            "LightGoldenrodYellow, LightGray, LightGreen, LightPink, LightSalmon, LightSeaGreen, LightSkyBlue, LightSlateGray, LightSteelBlue, " +
            "LightYellow, Lime, LimeGreen, Linen, Magenta, Maroon, MediumAquamarine, MediumBlue, MediumOrchid, MediumPurple, MediumSeaGreen, " +
            "MediumSlateBlue, MediumSpringGreen, MediumTurquoise, MediumVioletRed, MidnightBlue, MintCream, MistyRose, Moccasin, NavajoWhite, " +
            "Navy, OldLace, Olive, OliveDrab, Orange, OrangeRed, Orchid, PaleGoldenrod, PaleGreen, PaleTurquoise, PaleVioletRed, PapayaWhip, " +
            "PeachPuff, Peru, Pink, Plum, PowderBlue, Purple, Red, RosyBrown, RoyalBlue, SaddleBrown, Salmon, SandyBrown, SeaGreen, SeaShell, " +
            "Sienna, Silver, SkyBlue, SlateBlue, SlateGray, Snow, SpringGreen, SteelBlue, Tan, Teal, Thistle, Tomato, Turquoise, Violet, Wheat, " +
            "White, WhiteSmoke, Yellow, YellowGreen";
    }
}
