using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class EditingConvini : MonoBehaviour
{
    public Button ExportModButton;
    public ExtractElements ex;
    public Toggle DisableCol;
    public bool isOnCol = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ExportModButton.interactable = false;
         DisableCol.onValueChanged.AddListener(OnCollisionSet);
         PlateTog.onValueChanged.AddListener(ShowPlates);
         EXTog.onValueChanged.AddListener(ShowEx);
         HumanTog.onValueChanged.AddListener(ShowHuman);
    }
    public GameObject noWheel;
    public GameObject noCol;
    void Update()
    {
        noWheel.SetActive(true);
        noCol.SetActive(true);
        if(ex.Wheel_FL && ex.Wheel_FR && ex.Wheel_RL && ex.Wheel_RR)
        {
            ExportModButton.interactable = true;
            noWheel.SetActive(false);
            if(ex.Colliders.Count > 0)
            {
                noCol.SetActive(false);
                ExportModButton.interactable = true;
            }
            else
            {
                ExportModButton.interactable = false;
            }
        }
        else
        {
            
            ExportModButton.interactable = false;
        }
    }
    // Update is called once per frame
    void OnCollisionSet(bool ha)
    {
        isOnCol = !isOnCol;
        List<GameObject> objectsWithCollider = FindObjectsOfType<GameObject>(true).Where(go => go.name.ToLower().Contains("collider")).ToList();

        foreach (GameObject obj in objectsWithCollider){
            obj.SetActive(isOnCol);
        }

    }

    public Toggle PlateTog;
    public GameObject[] Plate;
    void ShowPlates(bool b){
        Plate[0].SetActive(b);
        Plate[1].SetActive(b);
        for(int i=0;  i < 2; i++){
            if (ex.Numberplate.Count > i)
            {
                Plate[i].transform.position = ex.Numberplate[i].transform.position;
                 Plate[i].transform.rotation = ex.Numberplate[i].transform.rotation;
            }
            else
            {
                Plate[1].SetActive(false);
            }
        }
    }

    public Toggle EXTog;
    public GameObject[] Ex;
    void ShowEx(bool b){
        Ex[0].SetActive(b);
        Ex[1].SetActive(b);
        Ex[2].SetActive(b);
        Ex[3].SetActive(b);
        for(int i=0;  i < 4; i++){
            if (ex.Exhaust.Count > i)
            {
                Ex[i].transform.position = ex.Exhaust[i].transform.position;
            }
            else
            {
                Ex[i].SetActive(false);
            }
        }
    }

    public Toggle HumanTog;
    public GameObject Human;
    void ShowHuman(bool b){
        
        Human.SetActive (b && ex.Person_Position);
        Human.transform.position = ex.Person_Position.transform.position;
    }
}
