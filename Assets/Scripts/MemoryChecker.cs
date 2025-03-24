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
        // Unity �ɂ���Ċ��蓖�Ă�ꂽ������
        Used = (Profiler.GetTotalAllocatedMemoryLong() >> 10) / 1024f;

        // �\��ς݂������蓖�Ă��Ă��Ȃ�������
        Unused = (Profiler.GetTotalUnusedReservedMemoryLong() >> 10) / 1024f;

        // Unity �����݂���я����̊��蓖�Ă̂��߂Ɋm�ۂ��Ă��鑍������
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