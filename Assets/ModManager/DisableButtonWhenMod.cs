#if MOD_INGAME
using UnityEngine;

using UnityEngine.UI;

public class DisableButtonWhenMod : MonoBehaviour
{
    public Button bt;
    public bool alam = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    // Update is called once per frame
    void OnEnable()
    {
        if(PlayerDataManager.instance.DGroup.playerData.carData[PlayerCarSelect.instance.CurrentCar].CarID == 87)
        {
            if(alam)
            AlamTextManager.instance.EnqueueMessage(LanguageManager.GetString(254));
            
            bt.interactable = false;
        }
        else
        {
            bt.interactable = true;;
        }
    }
}
#endif