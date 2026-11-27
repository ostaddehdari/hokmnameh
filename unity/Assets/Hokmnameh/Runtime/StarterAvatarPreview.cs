using System;
using UnityEngine;

namespace Hokmnameh.Unity
{
    // Small original placeholder portraits. Final art can replace these via a reviewed asset release.
    internal static class StarterAvatarPreview
    {
        private static readonly Texture2D[] Cache = new Texture2D[8];
        private static readonly Color32[] Backdrops = {
            new Color32(32,45,81,255), new Color32(83,41,82,255),
            new Color32(36,78,83,255), new Color32(89,51,40,255),
            new Color32(56,56,99,255), new Color32(49,80,59,255),
            new Color32(91,63,40,255), new Color32(39,67,107,255)
        };
        private static readonly Color32[] Robes = {
            new Color32(225,174,64,255), new Color32(170,135,205,255),
            new Color32(102,202,192,255), new Color32(221,143,96,255),
            new Color32(134,158,226,255), new Color32(166,201,115,255),
            new Color32(219,189,110,255), new Color32(124,189,217,255)
        };

        public static Texture2D Get(int number)
        {
            var index = Math.Max(0,Math.Min(7,number-1));
            if (Cache[index] != null) return Cache[index];
            const int size = 96;
            var portrait = new Texture2D(size,size,TextureFormat.RGBA32,false)
            { name = $"Starter avatar {number:00}", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size*size];
            var skin = new Color32(231,179,125,255);
            var hair = new Color32(38+index*7,29+index*3,40+index*5,255);
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                var dx=x-47; var dy=y-48; var distance=dx*dx+dy*dy;
                var color = distance>45*45 ? new Color32(0,0,0,0) : Backdrops[index];
                if (distance is >= 42*42 and <= 45*45) color=Robes[index];
                if (y<35 && Math.Abs(x-47)<20) color=hair;
                if (y>=31 && y<70 && (dx*dx)/400f + ((y-48)*(y-48))/484f < 1) color=skin;
                if (y>=64 && y<87 && (dx*dx)/900f + ((y-91)*(y-91))/784f < 1) color=Robes[index];
                if (y>=44 && y<=47 && (x is >= 37 and <= 41 or >= 54 and <= 58)) color=hair;
                if (y==60 && x>=42 && x<=53) color=hair;
                pixels[y*size+x]=color;
            }
            portrait.SetPixels32(pixels);
            portrait.Apply(false,true);
            Cache[index]=portrait;
            return portrait;
        }
    }
}
