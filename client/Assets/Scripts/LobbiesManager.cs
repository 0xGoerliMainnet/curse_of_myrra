using System;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbiesManager : LevelSelector
{
    [SerializeField]
    LobbiesListItem listItem;

    public override void GoToLevel()
    {
        base.GoToLevel();
        gameObject.GetComponent<MMTouchButton>().DisableButton();
    }

    public void CreateLobby()
    {
        StartCoroutine(WaitForLobbyCreation());
    }

    public void ConnectToLobby()
    {
        StartCoroutine(WaitForLobbyJoin());
    }

    public void Back()
    {
        LobbyConnection.Instance.Init();
        SceneManager.LoadScene("Lobbies");
    }

    public void Refresh()
    {
        LobbyConnection.Instance.Refresh();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        MMSoundManager.Instance.FreeAllSounds();
    }

    public void QuickGame()
    {
        LobbyConnection.Instance.QuickGame();
        StartCoroutine(Utils.WaitForGameCreation(this.LevelName));
    }

    public IEnumerator WaitForLobbyCreation()
    {
        LobbyConnection.Instance.CreateLobby();
        yield return new WaitUntil(
            () =>
                !string.IsNullOrEmpty(LobbyConnection.Instance.LobbySession)
                && LobbyConnection.Instance.playerId != UInt64.MaxValue
        );
        SceneManager.LoadScene("Lobby");
    }

    public void Reconnect()
    {
        LobbyConnection.Instance.Reconnect();
        SceneManager.LoadScene("CharacterSelection");
    }

    public IEnumerator WaitForLobbyJoin()
    {
        LobbyConnection.Instance.ConnectToLobby(listItem.idHash);
        yield return new WaitUntil(() => LobbyConnection.Instance.playerId != UInt64.MaxValue);
        SceneManager.LoadScene("Lobby");
    }
}
