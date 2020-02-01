using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Color = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Anime4KSharp
{
    public sealed class ImageProcess
    {
        public static void ComputeLuminance(Image<Color> origBitmap)
        {
            // This can be done in-place.
            int w = origBitmap.Width - 1;
            Parallel.For(0, origBitmap.Height - 1, y =>
            {
                Span<Color> scanline = origBitmap.GetPixelRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    float lum = GetBrightness(scanline[x]);
                    scanline[x].A = clamp(Convert.ToByte(lum * 255), 0, 0xFF);
                }
            });
        }

        //dotnet's impl, which may or may not be correct. it's missnamed
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetBrightness(Color pixel)
        {
            byte min = Math.Min(Math.Min(pixel.R, pixel.G), pixel.B);
            byte max = Math.Max(Math.Max(pixel.R, pixel.G), pixel.B);
            return (max + min) / (byte.MaxValue * 2f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color GetPixel(Span<Color> row, int x)
        {
            return row[x];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPixel(Span<Color> row, int x, Color c)
        {
            row[x] = c;
        }

        public static Image<Color> PushColor(Image<Color> oldBitmap, int strength)
        {
            Image<Color> newBitmap = oldBitmap.Clone();

            // Push color based on luminance.

            int h = oldBitmap.Height - 1;
            int w = oldBitmap.Width - 1;
            Parallel.For(0, h, y =>
            {
                var prevRow = y == 0 ? default : oldBitmap.GetPixelRowSpan(y - 1);
                var middleRow = oldBitmap.GetPixelRowSpan(y);
                var nextRow = oldBitmap.GetPixelRowSpan(y + 1);
                Span<Color> newMiddleRow = newBitmap.GetPixelRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    //Default translation constants
                    int xn = -1;
                    int xp = 1;
                    var yn = prevRow;
                    var yp = nextRow;

                    //If x or y is on the border, don't move out of bounds
                    if (x == 0)
                    {
                        xn = 0;
                    }
                    else if (x == w)
                    {
                        xp = 0;
                    }
                    if (y == 0)
                    {
                        yn = middleRow;
                    }
                    else if (y == h)
                    {
                        yp = middleRow;
                    }

                    /*
                     * Kernel defination:
                     * --------------
                     * [tl] [tc] [tr]
                     * [ml] [mc] [mc]
                     * [bl] [bc] [br]
                     * --------------
                     */

                    //Top column
                    var tl = GetPixel(yn, x + xn);
                    var tc = GetPixel(yn, x);
                    var tr = GetPixel(yn, x + xp);

                    //Middle column
                    var ml = GetPixel(middleRow, x + xn);
                    var mc = GetPixel(middleRow, x);
                    var mr = GetPixel(middleRow, x + xp);

                    //Bottom column
                    var bl = GetPixel(yp, x + xn);
                    var bc = GetPixel(yp, x);
                    var br = GetPixel(yp, x + xp);

                    var lightestColor = mc;

                    //Kernel 0 and 4
                    float maxDark = max3(br, bc, bl);
                    float minLight = min3(tl, tc, tr);

                    if (minLight > mc.A && minLight > maxDark)
                    {
                        lightestColor = getLargest(mc, lightestColor, tl, tc, tr, strength);
                    }
                    else
                    {
                        maxDark = max3(tl, tc, tr);
                        minLight = min3(br, bc, bl);
                        if (minLight > mc.A && minLight > maxDark)
                        {
                            lightestColor = getLargest(mc, lightestColor, br, bc, bl, strength);
                        }
                    }

                    //Kernel 1 and 5
                    maxDark = max3(mc, ml, bc);
                    minLight = min3(mr, tc, tr);

                    if (minLight > maxDark)
                    {
                        lightestColor = getLargest(mc, lightestColor, mr, tc, tr, strength);
                    }
                    else
                    {
                        maxDark = max3(mc, mr, tc);
                        minLight = min3(bl, ml, bc);
                        if (minLight > maxDark)
                        {
                            lightestColor = getLargest(mc, lightestColor, bl, ml, bc, strength);
                        }
                    }

                    //Kernel 2 and 6
                    maxDark = max3(ml, tl, bl);
                    minLight = min3(mr, br, tr);

                    if (minLight > mc.A && minLight > maxDark)
                    {
                        lightestColor = getLargest(mc, lightestColor, mr, br, tr, strength);
                    }
                    else
                    {
                        maxDark = max3(mr, br, tr);
                        minLight = min3(ml, tl, bl);
                        if (minLight > mc.A && minLight > maxDark)
                        {
                            lightestColor = getLargest(mc, lightestColor, ml, tl, bl, strength);
                        }
                    }

                    //Kernel 3 and 7
                    maxDark = max3(mc, ml, tc);
                    minLight = min3(mr, br, bc);

                    if (minLight > maxDark)
                    {
                        lightestColor = getLargest(mc, lightestColor, mr, br, bc, strength);
                    }
                    else
                    {
                        maxDark = max3(mc, mr, bc);
                        minLight = min3(tc, ml, tl);
                        if (minLight > maxDark)
                        {
                            lightestColor = getLargest(mc, lightestColor, tc, ml, tl, strength);
                        }
                    }
                    SetPixel(newMiddleRow, x, lightestColor);
                }
            });

            // Note that we don't have to re-calculate luminance again.
            return newBitmap;
        }
        public static Image<Color> ComputeGradient(Image<Color> oldBitmap)
        {
            Image<Color> newBitmap = oldBitmap.Clone();

            // Don't overwrite bm itself instantly after the one convolution is done. Do it after all convonlutions are done.
            int h = oldBitmap.Height - 1;
            int w = oldBitmap.Width - 1;

            // Sobel operator.
            int[,] sobelx = { {-1, 0, 1},
                              {-2, 0, 2},
                              {-1, 0, 1}};

            int[,] sobely = { {-1, -2, -1},
                              { 0, 0, 0},
                              { 1, 2, 1}};

            // Loop over each pixel and do convolution.
            Parallel.For(1, h, (int y) =>
            {
                var prevRow = oldBitmap.GetPixelRowSpan(y - 1);
                var middleRow = oldBitmap.GetPixelRowSpan(y);
                var nextRow = oldBitmap.GetPixelRowSpan(y + 1);
                Span<Color> newMiddleRow = newBitmap.GetPixelRowSpan(y);
                for (int x = 1; x < w; x++)
                {
                    Color pm1 = GetPixel(prevRow, x - 1);
                    Color p0 = GetPixel(prevRow, x);
                    Color pp1 = GetPixel(prevRow, x + 1);
                    Color mn1 = GetPixel(middleRow, x - 1);
                    Color m0 = GetPixel(middleRow, x);
                    Color mp1 = GetPixel(middleRow, x + 1);
                    Color nm1 = GetPixel(nextRow, x - 1);
                    Color n0 = GetPixel(nextRow, x);
                    Color np1 = GetPixel(nextRow, x + 1);

                    int dx = pm1.A * sobelx[0, 0] + p0.A * sobelx[0, 1] + pp1.A * sobelx[0, 2]
                              + mn1.A * sobelx[1, 0] + m0.A * sobelx[1, 1] + mp1.A * sobelx[1, 2]
                              + nm1.A * sobelx[2, 0] + n0.A * sobelx[2, 1] + np1.A * sobelx[2, 2];

                    int dy = pm1.A * sobely[0, 0] + p0.A * sobely[0, 1] + pp1.A * sobely[0, 2]
                           + mn1.A * sobely[1, 0] + m0.A * sobely[1, 1] + mp1.A * sobely[1, 2]
                           + nm1.A * sobely[2, 0] + n0.A * sobely[2, 1] + np1.A * sobely[2, 2];
                    double derivata = (dx * dx) + (dy * dy);

                    m0.A = (byte)(derivata > (255 * 255) ? 0 : (0xFF - (int)Math.Sqrt(derivata)));
                    SetPixel(newMiddleRow, x, m0);
                }
            });

            return newBitmap;
        }

        //Original HLSL's C# equivalent.
        //public static void ComputeGradient(ref Bitmap bm)
        //{
        //    Bitmap temp = new Bitmap(bm.Width, bm.Height);

        //    for (int x = 0; x < bm.Width - 1; x++)
        //    {
        //        for (int y = 0; y < bm.Height - 1; y++)
        //        {
        //            //Default translation constants
        //            int xn = -1;
        //            int xp = 1;
        //            int yn = -1;
        //            int yp = 1;

        //            //If x or y is on the border, don't move out of bounds
        //            if (x == 0)
        //            {
        //                xn = 0;
        //            }
        //            else if (x == bm.Width - 1)
        //            {
        //                xp = 0;
        //            }
        //            if (y == 0)
        //            {
        //                yn = 0;
        //            }
        //            else if (y == bm.Height - 1)
        //            {
        //                yp = 0;
        //            }

        //            var kernel = new List<Point>();
        //            //Top column
        //            //Point tl = new Point(x + xn, y + yn);
        //            //Point tc = new Point(x, y + yn);
        //            //Point tr = new Point(x + xp, y + yn);
        //            var tl = bm.GetPixel(x + xn, y + yn);
        //            var tc = bm.GetPixel(x, y + yn);
        //            var tr = bm.GetPixel(x + xp, y + yn);

        //            //Middle column
        //            //Point ml = new Point(x + xn, y);
        //            //Point mc = new Point(x, y);
        //            //Point mr = new Point(x + xp, y);
        //            var ml = bm.GetPixel(x + xn, y);
        //            var mc = bm.GetPixel(x, y);
        //            var mr = bm.GetPixel(x + xp, y);

        //            //Bottom column
        //            //Point bl = new Point(x + xn, y + yp);
        //            //Point bc = new Point(x, y + yp);
        //            //Point br = new Point(x + xp, y + yp);
        //            var bl = bm.GetPixel(x + xn, y + yp);
        //            var bc = bm.GetPixel(x, y + yp);
        //            var br = bm.GetPixel(x + xp, y + yp);

        //            int xgrad = (-tl.A + tr.A - ml.A - ml.A + mr.A + mr.A - bl.A + br.A);
        //            int ygrad = (-tl.A - tc.A - tc.A - tr.A + bl.A + bc.A + bc.A + br.A);

        //            double derivata = Math.Sqrt((xgrad * xgrad) + (ygrad * ygrad));

        //            if (derivata > 255)
        //            {
        //                temp.SetPixel(x, y, Color.FromArgb(255, mc.R, mc.G, mc.B));
        //            }
        //            else
        //            {
        //                temp.SetPixel(x, y, Color.FromArgb((int)derivata, mc.R, mc.G, mc.B));
        //            }
        //        }
        //    }

        //    // Write result to bm's alpha channel.
        //    Rectangle rect = new Rectangle(0, 0, bm.Width, bm.Height);
        //    bm = temp.Clone(rect, PixelFormat.Format32bppArgb);
        //    temp.Dispose();
        //}

        public static Image<Color> PushGradient(Image<Color> oldBitmap, int strength)
        {
            // Push color based on gradient.
            Image<Color> newBitmap = oldBitmap.Clone();

            int h = oldBitmap.Height - 1;
            int w = oldBitmap.Width - 1;

            Parallel.For(0, h, (int y) =>
            {
                var prevRow = y == 0 ? default : oldBitmap.GetPixelRowSpan(y - 1);
                var middleRow = oldBitmap.GetPixelRowSpan(y);
                var nextRow = oldBitmap.GetPixelRowSpan(y + 1);
                Span<Color> newMiddleRow = newBitmap.GetPixelRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    //Default translation constants
                    int xn = -1;
                    int xp = 1;
                    var yn = prevRow;
                    var yp = nextRow;

                    //If x or y is on the border, don't move out of bounds
                    if (x == 0)
                    {
                        xn = 0;
                    }
                    else if (x == w)
                    {
                        xp = 0;
                    }
                    if (y == 0)
                    {
                        yn = middleRow;
                    }
                    else if (y == h)
                    {
                        yp = middleRow;
                    }

                    //Top column
                    var tl = GetPixel(yn, x + xn);
                    var tc = GetPixel(yn, x);
                    var tr = GetPixel(yn, x + xp);

                    //Middle column
                    var ml = GetPixel(middleRow, x + xn);
                    var mc = GetPixel(middleRow, x);
                    var mr = GetPixel(middleRow, x + xp);

                    //Bottom column
                    var bl = GetPixel(yp, x + xn);
                    var bc = GetPixel(yp, x);
                    var br = GetPixel(yp, x + xp);

                    var lightestColor = mc;

                    //Kernel 0 and 4
                    float maxDark = max3(br, bc, bl);
                    float minLight = min3(tl, tc, tr);

                    if (minLight > mc.A && minLight > maxDark)
                    {
                        lightestColor = getAverage(mc, tl, tc, tr, strength);
                    }
                    else
                    {
                        maxDark = max3(tl, tc, tr);
                        minLight = min3(br, bc, bl);
                        if (minLight > mc.A && minLight > maxDark)
                        {
                            lightestColor = getAverage(mc, br, bc, bl, strength);
                        }
                    }

                    //Kernel 1 and 5
                    maxDark = max3(mc, ml, bc);
                    minLight = min3(mr, tc, tr);

                    if (minLight > maxDark)
                    {
                        lightestColor = getAverage(mc, mr, tc, tr, strength);
                    }
                    else
                    {
                        maxDark = max3(mc, mr, tc);
                        minLight = min3(bl, ml, bc);
                        if (minLight > maxDark)
                        {
                            lightestColor = getAverage(mc, bl, ml, bc, strength);
                        }
                    }

                    //Kernel 2 and 6
                    maxDark = max3(ml, tl, bl);
                    minLight = min3(mr, br, tr);

                    if (minLight > mc.A && minLight > maxDark)
                    {
                        lightestColor = getAverage(mc, mr, br, tr, strength);
                    }
                    else
                    {
                        maxDark = max3(mr, br, tr);
                        minLight = min3(ml, tl, bl);
                        if (minLight > mc.A && minLight > maxDark)
                        {
                            lightestColor = getAverage(mc, ml, tl, bl, strength);
                        }
                    }

                    //Kernel 3 and 7
                    maxDark = max3(mc, ml, tc);
                    minLight = min3(mr, br, bc);

                    if (minLight > maxDark)
                    {
                        lightestColor = getAverage(mc, mr, br, bc, strength);
                    }
                    else
                    {
                        maxDark = max3(mc, mr, bc);
                        minLight = min3(tc, ml, tl);
                        if (minLight > maxDark)
                        {
                            lightestColor = getAverage(mc, tc, ml, tl, strength);
                        }
                    }

                    // Remove alpha channel (which contains our graident) that is not needed.
                    lightestColor.A = 255;// = new Color(lightestColor.R, lightestColor.G, lightestColor.B, 255);
                    SetPixel(newMiddleRow, x, lightestColor);
                }
            });

            return newBitmap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte clamp(byte i, byte min, byte max)
        {
            if (i < min)
            {
                i = min;
            }
            else if (i > max)
            {
                i = max;
            }

            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int min3(Color a, Color b, Color c)
        {
            return Math.Min(Math.Min(a.A, b.A), c.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int max3(Color a, Color b, Color c)
        {
            return Math.Max(Math.Max(a.A, b.A), c.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color getLargest(Color cc, Color lightestColor, Color a, Color b, Color c, int strength)
        {
            byte ra = (byte)((cc.R * (0xFF - strength) + ((a.R + b.R + c.R) / 3) * strength) / 0xFF);
            byte ga = (byte)((cc.G * (0xFF - strength) + ((a.G + b.G + c.G) / 3) * strength) / 0xFF);
            byte ba = (byte)((cc.B * (0xFF - strength) + ((a.B + b.B + c.B) / 3) * strength) / 0xFF);
            byte aa = (byte)((cc.A * (0xFF - strength) + ((a.A + b.A + c.A) / 3) * strength) / 0xFF);

            var newColor = new Color(ra, ga, ba, aa);

            return newColor.A > lightestColor.A ? newColor : lightestColor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color getAverage(Color cc, Color a, Color b, Color c, int strength)
        {
            byte ra = (byte)((cc.R * (0xFF - strength) + ((a.R + b.R + c.R) / 3) * strength) / 0xFF);
            byte ga = (byte)((cc.G * (0xFF - strength) + ((a.G + b.G + c.G) / 3) * strength) / 0xFF);
            byte ba = (byte)((cc.B * (0xFF - strength) + ((a.B + b.B + c.B) / 3) * strength) / 0xFF);
            byte aa = (byte)((cc.A * (0xFF - strength) + ((a.A + b.A + c.A) / 3) * strength) / 0xFF);

            return new Color(ra, ga, ba, aa);
        }
    }
}
