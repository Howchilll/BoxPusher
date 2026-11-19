using System.Collections.Generic;
using System.Threading.Tasks;
using BoxPusher;
using BoxPusher.Sim;
using HowFrame;
using UnityEngine;

/// <summary>
/// 关卡编辑器面板：左边画笔，右边关卡位 / 尺寸 / 正式关卡读写 / 草稿位 / 求解评估 / 试玩。
/// 求解在后台线程跑（Sim 层不依赖引擎），这里每帧轮询结果。
/// </summary>
public partial class EditorPanel : PanelBase
{
    private readonly List<ToolItem> _tools = new();
    private readonly List<DraftItem> _drafts = new();
    private EditorController _editor;
    private Task<SolveResult> _solve;
    private LevelData _solving;
    private bool _saveAfterSolve;
    private bool _dirty;

    private static readonly EditorTool[] ToolOrder =
    {
        EditorTool.Wall, EditorTool.Floor, EditorTool.Goal, EditorTool.Void,
        EditorTool.Player, EditorTool.Box, EditorTool.Ice, EditorTool.Plate,
        EditorTool.Gate, EditorTool.Stone, EditorTool.PortalA, EditorTool.PortalB, EditorTool.PortalC, EditorTool.PortalD,
    };

    private void OnInit()
    {
        toolItem.SetActive(false);
        foreach (var t in ToolOrder)
        {
            var go = Instantiate(toolItem, toolsObj.transform);
            go.SetActive(true);
            var item = go.AddComponent<ToolItem>();
            item.Bind(t, SelectTool);
            _tools.Add(item);
        }
        draftItem.SetActive(false);
    }

    private void OnShow()
    {
        _editor = BoardStage.Instance != null ? BoardStage.Instance.Editor : null;
        if (_editor == null) return;
        _editor.Changed -= OnLevelChanged;
        _editor.Changed += OnLevelChanged;
        _editor.Notice -= OnNotice;
        _editor.Notice += OnNotice;
        if (_editor.Current.id <= 0) _editor.SetMeta(FirstFreeId(), _editor.Current.name);
        SelectTool(_editor.Tool);
        SyncFields();
        RefreshDrafts();
        RefreshTitle();
        BoardStage.Instance.Fit(BoardStage.EditViewport);
    }

    private void OnHide()
    {
        if (_editor == null) return;
        _editor.Changed -= OnLevelChanged;
        _editor.Notice -= OnNotice;
    }

    private static int FirstFreeId()
    {
        var ids = LevelStore.OfficialIds();
        return ids.Count == 0 ? 1 : ids[ids.Count - 1] + 1;
    }

    private void SelectTool(EditorTool t)
    {
        if (_editor != null) _editor.Tool = t;
        foreach (var item in _tools) item.SetSelected(item.Tool == t);
    }

    private void OnNotice(string msg) => Status(msg, UiTheme.Primary);

    private void OnLevelChanged()
    {
        _dirty = true;
        SyncFields();
        RefreshDrafts();
        RefreshTitle();
    }

    private void SyncFields()
    {
        var d = _editor.Current;
        idInput.SetTextWithoutNotify(d.id.ToString());
        nameInput.SetTextWithoutNotify(d.name);
        SyncSize();
    }

    private void SyncSize()
    {
        var d = _editor.Current;
        wInput.SetTextWithoutNotify(d.width.ToString());
        hInput.SetTextWithoutNotify(d.height.ToString());
    }

    private void RefreshTitle()
    {
        var d = _editor.Current;
        var official = LevelStore.HasOfficial(d.id) ? "已有正式关卡" : "空关卡位";
        titleTxt.text = $"关卡编辑器　关卡位 {d.id}（{official}）　{d.width}×{d.height}　箱子 {d.boxes.Length}" +
                        (_dirty ? UiTheme.Rich("　未保存", UiTheme.Primary) : "");
    }

