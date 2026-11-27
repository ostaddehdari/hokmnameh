using System;
using UnityEngine;

namespace Hokmnameh.Unity
{
    internal sealed class AvatarBadge
    {
        public void Draw(Rect frame, int avatar)
        {
            GUI.Box(frame, GUIContent.none);
            GUI.DrawTexture(new Rect(frame.x+5,frame.y+5,frame.width-10,frame.height-10),
                StarterAvatarPreview.Get(avatar),ScaleMode.ScaleToFit,true);
        }
    }
    internal sealed class NameBadge
    {
        public void Draw(Rect frame, string value, bool rtl)
        {
            GUI.Box(frame, GUIContent.none);
            var style=new GUIStyle(GUI.skin.label)
            { alignment=rtl ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, fontSize=17,
              padding=new RectOffset(8,8,2,2), clipping=TextClipping.Clip };
            GUI.Label(frame,string.IsNullOrWhiteSpace(value) ? "Guest" : value,style);
        }
    }
    internal sealed class LevelBadge
    {
        public void Draw(Rect frame, int value)
        {
            GUI.Box(frame,GUIContent.none);
            var style=new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter,fontSize=16 };
            GUI.Label(frame,"Lv " + Math.Max(1,value),style);
        }
    }
    internal sealed class ReputationBadge
    {
        public void Draw(Rect frame, int value)
        {
            GUI.Box(frame,GUIContent.none);
            var style=new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter,fontSize=16 };
            GUI.Label(frame,"Rep " + Math.Max(0,value),style);
        }
    }

    // One reusable row for lobby and play. The four badges have independent data sources.
    internal sealed class PlayerHeader
    {
        private readonly AvatarBadge _avatar=new AvatarBadge();
        private readonly NameBadge _name=new NameBadge();
        private readonly LevelBadge _level=new LevelBadge();
        private readonly ReputationBadge _reputation=new ReputationBadge();

        public void Draw(string nickname,string avatarId,int level,int reputation,bool rtl)
        {
            var area=GUILayoutUtility.GetRect(0,68,GUILayout.ExpandWidth(true));
            if (area.width < 280) return;
            var layout=ProfileHudLayout.Create(area.width,rtl);
            Rect At(HudRect r) => new Rect(area.x+r.X,area.y+r.Y,r.Width,r.Height);
            var number=1;
            if (avatarId != null && avatarId.StartsWith("avatar-",StringComparison.Ordinal))
                int.TryParse(avatarId.Substring(7),out number);
            _avatar.Draw(At(layout.Avatar),number);
            _name.Draw(At(layout.Name),nickname,rtl);
            _level.Draw(At(layout.Level),level);
            _reputation.Draw(At(layout.Reputation),reputation);
        }
    }
}
