using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LineHeightFix : MonoBehaviour
{
    private TextMeshProUGUI tmp;
    private float designLineHeight; // 设计时算出来的真实行高
    private float baseLineSpacing;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        baseLineSpacing = tmp.lineSpacing;

        // 记录设计时行高（用 Inspector 里的 fontSize）
        float defaultLineHeight = tmp.font.faceInfo.lineHeight * (tmp.fontSize / tmp.font.faceInfo.pointSize) * baseLineSpacing;
        designLineHeight = defaultLineHeight;
    }

    void LateUpdate()
    {
        // 当前默认行高（受 Auto Size 改变影响）
        float defaultLineHeight = tmp.font.faceInfo.lineHeight * (tmp.fontSize / tmp.font.faceInfo.pointSize) * baseLineSpacing;

        // 修正 lineSpacingAdjustment，把行高拉回到设计时的样子
        tmp.lineSpacingAdjustment = designLineHeight - defaultLineHeight;
    }
}
