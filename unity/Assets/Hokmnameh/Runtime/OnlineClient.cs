using System;
using System.Collections;
using System.Text;
using Hokmnameh.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Hokmnameh.Unity
{
    [Serializable] public sealed class OnlineGuest { public string accountId; public string nickname; public string accessToken; }
    [Serializable] public sealed class OnlineRoom { public string id; public string code; public string mode; public int playerCount; public int phase; public bool started; }
    [Serializable] public sealed class OnlineJoin { public OnlineRoom room; public int seat; }
    [Serializable] public sealed class OnlineState { public OnlineRoom room; public GameSnapshot game; public bool isYourTurn; }
    [Serializable] public sealed class OnlineProfile { public string accountId; public string nickname; public string avatarId; public long version; }
    [Serializable] public sealed class ProfileUpdate { public string nickname; public string avatarId; }

    // HTTP-only polling fallback for the first four-device server integration test.
    // Production client should subscribe to /ws/rooms/{roomId} and use HTTP for commands.
    public sealed class OnlineClient
    {
        private readonly MonoBehaviour _runner;
        private readonly string _baseUrl;
        private string _token = "";
        private string _roomId = "";
        private Coroutine _poller;
        private bool _creatingGuest;
        public string Status { get; private set; } = "Not signed in";
        public OnlineProfile Profile { get; private set; }
        public OnlineState State { get; private set; }
        public string InviteCode { get; private set; } = "";
        public OnlineClient(MonoBehaviour runner, string baseUrl)
        {
            _runner = runner; _baseUrl = baseUrl.TrimEnd('/');
        }
        public void CreateGuest(Action onReady = null)
        {
            if (!string.IsNullOrEmpty(_token)) { onReady?.Invoke(); return; }
            if (_creatingGuest) return;
            _creatingGuest = true;
            _runner.StartCoroutine(Request("POST", "/api/v1/identity/guest", null, data =>
            {
                _creatingGuest = false;
                var guest = JsonUtility.FromJson<OnlineGuest>(data);
                _token = guest.accessToken;
                Status = "Signed in as " + guest.nickname;
                RefreshProfile();
                onReady?.Invoke();
            }, null, () => _creatingGuest = false));
        }
        public void RefreshProfile()
        {
            if (!string.IsNullOrEmpty(_token)) _runner.StartCoroutine(Request("GET", "/api/v1/profile", null,
                data => Profile = JsonUtility.FromJson<OnlineProfile>(data)));
        }
        public void SaveProfile(string nickname, string avatarId)
        {
            if (Profile == null) return;
            var update = new ProfileUpdate { nickname = nickname, avatarId = avatarId };
            _runner.StartCoroutine(Request("PUT", "/api/v1/profile", JsonUtility.ToJson(update),
                data => { Profile = JsonUtility.FromJson<OnlineProfile>(data); Status = "Profile saved"; },
                "\"" + Profile.version + "\""));
        }
        public void CreateRoom(bool practice)
        {
            if (string.IsNullOrEmpty(_token)) { CreateGuest(() => CreateRoom(practice)); return; }
            _runner.StartCoroutine(Request("POST", "/api/v1/rooms",
            "{\"mode\":\"" + (practice ? "practice" : "free") + "\"}", data => SetRoom(JsonUtility.FromJson<OnlineJoin>(data))));
        }
        public void Join(string code)
        {
            if (string.IsNullOrEmpty(_token)) { CreateGuest(() => Join(code)); return; }
            _runner.StartCoroutine(Request("POST", "/api/v1/rooms/join/" + Uri.EscapeDataString(code.ToUpperInvariant()),
                null, data => SetRoom(JsonUtility.FromJson<OnlineJoin>(data))));
        }
        public void ChooseTrump(int suit) => Command("/trump", "{\"suit\":" + suit + "}");
        public void Play(int card) => Command("/play", "{\"card\":" + card + "}");
        private void Command(string suffix, string body)
        {
            if (string.IsNullOrEmpty(_roomId)) return;
            _runner.StartCoroutine(Request("POST", "/api/v1/rooms/" + _roomId + suffix, body, _ => Refresh()));
        }
        private void SetRoom(OnlineJoin joined)
        {
            _roomId = joined.room.id;
            InviteCode = joined.room.code;
            Status = "Room " + InviteCode + " | seat " + joined.seat;
            Refresh();
            if (_poller != null) _runner.StopCoroutine(_poller);
            _poller = _runner.StartCoroutine(Poll());
        }
        private IEnumerator Poll()
        {
            while (!string.IsNullOrEmpty(_roomId))
            {
                yield return new WaitForSecondsRealtime(0.75f);
                Refresh();
            }
        }
        public void Refresh()
        {
            if (!string.IsNullOrEmpty(_roomId))
                _runner.StartCoroutine(Request("GET", "/api/v1/rooms/" + _roomId, null, data => State = JsonUtility.FromJson<OnlineState>(data)));
        }
        private IEnumerator Request(string method, string path, string json, Action<string> onSuccess,
            string ifMatch = null, Action onFailure = null)
        {
            using (var request = new UnityWebRequest(_baseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (json != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(_token)) request.SetRequestHeader("Authorization", "Bearer " + _token);
                if (!string.IsNullOrEmpty(ifMatch)) request.SetRequestHeader("If-Match", ifMatch);
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Status = "HTTP " + request.responseCode + " " + request.error;
                    onFailure?.Invoke();
                }
                else
                {
                    try { onSuccess?.Invoke(request.downloadHandler.text); }
                    catch (Exception e) { Status = "Invalid server response: " + e.Message; }
                }
            }
        }
        public void Stop()
        {
            _roomId = "";
            if (_poller != null) _runner.StopCoroutine(_poller);
            _poller = null;
        }
    }
}
