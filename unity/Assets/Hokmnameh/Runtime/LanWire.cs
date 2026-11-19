using System;

namespace Hokmnameh.Unity
{
    [Serializable]
    public sealed class LanPacket
    {
        public string type = "";
        public int seat = -1;
        public int card = -1;
        public int suit = -1;
        public string state = "";
        public string message = "";
    }
}
