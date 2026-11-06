using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>동료 뽑기 — 결제(소환권 우선→보석) 후 뒤끝 확률 차트로 서버 추첨, companionId 로 동료/조각 지급·저장, 서버 실패 시 환불 (확률 조작 방지로 서버가 결정).</summary>
// [차트] Assets/Datas/BackEndChart/CompanionGachaChart.xlsx (percent·companionId 열) — 업로드 후 파일 ID 를 probabilityFileId 에
public class GachaSystem : MonoBehaviour
{
    public static GachaSystem Instance;

    // 결제 규칙: 소환권이 소환 횟수만큼 있으면 소환권(1회 = 1장)을 쓰고, 모자라면 아래 보석 비용을 씁니다.
    [Header("뽑기 비용 (보석) — 소환권이 모자랄 때")]
    [SerializeField] private int cost1   = 300;
    [SerializeField] private int cost10  = 2700;
    [SerializeField] private int cost100 = 27000;

    // 확률은 뒤끝 확률 차트의 percent 열이 정합니다. (예전 등급별 chance 값은 더 이상 쓰지 않음)
    [Header("뒤끝 확률 차트")]
    [Tooltip("뒤끝 콘솔 > 차트 관리에서 분류 percent 로 올린 동료 뽑기 차트의 파일 ID.\n" +
             "차트를 다시 올리면 파일 ID 가 바뀌므로 여기도 바꿔야 합니다.")]
    [SerializeField] private string probabilityFileId = "";

    [Tooltip("차트에서 동료를 가리키는 열 이름 (CompanionData.id 와 같은 값)")]
    [SerializeField] private string companionIdColumn = "";

    // 가챠에서 나오는 동료 목록. 도감(CompanionCodexUI)도 같은 에셋을 봅니다.
    [Header("동료 풀 에셋 — 도감과 공유")]
    [Tooltip("CompanionPool 에셋을 연결하세요. 비어 있으면 소환할 수 없습니다.")]
    [SerializeField] private CompanionPoolAsset poolAsset;

