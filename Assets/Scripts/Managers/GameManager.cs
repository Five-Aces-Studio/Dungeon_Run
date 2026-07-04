using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : Singleton<GameManager>
{
    private bool isGameActive = true;
    [SerializeField] private float restartTime;
    [SerializeField] private TextMeshProUGUI winLoseDisplay;
    private void OnEnable()
    {
        BossEvents.OnBossDeath += PlayerWin;
        PlayerEvents.OnPlayerDeath += PlayerLose;
    }

    private void OnDisable()
    {
        BossEvents.OnBossDeath -= PlayerWin;
        PlayerEvents.OnPlayerDeath -= PlayerLose;
    }

    public bool IsGameActive()
    {
        return isGameActive;
    }

    private void PlayerWin()
    {
        isGameActive = false;
        winLoseDisplay.text = "You defeated the boss. Nah I'd win";
        StartCoroutine(RestarGame());
    }

    private void PlayerLose()
    {
        isGameActive = false;
         winLoseDisplay.text = "Game Over";
         StartCoroutine(RestarGame());
    }

    private IEnumerator RestarGame()
    {
        yield return new WaitForSeconds(restartTime);
        SceneManager.LoadScene("GameScene");
    }
}
