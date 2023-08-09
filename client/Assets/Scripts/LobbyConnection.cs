using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.Networking;

public class LobbyConnection : MonoBehaviour
{
    [Tooltip("IP to connect to. If empty, localhost will be used")]
    public string server_name = "LocalHost";
    public string server_ip = "localhost";
    public List<string> lobbiesList;
    public List<string> gamesList;
    public static LobbyConnection Instance;
    public string GameSession;
    public string LobbySession;
    public ulong playerId;
    public bool isHost = false;
    public ulong hostId;
    public int playerCount;
    public Dictionary<ulong, string> playersIdName = new Dictionary<ulong, string>();
    public uint serverTickRate_ms;
    public string serverHash;
    public ServerGameSettings serverSettings;

    public bool gameStarted = false;
    public bool errorConnection = false;
    public bool errorOngoingGame = false;
    public string clientId;
    public bool reconnect = false;
    public bool reconnectPossible = false;
    public bool reconnectToCharacterSelection = false;
    public int reconnectPlayerCount;
    public string reconnectServerHash;
    public string reconnectGameId;
    public ulong reconnectPlayerId;
    public Dictionary<ulong, string> reconnectPlayers;
    public ServerGameSettings reconnectServerSettings;

    WebSocket ws;

    [Serializable]
    public class Session
    {
        public string lobby_id;
    }

    [Serializable]
    public class LobbiesResponse
    {
        public List<string> lobbies;
    }

    [Serializable]
    public class GamesResponse
    {
        public List<string> current_games;
    }

    [Serializable]
    public class CurrentGameResponse
    {
        public bool ongoing_game;
        public bool on_character_selection;
        public int player_count;
        public string server_hash;
        public string current_game_id;
        public ulong current_game_player_id;
        public List<Player> players;
        public Configs game_config;

        [Serializable]
        public class Player
        {
            public ulong id;
            public string character_name;
        }

        [Serializable]
        public class Configs
        {
            public string runner_config;
            public string character_config;
            public string skills_config;
        }
    }

    class AcceptAllCertificates : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }

    private void Awake()
    {
        this.Init();
        LoadClientId();
        MaybeReconnect();
        PopulateLists();
    }

    public void Init()
    {
        if (Instance != null)
        {
            if (this.ws != null)
            {
                this.ws.Close();
            }

            Destroy(gameObject);
            return;
        }
        Instance = this;
        this.server_ip = SelectServerIP.GetServerIp();
        this.server_name = SelectServerIP.GetServerName();
        this.playerId = UInt64.MaxValue;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null)
        {
            ws.DispatchMessageQueue();
        }
