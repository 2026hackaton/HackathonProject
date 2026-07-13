using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 타이머, 할당량, 점수, 카운트다운, 종료/순위 판정을 담당하는 싱글턴.
/// 씬에 빈 오브젝트 하나 만들어서 붙이고, characters 리스트에 플레이어+CPU 3명을
/// 인스펙터에서 순서대로 등록하세요 (점수 집계/순위용 — 밀치기/AI 판정 자체는
/// CharacterState.All을 직접 쓰므로 이 리스트에 안 넣어도 게임플레이엔 지장 없습니다).
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("설정")]
    [SerializeField] float matchDuration = 90f;
    [SerializeField] int quota = 15;
    [SerializeField] int countdownFrom = 3;

    [Header("참가자 (씬에 미리 배치한 순서대로 등록)")]
    [SerializeField] List<CharacterState> characters = new List<CharacterState>();

    public float MatchTimer { get; private set; }
    public bool IsPlaying { get; private set; }

    readonly Dictionary<CharacterState, int> scores = new Dictionary<CharacterState, int>();

    public event Action<CharacterState, int> OnScoreChanged;
    public event Action OnMatchEnded;
    public event Action<int> OnCountdownTick;

    void Awake()
    {
        Instance = this;
        foreach (var c in characters)
        {
            if (c != null) scores[c] = 0;
        }
    }

    void Start()
    {
        StartCoroutine(CountdownThenPlay());
    }

    IEnumerator CountdownThenPlay()
    {
        for (int i = countdownFrom; i > 0; i--)
        {
            OnCountdownTick?.Invoke(i);
            yield return new WaitForSeconds(1f);
        }
        OnCountdownTick?.Invoke(0);
        MatchTimer = matchDuration;
        IsPlaying = true;
    }

    void Update()
    {
        if (!IsPlaying) return;
        MatchTimer -= Time.deltaTime;
        if (MatchTimer <= 0f)
        {
            MatchTimer = 0f;
            EndMatch();
        }
    }

    public void AddScore(CharacterState scorer)
    {
        if (scorer == null || !IsPlaying) return;
        if (!scores.ContainsKey(scorer)) scores[scorer] = 0;
        scores[scorer]++;
        OnScoreChanged?.Invoke(scorer, scores[scorer]);
        if (scores[scorer] >= quota) EndMatch();
    }

    public int GetScore(CharacterState c) => scores.TryGetValue(c, out int v) ? v : 0;

    void EndMatch()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        OnMatchEnded?.Invoke();

        var ranked = characters.Where(c => c != null)
                                .OrderByDescending(c => GetScore(c))
                                .ToList();

        string log = "=== 배송 결과 ===\n";
        for (int i = 0; i < ranked.Count; i++)
            log += $"{i + 1}위 {ranked[i].displayName} - {GetScore(ranked[i])}개\n";
        Debug.Log(log);
    }
}
