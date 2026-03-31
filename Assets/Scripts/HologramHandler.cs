using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HologramHandler : MonoBehaviour
{
    [SerializeField] GameObject holoGameObject;

    // Start is called before the first frame update
    void Start()
    {
        if(holoGameObject == null)
        {
            holoGameObject = GameObject.FindGameObjectWithTag("hologram");
        }
        holoGameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void setPosition(Vector3 upVector, Vector3 position)
    {
        holoGameObject.transform.up = upVector;
        holoGameObject.transform.position = position;
    }

    public void setRotation(Quaternion rotation)
    {
        holoGameObject.transform.rotation = rotation;
    }

    public void setActive(bool val)
    {
        holoGameObject.SetActive(val);
    }
}
