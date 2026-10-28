using TMPro;
using UnityEngine;

public sealed class PlayerGoldUI : MonoBehaviour
{
    [SerializeField] private PlayerGoldWallet wallet;
    [SerializeField] private TMP_Text amount;
    public string DisplayedGold => amount.text;
    private void OnEnable(){wallet.Changed+=Refresh;Refresh();}
    private void OnDisable(){if(wallet!=null)wallet.Changed-=Refresh;}
    private void Refresh()=>amount.text=wallet.Gold.ToString("N0")+" Gold";
}
