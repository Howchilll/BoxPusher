using HowFrame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayPanel : PanelBase
{
//Define
    [SerializeField] private Button backBtn;
    [SerializeField] private TextMeshProUGUI titleTxt;
    [SerializeField] private TextMeshProUGUI statsTxt;
    [SerializeField] private Button undoBtn;
    [SerializeField] private Button restartBtn;
    [SerializeField] private Button hintBtn;
//end Define

    protected override void Init()
    {
        //Init
        backBtn = transform.Find("Top/Back_Btn").GetComponent<Button>();
        backBtn.onClick.AddListener(OnBackBtnClick);
        titleTxt = transform.Find("Top/Title_Txt").GetComponent<TextMeshProUGUI>();
        statsTxt = transform.Find("Top/Stats_Txt").GetComponent<TextMeshProUGUI>();
        undoBtn = transform.Find("Bottom/Undo_Btn").GetComponent<Button>();
        undoBtn.onClick.AddListener(OnUndoBtnClick);
        restartBtn = transform.Find("Bottom/Restart_Btn").GetComponent<Button>();
        restartBtn.onClick.AddListener(OnRestartBtnClick);
        hintBtn = transform.Find("Bottom/Hint_Btn").GetComponent<Button>();
        hintBtn.onClick.AddListener(OnHintBtnClick);
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
}
