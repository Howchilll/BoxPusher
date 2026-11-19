using HowFrame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class EditorPanel : PanelBase
{
//Define
    [SerializeField] private TextMeshProUGUI titleTxt;
    [SerializeField] private GameObject toolsObj;
    [SerializeField] private GameObject toolItem;
    [SerializeField] private Button idPrevBtn;
    [SerializeField] private TMP_InputField idInput;
    [SerializeField] private Button idNextBtn;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField wInput;
    [SerializeField] private TMP_InputField hInput;
    [SerializeField] private Button resizeBtn;
    [SerializeField] private Button newBtn;
    [SerializeField] private Button loadOfficialBtn;
    [SerializeField] private Button saveOfficialBtn;
    [SerializeField] private Button saveDraftBtn;
    [SerializeField] private TextMeshProUGUI draftsTxt;
    [SerializeField] private GameObject draftContent;
    [SerializeField] private GameObject draftItem;
    [SerializeField] private Button analyzeBtn;
    [SerializeField] private Button testBtn;
    [SerializeField] private TextMeshProUGUI statusTxt;
    [SerializeField] private Button backBtn;
//end Define

    protected override void Init()
    {
        //Init
        titleTxt = transform.Find("Top/Title_Txt").GetComponent<TextMeshProUGUI>();
        toolsObj = transform.Find("Left/Tools_Obj").gameObject;
        toolItem = transform.Find("Left/Tools_Obj/Tool_Item").gameObject;
        idPrevBtn = transform.Find("Right/IdRow/IdPrev_Btn").GetComponent<Button>();
        idPrevBtn.onClick.AddListener(OnIdPrevBtnClick);
        idInput = transform.Find("Right/IdRow/Id_Input").GetComponent<TMP_InputField>();
        idInput.onEndEdit.AddListener(OnIdInputEndEdit);
        idNextBtn = transform.Find("Right/IdRow/IdNext_Btn").GetComponent<Button>();
        idNextBtn.onClick.AddListener(OnIdNextBtnClick);
        nameInput = transform.Find("Right/Name_Input").GetComponent<TMP_InputField>();
        nameInput.onEndEdit.AddListener(OnNameInputEndEdit);
        wInput = transform.Find("Right/SizeRow/W_Input").GetComponent<TMP_InputField>();
        hInput = transform.Find("Right/SizeRow/H_Input").GetComponent<TMP_InputField>();
        resizeBtn = transform.Find("Right/SizeRow/Resize_Btn").GetComponent<Button>();
        resizeBtn.onClick.AddListener(OnResizeBtnClick);
        newBtn = transform.Find("Right/SizeRow/New_Btn").GetComponent<Button>();
        newBtn.onClick.AddListener(OnNewBtnClick);
        loadOfficialBtn = transform.Find("Right/FileRow/LoadOfficial_Btn").GetComponent<Button>();
        loadOfficialBtn.onClick.AddListener(OnLoadOfficialBtnClick);
        saveOfficialBtn = transform.Find("Right/FileRow/SaveOfficial_Btn").GetComponent<Button>();
        saveOfficialBtn.onClick.AddListener(OnSaveOfficialBtnClick);
        saveDraftBtn = transform.Find("Right/SaveDraft_Btn").GetComponent<Button>();
        saveDraftBtn.onClick.AddListener(OnSaveDraftBtnClick);
        draftsTxt = transform.Find("Right/Drafts_Txt").GetComponent<TextMeshProUGUI>();
        draftContent = transform.Find("Right/Drafts_Scr/Viewport/Content").gameObject;
        draftItem = transform.Find("Right/Drafts_Scr/Viewport/Content/Draft_Item").gameObject;
        analyzeBtn = transform.Find("Right/ActionRow/Analyze_Btn").GetComponent<Button>();
        analyzeBtn.onClick.AddListener(OnAnalyzeBtnClick);
        testBtn = transform.Find("Right/ActionRow/Test_Btn").GetComponent<Button>();
        testBtn.onClick.AddListener(OnTestBtnClick);
        statusTxt = transform.Find("Right/Status_Txt").GetComponent<TextMeshProUGUI>();
        backBtn = transform.Find("Right/Back_Btn").GetComponent<Button>();
        backBtn.onClick.AddListener(OnBackBtnClick);
        //end Init
        OnInit();
    }

    protected override void WhenShow() => OnShow();
    protected override void WhenHide() => OnHide();
}
