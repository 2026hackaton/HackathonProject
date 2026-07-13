using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameManager 이벤트를 화면에 붙이는 최소 예시입니다.
/// 필요한 Text/패널만 인스펙터에 연결해서 쓰세요 (TextMeshPro를 쓴다면 Text를
/// TMP_Text로 바꾸기만 하면 됩니다).
/// </summary>
public class MatchUI : MonoBehaviour
{
    [SerializeField] Text timerText;
    [SerializeField] GameObject countdownRoot;
    [SerializeField] Text countdownText;
    [SerializeField] GameObject gameOverRoot;
    [SerializeField] Text resultText;

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.OnCountdownTick += HandleCountdown;
        gm.OnScoreChanged += HandleScoreChanged;
        gm.OnMatchEnded += HandleMatchEnded;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.IsPlaying && timerText != null)
            timerText.text = Mathf.CeilToInt(gm.MatchTimer).ToString();
    }

    void HandleCountdown(int val)
    {
        if (countdownRoot == null) return;
        if (val <= 0)
        {
            countdownRoot.SetActive(false);
            return;
        }
        countdownRoot.SetActive(true);
        if (countdownText != null) countdownText.text = val.ToString();
    }

    void HandleScoreChanged(CharacterState who, int newScore)
    {
        Debug.Log($"[점수] {who.displayName}: {newScore}");
    }

    void HandleMatchEnded()
    {
        if (gameOverRoot != null) gameOverRoot.SetActive(true);
        if (resultText != null) resultText.text = "배송 종료!";
    }
}
