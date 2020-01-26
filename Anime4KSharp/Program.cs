using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.IO;

namespace Anime4KSharp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Error: Please specify input and output png files");
                return;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            Image<Rgba32> image;
            using (Stream s = File.OpenRead(inputFile))
                image = Image.Load<Rgba32>(s);

            float scale = 2f;

            if (args.Length >= 3)
            {
                scale = float.Parse(args[2]);
            }

            float pushStrength = scale / 6f;
            float pushGradStrength = scale / 2f;

            if (args.Length >= 4)
            {
                pushStrength = float.Parse(args[4]);
            }

            if (args.Length >= 5)
            {
                pushGradStrength = float.Parse(args[3]);
            }

            upscale(image, (int)(image.Width * scale), (int)(image.Height * scale));
            //Save(image, "Bicubic.png");

            DateTime begin = DateTime.UtcNow;
            // Push twice to get sharper lines.
            for (int i = 0; i < 2; i++)
            {
                // Compute Luminance and store it to alpha channel.
                ImageProcess.ComputeLuminance(image);
                //Save(image, "Luminance.png");

                Image<Rgba32> img2;
                // Push (Notice that the alpha channel is pushed with rgb channels).
                using (image)
                    img2 = ImageProcess.PushColor(image, clamp((int)(pushStrength * 255), 0, 0xFFFF));
                //Save(img2, "Push.png");
                image = img2;

                // Compute Gradient of Luminance and store it to alpha channel.
                using (image)
                    img2 = ImageProcess.ComputeGradient(image);
                //Save(img2, "Grad.png");
                image = img2;

                // Push Gradient
                using (image)
                    img2 = ImageProcess.PushGradient(image, clamp((int)(pushGradStrength * 255), 0, 0xFFFF));
                //Save(img2, "PushGrad.png");
                image = img2;
            }
            TimeSpan span = DateTime.UtcNow - begin;
            Console.WriteLine(span.TotalMilliseconds);
            Save(image, outputFile);
        }

        static void Save(Image<Rgba32> image, string file)
        {
            using (Image<Rgb24> outImage = image.CloneAs<Rgb24>())
            using (Stream s = File.Create(file))
                outImage.SaveAsPng(s);
        }

        static void upscale(Image<Rgba32> bm, int width, int height, bool compand = false)
        {
            bm.Mutate(c => c.Resize(width, height, new BicubicResampler(), compand));
        }

        private static int clamp(int i, int min, int max)
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
    }
}
