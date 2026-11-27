using System;

namespace Hokmnameh.Unity
{
    public readonly struct SafeAreaFrame
    {
        public readonly float X, Y, Width, Height, Scale;
        public SafeAreaFrame(float x,float y,float width,float height,float scale)
        { X=x; Y=y; Width=width; Height=height; Scale=scale; }
    }

    // Screen.safeArea is bottom-left based; IMGUI's origin is top-left.
    public static class SafeAreaLayout
    {
        public static SafeAreaFrame Create(float screenWidth,float screenHeight,
            float safeX,float safeY,float safeWidth,float safeHeight)
        {
            if (screenWidth<=0 || screenHeight<=0 || safeX<0 || safeY<0 ||
                safeWidth<280 || safeHeight<300 || safeX+safeWidth>screenWidth ||
                safeY+safeHeight>screenHeight || float.IsNaN(safeWidth) || float.IsNaN(safeHeight))
                throw new ArgumentOutOfRangeException(nameof(safeWidth));
            var scale=Math.Min(safeWidth/540f,safeHeight/950f);
            return new SafeAreaFrame(safeX/scale+18,(screenHeight-safeY-safeHeight)/scale+20,
                safeWidth/scale-36,safeHeight/scale-40,scale);
        }
    }
}
