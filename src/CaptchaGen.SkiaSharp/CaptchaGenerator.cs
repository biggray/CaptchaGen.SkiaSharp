using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace CaptchaGen.SkiaSharp
{
    public class CaptchaGenerator : ICaptchaGenerator
    {
        protected static Random RandomGen { get; set; } = new Random();

        protected SKColor PaintColor { get; set; }
        protected SKColor BackgroundColor { get; set; }
        protected SKColor NoisePointColor { get; set; }

        protected int ImageWidth { get; set; }
        protected int ImageHeight { get; set; }

        protected string FontName { get; set; }
        protected int FontSize { get; set; }

        protected Func<(int oldX, int oldY), (int newX, int newY)> DistortionFunc { get; set; }
        protected Func<IEnumerable<(int x, int y)>> NoisePointMapGenFunc { get; set; }

        public CaptchaGenerator(
            string paintColorHex = "#808080", string backgroundColorHex = "#F5DEB3", string noisePointColorHex = "#D3D3D3",
            int imageWidth = 120, int imageHeight = 48,
            string fontName = null, int fontSize = 20,
            bool enableDistortion = true, int minDistortion = 5, int maxDistortion = 15,
            bool enableNoisePoints = true, double noisePointsPercent = 0.05
        ) : this(
            SKColor.Parse(paintColorHex), SKColor.Parse(backgroundColorHex), SKColor.Parse(noisePointColorHex),
            imageWidth, imageHeight,
            fontName, fontSize,
            enableDistortion, minDistortion, maxDistortion,
            enableNoisePoints, noisePointsPercent
        )
        {

        }

        public CaptchaGenerator(
            SKColor paintColor, SKColor backgroundColor, SKColor noisePointColor,
            int imageWidth = 120, int imageHeight = 48,
            string fontName = null, int fontSize = 20,
            bool enableDistortion = true, int minDistortion = 5, int maxDistortion = 15,
            bool enableNoisePoints = true, double noisePointsPercent = 0.05
        ) : this(paintColor, backgroundColor, noisePointColor, imageWidth, imageHeight, fontName, fontSize, null, null)
        {
            if (enableDistortion)
                DistortionFunc =
                    oldPos =>
                        {
                            var randomDistortion = minDistortion + (maxDistortion - minDistortion) * RandomGen.NextDouble();
                            if (RandomGen.NextDouble() > 0.5) randomDistortion *= -1;
                            var newX = (int)(oldPos.oldX + (randomDistortion * Math.Sin(Math.PI * oldPos.oldX / 64.0)));
                            var newY = (int)(oldPos.oldY + (randomDistortion * Math.Cos(Math.PI * oldPos.oldY / 64.0)));
                            if (newX < 0 || newX >= imageWidth) newX = 0;
                            if (newY < 0 || newY >= imageHeight) newY = 0;

                            return (newX, newY);
                        };

            if (enableNoisePoints)
                NoisePointMapGenFunc =
                    () =>
                        {
                            var noisePointCount = (int)(imageWidth * imageHeight * noisePointsPercent);
                            var noisePointPosList = Enumerable.Range(0, noisePointCount)
                                .Select(
                                    x =>
                                        (
                                            RandomGen.Next(imageWidth),
                                            RandomGen.Next(imageHeight)
                                        )
                                ).ToArray();
                            return noisePointPosList;
                        };
        }

        public CaptchaGenerator(
            SKColor paintColor, SKColor backgroundColor, SKColor noisePointColor,
            int imageWidth = 120, int imageHeight = 48,
            string fontName = null, int fontSize = 20,
            Func<(int oldX, int oldY), (int newX, int newY)> distortionFunc = null,
            Func<IEnumerable<(int x, int y)>> noisePointMapGenFunc = null
        )
        {
            PaintColor = paintColor;
            BackgroundColor = backgroundColor;
            NoisePointColor = noisePointColor;

            ImageWidth = imageWidth;
            ImageHeight = imageHeight;

            FontName = fontName;
            FontSize = fontSize;

            DistortionFunc = distortionFunc;
            NoisePointMapGenFunc = noisePointMapGenFunc;
        }

        public byte[] GenerateImageAsByteArray(
            string captchaCode,
            SKEncodedImageFormat imageFormat = SKEncodedImageFormat.Jpeg, int imageQuality = 80
        ) => BuildImage(captchaCode)
            .Encode(imageFormat, imageQuality)
            .ToArray();

        public Stream GenerateImageAsStream(
            string captchaCode,
            SKEncodedImageFormat imageFormat = SKEncodedImageFormat.Jpeg, int imageQuality = 80
        ) => BuildImage(captchaCode)
            .Encode(imageFormat, imageQuality)
            .AsStream();

        protected SKImage BuildImage(string captchaCode)
        {
            var imageInfo = new SKImageInfo(ImageWidth, ImageHeight, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using (var plainSkSurface = SKSurface.Create(imageInfo))
            {
                var plainCanvas = plainSkSurface.Canvas;
                plainCanvas.Clear(BackgroundColor);

                using (var paintInfo = new SKPaint())
                {
                    paintInfo.Typeface = SKTypeface.FromFamilyName(FontName, SKFontStyle.Italic);
                    paintInfo.TextSize = FontSize;
                    paintInfo.Color = PaintColor;
                    paintInfo.IsAntialias = true;

                    var xToDraw = (ImageWidth - paintInfo.MeasureText(captchaCode)) / 2;
                    var yToDraw = (ImageHeight - FontSize) / 2 + FontSize;
                    plainCanvas.DrawText(captchaCode, xToDraw, yToDraw, paintInfo);
                }
                plainCanvas.Flush();

                if (
                    null == DistortionFunc
                    && null == NoisePointMapGenFunc
                ) return plainSkSurface.Snapshot();

                using (var captchaSkSurface = SKSurface.Create(imageInfo))
                {
                    var captchaCanvas = captchaSkSurface.Canvas;

                    if (null != DistortionFunc)
                    {
                        var plainPixmap = plainSkSurface.PeekPixels();
                        for (int x = 0; x < ImageWidth; x++)
                        {
                            for (int y = 0; y < ImageHeight; y++)
                            {
                                var (newX, newY) = DistortionFunc((x, y));
                                var originalPixel = plainPixmap.GetPixelColor(newX, newY);

                                captchaCanvas.DrawPoint(x, y, originalPixel);
                            }
                        }
                    }

                    if (null != NoisePointMapGenFunc)
                    {
                        var noisePointMap = NoisePointMapGenFunc();
                        for (var i = 0; i < noisePointMap.Count(); i++)
                        {
                            var noisePointPos = noisePointMap.ElementAt(i);
                            captchaCanvas.DrawPoint(noisePointPos.x, noisePointPos.y, NoisePointColor);
                        }
                    }

                    captchaCanvas.Flush();

                    return captchaSkSurface.Snapshot();
                }
            }
        }
    }
}