    private void RefreshDrafts()
    {
        var list = LevelStore.ListDrafts(_editor.Current.id);
        while (_drafts.Count < list.Count)
        {
            var go = Instantiate(draftItem, draftContent.transform);
            _drafts.Add(go.AddComponent<DraftItem>());
        }
        for (var i = 0; i < _drafts.Count; i++)
        {
            var item = _drafts[i];
            if (i >= list.Count) { item.gameObject.SetActive(false); continue; }
            // 新的在上面
            var info = list[list.Count - 1 - i];
            var d = info.Data;
            var stars = d.stars > 0 ? " " + Difficulty.StarText(d.stars) : "";
            var name = string.IsNullOrEmpty(d.name) ? "未命名" : d.name;
            item.gameObject.SetActive(true);
            item.Bind($"#{info.Slot:00}  {name}  {d.width}×{d.height}{stars}\n<size=70%><color=#9AA3B5>{d.savedAt}  {d.note}</color></size>",
                () => LoadDraft(info));
        }
        draftsTxt.text = list.Count == 0 ? "草稿位（空）" : $"草稿位（{list.Count} 个，点击载入）";
    }

    private void LoadDraft(DraftInfo info)
    {
        _editor.Load(info.Data);
        _dirty = false;
        SyncFields();
        RefreshTitle();
        Status($"已载入草稿位 #{info.Slot:00}", UiTheme.Text);
    }

    private void Status(string text, Color color)
    {
        statusTxt.text = text;
        statusTxt.color = color;
    }

    // ---------------- 关卡位 / 名字 / 尺寸 ----------------

    private void ChangeId(int id)
    {
        id = Mathf.Clamp(id, 1, 999);
        _editor.SetMeta(id, _editor.Current.name);
        idInput.SetTextWithoutNotify(id.ToString());
        RefreshDrafts();
        RefreshTitle();
        Status(LevelStore.HasOfficial(id) ? $"关卡位 {id} 已有正式关卡，可以点“载入正式关卡”查看" : $"关卡位 {id} 还没有正式关卡", UiTheme.Muted);
    }

    private void OnIdPrevBtnClick() => ChangeId(_editor.Current.id - 1);
    private void OnIdNextBtnClick() => ChangeId(_editor.Current.id + 1);

    private void OnIdInputEndEdit(string s)
    {
        if (int.TryParse(s, out var id)) ChangeId(id);
        else idInput.SetTextWithoutNotify(_editor.Current.id.ToString());
    }

    private void OnNameInputEndEdit(string s)
    {
        _editor.SetMeta(_editor.Current.id, s.Trim());
        _dirty = true;
        RefreshTitle();
    }

    private bool ReadSize(out int w, out int h)
    {
        var ok = int.TryParse(wInput.text, out w) & int.TryParse(hInput.text, out h);
        if (!ok) Status("宽高要填数字", UiTheme.Bad);
        return ok;
    }

    private void OnResizeBtnClick()
    {
        if (!ReadSize(out var w, out var h)) return;
        _editor.Resize(w, h);
        Status($"尺寸改为 {_editor.Current.width}×{_editor.Current.height}（保留左上角内容，范围 {EditorController.MinSize}~{EditorController.MaxWidth}×{EditorController.MaxHeight}）", UiTheme.Text);
    }

    private void OnNewBtnClick()
    {
        if (!ReadSize(out var w, out var h)) return;
        _editor.NewEmpty(w, h);
        Status($"新建 {_editor.Current.width}×{_editor.Current.height} 空白关卡（Ctrl+Z 可撤销）", UiTheme.Text);
    }

    // ---------------- 读写 ----------------

    private void OnLoadOfficialBtnClick()
    {
        var id = _editor.Current.id;
        var d = LevelStore.LoadOfficial(id);
        if (d == null)
        {
            Status($"关卡位 {id} 还没有正式关卡", UiTheme.Bad);
            return;
        }
        _editor.Load(d);
        _dirty = false;
        SyncFields();
        RefreshTitle();
        Status($"已载入正式关卡 {id}：{d.name}\n{Summary(d)}", UiTheme.Text);
    }

    private void OnSaveDraftBtnClick()
    {
        var slot = LevelStore.SaveDraft(_editor.Current);
        GameAudio.Play("save");
        RefreshDrafts();
        Status($"已存为关卡位 {_editor.Current.id} 的新草稿 #{slot:00}（草稿只新增，不会覆盖）", UiTheme.Good);
    }

