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
        [ExcludeFromCodeCoverage(Justification = "Testing this would only test the constructor of System.Drawing.Bitmap and we trust the .NET-framework")]
        public static unsafe string Encode(Image image, int componentsX, int componentsY, IProgress<int>? progress = null)
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
                bmpData = temporaryBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, temporaryBitmap.PixelFormat);

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
        /// <returns>The decoded preview</returns>
        [ExcludeFromCodeCoverage]
        public static Image Decode(string blurhash, int outputWidth, int outputHeight, double punch = 1.0)
        {
            var pixelData = new Pixel[outputWidth, outputHeight];
            Core.Decode(blurhash, pixelData, punch);
            return ConvertToBitmap(pixelData);
        }

        /// <summary>
        /// Converts the library-independent representation of pixels into a bitmap
        /// </summary>
        /// <param name="pixelData">The library-independent representation of the image</param>
        /// <returns>A <c>System.Drawing.Bitmap</c> in 32bpp-RGB representation</returns>
        public static unsafe Bitmap ConvertToBitmap(Pixel[,] pixelData)
        {
            var width = pixelData.GetLength(0);
            var height = pixelData.GetLength(1);

            var data = new byte[width * height * 4];

            var index = 0;
            for (var yPixel = 0; yPixel < height; yPixel++)
            for (var xPixel = 0; xPixel < width; xPixel++)
            {
                var pixel = pixelData[xPixel, yPixel];

                data[index++] = (byte)MathUtils.LinearTosRgb(pixel.Blue);
                data[index++] = (byte)MathUtils.LinearTosRgb(pixel.Green);
                data[index++] = (byte)MathUtils.LinearTosRgb(pixel.Red);
                data[index++] = 0;
            }

            Bitmap bmp;

            fixed (byte* ptr = data)
            {
                bmp = new Bitmap(width, height, width * 4, PixelFormat.Format32bppRgb, new IntPtr(ptr));
            }

            return bmp;
        }
    }
}
