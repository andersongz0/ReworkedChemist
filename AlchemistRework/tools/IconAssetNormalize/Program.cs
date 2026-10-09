using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Technical normalization only: no recoloring, drawing, or AI-art substitution.
// The master is produced by imagegen. Keep the original master unchanged.
if (args.Length is <2 or >3) throw new ArgumentException("Pass generated master, NEW output folder, optional registered key.");
string key=args.Length==3?args[2]:"venom-flask";
if(!key.All(c=>char.IsAsciiLetterLower(c)||c=='-'))throw new ArgumentException("Invalid icon key.");
var output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Refusing to overwrite existing icon assets.");
using var source = new Bitmap(args[0]);
var left = source.Width; var top = source.Height; var right = -1; var bottom = -1;
for (var y = 0; y < source.Height; y++)
for (var x = 0; x < source.Width; x++)
{
    if (source.GetPixel(x, y).A < 16) continue;
    left = Math.Min(left, x); top = Math.Min(top, y);
    right = Math.Max(right, x); bottom = Math.Max(bottom, y);
}
if (right < left || bottom < top) throw new InvalidDataException("No visible icon.");
// Preserve a two-pixel antialiased border around the visible subject.
left = Math.Max(0, left - 2); top = Math.Max(0, top - 2);
right = Math.Min(source.Width - 1, right + 2); bottom = Math.Min(source.Height - 1, bottom + 2);
var crop = Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
Directory.CreateDirectory(output);
foreach (var spec in new[]{
    (Name:key+".item", Size:100, Box:new Rectangle(28,16,48,68)),
    (Name:key+".item-small", Size:48, Box:new Rectangle(12,9,25,31)),
    (Name:key+".ability", Size:96, Box:new Rectangle(20,6,56,84))})
{
    using var target = new Bitmap(spec.Size, spec.Size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(target))
    {
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var scale = Math.Min((float)spec.Box.Width/crop.Width,(float)spec.Box.Height/crop.Height);
        var w = (int)Math.Round(crop.Width*scale); var h = (int)Math.Round(crop.Height*scale);
        var rect = new Rectangle(spec.Box.X+(spec.Box.Width-w)/2,spec.Box.Y+(spec.Box.Height-h)/2,w,h);
        using var wrap = new ImageAttributes(); wrap.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(source,rect,crop.X,crop.Y,crop.Width,crop.Height,GraphicsUnit.Pixel,wrap);
    }
    var path = Path.Combine(output,spec.Name+".png");
    target.Save(path,ImageFormat.Png);
    using var reopened = new Bitmap(path);
    if (reopened.Width != spec.Size || reopened.Height != spec.Size || reopened.GetPixel(0,0).A != 0)
        throw new InvalidDataException("Native dimensions/transparency failed.");
    Console.WriteLine($"PASS {spec.Name}: {spec.Size}x{spec.Size}, transparent PNG");
}
