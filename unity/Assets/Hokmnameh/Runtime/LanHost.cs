using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hokmnameh.Core;
using UnityEngine;

namespace Hokmnameh.Unity
{
    // Prototype Android hotspot/LAN match authority. Does NOT require internet.
    // Realtime socket IO is off the main thread; rules changes happen on Unity's main thread.
    // This unencrypted LAN transport is for trusted local sessions only, not public use.
    public sealed class LanHost : IDisposable
    {
        public const int Port = 37991;
        private sealed class Peer
        {
            public TcpClient Client;
            public StreamWriter Writer;
            public int Seat;
            public readonly object WriterGate = new object();
            public void Send(LanPacket packet)
            {
                lock (WriterGate) { Writer.WriteLine(JsonUtility.ToJson(packet)); Writer.Flush(); }
            }
        }
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly Dictionary<int, Peer> _peers = new Dictionary<int, Peer>();
        private readonly HashSet<int> _lostSeats = new HashSet<int>();
        private TcpListener _listener;
        private DateTime _deadlineUtc = DateTime.UtcNow;
        public HokmGame Game { get; } = new HokmGame();
        public int ConnectedPlayers { get { return _peers.Count + 1; } }
        public string Status { get; private set; } = "Waiting for players";

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start(3);
            Task.Run(AcceptLoop);
        }

        private async Task AcceptLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    TcpClient c = await _listener.AcceptTcpClientAsync();
                    _mainThread.Enqueue(() => Register(c));
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }
            }
        }
        private void Register(TcpClient c)
        {
            if (_peers.Count >= 3 || Game.Round > 0) { c.Close(); return; }
            int seat = 1;
            while (_peers.ContainsKey(seat) && seat <= 3) seat++;
            c.NoDelay = true;
            var stream = c.GetStream();
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            var reader = new StreamReader(stream, Encoding.UTF8);
            var peer = new Peer { Client = c, Writer = writer, Seat = seat };
            _peers[seat] = peer;
            _lostSeats.Remove(seat);
            peer.Send(new LanPacket { type = "welcome", seat = seat, message = "LAN seat assigned" });
            Task.Run(() => ReadLoop(peer, reader));
            Status = "Players: " + ConnectedPlayers + "/4";
            if (_peers.Count == 3) { Game.StartRound(0); _deadlineUtc = DateTime.UtcNow.AddSeconds(30); Publish(); }
        }
        private void ReadLoop(Peer peer, StreamReader reader)
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    if (line.Length > 8192) break;
                    _mainThread.Enqueue(() => Receive(peer, line));
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            _mainThread.Enqueue(() => Disconnect(peer));
        }
        private void Disconnect(Peer peer)
        {
            Peer existing;
            if (_peers.TryGetValue(peer.Seat, out existing) && ReferenceEquals(existing, peer))
            {
                _peers.Remove(peer.Seat);
                _lostSeats.Add(peer.Seat);
                Status = "Seat " + peer.Seat + " left; bot takes over";
                peer.Client.Close();
            }
        }
        private void Receive(Peer peer, string json)
        {
            if (Game.Phase == GamePhase.Finished || Game.Round == 0) return;
            try
            {
                LanPacket packet = JsonUtility.FromJson<LanPacket>(json);
                if (packet == null) return;
                if (packet.type == "trump") Game.ChooseTrump(peer.Seat, packet.suit);
                else if (packet.type == "play") Game.Play(peer.Seat, packet.card);
                else return;
                _deadlineUtc = DateTime.UtcNow.AddSeconds(30);
                Publish();
            }
            catch (GameRuleException ex)
            {
                try { peer.Send(new LanPacket { type = "error", message = ex.Message }); } catch (IOException) { }
            }
        }
        public void LocalChooseTrump(int suit)
        {
            Game.ChooseTrump(0, suit); _deadlineUtc = DateTime.UtcNow.AddSeconds(30); Publish();
        }
        public void LocalPlay(int card)
        {
            Game.Play(0, card); _deadlineUtc = DateTime.UtcNow.AddSeconds(30); Publish();
        }
        public void Pump()
        {
            Action action;
            while (_mainThread.TryDequeue(out action)) action();
            if (Game.Round == 0 || Game.Phase == GamePhase.Finished) return;
            if (Game.Phase == GamePhase.Waiting)
            {
                Game.StartRound(Game.Hakem); _deadlineUtc = DateTime.UtcNow.AddSeconds(30); Publish(); return;
            }
            int seat = Game.Phase == GamePhase.ChoosingTrump ? Game.Hakem : Game.Turn;
            if (_lostSeats.Contains(seat) || DateTime.UtcNow >= _deadlineUtc)
            {
                if (Game.Phase == GamePhase.ChoosingTrump) Game.ChooseTrump(seat, HokmBot.PickTrump(Game, seat));
                else Game.Play(seat, HokmBot.ChooseCard(Game, seat));
                _deadlineUtc = DateTime.UtcNow.AddSeconds(30); Publish();
            }
        }
        private void Publish()
        {
            foreach (Peer peer in _peers.Values)
            {
                try { peer.Send(new LanPacket { type = "state", state = JsonUtility.ToJson(Game.Snapshot(peer.Seat)) }); }
                catch (IOException) { /* peer removed by reader on disconnect */ }
            }
        }
        public void Dispose()
        {
            _stop.Cancel();
            try { _listener?.Stop(); } catch (SocketException) { }
            foreach (Peer peer in _peers.Values) peer.Client.Close();
            _peers.Clear();
            _stop.Dispose();
        }
    }
}
