using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectServerIP : MonoBehaviour
{
    [SerializeField]
    Text IP;

    [SerializeField]
    TextMeshProUGUI serverName;

    [SerializeField]
    Sprite selectedButtonSprite;

    public static string serverIp;
    public static string serverNameString;

    public void SetServerIp()
    {
        serverIp = IP.text;
        serverNameString = serverName.text;
        GetComponent<Image>().sprite = selectedButtonSprite;
        LobbyConnection.Instance.Refresh();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static string GetServerIp()
    {
        return string.IsNullOrEmpty(serverIp) ? "localhost" : serverIp;
    }

    public static string GetServerName()
    {
        return string.IsNullOrEmpty(serverNameString) ? "LocalHost" : serverNameString;
    }
}
