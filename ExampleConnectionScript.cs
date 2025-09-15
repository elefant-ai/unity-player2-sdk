using TMPro;
using UnityEngine;

public class ExampleConnectionScript : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI text;


    public void Connected()
    {
        text.text = "Connected";
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
