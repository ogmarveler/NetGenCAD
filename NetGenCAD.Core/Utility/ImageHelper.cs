using SkiaSharp;

namespace NetGenCAD.Core.Utility
{
    public class ImageHelper
    {
        public static (double Width, double Height, double Depth, string filePath) GetPngDimensions(string filePath, bool autoSmoothFile = false)
        {
            if (autoSmoothFile)
                filePath = ApplyAutoSmooth(filePath);

            using var image = SKBitmap.Decode(filePath)
                ?? throw new InvalidDataException($"Unable to decode image: {filePath}");
            var (width, height, maxZ) = GetSurfaceDimensions(image);
            return (width, height, maxZ, filePath); // Default 100mm for Z axis, OpenSCAD
        }

        public static string ApplyAutoSmooth(string inputPath, bool useGaussianBlur = true, float gaussianSigma = 2.0f, int boxRadius = 1)
        {
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = Path.GetExtension(inputPath);
            string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
            string outputPath = Path.Combine(directory, $"{fileName}_frmt{extension}");

            using var original = SKBitmap.Decode(inputPath)
                ?? throw new InvalidDataException($"Unable to decode image: {inputPath}");

            // ── Crop to content (and optionally force grayscale) before any smoothing ──
            using var image = CropToContent(ToGrayscale(original))
                ?? throw new InvalidDataException("Image is fully transparent; nothing to process.");

            if (useGaussianBlur && (!float.IsFinite(gaussianSigma) || gaussianSigma <= 0))
                throw new ArgumentOutOfRangeException(nameof(gaussianSigma), "Gaussian sigma must be finite and greater than zero.");
            if (!useGaussianBlur && boxRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(boxRadius), "Box radius cannot be negative.");

            int pixelCount = checked(image.Width * image.Height);
            var grayscale = new byte[pixelCount];
            var alpha = new byte[pixelCount];

            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    int index = y * image.Width + x;
                    grayscale[index] = (byte)Math.Clamp((int)Math.Round(
                        0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue), 0, 255);
                    alpha[index] = pixel.Alpha;
                }

            var (kernel, radius) = useGaussianBlur
                ? CreateGaussianKernel(gaussianSigma)
                : CreateBoxKernel(boxRadius);
            grayscale = ApplyBlur(grayscale, image.Width, image.Height, kernel, radius);
            alpha = ApplyBlur(alpha, image.Width, image.Height, kernel, radius);

            using var smoothed = new SKBitmap(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int index = y * image.Width + x;
                    byte gray = grayscale[index];
                    smoothed.SetPixel(x, y, new SKColor(gray, gray, gray, alpha[index]));
                }

            using var encodedImage = SKImage.FromBitmap(smoothed);
            using var data = encodedImage.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the smoothed image as PNG.");
            using (var output = File.Create(outputPath))
                data.SaveTo(output);

            return outputPath;
        }

        /*public static string ApplyAutoSmooth(string inputPath, bool useGaussianBlur = true, float gaussianSigma = 2.0f, int boxRadius = 1)
        {
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = Path.GetExtension(inputPath);
            string directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
            string outputPath = Path.Combine(directory, $"{fileName}_smooth{extension}");

            using var image = SKBitmap.Decode(inputPath)
                ?? throw new InvalidDataException($"Unable to decode image: {inputPath}");

            if (useGaussianBlur && (!float.IsFinite(gaussianSigma) || gaussianSigma <= 0))
                throw new ArgumentOutOfRangeException(nameof(gaussianSigma), "Gaussian sigma must be finite and greater than zero.");
            if (!useGaussianBlur && boxRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(boxRadius), "Box radius cannot be negative.");

            int pixelCount = checked(image.Width * image.Height);
            var grayscale = new byte[pixelCount];
            var alpha = new byte[pixelCount];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    int index = y * image.Width + x;
                    grayscale[index] = (byte)Math.Clamp((int)Math.Round(
                        0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue), 0, 255);
                    alpha[index] = pixel.Alpha;
                }

            var (kernel, radius) = useGaussianBlur
                ? CreateGaussianKernel(gaussianSigma)
                : CreateBoxKernel(boxRadius);
            grayscale = ApplyBlur(grayscale, image.Width, image.Height, kernel, radius);
            alpha = ApplyBlur(alpha, image.Width, image.Height, kernel, radius);

            using var smoothed = new SKBitmap(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int index = y * image.Width + x;
                    byte gray = grayscale[index];
                    smoothed.SetPixel(x, y, new SKColor(gray, gray, gray, alpha[index]));
                }

            using var encodedImage = SKImage.FromBitmap(smoothed);
            using var data = encodedImage.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("SkiaSharp could not encode the smoothed image as PNG.");
            using (var output = File.Create(outputPath))
                data.SaveTo(output);

            return outputPath;
        }*/

