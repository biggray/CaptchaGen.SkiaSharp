using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace CaptchaGen.SkiaSharp
{
    public interface ICaptchaGenerator
    {
        byte[] GenerateImageAsByteArray(string captchaCode, SKEncodedImageFormat imageFormat = SKEncodedImageFormat.Jpeg, int imageQuality = 80);
        Stream GenerateImageAsStream(string captchaCode, SKEncodedImageFormat imageFormat = SKEncodedImageFormat.Jpeg, int imageQuality = 80);
    }
}