#endif
    }

    private void PopulateLists()
    {
        this.lobbiesList = new List<string>();
        this.gamesList = new List<string>();
        StartCoroutine(GetLobbies());
        StartCoroutine(GetGames());
    }

    private void LoadClientId()
    {
        if (!PlayerPrefs.HasKey("client_id"))
        {
            Guid g = Guid.NewGuid();
            PlayerPrefs.SetString("client_id", g.ToString());
        }

        this.clientId = PlayerPrefs.GetString("client_id");
    }

    private void MaybeReconnect()
    {
        StartCoroutine(GetCurrentGame());
    }

    public void CreateLobby()
    {
        StartCoroutine(GetRequest(makeUrl("/new_lobby")));
    }

    public void ConnectToLobby(string matchmaking_id)
    {
        ConnectToSession(matchmaking_id);
        LobbySession = matchmaking_id;
    }

    public void Refresh()
    {
        this.server_ip = SelectServerIP.GetServerIp();
        this.server_name = SelectServerIP.GetServerName();
        PopulateLists();
        MaybeReconnect();
    }

    public void QuickGame()
    {
        StartCoroutine(GetRequest(makeUrl("/new_lobby")));
        StartCoroutine(WaitLobbyCreated());
    }

    public IEnumerator StartGame()
    {
        yield return GameSettings.ParseSettingsCoroutine(settings =>
        {
            serverSettings = settings;
        });
        LobbyEvent lobbyEvent = new LobbyEvent
        {
            Type = LobbyEventType.StartGame,
            GameConfig = serverSettings
        };

        serverTickRate_ms = (uint)serverSettings.RunnerConfig.ServerTickrateMs;

        using (var stream = new MemoryStream())
        {
            lobbyEvent.WriteTo(stream);
            var msg = stream.ToArray();
            ws.Send(msg);
        }
    }

    public void Reconnect()
    {
        this.reconnect = true;
        this.GameSession = this.reconnectGameId;
        this.playerId = this.reconnectPlayerId;
        this.serverSettings = this.reconnectServerSettings;
        this.serverTickRate_ms = (uint)this.serverSettings.RunnerConfig.ServerTickrateMs;
        this.serverHash = this.reconnectServerHash;
        this.playerCount = this.reconnectPlayerCount;
        this.gameStarted = true;
    }

    private IEnumerator WaitLobbyCreated()
    {
        yield return new WaitUntil(() => !string.IsNullOrEmpty(LobbySession));
        yield return StartGame();
    }

    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            webRequest.certificateHandler = new AcceptAllCertificates();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                case UnityWebRequest.Result.ProtocolError:
                    break;
                case UnityWebRequest.Result.Success:
                    Session session = JsonUtility.FromJson<Session>(
                        webRequest.downloadHandler.text
                    );
                    Debug.Log("Creating and joining lobby ID: " + session.lobby_id);
                    ConnectToSession(session.lobby_id);
                    break;
            }
        }
    }

    IEnumerator GetLobbies()
    {
        string url = makeUrl("/current_lobbies");
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.certificateHandler = new AcceptAllCertificates();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                case UnityWebRequest.Result.ProtocolError:
                    break;
                case UnityWebRequest.Result.Success:
                    var response = JsonUtility.FromJson<LobbiesResponse>(
                        webRequest.downloadHandler.text
                    );
                    lobbiesList = response.lobbies;
                    break;
            }
        }
    }

    IEnumerator GetGames()
    {
        string url = makeUrl("/current_games");
        Debug.Log(url);
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.certificateHandler = new AcceptAllCertificates();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.Success:
                    GamesResponse response = JsonUtility.FromJson<GamesResponse>(
                        webRequest.downloadHandler.text
                    );
                    gamesList = response.current_games;
                    break;
                default:
                    break;
            }
        }
    }

    IEnumerator GetCurrentGame()
    {
        string url = makeUrl("/player_game/" + this.clientId);
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.certificateHandler = new AcceptAllCertificates();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.Success:
                    CurrentGameResponse response = JsonUtility.FromJson<CurrentGameResponse>(
                        webRequest.downloadHandler.text
                    );

                    if (response.ongoing_game)
                    {
                        this.reconnectPossible = true;
                        this.reconnectToCharacterSelection = response.on_character_selection;
                        this.reconnectPlayerCount = response.player_count;
                        this.reconnectGameId = response.current_game_id;
                        this.reconnectPlayerId = response.current_game_player_id;
                        this.reconnectPlayerCount = response.player_count;
                        this.reconnectServerHash = response.server_hash;

                        this.reconnectPlayers = new Dictionary<ulong, string>();
                        response.players.ForEach(
                            player => this.reconnectPlayers.Add(player.id, player.character_name)
                        );

                        this.reconnectServerSettings = parseReconnectServerSettings(
                            response.game_config
                        );
                        this.errorOngoingGame = true;
                    }
                    break;
                default:
                    break;
            }
        }
    }

    private void ConnectToSession(string session_id)
    {
        string url = makeWebsocketUrl("/matchmaking/" + session_id);
        ws = new WebSocket(url);
        ws.OnMessage += OnWebSocketMessage;
        ws.OnError += (e) =>
        {
            Debug.Log("Error received: " + e);
        };
        ws.OnOpen += () =>
        {
            LobbySession = session_id;
        };
        ws.Connect();
    }

    private void OnWebSocketMessage(byte[] data)
    {
        try
        {
            LobbyEvent lobby_event = LobbyEvent.Parser.ParseFrom(data);
            switch (lobby_event.Type)
            {
                case LobbyEventType.Connected:
                    Debug.Log(
                        "Connected to lobby "
                            + lobby_event.LobbyId
                            + " as player_id "
                            + lobby_event.PlayerInfo.PlayerId
                    );
                    this.playerId = lobby_event.PlayerInfo.PlayerId;
                    break;

                case LobbyEventType.PlayerAdded:
                    this.hostId = lobby_event.HostPlayerId;
                    this.isHost = this.playerId == this.hostId;
                    this.playerCount = lobby_event.PlayersInfo.Count();
                    lobby_event.PlayersInfo
                        .ToList()
                        .ForEach(
                            playerInfo =>
                                this.playersIdName[playerInfo.PlayerId] = playerInfo.PlayerName
                        );
                    break;

                case LobbyEventType.PlayerRemoved:
                    this.playerCount = lobby_event.PlayersInfo.Count();
                    this.hostId = lobby_event.HostPlayerId;
                    this.isHost = this.playerId == this.hostId;
                    this.playersIdName.Remove(lobby_event.RemovedPlayerInfo.PlayerId);
                    break;

                case LobbyEventType.GameStarted:
                    GameSession = lobby_event.GameId;
                    serverSettings = lobby_event.GameConfig;
                    serverTickRate_ms = (uint)serverSettings.RunnerConfig.ServerTickrateMs;
                    serverHash = lobby_event.ServerHash;
                    gameStarted = true;
                    break;

                default:
                    Debug.Log("Message received is: " + lobby_event.Type);
                    break;
            }
            ;
        }
        catch (Exception e)
        {
            Debug.Log("InvalidProtocolBufferException: " + e);
        }
    }

    private string makeUrl(string path)
    {
        if (server_ip.Contains("localhost"))
        {
            return "http://" + server_ip + ":4000" + path;
        }
        else if (server_ip.Contains("10.150.20.186"))
        {
            return "http://" + server_ip + ":4000" + path;
        }
        else
        {
            return "https://" + server_ip + path;
        }
    }

    private string makeWebsocketUrl(string path)
    {
        if (server_ip.Contains("localhost"))
        {
            return "ws://" + server_ip + ":4000" + path;
        }
        else if (server_ip.Contains("10.150.20.186"))
        {
            return "ws://" + server_ip + ":4000" + path;
        }
        else
        {
            return "wss://" + server_ip + path;
        }
    }

    public bool isConnectionOpen()
    {
        return ws.State == NativeWebSocket.WebSocketState.Open;
    }

    private ServerGameSettings parseReconnectServerSettings(CurrentGameResponse.Configs configs)
    {
        JsonParser parser = new JsonParser(new JsonParser.Settings(100000)); //GameSettings

        RunnerConfig parsedRunner = parser.Parse<RunnerConfig>(
            configs.runner_config.TrimStart('\uFEFF')
        );
        CharacterConfig characters = parser.Parse<CharacterConfig>(
            configs.character_config.TrimStart('\uFEFF')
        );
        SkillsConfig skills = parser.Parse<SkillsConfig>(configs.skills_config.TrimStart('\uFEFF'));

        return new ServerGameSettings
        {
            RunnerConfig = parsedRunner,
            CharacterConfig = characters,
            SkillsConfig = skills,
        };
    }
}
