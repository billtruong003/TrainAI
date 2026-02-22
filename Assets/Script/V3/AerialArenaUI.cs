using UnityEngine;
using UnityEngine.UI;

public class AerialArenaUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private AerialArenaController arena;

    [Header("UI References")]
    [SerializeField] private Text scoreTextA; // Gán Text UI cho Team A
    [SerializeField] private Text scoreTextB; // Gán Text UI cho Team B
    [SerializeField] private Text episodeText; // Hiển thị số vòng đấu
    [SerializeField] private Text statusText; // Hiển thị trạng thái (Combat/Warmup)

    private int _episodes = 0;

    private void Start()
    {
        if (arena == null) arena = FindObjectOfType<AerialArenaController>();
    }

    private void Update()
    {
        if (arena == null) return;

        // Cập nhật điểm số
        if (scoreTextA) scoreTextA.text = $"RED: {arena.ScoreA}";
        if (scoreTextB) scoreTextB.text = $"BLUE: {arena.ScoreB}";

        // Logic đếm số Episode đơn giản (dựa vào việc Score thay đổi hoặc có thể bind event sau)
        // Hiện tại ta chỉ cập nhật trạng thái
        if (statusText) statusText.text = $"STATE: {GetStateName()}";
    }

    private string GetStateName()
    {
        return arena.CurrentState.ToString().ToUpper();
    }

    // Tự động cập nhật từ Controller
    private void LateUpdate()
    {
        if (arena == null) return;
        if (episodeText) episodeText.text = $"MATCH: {arena.MatchCount}";
    }
}