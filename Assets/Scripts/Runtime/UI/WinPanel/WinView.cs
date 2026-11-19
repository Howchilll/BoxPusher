using HowFrame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class WinPanel : PanelBase
{
//Define
    [SerializeField] private TextMeshProUGUI titleTxt;
    [SerializeField] private TextMeshProUGUI resultTxt;
    [SerializeField] private Button nextBtn;
    [SerializeField] private Button retryBtn;
    [SerializeField] private Button menuBtn;
    [SerializeField] private TextMeshProUGUI menuTxt;
//end Define

    protected override void Init()
    {
        //Init
        titleTxt = transform.Find("Card/Title_Txt").GetComponent<TextMeshProUGUI>();
        resultTxt = transform.Find("Card/Result_Txt").GetComponent<TextMeshProUGUI>();
        nextBtn = transform.Find("Card/Buttons/Next_Btn").GetComponent<Button>();
        nextBtn.onClick.AddListener(OnNextBtnClick);
        retryBtn = transform.Find("Card/Buttons/Retry_Btn").GetComponent<Button>();
        retryBtn.onClick.AddListener(OnRetryBtnClick);
        menuBtn = transform.Find("Card/Buttons/Menu_Btn").GetComponent<Button>();
        menuBtn.onClick.AddListener(OnMenuBtnClick);
        menuTxt = transform.Find("Card/Buttons/Menu_Btn/Label").GetComponent<TextMeshProUGUI>();
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
    protected override void WhenShowWithParameter(object parameter) => OnShowWithParameter(parameter);
}
