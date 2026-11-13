using UnityEngine;
using MobileModSystem;
public class ModImportButton : MonoBehaviour
{
    public ExtractElements ex;
    public void ClickToLoadMod()
    {
         MobileModController.Instance.PickAndImportModPackage(OnModImported);
        //LoadRecentModPackage

    }


    private void OnModImported(GameObject importedRoot)
    {
           ex.ExtractFromModel();
    }
}