    // 중복 시 조각 전환량은 풀 에셋(CompanionPoolAsset)에 있습니다 — 진화(도감 씬)와 같은 값을 쓰기 위해.

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (poolAsset == null)
            Debug.LogError("[Gacha] Pool Asset 이 연결되지 않았습니다. 소환 결과가 비어 재화가 환불됩니다.", this);
    }

    // ── 뽑기 결과 구조체 ──
    public struct GachaResult
    {
        public CompanionData data;
        public bool          isDuplicate;  // 중복 여부
        public int           fragmentGain; // 조각 획득량
    }

    // ── 뽑기 공개 API (결과는 메인 스레드 콜백, 실패면 빈 리스트) ──
    public void DrawOne(Action<List<GachaResult>> onDone)     => Draw(1,   cost1,   onDone);
    public void DrawTen(Action<List<GachaResult>> onDone)     => Draw(10,  cost10,  onDone);
    public void DrawHundred(Action<List<GachaResult>> onDone) => Draw(100, cost100, onDone);

    /// <summary>서버 응답을 기다리는 중인가. 그동안 다시 뽑기를 막습니다 (이중 결제 방지).</summary>
    public bool IsDrawing { get; private set; }

    /// <summary>count 회 소환의 보석 비용 (1 / 10 / 100 회만 정의됨, 그 외는 -1).</summary>
    public int GemCostFor(int count) => count switch
    {
        1   => cost1,
        10  => cost10,
        100 => cost100,
        _   => -1
    };

    /// <summary>지금 count 회 소환하면 소환권으로 결제되는가. (버튼 비용 표시용 — 결제 규칙과 같은 판단)</summary>
    public static bool WillPayWithTickets(int count) => GachaTicket.Count >= count;

    /// <summary>어떤 재화로 결제했는가 — 결과가 없을 때 같은 재화로 되돌려 주기 위해 기억합니다.</summary>
    private enum Payment { None, Ticket, Gem }

    private void Draw(int count, int gemCost, Action<List<GachaResult>> onDone)
    {
        List<GachaResult> results = new List<GachaResult>();

        if (IsDrawing) { onDone?.Invoke(results); return; }

        if (CurrencyManager.Instance == null)
        {
            Debug.LogError("[Gacha] CurrencyManager가 없습니다.");
            onDone?.Invoke(results);
            return;
        }

        if (string.IsNullOrWhiteSpace(probabilityFileId))
        {
            Debug.LogError("[Gacha] probabilityFileId 가 비어 있습니다. 뒤끝 확률 차트의 파일 ID 를 인스펙터에 넣으세요.", this);
            onDone?.Invoke(results);
            return;
        }

        // 재화 부족 → 빈 결과를 돌려줍니다. (호출한 UI 가 results.Count == 0 으로 판단)
        Payment paid = Pay(count, gemCost);
        if (paid == Payment.None)
        {
            onDone?.Invoke(results);
            return;
        }

        // 결제 먼저 → 서버 실패 시 Refund (응답 전 앱이 꺼져도 저장 전이라 재화 유지)
        IsDrawing = true;
        string fileId = probabilityFileId.Trim();
        BackendManager.Call(cb => Backend.Probability.GetProbabilitys(fileId, count, cb), bro =>
        {
            IsDrawing = false;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogError($"[Gacha] 서버 추첨 실패 — 결제를 되돌립니다: {BackendManager.Describe(bro)}");
                Refund(paid, count, gemCost);
                onDone?.Invoke(results);
                return;
            }

            foreach (CompanionData data in ParsePicks(bro))
                results.Add(ProcessResult(data));

            if (results.Count > 0)
            {
                if (results.Count < count)
                    Debug.LogWarning($"[Gacha] {count}회 중 {results.Count}회만 지급했습니다. 차트의 {companionIdColumn} 값과 풀 에셋을 확인하세요.");

                // ★ 미션 진행도 / 가이드 퀘스트 리포트 (실제 뽑힌 개수만큼)
                MissionManager.Instance?.ReportGachaPull(results.Count);
                GuideQuestManager.Instance?.ReportSummon(results.Count);
            }
            else
            {
                // 한 장도 지급하지 못했으면 결제를 되돌립니다. (안 그러면 재화만 사라집니다)
                Refund(paid, count, gemCost);
            }

            // ★ 재화 차감 + 동료/조각 획득을 한 번에 저장 (루프 밖에서 1회)
            SaveManager.Instance?.Save();

            onDone?.Invoke(results);
        });
    }

    /// <summary>소환권 우선, 모자라면 보석. 둘 다 안 되면 아무것도 차감하지 않고 None.</summary>
    private static Payment Pay(int count, int gemCost)
    {
        if (GachaTicket.TrySpend(count)) return Payment.Ticket;
        if (gemCost > 0 && CurrencyManager.Instance.SpendGem(gemCost)) return Payment.Gem;
        return Payment.None;
    }

    private static void Refund(Payment paid, int count, int gemCost)
    {
        Debug.LogWarning("[Gacha] 지급한 동료가 없어 결제를 되돌립니다.");

        if (paid == Payment.Ticket) GachaTicket.Add(count);
        else if (paid == Payment.Gem) CurrencyManager.Instance?.AddGem(gemCost);
    }

    // ── 중복 처리 ──
    private GachaResult ProcessResult(CompanionData data)
    {
        GachaResult result = new GachaResult { data = data };

        bool alreadyOwned = IsAlreadyOwned(data);

        if (alreadyOwned)
        {
            // ✅ 중복 → 조각으로 전환
            result.isDuplicate  = true;
            result.fragmentGain = GetFragmentAmount(data.grade);
            CompanionFragment.Instance?.AddFragment(data, result.fragmentGain);
        }
        else
        {
            // ✅ 신규 → 동료 획득
            result.isDuplicate  = false;
            result.fragmentGain = 0;
            CompanionManager.Instance?.AddCompanion(data);
        }

        return result;
    }

    /// <summary>이미 보유한 동료인가 (이름·에셋 참조가 아닌 id 비교).</summary>
    private bool IsAlreadyOwned(CompanionData data)
    {
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return false;   // 매니저가 없으면 신규로 처리 (기존 동작 유지)

        foreach (CompanionData owned in cm.GetOwnedCompanionData())
        {
            if (owned != null && owned.id == data.id)
                return true;
        }
        return false;
    }

    private int GetFragmentAmount(CompanionGrade grade)
        => poolAsset != null ? poolAsset.DuplicateFragments(grade) : 0;

    /// <summary>가챠가 쓰는 풀 에셋. 도감이 자기 칸을 비워 뒀을 때의 예비 경로입니다.</summary>
    public CompanionPoolAsset Pool => poolAsset;

    // ── 서버 추첨 결과 → 동료 ──

    /// <summary>응답 elements 를 풀 에셋의 동료로 변환 (풀에 없는 id 는 경고 후 건너뜀).</summary>
    private List<CompanionData> ParsePicks(BackendReturnObject bro)
    {
        List<CompanionData> picks = new List<CompanionData>();

        JsonData elements;
        try
        {
            JsonData root = bro.GetFlattenJSON();
            elements = root != null && root.IsObject && root.ContainsKey("elements") ? root["elements"] : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Gacha] 서버 응답을 읽지 못했습니다: {e.Message}\n{bro}");
            return picks;
        }

        if (elements == null || !elements.IsArray)
        {
            Debug.LogError($"[Gacha] 서버 응답에 elements 가 없습니다: {bro}");
            return picks;
        }

        Dictionary<string, CompanionData> byId = BuildIdLookup();

        for (int i = 0; i < elements.Count; i++)
        {
            JsonData row = elements[i];
            string id = row != null && row.IsObject && row.ContainsKey(companionIdColumn)
                ? row[companionIdColumn]?.ToString()
                : null;

            if (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out CompanionData data))
                picks.Add(data);
            else
                Debug.LogWarning($"[Gacha] 차트의 {companionIdColumn} '{id}' 에 맞는 동료가 풀 에셋에 없습니다.");
        }
        return picks;
    }

    /// <summary>풀 에셋의 동료를 id 로 찾는 표. 뽑을 때마다 만들어도 18명 정도라 비용이 거의 없습니다.</summary>
    private Dictionary<string, CompanionData> BuildIdLookup()
    {
        var byId = new Dictionary<string, CompanionData>();

        // 'poolAsset != null' 은 유니티식 null 검사입니다 (연결 안 됨 / 파괴됨 모두 걸러냄).
        if (poolAsset == null) return byId;

        foreach (CompanionData d in poolAsset.All())
            if (d != null && !string.IsNullOrEmpty(d.id) && !byId.ContainsKey(d.id))
                byId.Add(d.id, d);

        return byId;
    }
}