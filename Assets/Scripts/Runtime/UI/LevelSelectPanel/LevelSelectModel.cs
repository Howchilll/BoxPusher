using System.Collections.Generic;
using BoxPusher;
using HowFrame;

/// <summary>选关：关卡卡片放在 ScrollView 里，按正式关卡顺序生成，前一关通关后解锁下一关</summary>
public partial class LevelSelectPanel : PanelBase
{
    private readonly List<LevelItem> _items = new();

    private void OnInit()
    {
        levelItem.SetActive(false);
    }

    private void OnShow()
    {
        GameApp.RefreshLevels();
        var order = GameApp.LevelOrder;
        while (_items.Count < order.Count)
        {
            var go = Instantiate(levelItem, gridObj.transform);
            _items.Add(go.AddComponent<LevelItem>());
        }
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (i >= order.Count)
            {
                item.gameObject.SetActive(false);
                continue;
            }
            var id = order[i];
            item.gameObject.SetActive(true);
            item.Bind(id, LevelStore.LoadOfficial(id), Progress.Get(id), Progress.IsUnlocked(order, i), () => GameApp.PlayLevel(id));
        }
        levelsScr.verticalNormalizedPosition = 1;
    }

    private void OnHide() { }

    private void OnBackBtnClick() => GameApp.Go(GameScreen.Title);
}
