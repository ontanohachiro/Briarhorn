using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

public sealed class UnityMemoryChecker
{
    public float Used { get; private set; }
    public float Unused { get; private set; }
    public float Total { get; private set; }

    public string UsedText { get; private set; }
    public string UnusedText { get; private set; }
    public string TotalText { get; private set; }

    public void Update()
    {
        // Unity ‚É‚æ‚Á‚ÄŠ„‚è“–‚Ä‚ç‚ê‚½ƒƒ‚ƒŠ
        Used = (Profiler.GetTotalAllocatedMemoryLong() >> 10) / 1024f;

        // —\–ñÏ‚Ý‚¾‚ªŠ„‚è“–‚Ä‚ç‚ê‚Ä‚¢‚È‚¢ƒƒ‚ƒŠ
        Unused = (Profiler.GetTotalUnusedReservedMemoryLong() >> 10) / 1024f;

        // Unity ‚ªŒ»Ý‚¨‚æ‚Ñ«—ˆ‚ÌŠ„‚è“–‚Ä‚Ì‚½‚ß‚ÉŠm•Û‚µ‚Ä‚¢‚é‘ƒƒ‚ƒŠ
        Total = (Profiler.GetTotalReservedMemoryLong() >> 10) / 1024f;

        UsedText = Used.ToString("0.0") + " MB";
        UnusedText = Unused.ToString("0.0") + " MB";
        TotalText = Total.ToString("0.0") + " MB";
    }
}
public class MemoryChecker : MonoBehaviour
{
    public TMP_Text m_text;

    private readonly UnityMemoryChecker m_unityMemoryChecker =
        new UnityMemoryChecker();

    private void Update()
    {
        m_unityMemoryChecker.Update();

        var sb = new StringBuilder();
        sb.AppendLine("<b>Unity</b>");
        sb.AppendLine();
        sb.AppendLine($"    Used: {m_unityMemoryChecker.UsedText}");
        sb.AppendLine($"    Unused: {m_unityMemoryChecker.UnusedText}");
        sb.AppendLine($"    Total: {m_unityMemoryChecker.TotalText}");

        var text = sb.ToString();
        m_text.text = text;
    }
}