#if MOD_INGAME
using UnityEngine;

public class AddModdingCar : MonoBehaviour
{
    void Awake()
    {
        AddModCar();
    }
    public void AddModCar()
    {
        if(PlayerCarDataManager.instance.GetMyCarCount(87) == 0 && PlayerDataManager.instance.HasProduct("Modding"))
        {
            PlayerCarDataManager.instance.AddCar(87);
            Initiate.Fade("main", Color.black, 0.5f);
        }
    }
}
#endif