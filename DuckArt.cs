using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DesktopDuck;

internal static class DuckArt
{
    public const int Size = 24;

    private static readonly string[] Pixels =
    [
        "........................",
        "........................",
        "........................",
        "..............OOOOOO....",
        ".............OYYYYYYO...",
        "............OYYHHYYYYO..",
        "............OYHHHYYYYO..",
        "............OYYYYYKYYO..",
        "............OYYYYYKYYO..",
        "............OYYYYYYYO...",
        "............OYYYYYYYOBBO",
        "...OO......OYYYYYYYYOBBO",
        "...OYO....OYYYYYYYYYO...",
        "...OYYOOOOYYYYYYYYYYO...",
        "...OYYYYYYYYYYYYYYYYO...",
        "...OYYYYYYYYYYYYYYYYO...",
        "....OYYYYYGGGGYYYYYYO...",
        "....OYYYYGYYYYGYYYYYO...",
        ".....OYYYYGGGGYYYYYO....",
        "......OYYYYYYYYYYYO.....",
        ".......OOOOOOOOOOO......",
        ".........B....B.........",
        "........BB....BB........",
        "........................"
    ];

    public static Bitmap CreateFrame(int step, bool quacking)
    {
        var bitmap = new Bitmap(Size, Size);
        using Graphics graphics = Graphics.FromImage(bitmap);
        using var outline = new SolidBrush(Color.FromArgb(78, 49, 24));
        using var yellow = new SolidBrush(Color.FromArgb(255, 216, 45));
        using var highlight = new SolidBrush(Color.FromArgb(255, 244, 145));
        using var gold = new SolidBrush(Color.FromArgb(230, 166, 25));
        using var orange = new SolidBrush(Color.FromArgb(255, 133, 31));
        using var black = new SolidBrush(Color.FromArgb(24, 23, 24));

        for (int y = 0; y < Pixels.Length; y++)
        {
            for (int x = 0; x < Pixels[y].Length; x++)
            {
                char pixel = Pixels[y][x];
                if (y >= 21)
                {
                    continue;
                }

                Brush? brush = pixel switch
                {
                    'O' => outline,
                    'Y' => yellow,
                    'H' => highlight,
                    'G' => gold,
                    'B' => orange,
                    'K' => black,
                    _ => null
                };
                if (brush is not null)
                {
                    graphics.FillRectangle(brush, x, y, 1, 1);
                }
            }
        }

        int leftFoot = step == 1 ? 8 : 9;
        int rightFoot = step == 1 ? 15 : 14;
        graphics.FillRectangle(orange, 9, 21, 1, 1);
        graphics.FillRectangle(orange, 14, 21, 1, 1);
        graphics.FillRectangle(orange, leftFoot - 1, 22, 3, 1);
        graphics.FillRectangle(orange, rightFoot, step == 1 ? 21 : 22, 3, 1);

        if (quacking)
        {
            graphics.FillRectangle(black, 21, 11, 3, 1);
            graphics.FillRectangle(orange, 21, 12, 3, 1);
        }

        return bitmap;
    }

    public static Icon CreateIcon()
    {
        using Bitmap frame = CreateFrame(0, false);
        using var bitmap = new Bitmap(32, 32);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(frame, new Rectangle(0, 0, 32, 32));
        }

        nint handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
}
