using UnityEngine;

public class ShowAndHide : MonoBehaviour
{
    public GameObject HideAndShow;
    public void Click(){
        if(HideAndShow.activeSelf){
            HideAndShow.SetActive(false);
        }else{
            HideAndShow.SetActive(true);
        }
    }
}
