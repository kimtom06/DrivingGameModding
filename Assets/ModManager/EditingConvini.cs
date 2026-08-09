using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class EditingConvini : MonoBehaviour
{
    public Toggle DisableCol;
    public bool isOnCol = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
         DisableCol.onValueChanged.AddListener(OnCollisionSet);
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
}
