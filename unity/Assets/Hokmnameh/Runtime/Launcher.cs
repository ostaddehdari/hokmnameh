using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Hokmnameh.Core;
using UnityEngine;

namespace Hokmnameh.Unity
{
    // Functional, intentionally unstyled gameplay prototype. Replace OnGUI with designed UGUI UI.
    public sealed class Launcher : MonoBehaviour
    {
        private enum Mode { Menu, Practice, Host, Client, Online }
        private Mode _mode;
        private HokmGame _practice;
        private LanHost _host;
        private LanClient _client;
        private OnlineClient _online;
        private string _onlineUrl = "https://srun.ir/hokmnameh";
        private string _inviteCode = "";
        private string _hostIp = "192.168.43.1";
        private string _error = "";
        private string _nickname = "";
        private int _avatarChoice = 1;
        private long _seenProfileVersion;
        private long _seenNicknameVersion;
        private GUIStyle _title;
        private GUIStyle _body;
        private readonly PlayerHeader _playerHeader = new PlayerHeader();
        private Vector2 _scroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<Launcher>() != null) return;
            var go = new GameObject("Hokmnameh Prototype Launcher");
            DontDestroyOnLoad(go);
            go.AddComponent<Launcher>();
        }
        private void Update()
        {
            _host?.Pump();
            _client?.Pump();
            if (_practice == null) return;
            for (int step = 0; step < 30; ++step)
            {
                if (_practice.Phase == GamePhase.Finished) break;
                if (_practice.Phase == GamePhase.Waiting) { _practice.StartRound(_practice.Hakem); continue; }
                int seat = _practice.Phase == GamePhase.ChoosingTrump ? _practice.Hakem : _practice.Turn;
                if (seat == 0) break;
                if (_practice.Phase == GamePhase.ChoosingTrump) _practice.ChooseTrump(seat, HokmBot.PickTrump(_practice, seat));
                else _practice.Play(seat, HokmBot.ChooseCard(_practice, seat));
            }
        }
        private void Awake() { Screen.sleepTimeout = SleepTimeout.NeverSleep; }
        private void OnDestroy() { _host?.Dispose(); _client?.Dispose(); Screen.sleepTimeout = SleepTimeout.SystemSetting; }
        private void OnGUI()
        {
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _body = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            }
            var safe = Screen.safeArea;
            var frame = SafeAreaLayout.Create(Screen.width, Screen.height,
                safe.x, safe.y, safe.width, safe.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(frame.Scale, frame.Scale, 1));
            GUILayout.BeginArea(new Rect(frame.X, frame.Y, frame.Width, frame.Height));
            GUILayout.Label("HOKMNAMEH", _title);
            GUILayout.Label("Playable engineering prototype | v0.1", _body);
            if (_mode == Mode.Menu) DrawMenu();
            else
            {
                if (_mode == Mode.Online)
                    _playerHeader.Draw(_online?.Profile?.nickname, _online?.Profile?.avatarId,
                        _online?.Progress?.level ?? 1, _online?.Reputation?.score ?? 0, true);
                else if (_mode == Mode.Practice)
                    _playerHeader.Draw("Guest", "avatar-01", 1, 0, true);
                if (GUILayout.Button("← Back to menu", GUILayout.Height(46))) ResetAll();
                if (_mode == Mode.Client) GUILayout.Label(_client?.Status + " | Seat " + _client?.Seat, _body);
                if (_mode == Mode.Online)
                {
                    DrawOnlineControls();
                }
                if (_mode == Mode.Host) GUILayout.Label(_host?.Status + " | Host IP: " + LocalIp(), _body);
                var snapshot = _mode == Mode.Practice ? _practice?.Snapshot(0) :
                    _mode == Mode.Host ? (_host?.Game.Round > 0 ? _host.Game.Snapshot(0) : null) :
                    _mode == Mode.Client ? _client?.Snapshot : _online?.State?.game;
                DrawGame(snapshot);
            }
            if (!string.IsNullOrEmpty(_error)) GUILayout.Label(_error, _body);
            GUILayout.EndArea();
        }
        private void DrawMenu()
        {
            GUILayout.Space(40);
            if (GUILayout.Button("Play vs 3 bots (offline)", GUILayout.Height(70)))
            {
                _practice = new HokmGame(); _practice.StartRound(0); _mode = Mode.Practice;
            }
            if (GUILayout.Button("Host local game (1 + 3 phones)", GUILayout.Height(70)))
            {
                try { _host = new LanHost(); _host.Start(); _mode = Mode.Host; }
                catch (Exception e) { _error = e.Message; }
            }
            if (GUILayout.Button("Online server (beta HTTP)", GUILayout.Height(70)))
            {
                _online = new OnlineClient(this, _onlineUrl);
                _mode = Mode.Online;
                _online.CreateGuest();
            }
            GUILayout.Label("Enter host phone's local IPv4 address", _body);
            _hostIp = GUILayout.TextField(_hostIp, GUILayout.Height(48));
            if (GUILayout.Button("Join local game", GUILayout.Height(70)))
            {
                try
                {
                    _client = new LanClient(); _mode = Mode.Client;
                    var task = _client.ConnectAsync(_hostIp);
                    task.ContinueWith(t => { if (t.IsFaulted) Debug.LogError(t.Exception); });
                }
                catch (Exception e) { _error = e.Message; }
            }
            GUILayout.Label("For hotspot: join host Wi-Fi, then enter host gateway IP.", _body);
        }
        private void DrawOnlineControls()
        {
            GUILayout.Label(_online?.Status ?? "Offline", _body);
            var profile = _online?.Profile;
            if (profile != null)
            {
                if (_seenProfileVersion != profile.version || _seenNicknameVersion != profile.nicknameVersion)
                {
                    _nickname = profile.nickname;
                    int.TryParse(profile.avatarId.Substring(7), out _avatarChoice);
                    _avatarChoice = Math.Max(1, Math.Min(8, _avatarChoice));
                    _seenProfileVersion = profile.version;
                    _seenNicknameVersion = profile.nicknameVersion;
                }
                GUILayout.Label("Nickname and starter avatar", _body);
                _nickname = GUILayout.TextField(_nickname, GUILayout.Height(44));
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(StarterAvatarPreview.Get(_avatarChoice), GUILayout.Width(112), GUILayout.Height(112));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(60), GUILayout.Height(44))) _avatarChoice = _avatarChoice == 1 ? 8 : _avatarChoice - 1;
                GUILayout.Label("Avatar " + _avatarChoice.ToString("00"), _body);
                if (GUILayout.Button("▶", GUILayout.Width(60), GUILayout.Height(44))) _avatarChoice = _avatarChoice == 8 ? 1 : _avatarChoice + 1;
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Save profile", GUILayout.Height(44)))
                    _online.SaveProfile(_nickname, "avatar-" + _avatarChoice.ToString("00"));
            }
            if (!string.IsNullOrEmpty(_online?.InviteCode))
                GUILayout.Label("Invite room code: " + _online.InviteCode, _body);
            if (profile == null)
            {
                GUILayout.Label("Preparing your guest profile...", _body);
                if (GUILayout.Button("Retry connection", GUILayout.Height(44))) _online.CreateGuest();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create free room", GUILayout.Height(44))) _online.CreateRoom(false);
            GUI.enabled = profile != null;
            if (GUILayout.Button("Start bot practice", GUILayout.Height(44)))
                _online.StartPractice(_nickname, "avatar-" + _avatarChoice.ToString("00"));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            _inviteCode = GUILayout.TextField(_inviteCode, GUILayout.Height(44));
            if (GUILayout.Button("Join room by code", GUILayout.Height(44))) _online.Join(_inviteCode);
        }
        private void DrawGame(GameSnapshot s)
        {
            if (s == null) { GUILayout.Label("Waiting for all four players...", _body); return; }
            GUILayout.Label("Round " + s.round + " | Scores " + s.matchPoints[0] + " – " + s.matchPoints[1], _body);
            GUILayout.Label("Hakem seat: " + s.hakem + " | Trump: " + (s.trump < 0 ? "?" : ((Suit)s.trump).ToString()), _body);
            GUILayout.Label("Your seat: " + s.yourSeat + " | Turn seat: " + s.turn, _body);
            GUILayout.Label("Tricks: " + s.tricksWon[0] + " – " + s.tricksWon[1], _body);
            if (s.phase == (int)GamePhase.Finished)
            { GUILayout.Label("Match ended. Team " + s.winnerTeam + " won", _title); return; }
            GUILayout.Label("Cards in other hands: " + string.Join(" / ", s.handCounts), _body);
            GUILayout.Label("Trick: " + TrickString(s.trick), _body);
            if (s.phase == (int)GamePhase.ChoosingTrump && s.yourSeat == s.hakem)
            {
                GUILayout.Label("Choose hokm:", _body);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 4; ++i)
                {
                    int suit = i;
                    if (GUILayout.Button(((Suit)i).ToString(), GUILayout.Height(64))) DoTrump(suit);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Your cards (tap to play on your turn):", _body);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(270));
            foreach (int card in s.hand)
            {
                int selected = card;
                var label = ((Suit)HokmGame.SuitOf(card)).ToString() + " " + HokmGame.RankOf(card);
                bool yourTurn = s.phase == (int)GamePhase.Playing && s.turn == s.yourSeat;
                GUI.enabled = yourTurn;
                if (GUILayout.Button(label, GUILayout.Height(44))) DoPlay(selected);
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            if (s.lastTrick.Length > 0) GUILayout.Label("Last trick: " + TrickString(s.lastTrick), _body);
        }
        private static string TrickString(CardPlayed[] trick)
        {
            var text = "";
            foreach (var p in trick) text += "[" + p.seat + ":" + ((Suit)HokmGame.SuitOf(p.card)) + HokmGame.RankOf(p.card) + "] ";
            return text;
        }
        private void DoTrump(int suit)
        {
            try
            {
                if (_mode == Mode.Practice) _practice.ChooseTrump(0, suit);
                if (_mode == Mode.Host) _host.LocalChooseTrump(suit);
                if (_mode == Mode.Client) _client.ChooseTrump(suit);
                if (_mode == Mode.Online) _online.ChooseTrump(suit);
            }
            catch (GameRuleException e) { _error = e.Message; }
        }
        private void DoPlay(int card)
        {
            try
            {
                if (_mode == Mode.Practice) _practice.Play(0, card);
                if (_mode == Mode.Host) _host.LocalPlay(card);
                if (_mode == Mode.Client) _client.Play(card);
                if (_mode == Mode.Online) _online.Play(card);
            }
            catch (GameRuleException e) { _error = e.Message; }
        }
        private void ResetAll()
        {
            _host?.Dispose(); _host = null;
            _client?.Dispose(); _client = null;
            _online?.Stop(); _online = null;
            _practice = null; _error = ""; _mode = Mode.Menu;
            _seenProfileVersion = 0;
            _seenNicknameVersion = 0;
        }
        private static string LocalIp()
        {
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                foreach (var address in network.GetIPProperties().UnicastAddresses)
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(address.Address) && !address.Address.ToString().StartsWith("169.254"))
                        return address.Address.ToString();
            return "Check Android hotspot gateway";
        }
    }
}
