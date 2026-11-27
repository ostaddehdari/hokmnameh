using System;

namespace Hokmnameh.Unity
{
    // Engine-independent layout. Visual RTL changes order, while text shaping belongs to the UI renderer.
    public readonly struct HudRect
    {
        public readonly float X, Y, Width, Height;
        public HudRect(float x, float y, float width, float height)
        { X=x; Y=y; Width=width; Height=height; }
    }

    public readonly struct ProfileHudLayout
    {
        public readonly HudRect Avatar, Name, Level, Reputation;
        private ProfileHudLayout(HudRect avatar,HudRect name,HudRect level,HudRect reputation)
        { Avatar=avatar; Name=name; Level=level; Reputation=reputation; }

        public static ProfileHudLayout Create(float width, bool rtl)
        {
            if (width < 280 || float.IsNaN(width) || float.IsInfinity(width))
                throw new ArgumentOutOfRangeException(nameof(width));
            const float gap=6, height=68, avatarWidth=68, levelWidth=64, reputationWidth=72;
            var nameWidth=width-avatarWidth-levelWidth-reputationWidth-3*gap;
            var widths=new[] { avatarWidth,nameWidth,levelWidth,reputationWidth };
            var rects=new HudRect[4];
            var cursor=rtl ? width : 0f;
            for (int i=0;i<widths.Length;i++)
            {
                if (rtl) { cursor-=widths[i]; rects[i]=new HudRect(cursor,0,widths[i],height); cursor-=gap; }
                else { rects[i]=new HudRect(cursor,0,widths[i],height); cursor+=widths[i]+gap; }
            }
            return new ProfileHudLayout(rects[0],rects[1],rects[2],rects[3]);
        }
    }
}
