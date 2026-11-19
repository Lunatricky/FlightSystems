using VRageMath;

namespace IngameScript
{
    static class ColorMap
    {
        public static Color SelectedColor(Color col, double factor)
        {
            if (col.R < 10 && col.G < 10 && col.B < 10)
                return Color.Lighten(col, factor);
            else return Color.Darken(col, factor);
        }
    }
}