        public static (float WidthMm, float HeightMm, float MaxZMm) GetSurfaceDimensions(SKBitmap image)
        {
            float maxIntensity = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    float intensity = 0.2126f * pixel.Red / 255f + 0.7152f * pixel.Green / 255f + 0.0722f * pixel.Blue / 255f;
                    maxIntensity = Math.Max(maxIntensity, intensity);
                }

            float widthMm = image.Width;
            float heightMm = image.Height;
            float maxZMm = maxIntensity * 100f; // OpenSCAD’s 0–100 mm normalization

            return (widthMm, heightMm, maxZMm);
        }

        [Obsolete]
        private static SKBitmap ToGrayscale(SKBitmap source)
        {
            if (source == null) return null;

            var info = source.Info;
            using var grayscaleBitmap = new SKBitmap(info);
            using var canvas = new SKCanvas(grayscaleBitmap);

            // Luminosity grayscale matrix (preserves alpha)
            float[] matrix =
            {
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0,       0,       0,       1, 0
    };

            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(matrix)
            };

            canvas.DrawBitmap(source, 0, 0, paint);
            canvas.Flush();

            return grayscaleBitmap.Copy(); // return a copy so the using block can dispose the temporary
        }

        [Obsolete]
        private static SKBitmap CropToContent(SKBitmap source, byte alphaThreshold = 1)
        {
            if (source == null) return null;

            int width = source.Width;
            int height = source.Height;

            int left = width, top = height, right = 0, bottom = 0;
            bool found = false;

            // Scan for non-transparent pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var color = source.GetPixel(x, y);
                    if (color.Alpha > alphaThreshold)
                    {
                        found = true;
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
                }
            }

            if (!found)
                return null; // fully transparent

            // Inclusive bounds
            var cropRect = new SKRectI(left, top, right + 1, bottom + 1);

            var cropped = new SKBitmap(cropRect.Width, cropRect.Height, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(source, cropRect, new SKRect(0, 0, cropRect.Width, cropRect.Height));
            canvas.Flush();

            return cropped;
        }

        private static (double[] Kernel, int Radius) CreateGaussianKernel(float sigma)
        {
            int radius = checked((int)Math.Ceiling(sigma * 3));
            var kernel = new double[checked(radius * 2 + 1)];
            double sum = 0;
            for (int offset = -radius; offset <= radius; offset++)
            {
                double weight = Math.Exp(-(offset * offset) / (2 * sigma * sigma));
                kernel[offset + radius] = weight;
                sum += weight;
            }

            for (int i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;

            return (kernel, radius);
        }

        private static (double[] Kernel, int Radius) CreateBoxKernel(int radius)
        {
            var kernel = new double[checked(radius * 2 + 1)];
            Array.Fill(kernel, 1.0 / kernel.Length);
            return (kernel, radius);
        }

        private static byte[] ApplyBlur(byte[] source, int width, int height, double[] kernel, int radius)
        {
            if (radius == 0)
                return source;

            var horizontal = new byte[source.Length];
            var result = new byte[source.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double value = 0;
                    for (int offset = -radius; offset <= radius; offset++)
                    {
                        int sampleX = Math.Clamp(x + offset, 0, width - 1);
                        value += source[y * width + sampleX] * kernel[offset + radius];
                    }
                    horizontal[y * width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
                }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double value = 0;
                    for (int offset = -radius; offset <= radius; offset++)
                    {
                        int sampleY = Math.Clamp(y + offset, 0, height - 1);
                        value += horizontal[sampleY * width + x] * kernel[offset + radius];
                    }
                    result[y * width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
                }

            return result;
        }
    }
}
