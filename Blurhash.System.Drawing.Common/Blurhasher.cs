using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Blurhash;

namespace System.Drawing.Blurhash
{
    [SupportedOSPlatform("windows")]
    public static class Blurhasher
    {
        /// <summary>
        /// Encodes a picture into a Blurhash string
        /// </summary>
        /// <param name="image">The picture to encode</param>
        /// <param name="componentsX">The number of components used on the X-Axis for the DCT</param>
        /// <param name="componentsY">The number of components used on the Y-Axis for the DCT</param>
        /// <param name="progress">A progress reporter</param>
        /// <returns>The resulting Blurhash string</returns>
        [ExcludeFromCodeCoverage(Justification =
            "Testing this would only test the constructor of System.Drawing.Bitmap and we trust the .NET-framework")]
        public static unsafe string Encode(Image image,
            int componentsX,
            int componentsY,
            IProgress<int>? progress = null)
        {
            var width = image.Width;
            var height = image.Height;

            if (image is not Bitmap { PixelFormat: PixelFormat.Format24bppRgb } temporaryBitmap)
            {
                temporaryBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

                using var graphics = Graphics.FromImage(temporaryBitmap);
                graphics.DrawImageUnscaled(image, 0, 0);
            }

            var encoder = new StreamedEncoder(componentsX, componentsY, width, height, progress);

            BitmapData? bmpData = null;
            try
            {
                // Lock the bitmap's bits.
                bmpData = temporaryBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
                    temporaryBitmap.PixelFormat);

                // Get the address of the first line.
                var ptr = bmpData.Scan0;

                var rgb = (byte*)ptr.ToPointer();
                Span<StreamedPixel> buffer = stackalloc StreamedPixel[width];

                for (var y = 0; y < height; y++)
                {
                    var index = bmpData.Stride * y;

                    for (var x = 0; x < width; x++)
                    {
                        ref var res = ref buffer[x];
                        res.Blue = MathUtils.SRgbToLinear(rgb[index++]);
                        res.Green = MathUtils.SRgbToLinear(rgb[index++]);
                        res.Red = MathUtils.SRgbToLinear(rgb[index++]);
                        res.X = x;
                        res.Y = y;
                    }

                    encoder.Process(buffer);
                }

                return encoder.Finish();
            }
            finally
            {
                if (bmpData is not null) temporaryBitmap.UnlockBits(bmpData);
                if (temporaryBitmap != image) temporaryBitmap.Dispose();
            }
        }

        /// <summary>
        /// Decodes a Blurhash string into a <c>System.Drawing.Image</c>
        /// </summary>
        /// <param name="blurhash">The blurhash string to decode</param>
        /// <param name="outputWidth">The desired width of the output in pixels</param>
        /// <param name="outputHeight">The desired height of the output in pixels</param>
        /// <param name="punch">A value that affects the contrast of the decoded image. 1 means normal, smaller values will make the effect more subtle, and larger values will make it stronger.</param>
        /// <param name="progress">A progress reporter</param>
        /// <returns>The decoded preview</returns>
        [ExcludeFromCodeCoverage]
        public static unsafe Image Decode(string blurhash, int outputWidth, int outputHeight, double punch = 1.0,
            IProgress<int>? progress = null)
        {
            var data = new byte[outputWidth * outputHeight * 4];

            var decoder = new StreamedDecoder(blurhash, outputWidth, outputHeight, ResultCallback, punch, progress);
            decoder.Run();

            Bitmap bmp;

            fixed (byte* ptr = data)
            {
                bmp = new Bitmap(outputWidth, outputHeight, outputWidth * 4, PixelFormat.Format32bppRgb, new IntPtr(ptr));
            }
            
            return bmp;

            void ResultCallback(ReadOnlySpan<StreamedPixel> buffer)
            {
                foreach (var streamedPixel in buffer)
                {
                    var index = (streamedPixel.Y * outputWidth + streamedPixel.X) * 4;
                    data[index] = (byte)MathUtils.LinearTosRgb(streamedPixel.Blue);
                    data[index + 1] = (byte)MathUtils.LinearTosRgb(streamedPixel.Green);
                    data[index + 2] = (byte)MathUtils.LinearTosRgb(streamedPixel.Red);
                    data[index + 3] = 0;
                }
            }
        }
    }
}