    /// <summary>保存正式关卡：先校验结构，再求解；证明无解的不允许保存</summary>
    private void OnSaveOfficialBtnClick()
    {
        if (!CheckStructure("无法保存为正式关卡")) return;
        StartSolve(true);
    }

    private void OnAnalyzeBtnClick()
    {
        if (!CheckStructure("无法求解评估")) return;
        StartSolve(false);
    }

    private void OnTestBtnClick()
    {
        if (!CheckStructure("无法试玩")) return;
        GameApp.PlayTest(_editor.Current);
    }

    private void OnBackBtnClick() => GameApp.BackToMainMenu();

    /// <summary>结构校验：传送门不成对、机关门 / 机关板不成对或超量等错误弹窗警告并中止（草稿不受限制）</summary>
    private bool CheckStructure(string action)
    {
        var warnings = new List<string>();
        var errors = LevelValidator.Validate(_editor.Current, warnings);
        if (errors.Count > 0)
        {
            Status("结构错误：\n· " + string.Join("\n· ", errors), UiTheme.Bad);
            GameApp.Alert(action, "· " + string.Join("\n· ", errors) + "\n\n（草稿不受限制，可以先存草稿）");
            return false;
        }
        if (warnings.Count > 0) Status("注意：" + string.Join("；", warnings), UiTheme.Primary);
        return true;
    }

    // ---------------- 后台求解 ----------------

    private void StartSolve(bool thenSave)
    {
        if (_solve != null) return;
        _solving = _editor.Current.Clone();
        _saveAfterSolve = thenSave;
        var data = _solving;
        _solve = Task.Run(() => Difficulty.Evaluate(data, 1_500_000));
        SetBusy(true);
        Status("求解中…（广度优先搜索最少推箱解）", UiTheme.Muted);
    }

    private void SetBusy(bool busy)
    {
        saveOfficialBtn.interactable = !busy;
        analyzeBtn.interactable = !busy;
        testBtn.interactable = !busy;
    }

    private void Update()
    {
        if (_solve == null || !_solve.IsCompleted) return;
        var r = _solve.IsFaulted ? null : _solve.Result;
        if (_solve.IsFaulted) Debug.LogException(_solve.Exception);
        _solve = null;
        SetBusy(false);
        if (r == null)
        {
            Status("求解出错，详见 Console", UiTheme.Bad);
            return;
        }

        var d = _solving;
        var report = Report(d, r);
        if (!_saveAfterSolve)
        {
            Status(report, r.Solved ? UiTheme.Good : r.Aborted ? UiTheme.Primary : UiTheme.Bad);
            return;
        }
        if (!r.Solved && !r.Aborted)
        {
            Status(report + "\n无解的关卡不能保存为正式关卡，可以先存草稿", UiTheme.Bad);
            GameAudio.Play("deny");
            return;
        }
        // 求解结果（parPushes / stars）写进编辑中的关卡再存
        var cur = _editor.Current;
        cur.parPushes = d.parPushes;
        cur.parMoves = d.parMoves;
        cur.score = d.score;
        cur.stars = d.stars;
        var backup = LevelStore.SaveOfficial(cur);
        _dirty = false;
        GameAudio.Play("save");
        RefreshDrafts();
        RefreshTitle();
        Status($"已保存为正式关卡 {cur.id}" + (backup > 0 ? $"（旧版本已自动备份到草稿 #{backup:00}）" : "") + "\n" + report,
            r.Solved ? UiTheme.Good : UiTheme.Primary);
    }

    private static string Report(LevelData d, SolveResult r)
    {
        if (!r.Solved) return r.Message + $"（展开 {r.Explored} 个状态，{r.Millis} ms）";
        return $"可解　最少推箱 {r.Pushes}　步数 {r.Moves}\n搜索展开 {r.Explored} 个状态，用时 {r.Millis} ms\n难度 {Difficulty.StarText(d.stars)}（{d.score} 分）";
    }

    private static string Summary(LevelData d) =>
        d.stars > 0 ? $"{Difficulty.StarText(d.stars)}　最少推箱 {d.parPushes}　参考 {d.parMoves} 步" : "未评估";
}
