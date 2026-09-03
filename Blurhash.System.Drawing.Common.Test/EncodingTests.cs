using System.Drawing;
using System.Drawing.Blurhash;
using System.Runtime.Versioning;
using FluentAssertions;
using Xunit;

namespace Blurhash.System.Drawing.Common.Test;

[SupportedOSPlatform("Windows")]
public class EncodingTests
{
    private const string BlurHash = "|HFFaXYk^6#M9vF~W@j=#*@-5b,1J5PBV=R:s;w[@[or[k6oO[TLtJrqnO};Fxi^OZE3NgM}sps,jMFxS#OtcXnzRjxZxHj]OYNeWGJCs9xunhwIXBIpNaxHNGr;v}aeo0XmxZXS$et6#*$ft6nhxHnNV@w{nOaKwfNHo0";

    [SkippableFact]
    public void TestEncoding()
    {
        var sourceImage = Image.FromFile("TestData/input.jpg");
        var blurhash = Blurhasher.Encode(sourceImage, 9, 9);

        blurhash.Should().Be(BlurHash);
    }

    [SkippableFact]
    public void TestDecoding()
    {
        var targetImage = Blurhasher.Decode(BlurHash, 300, 200);
        targetImage.Width.Should().Be(300);
        targetImage.Height.Should().Be(200);
        targetImage.Save("output.png");
    }
}