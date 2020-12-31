typedef int2 ProbePos;
typedef uint2 TexPixelPos;
typedef float2 TexUvs;

TexPixelPos probePosToPixel(ProbePos p, int bandPixel)
{
    return p * int2(pixelsPerProbe + 2, 1) + int2(bandPixel + 1, 0);
}

TexUvs probeToPixelFloat(ProbePos probePos, float bandPixel)
{
    return probePos * float2(pixelsPerProbe + 2, 1) + float2(bandPixel + 1, 0);
}
