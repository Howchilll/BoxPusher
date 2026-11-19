using HowFrame;
using UnityEngine;
using UnityEngine.UI;

public partial class LevelSelectPanel : PanelBase
{
//Define
    [SerializeField] private Button backBtn;
    [SerializeField] private ScrollRect levelsScr;
    [SerializeField] private GameObject gridObj;
    [SerializeField] private GameObject levelItem;
//end Define

    protected override void Init()
    {
        //Init
        backBtn = transform.Find("Header/Back_Btn").GetComponent<Button>();
        backBtn.onClick.AddListener(OnBackBtnClick);
        levelsScr = transform.Find("Levels_Scr").GetComponent<ScrollRect>();
        gridObj = transform.Find("Levels_Scr/Viewport/Grid_Obj").gameObject;
        levelItem = transform.Find("Levels_Scr/Viewport/Grid_Obj/Level_Item").gameObject;
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
}
