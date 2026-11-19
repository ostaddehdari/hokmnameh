using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hokmnameh.Core;
using UnityEngine;

namespace Hokmnameh.Unity
{
    public sealed class LanClient : IDisposable
    {
        private TcpClient _socket;
        private StreamWriter _writer;
        private readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
        private readonly object _writerLock = new object();
        public int Seat { get; private set; } = -1;
        public GameSnapshot Snapshot { get; private set; }
        public string Status { get; private set; } = "Not connected";
        public async Task ConnectAsync(string ip)
        {
            if (_socket != null) throw new InvalidOperationException("Already connected");
            var socket = new TcpClient();
            // The IP is typed by the player; local network access requires the Android INTERNET permission.
            await socket.ConnectAsync(ip, LanHost.Port);
            _socket = socket;
            _socket.NoDelay = true;
            var stream = socket.GetStream();
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var reader = new StreamReader(stream, Encoding.UTF8);
            Status = "Connected";
            Task.Run(() => ReadLoop(reader));
        }
        private void ReadLoop(StreamReader reader)
        {
            try
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null || line.Length > 8192) break;
                    // Unity JSON parsing is deliberately restricted to the main thread.
                    _inbox.Enqueue(line);
                }
            }
            catch (Exception) { }
            _inbox.Enqueue("{\"type\":\"error\",\"message\":\"Connection lost\"}");
        }
        public void Pump()
        {
            string line;
            while (_inbox.TryDequeue(out line))
            {
                var p = JsonUtility.FromJson<LanPacket>(line);
                if (p == null) continue;
                if (p.type == "welcome") Seat = p.seat;
                if (p.type == "state") Snapshot = JsonUtility.FromJson<GameSnapshot>(p.state);
                if (p.type == "error") Status = p.message;
            }
        }
        public void ChooseTrump(int suit) { Send(new LanPacket { type = "trump", suit = suit }); }
        public void Play(int card) { Send(new LanPacket { type = "play", card = card }); }
        private void Send(LanPacket packet)
        {
            lock (_writerLock)
            {
                if (_writer == null) return;
                try { _writer.WriteLine(JsonUtility.ToJson(packet)); _writer.Flush(); }
                catch (IOException) { Status = "Connection lost"; }
            }
        }
        public void Dispose() { _socket?.Close(); }
    }
}
